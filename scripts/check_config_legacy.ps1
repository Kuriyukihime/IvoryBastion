param([string]$Root=([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))))
$ErrorActionPreference='Stop'
# The installed BepInEx 5 DLL is Required32Bit. Compile the actual catalog and
# binding code for the matching Framework CLR, without modifying dependencies.
$output=Join-Path $Root '.build/config_legacy'
[IO.Directory]::CreateDirectory($output) | Out-Null
$game='X:\SteamLibrary\steamapps\common\Risk of Rain 2\Risk of Rain 2_Data\Managed'
$core=Join-Path $env:APPDATA 'Thunderstore Mod Manager/DataFolder/RiskOfRain2/profiles/Papi and Ruby/BepInEx/core'
foreach($path in @((Join-Path $core 'BepInEx.dll'),(Join-Path $game 'UnityEngine.dll'),(Join-Path $game 'UnityEngine.CoreModule.dll'),(Join-Path $game 'UnityEngine.InputLegacyModule.dll'))){Copy-Item -LiteralPath $path -Destination $output -Force}
$sdk=Get-ChildItem -LiteralPath (Join-Path $env:ProgramFiles 'dotnet/sdk') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$compiler=Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll'
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319'
$exe=Join-Path $output 'ConfigLegacyChecks.exe'
$arguments=@($compiler,'/nologo','/target:exe','/platform:x86','/optimize+','/langversion:latest','/nostdlib',('/out:'+$exe))
foreach($dll in @('mscorlib.dll','System.dll','System.Core.dll')){$arguments+=('/reference:'+(Join-Path $framework $dll))}
foreach($dll in @('BepInEx.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.InputLegacyModule.dll')){$arguments+=('/reference:'+(Join-Path $output $dll))}
$arguments+=@((Join-Path $Root 'src/BastionBalanceValues.cs'),(Join-Path $Root 'src/BastionBalanceConfig.cs'),(Join-Path $Root 'src/BastionHotkey.cs'),(Join-Path $Root 'src/BastionConfigDefaults.cs'),(Join-Path $Root 'scripts/offline_checks/HotkeyLegacyChecks.cs'),(Join-Path $Root 'scripts/offline_checks/ConfigDefaultsChecks.cs'),(Join-Path $Root 'scripts/offline_checks/ConfigLegacyChecks.cs'))
& dotnet @arguments
if($LASTEXITCODE -ne 0){throw 'Legacy config test compilation failed.'}
& $exe $Root
if($LASTEXITCODE -ne 0){throw 'Real BepInEx config tests failed.'}
