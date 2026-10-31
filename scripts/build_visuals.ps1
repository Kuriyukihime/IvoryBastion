param(
    [string]$AssetPython = (Join-Path $PSScriptRoot '..\..\Nightbound\.build\shape-env\Scripts\python.exe'),
    [switch]$RebuildMeshes
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$AssetPython = [IO.Path]::GetFullPath($AssetPython)
if (-not (Test-Path -LiteralPath $AssetPython)) { throw 'Set -AssetPython to a Python environment with scripts/presentation-requirements.txt installed.' }
function Run-AssetScript([string]$Script, [string[]]$ScriptArgs = @()) {
    & $AssetPython (Join-Path $projectRoot ('scripts/' + $Script)) @ScriptArgs
    if ($LASTEXITCODE -ne 0) { throw ('Asset step failed: ' + $Script) }
}
Push-Location -LiteralPath $projectRoot
try {
    if ($RebuildMeshes) {
        Run-AssetScript 'seraph_hybrid.py' @('mobile')
        Run-AssetScript 'seraph_hybrid.py' @('siege')
    }
    Run-AssetScript 'make_presentation_media.py'
    Run-AssetScript 'make_thruster_vfx.py'
    dotnet build IvoryBastion.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Initial build failed.' }
    dotnet run --project scripts/offline_checks/Checks.csproj -c Release -- .
    if ($LASTEXITCODE -ne 0) { throw 'Runtime geometry export/checks failed.' }
    dotnet run --project scripts/effect_study/Effects.csproj -c Release -- .
    if ($LASTEXITCODE -ne 0) { throw 'Effect geometry export/checks failed.' }
    dotnet run --project scripts/thruster_study/Thrusters.csproj -c Release -- .
    if ($LASTEXITCODE -ne 0) { throw 'Thruster geometry export/checks failed.' }
    Run-AssetScript 'presentation_mesh.py'
    Run-AssetScript 'review_reconstructed_seraph.py'
    Run-AssetScript 'review_presentation_poses.py'
    Run-AssetScript 'review_expressive_flight.py' @('--motion')
    Run-AssetScript 'preview_shield.py'
    Run-AssetScript 'preview_siege_shield.py'
    Run-AssetScript 'preview_effects.py'
    Run-AssetScript 'preview_thruster_plumes.py'
    Run-AssetScript 'review_presentation_hud.py'
    Run-AssetScript 'make_package_icon.py'
    Run-AssetScript 'verify_assets.py'
    dotnet build IvoryBastion.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Final build failed.' }
    dotnet run --project scripts/offline_checks/Checks.csproj -c Release -- .
    if ($LASTEXITCODE -ne 0) { throw 'Final offline checks failed.' }
    # Packaging is separate; inspect the rendered previews first.
} finally { Pop-Location }
