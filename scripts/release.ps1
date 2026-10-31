param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$Notes,
    [switch]$Publish,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
if ($CheckOnly -and ($Publish -or $Version -or $Notes)) {
    throw 'Use -CheckOnly on its own; it does not change or publish a release.'
}
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifestPath = Join-Path $root 'package/manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$credentialPath = Join-Path $env:APPDATA 'IvoryBastionPublishing/thunderstore-token.xml'
$api = 'https://thunderstore.io/api/experimental/package/Kuriyukihime/IvoryBastion/'
$listing = 'https://thunderstore.io/c/riskofrain2/p/Kuriyukihime/IvoryBastion/'
$published = Invoke-RestMethod -Uri $api -TimeoutSec 45
if ($published.full_name -ne 'Kuriyukihime-IvoryBastion' -or
    @($published.community_listings | Where-Object { $_.community -eq 'riskofrain2' }).Count -ne 1) {
    throw 'Could not verify the existing Ivory Bastion listing and community.'
}
if ($manifest.name -ne 'IvoryBastion') { throw 'The published package name must remain IvoryBastion.' }
$releaseVersion = $manifest.version_number
if ($Version) { $releaseVersion = $Version }

function Test-ReleaseArchive([string]$Path,[string]$ExpectedVersion) {
    if (-not (Test-Path -LiteralPath $Path)) { throw ('Release ZIP is missing: ' + $Path) }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $expected = @('manifest.json','README.md','CHANGELOG.md','icon.png','LICENSE','BepInEx/plugins/IvoryBastion/IvoryBastion.dll')
        $paths = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\','/') })
        if ($paths.Count -ne $expected.Count -or @(Compare-Object $expected $paths).Count -ne 0) {
            throw 'Release ZIP does not match the six-file upload allowlist.'
        }
        $reader = New-Object IO.StreamReader($archive.GetEntry('manifest.json').Open())
        try { $zipManifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        if ($zipManifest.name -ne 'IvoryBastion' -or $zipManifest.version_number -ne $ExpectedVersion) {
            throw 'Release ZIP package identity or version is incorrect.'
        }
        if (@(Compare-Object @($manifest.dependencies) @($zipManifest.dependencies)).Count -ne 0) {
            throw 'Release ZIP dependencies do not match package/manifest.json.'
        }
        $builtDll = Join-Path $root 'bin/Release/netstandard2.1/IvoryBastion.dll'
        if ([Reflection.AssemblyName]::GetAssemblyName($builtDll).Version.ToString(3) -ne $ExpectedVersion) {
            throw 'Built DLL version differs from the release version.'
        }
        $pluginSource = Get-Content -LiteralPath (Join-Path $root 'src/BastionPlugin.cs') -Raw
        if ($pluginSource -notmatch ('Version="' + [regex]::Escape($ExpectedVersion) + '"')) {
            throw 'Plugin version differs from the release version.'
        }
        $hashAlgorithm = [Security.Cryptography.SHA256]::Create()
        $dllEntry = @($archive.Entries | Where-Object { $_.FullName.Replace('\','/') -eq 'BepInEx/plugins/IvoryBastion/IvoryBastion.dll' })[0]
        $dllStream = $dllEntry.Open()
        try { $zipHash = [BitConverter]::ToString($hashAlgorithm.ComputeHash($dllStream)).Replace('-','') }
        finally { $dllStream.Dispose(); $hashAlgorithm.Dispose() }
        if ($zipHash -ne (Get-FileHash -LiteralPath $builtDll -Algorithm SHA256).Hash) {
            throw 'Packaged DLL differs from the verified build.'
        }
    } finally { $archive.Dispose() }
}

Push-Location $root
try {
    if ($CheckOnly) {
        & (Join-Path $PSScriptRoot 'package_dependencies.ps1') -CheckOnly
        Test-ReleaseArchive (Join-Path $root ('dist/IvoryBastion-' + $releaseVersion + '.zip')) $releaseVersion
        Write-Host ('Published version: ' + $published.latest.version_number)
        Write-Host ('Local package verified: ' + $releaseVersion)
        if ([version]$releaseVersion -le [version]$published.latest.version_number) {
            Write-Host 'The next upload needs a higher version; the current published version cannot be replaced.'
        }
        $hasCredential = -not [string]::IsNullOrWhiteSpace($env:TCLI_AUTH_TOKEN) -or (Test-Path -LiteralPath $credentialPath)
        Write-Host ('Publishing credential configured: ' + $hasCredential)
        return
    }

    # Reject immutable or older versions before changing files or sending an upload.
    if ([version]$releaseVersion -le [version]$published.latest.version_number) {
        $latestVersion = [version]$published.latest.version_number
        $nextVersion = '{0}.{1}.0' -f $latestVersion.Major,($latestVersion.Minor + 1)
        throw ('Version ' + $releaseVersion + ' is already published or older. Specify a newer -Version, for example ' + $nextVersion + '.')
    }
    if ([version]$releaseVersion -lt [version]$manifest.version_number) { throw 'A release cannot lower the local version.' }
    $changelogPath = Join-Path $root 'package/CHANGELOG.md'
    $changelog = Get-Content -LiteralPath $changelogPath -Raw -Encoding UTF8
    if ($changelog -notmatch ('(?m)^#{1,2}\s+' + [regex]::Escape($releaseVersion) + '\s*$')) {
        if ([string]::IsNullOrWhiteSpace($Notes)) {
            throw ('Add changelog notes for ' + $releaseVersion + ' or pass -Notes before preparing this release.')
        }
        $changelog = '# ' + $releaseVersion + "`n" + $Notes.Trim() + "`n`n" + $changelog
    }

    $encoding = New-Object Text.UTF8Encoding($false)
    $manifest.version_number = $releaseVersion
    $projectPath = Join-Path $root 'IvoryBastion.csproj'
    $project = Get-Content -LiteralPath $projectPath -Raw
    if ([regex]::Matches($project,'<Version>[^<]+</Version>').Count -ne 1) { throw 'Expected one project version.' }
    $pluginPath = Join-Path $root 'src/BastionPlugin.cs'
    $plugin = Get-Content -LiteralPath $pluginPath -Raw
    if ([regex]::Matches($plugin,'Version="\d+\.\d+\.\d+"').Count -ne 1) { throw 'Expected one plugin version.' }
    [IO.File]::WriteAllText($manifestPath,((ConvertTo-Json -InputObject $manifest -Depth 5) + "`n"),$encoding)
    [IO.File]::WriteAllText($projectPath,([regex]::Replace($project,'<Version>[^<]+</Version>',('<Version>' + $releaseVersion + '</Version>'))),$encoding)
    [IO.File]::WriteAllText($pluginPath,([regex]::Replace($plugin,'Version="\d+\.\d+\.\d+"',('Version="' + $releaseVersion + '"'))),$encoding)
    [IO.File]::WriteAllText($changelogPath,$changelog,$encoding)
    $readmePath = Join-Path $root 'package/README.md'
    $readme = Get-Content -LiteralPath $readmePath -Raw -Encoding UTF8
    $readme = [regex]::Replace($readme,'(IvoryBastion(?:-Dependencies)?-)\d+\.\d+\.\d+(\.(?:zip|r2z))',('${1}' + $releaseVersion + '${2}'))
    [IO.File]::WriteAllText($readmePath,$readme,$encoding)

    dotnet build IvoryBastion.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    dotnet run --project scripts/offline_checks/Checks.csproj -c Release -- .
    if ($LASTEXITCODE -ne 0) { throw 'Offline release checks failed.' }
    dotnet run --project scripts/review_checks/ReviewChecks.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Presentation review checks failed.' }
    dotnet run --project scripts/animation_review/AnimationReview.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Animation and energy surface checks failed.' }
    dotnet run --project scripts/camera_checks/Checks.csproj -c Release -- .
    if ($LASTEXITCODE -ne 0) { throw 'Camera cache and fade checks failed.' }
    dotnet run --project scripts/hud_aa_checks/Checks.csproj -c Release -- .build/hud-aa-geometry.json
    if ($LASTEXITCODE -ne 0) { throw 'HUD edge coverage checks failed.' }
    dotnet run --project scripts/defense_checks/Checks.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Defense integration checks failed.' }
    dotnet run --project scripts/flight_checks/Checks.csproj -c Release -- .
    if ($LASTEXITCODE -ne 0) { throw 'Flight/native gravity checks failed.' }
    dotnet run --project scripts/state_checks/Checks.csproj -c Release -- .
    if ($LASTEXITCODE -ne 0) { throw 'Native state identity checks failed.' }
    dotnet run --project scripts/network_checks/Checks.csproj -c Release -- .
    if ($LASTEXITCODE -ne 0) { throw 'Networking runtime/native checks failed.' }
dotnet run --project scripts/showcase_checks/ShowcaseChecks.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Selection placement and menu audio checks failed.' }
dotnet run --project scripts/language_checks/Checks.csproj -c Release -- .
if ($LASTEXITCODE -ne 0) { throw 'Startup language and balance-description checks failed.' }
dotnet run --project scripts/utility_checks/Checks.csproj -c Release -- .
if ($LASTEXITCODE -ne 0) { throw 'Utility ability and independent-charge checks failed.' }
dotnet run --project scripts/mine_visual_checks/MineVisualChecks.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Attached mine and target-mark visual checks failed.' }
dotnet run --project scripts/mine_electric_checks/Checks.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Charged mine electrical geometry and lifecycle checks failed.' }
    & (Join-Path $PSScriptRoot 'package.ps1')
    $zip = Join-Path $root ('dist/IvoryBastion-' + $releaseVersion + '.zip')
    Test-ReleaseArchive $zip $releaseVersion
    Write-Host ('Upload-ready package: ' + $zip)
    if (-not $Publish) { return }

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Could not restore the pinned Thunderstore CLI.' }
    $previousToken = $env:TCLI_AUTH_TOKEN
    $loadedToken = $null
    try {
        if ([string]::IsNullOrWhiteSpace($env:TCLI_AUTH_TOKEN)) {
            if (-not (Test-Path -LiteralPath $credentialPath)) {
                throw 'The upload ZIP is ready. Run scripts/setup_thunderstore_token.ps1 once to configure publishing, or provide TCLI_AUTH_TOKEN.'
            }
            $loadedToken = Import-Clixml -LiteralPath $credentialPath
            if ($loadedToken -isnot [Security.SecureString]) { throw 'The local publishing credential is invalid.' }
            $tokenPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($loadedToken)
            try { $env:TCLI_AUTH_TOKEN = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($tokenPointer) }
            finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($tokenPointer) }
        }
        dotnet tool run tcli -- publish --config-path thunderstore.toml --file $zip
        if ($LASTEXITCODE -ne 0) { throw 'Thunderstore did not confirm publication. Review its error before retrying.' }
    } finally {
        $env:TCLI_AUTH_TOKEN = $previousToken
        if ($loadedToken) { $loadedToken.Dispose() }
    }
    $receipt = [ordered]@{
        publishedAtUtc = [DateTime]::UtcNow.ToString('o')
        package = 'Kuriyukihime-IvoryBastion'
        version = $releaseVersion
        community = 'riskofrain2'
        listing = $listing
        sha256 = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    }
    [IO.File]::WriteAllText((Join-Path $root ('dist/IvoryBastion-' + $releaseVersion + '-upload.json')),(ConvertTo-Json -InputObject $receipt),$encoding)
    Write-Host ('Published ' + $releaseVersion + ': ' + $listing)
} finally { Pop-Location }
