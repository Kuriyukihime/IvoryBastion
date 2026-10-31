$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifest = Get-Content -LiteralPath (Join-Path $root 'package/manifest.json') -Raw | ConvertFrom-Json
$dll = Join-Path $root 'bin/Release/netstandard2.1/IvoryBastion.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw 'Build the release DLL before packaging.' }
$version = [Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString(3)
if ($version -ne $manifest.version_number) { throw 'DLL and manifest versions differ.' }
# Keep the original mobile launcher portrait even after model-preview renders.
$portrait = Join-Path $root 'assets/source/package_mobile_icon.png'
if ((Get-FileHash -LiteralPath $portrait -Algorithm SHA256).Hash -ne '12022AF9291E7E38561D349A298006E733E623A834670D2B93D05B469349BF9A') { throw 'The original launcher portrait source changed.' }
Copy-Item -LiteralPath $portrait -Destination (Join-Path $root 'package/icon.png') -Force
& (Join-Path $PSScriptRoot 'sync_thunderstore_config.ps1')
& (Join-Path $PSScriptRoot 'package_dependencies.ps1')
$stage = Join-Path $root ('.build/package-' + [Guid]::NewGuid().ToString('N'))
try {
$plugin = Join-Path $stage 'BepInEx/plugins/IvoryBastion'
New-Item -ItemType Directory -Path $plugin -Force | Out-Null
foreach ($name in @('manifest.json','README.md','CHANGELOG.md','icon.png','LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $root ('package/' + $name)) -Destination (Join-Path $stage $name)
}
Copy-Item -LiteralPath $dll -Destination (Join-Path $plugin 'IvoryBastion.dll')
$zip = Join-Path $root ('dist/IvoryBastion-' + $manifest.version_number + '.zip')
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
if (Test-Path -LiteralPath $zip) {
    $backup = Join-Path $root ('dist/IvoryBastion-' + $manifest.version_number + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.zip')
    Move-Item -LiteralPath $zip -Destination $backup
}
$archive = [IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $stage -File -Recurse) {
        $entryPath = $file.FullName.Substring($stage.Length + 1).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$entryPath,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $paths = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\','/') })
    foreach ($required in @('manifest.json','README.md','CHANGELOG.md','icon.png','LICENSE','BepInEx/plugins/IvoryBastion/IvoryBastion.dll')) {
        if ($paths -notcontains $required) { throw ('Missing package file: ' + $required) }
    }
    if ($paths.Count -ne 6) { throw 'Unexpected package contents.' }
} finally { $archive.Dispose() }
Get-Item -LiteralPath $zip | Select-Object FullName,Length
Get-FileHash -LiteralPath $zip -Algorithm SHA256
} finally {
    # Staging contains a full uncompressed plugin copy; never retain it per build.
    $stageRoot=[IO.Path]::GetFullPath((Join-Path $root '.build'))+[IO.Path]::DirectorySeparatorChar
    $resolvedStage=[IO.Path]::GetFullPath($stage)
    if(-not $resolvedStage.StartsWith($stageRoot,[StringComparison]::OrdinalIgnoreCase) -or
       [IO.Path]::GetFileName($resolvedStage) -notmatch '^package-[a-f0-9]{32}$') {
        throw 'Refusing to clean a staging path outside this repository.'
    }
    if(Test-Path -LiteralPath $resolvedStage){Remove-Item -LiteralPath $resolvedStage -Recurse -Force}
}
