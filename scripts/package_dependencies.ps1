param(
    [string]$ManifestPath = (Join-Path $PSScriptRoot '../package/manifest.json'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../dist'),
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$apiRoot = 'https://thunderstore.io/api/experimental/package/'
$catalogUrl = 'https://thunderstore.io/c/riskofrain2/api/v1/package/'
$selected = @{}
$metadata = @{}
$pending = New-Object 'System.Collections.Generic.Queue[string]'

function Split-Dependency([string]$Dependency) {
    if ($Dependency -notmatch '^([A-Za-z0-9_]+)-([A-Za-z0-9_]+)-(\d+\.\d+\.\d+)$') {
        throw ('Invalid Thunderstore dependency: ' + $Dependency)
    }
    [pscustomobject]@{
        Owner = $Matches[1]
        Name = $Matches[2]
        Version = [version]$Matches[3]
        Package = $Matches[1] + '-' + $Matches[2]
        FullName = $Dependency
    }
}

function Select-Dependency([string]$Dependency) {
    $parts = Split-Dependency $Dependency
    if (-not $selected.ContainsKey($parts.Package) -or
        $parts.Version -gt $selected[$parts.Package].Version) {
        $selected[$parts.Package] = $parts
        $pending.Enqueue($Dependency)
    }
}

$direct = @($manifest.dependencies)
if ($direct.Count -eq 0) { throw 'The manifest has no dependencies.' }
$directPackages = @($direct | ForEach-Object { (Split-Dependency $_).Package })
if (@($directPackages | Select-Object -Unique).Count -ne $direct.Count) {
    throw 'The manifest contains duplicate dependency packages.'
}
foreach ($dependency in $direct) { Select-Dependency $dependency }

# The global version API can resolve a package that the selected game's
# launcher cannot see. Resolve from the same community catalog as the manager.
$catalog = Invoke-RestMethod -Uri $catalogUrl -TimeoutSec 60 -Headers @{
    'User-Agent' = 'IvoryBastion-DependencyValidator'
}
$catalogPackages = @{}
foreach ($package in $catalog) { $catalogPackages[$package.full_name] = $package }

# Resolve one version per package. A newer directly required version takes
# precedence over older versions mentioned by another library's manifest.
while ($pending.Count -gt 0) {
    $dependency = $pending.Dequeue()
    $parts = Split-Dependency $dependency
    if ($selected[$parts.Package].FullName -ne $dependency) { continue }
    if (-not $catalogPackages.ContainsKey($parts.Package)) {
        throw ('Package is absent from the Risk of Rain 2 launcher catalog: ' + $parts.Package)
    }
    $entries = @($catalogPackages[$parts.Package].versions | Where-Object {
        $_.full_name -eq $dependency -and $_.is_active -eq $true
    })
    if ($entries.Count -ne 1) {
        throw ('Active version is absent from the Risk of Rain 2 launcher catalog: ' + $dependency)
    }
    $entry = $entries[0]
    $metadata[$dependency] = $entry
    foreach ($child in @($entry.dependencies)) { Select-Dependency $child }
}

$resolved = @($selected.Values | Sort-Object Package)
foreach ($parts in $resolved) {
    if (-not $metadata.ContainsKey($parts.FullName)) {
        throw ('Missing dependency metadata: ' + $parts.FullName)
    }
    foreach ($child in @($metadata[$parts.FullName].dependencies)) {
        $required = Split-Dependency $child
        if (-not $selected.ContainsKey($required.Package) -or
            $selected[$required.Package].Version -lt $required.Version) {
            throw ('Unresolved transitive dependency: ' + $child)
        }
    }
}
Write-Host ('Verified {0} direct requirements and {1} total packages in the live Risk of Rain 2 launcher catalog.' -f $direct.Count,$resolved.Count)
if ($CheckOnly) { return }

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$profile = [ordered]@{
    profileName = 'Ivory Bastion ' + $manifest.version_number
    mods = @($resolved | ForEach-Object {
        [ordered]@{
            name = $_.Package
            version = [ordered]@{ major = $_.Version.Major; minor = $_.Version.Minor; patch = $_.Version.Build }
            enabled = $true
        }
    })
}
$encoding = New-Object Text.UTF8Encoding($false)
$profileJson = ConvertTo-Json -InputObject $profile -Depth 6
# JSON is valid YAML, which is what the manager expects in export.r2x.
$profilePath = Join-Path $OutputDirectory ('IvoryBastion-Dependencies-' + $manifest.version_number + '.r2z')
$temporaryPath = $profilePath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$archive = [IO.Compression.ZipFile]::Open($temporaryPath,[IO.Compression.ZipArchiveMode]::Create)
try {
    $entry = $archive.CreateEntry('export.r2x')
    $writer = New-Object IO.StreamWriter($entry.Open(),$encoding)
    try { $writer.Write($profileJson) } finally { $writer.Dispose() }
} finally { $archive.Dispose() }

$archive = [IO.Compression.ZipFile]::OpenRead($temporaryPath)
try {
    if ($archive.Entries.Count -ne 1 -or $archive.Entries[0].FullName -ne 'export.r2x') {
        throw 'Unexpected dependency profile contents.'
    }
    $reader = New-Object IO.StreamReader($archive.Entries[0].Open())
    try { $roundtrip = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($roundtrip.mods.Count -ne $resolved.Count -or $roundtrip.profileName -ne $profile.profileName) {
        throw 'Dependency profile validation failed.'
    }
} finally { $archive.Dispose() }
Move-Item -LiteralPath $temporaryPath -Destination $profilePath -Force

$instructions = @(
    ('IVORY BASTION ' + $manifest.version_number + ' - LOCAL ZIP INSTALLATION'),
    '',
    'A local mod ZIP does not automatically download its manifest dependencies.',
    'For automatic downloads through Thunderstore Mod Manager or r2modman:',
    '',
    '1. Select Risk of Rain 2 and return to the profile selection screen.',
    '2. Choose Import/Update > Import from file and select:',
    ('   ' + [IO.Path]::GetFileName($profilePath)),
    '3. Choose CREATE A NEW PROFILE. Do not update an existing profile with this file;',
    '   updating replaces its mod selection. The manager will download the libraries.',
    '4. Open the new profile, then Settings > Import local mod and select:',
    ('   IvoryBastion-' + $manifest.version_number + '.zip'),
    '',
    'EXISTING PROFILES',
    'To keep an existing mod selection, install any missing requirements from the',
    'Online tab in that profile, then import the character ZIP. Package links follow.',
    'Install the same Ivory Bastion version and required libraries for every player.',
    '',
    'DIRECT REQUIREMENTS (verified against Thunderstore)',
    ''
)
foreach ($dependency in $direct) {
    $parts = Split-Dependency $dependency
    $instructions += $dependency
    $instructions += 'https://thunderstore.io/c/riskofrain2/p/' + $parts.Owner + '/' + $parts.Name + '/' + $parts.Version + '/'
}
$instructions += @('', 'The dependency profile also includes required libraries of these libraries.',
    'It contains no character DLL, game files, personal settings, or other profile data.',
    '', 'Manager behavior: https://wiki.thunderstore.io/mods/mod-not-visible')
$instructionsPath = Join-Path $OutputDirectory ('IvoryBastion-Install-' + $manifest.version_number + '.txt')
[IO.File]::WriteAllLines($instructionsPath,[string[]]$instructions,$encoding)

$audit = [ordered]@{
    checkedAtUtc = [DateTime]::UtcNow.ToString('o')
    api = $apiRoot
    launcherCatalog = $catalogUrl
    modVersion = $manifest.version_number
    directRequirements = $direct
    resolvedPackages = @($resolved | ForEach-Object {
        [ordered]@{
            fullName = $_.FullName
            dependencies = @($metadata[$_.FullName].dependencies)
            downloadUrl = $metadata[$_.FullName].download_url
        }
    })
}
$auditPath = Join-Path $OutputDirectory ('IvoryBastion-Dependencies-' + $manifest.version_number + '.json')
[IO.File]::WriteAllText($auditPath,(ConvertTo-Json -InputObject $audit -Depth 7),$encoding)
Get-Item -LiteralPath $profilePath,$instructionsPath,$auditPath | Select-Object FullName,Length
