param([switch]$Publish)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$folder=Join-Path $root 'docs/wiki'
$manifest=Get-Content -LiteralPath (Join-Path $folder 'wiki-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($manifest.namespace -ne 'Kuriyukihime' -or $manifest.package -ne 'IvoryBastion'){throw 'Unexpected wiki package identity.'}
$api='/api/experimental/package/Kuriyukihime/IvoryBastion/wiki/'
$publicRoot='https://thunderstore.io/c/riskofrain2/p/Kuriyukihime/IvoryBastion/wiki/'
$encoding=New-Object Text.UTF8Encoding($false)
$credentialPath=Join-Path $env:APPDATA 'IvoryBastionPublishing/thunderstore-token.xml'
$loadedToken=$null;$headers=@{};$wikiToken=$null
function Save-Json($path,$value){[IO.File]::WriteAllText($path,(ConvertTo-Json -InputObject $value -Depth 20),$encoding)}
function Hash-Text([string]$value){$sha=[Security.Cryptography.SHA256]::Create();try{return ([BitConverter]::ToString($sha.ComputeHash($encoding.GetBytes($value)))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}}
function Read-Page([string]$file){return [IO.File]::ReadAllText((Join-Path $folder $file),$encoding)}
function Invoke-WikiApi([string]$path,[string]$method='Get',$body=$null,[switch]$AllowMissing){
    if($path -notlike '/api/experimental/*' -or $method -notin @('Get','Post')){throw 'Unexpected wiki API request.'}
    if($method -eq 'Post' -and ($path -ne $api -or -not $Publish)){throw 'Wiki writes require explicit -Publish and the exact package endpoint.'}
    try{
        # Avoid stale CDN reads immediately after publishing a package or page.
        $uri='https://thunderstore.io'+$path
        if($method -eq 'Get'){$uri+='?verify='+[Guid]::NewGuid().ToString('N')}
        $request=@{Uri=$uri;Method=$method;Headers=$headers;TimeoutSec=45;ErrorAction='Stop'}
        if($null -ne $body){$request.Body=$encoding.GetBytes((ConvertTo-Json -InputObject $body -Depth 10 -Compress));$request.ContentType='application/json; charset=utf-8'}
        return Invoke-RestMethod @request
    }catch{
        $status=0;if($_.Exception.Response){$status=[int]$_.Exception.Response.StatusCode}
        if($AllowMissing -and $status -eq 404){return $null}
        $detail=''
        try{$failure=$_.ErrorDetails.Message | ConvertFrom-Json;if($failure.detail){$detail=' '+[string]$failure.detail}}catch{}
        if($wikiToken){$detail=$detail.Replace($wikiToken,'[redacted]')}
        if($detail.Length -gt 400){$detail=$detail.Substring(0,400)}
        throw ('Wiki API '+$method+' '+$path+' failed (HTTP '+$status+').'+$detail)
    }
}
try{
    $wikiToken=$env:TCLI_AUTH_TOKEN
    if([string]::IsNullOrWhiteSpace($wikiToken)){
        $loadedToken=Import-Clixml -LiteralPath $credentialPath
        if($loadedToken -isnot [Security.SecureString]){throw 'The local publishing credential is invalid.'}
        $pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($loadedToken)
        try{$wikiToken=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)}finally{[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)}
    }
    $headers.Authorization='Bearer '+$wikiToken
    $profile=Invoke-WikiApi '/api/experimental/current-user/'
    if(@($profile.teams) -notcontains 'Kuriyukihime'){throw 'The publishing account does not report membership in the owning team.'}
    $package=Invoke-WikiApi '/api/experimental/package/Kuriyukihime/IvoryBastion/'
    $wiki=Invoke-WikiApi $api -AllowMissing
    $existing=@();if($wiki){$existing=@($wiki.pages)}
    $remote=@{};$backup=@()
    foreach($page in $existing){
        if([string]$page.id -notmatch '^\d+$'){throw 'Invalid existing wiki page ID.'}
        $copy=Invoke-WikiApi ('/api/experimental/wiki/page/'+$page.id+'/')
        $remote[[string]$page.id]=$copy;$backup+=$copy
    }
    $plan=@();$mapping=@{};$known=@{}
    $statePath=Join-Path $root '.build/wiki-public-pages.json'
    if(Test-Path -LiteralPath $statePath){
        $state=Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
        if($state.package -ne 'Kuriyukihime-IvoryBastion'){throw 'Unexpected saved wiki page map.'}
        foreach($item in $state.pages){$known[$item.key]=[string]$item.id}
    }
    foreach($page in $manifest.pages){
        if($page.file -notmatch '^\d{2}-[a-z0-9-]+\.md$'){throw 'Unexpected local wiki filename.'}
        $text=Read-Page $page.file
        if((Hash-Text $text) -ne $page.sha256){throw ('Wiki page changed since generation: '+$page.file)}
        $matches=@($existing | Where-Object { $_.title -ceq $page.title })
        if($known.ContainsKey($page.key) -and $remote.ContainsKey($known[$page.key])){
            $savedPage=$remote[$known[$page.key]]
            if($savedPage.title -cne $page.title){throw ('Wiki title rename needs review: '+$page.key)}
            $matches=@($savedPage)
        }
        if($matches.Count -gt 1){throw ('Duplicate public wiki titles need review: '+$page.title)}
        $id=$null;if($matches.Count){$id=[string]$matches[0].id;$mapping[$page.file]=$matches[0]}
        $plan+=@{key=$page.key;title=$page.title;file=$page.file;id=$id;action=$(if($id){'update if changed'}else{'create'});sha256=$page.sha256}
    }
    Save-Json (Join-Path $root '.build/wiki-publish-plan.json') @{version=$manifest.version;localStatus=$manifest.status;publicModVersion=$package.latest.version_number;pages=$plan;unknownPublicPages=@($existing | Where-Object { $_.title -cnotin @($manifest.pages.title) })}
    Write-Host ('Wiki access verified for owning team; '+$manifest.pages.Count+' local pages, '+$existing.Count+' current public pages. Plan: .build/wiki-publish-plan.json')
    if(-not $Publish){return}
    if($manifest.status -ne 'released' -or $package.latest.version_number -ne $manifest.version){throw 'Publish the matching mod version first and generate a released wiki before publishing its pages.'}
    foreach($page in $manifest.pages){if((Read-Page $page.file) -match 'Unreleased .+ review draft'){throw 'Unreleased wiki banner must not be published.'}}
    $backupFolder=Join-Path $root '.build/wiki-backups';[IO.Directory]::CreateDirectory($backupFolder)|Out-Null
    Save-Json (Join-Path $backupFolder ((Get-Date -Format 'yyyyMMdd-HHmmss')+'.json')) @{version=$manifest.version;wiki=$wiki;pages=$backup}
    function Save-Mapping {Save-Json $statePath @{package='Kuriyukihime-IvoryBastion';pages=@($manifest.pages | Where-Object {$mapping.ContainsKey($_.file)} | ForEach-Object {@{key=$_.key;file=$_.file;title=$_.title;id=$mapping[$_.file].id;slug=$mapping[$_.file].slug}})}}
    # Create full, readable pages with local crosslinks temporarily rendered as
    # plain labels. All content is useful before the second pass adds URLs.
    foreach($page in $manifest.pages){
        if($mapping.ContainsKey($page.file)){continue}
        $plain=[regex]::Replace((Read-Page $page.file),'\[([^\]]+)\]\((\d{2}-[a-z0-9-]+\.md)(#[^)]*)?\)','$1')
        $created=Invoke-WikiApi $api 'Post' @{title=$page.title;markdown_content=$plain}
        if([string]$created.id -notmatch '^\d+$' -or [string]$created.slug -notmatch '^\d+-[a-z0-9-]+$'){throw 'Unexpected created wiki page identity; refetch before retrying.'}
        $mapping[$page.file]=$created;$remote[[string]$created.id]=$created;Save-Mapping
        Write-Host ('Created '+$page.title)
    }
    $receipt=@()
    foreach($page in $manifest.pages){
        $text=Read-Page $page.file
        foreach($target in $manifest.pages){$text=$text.Replace(']('+ $target.file +')',']('+ $publicRoot+$mapping[$target.file].slug +')')}
        if($text -match '\]\(\d{2}-[a-z0-9-]+\.md'){throw 'Unresolved local wiki crosslink.'}
        $id=[string]$mapping[$page.file].id
        if(([string]$remote[$id].markdown_content).Trim() -cne $text.Trim()){
            $saved=Invoke-WikiApi $api 'Post' @{id=$id;title=$page.title;markdown_content=$text}
            if([string]$saved.id -ne $id){throw 'Wiki update changed a page ID.'}
        }
        $verified=Invoke-WikiApi ('/api/experimental/wiki/page/'+$id+'/')
        if($verified.title -cne $page.title -or $verified.markdown_content.Trim() -cne $text.Trim()){throw ('Public wiki verification failed: '+$page.title)}
        $receipt+=@{key=$page.key;id=$id;title=$page.title;url=($publicRoot+$verified.slug);sha256=(Hash-Text $verified.markdown_content.Trim())}
        Write-Host ('Verified '+$page.title)
    }
    $index=Invoke-WikiApi $api
    foreach($page in $receipt){if([string]$page.id -notin @($index.pages | ForEach-Object {[string]$_.id})){throw 'Verified page missing from public wiki index.'}}
    Save-Mapping
    Save-Json (Join-Path $root ('dist/IvoryBastion-Wiki-'+$manifest.version+'-upload.json')) @{publishedAtUtc=[DateTime]::UtcNow.ToString('o');version=$manifest.version;package='Kuriyukihime-IvoryBastion';wiki=$publicRoot;pages=$receipt}
    Write-Host ('Published and verified '+$receipt.Count+' wiki pages: '+$publicRoot)
}finally{
    $headers.Clear();$wikiToken=$null
    if($loadedToken){$loadedToken.Dispose()}
}
