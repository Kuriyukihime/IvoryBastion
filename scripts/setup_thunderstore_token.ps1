$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This setup uses Windows user-bound credential encryption. On other systems, set TCLI_AUTH_TOKEN instead.'
}
$credentialDirectory = Join-Path $env:APPDATA 'IvoryBastionPublishing'
$credentialPath = Join-Path $credentialDirectory 'thunderstore-token.xml'
Write-Host 'Create a service account under Thunderstore > Teams > Kuriyukihime > Service Accounts.'
Write-Host 'Paste its token into the hidden prompt below. It will be encrypted for this Windows user.'
$secureToken = Read-Host -Prompt 'Thunderstore publishing token' -AsSecureString
try {
    if ($secureToken.Length -eq 0) { throw 'No token was entered.' }
    New-Item -ItemType Directory -Path $credentialDirectory -Force | Out-Null
    $secureToken | Export-Clixml -LiteralPath $credentialPath
    Write-Host 'Publishing credential saved outside the project and release packages.'
} finally { $secureToken.Dispose() }
