$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifest = Get-Content -LiteralPath (Join-Path $root 'package/manifest.json') -Raw | ConvertFrom-Json
if ($manifest.name -ne 'IvoryBastion') { throw 'The published package name must remain IvoryBastion.' }

function Quote-Toml([string]$Value) { ConvertTo-Json -InputObject $Value -Compress }
$lines = @(
    '# Generated from package/manifest.json by scripts/sync_thunderstore_config.ps1.',
    '# The publishing credential is supplied separately through TCLI_AUTH_TOKEN.',
    '[config]',
    'schemaVersion = "0.0.1"',
    '',
    '[package]',
    'namespace = "Kuriyukihime"',
    ('name = ' + (Quote-Toml $manifest.name)),
    ('versionNumber = ' + (Quote-Toml $manifest.version_number)),
    ('description = ' + (Quote-Toml $manifest.description)),
    ('websiteUrl = ' + (Quote-Toml $manifest.website_url)),
    'containsNsfwContent = false',
    '',
    '[package.dependencies]'
)
foreach ($dependency in $manifest.dependencies) {
    if ($dependency -notmatch '^(.+)-(\d+\.\d+\.\d+)$') { throw ('Invalid dependency: ' + $dependency) }
    $lines += (Quote-Toml $Matches[1]) + ' = ' + (Quote-Toml $Matches[2])
}
$lines += @(
    '',
    '[build]',
    'icon = "package/icon.png"',
    'readme = "package/README.md"',
    'outdir = ".build/tcli-packages"',
    '',
    '[[build.copy]]',
    'source = "bin/Release/netstandard2.1/IvoryBastion.dll"',
    'target = "BepInEx/plugins/IvoryBastion/IvoryBastion.dll"',
    '',
    '[[build.copy]]',
    'source = "package/CHANGELOG.md"',
    'target = "CHANGELOG.md"',
    '',
    '[[build.copy]]',
    'source = "package/LICENSE"',
    'target = "LICENSE"',
    '',
    '[publish]',
    'repository = "https://thunderstore.io"',
    'communities = ["riskofrain2"]',
    '',
    '# Preserve the categories already selected on the published listing.',
    '[publish.categories]',
    'riskofrain2 = ["player-characters", "ai-generated"]',
    ''
)
[IO.File]::WriteAllLines((Join-Path $root 'thunderstore.toml'),[string[]]$lines,(New-Object Text.UTF8Encoding($false)))
