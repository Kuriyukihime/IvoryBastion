param([string]$PreviousVersion='0.8.0',[string]$CurrentVersion='0.9.0',[switch]$AllowPresentationChanges,[switch]$AllowLaserLockOnChanges,[switch]$AllowRequestReliabilityChanges,[switch]$AllowBalanceConfigurationChanges,[switch]$AllowFlightGravityChanges,[switch]$AllowIndependentAegisChanges,[switch]$AllowStateResolutionChanges,[switch]$AllowDeliveryAuditChanges,[switch]$AllowUtilitySkillChanges,[switch]$AllowUpgradeTreeChanges,[switch]$AllowTargetingChanges)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -LiteralPath (Join-Path $root 'scripts/offline_checks/bin/Release/net10.0/Mono.Cecil.dll')
function Read-PackagedAssembly([string]$version) {
    $archivePath=Join-Path $root ('dist/IvoryBastion-'+$version+'.zip')
    if(-not (Test-Path -LiteralPath $archivePath)) {
        $indexPath=Join-Path $root 'docs/local-archives.json'
        if(-not (Test-Path -LiteralPath $indexPath)){throw ('Release baseline not found: '+$archivePath)}
        $index=Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $baseline=@($index.archives | Where-Object version -EQ $version)
        if($baseline.Count -ne 1 -or -not (Test-Path -LiteralPath $baseline[0].path)) {throw ('Archived release baseline not found: '+$version)}
        $archivePath=$baseline[0].path
        if((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $baseline[0].sha256){throw ('Archived baseline checksum differs: '+$version)}
    }
    $archive=[IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $entry=$archive.Entries | Where-Object { $_.FullName.Replace('\','/').EndsWith('/IvoryBastion.dll') } | Select-Object -First 1
        if (-not $entry) { throw 'Package is missing its plugin DLL.' }
        $memory=New-Object IO.MemoryStream
        $source=$entry.Open()
        try { $source.CopyTo($memory) } finally { $source.Dispose() }
        $memory.Position=0
        return [Mono.Cecil.AssemblyDefinition]::ReadAssembly($memory)
    } finally { $archive.Dispose() }
}
function Method-Shape($method) {
    $parts=New-Object 'System.Collections.Generic.List[string]'
    $parts.Add($method.FullName);$parts.Add($method.Attributes.ToString())
    if ($method.HasBody) {
        $parts.Add('init='+$method.Body.InitLocals+' stack='+$method.Body.MaxStackSize)
        foreach ($variable in $method.Body.Variables) { $parts.Add('local '+$variable.VariableType.FullName) }
        foreach ($instruction in $method.Body.Instructions) { $parts.Add($instruction.ToString()) }
        foreach ($handler in $method.Body.ExceptionHandlers) {
            $parts.Add('handler '+$handler.HandlerType+' '+$handler.TryStart+' '+$handler.TryEnd+' '+$handler.HandlerStart+' '+$handler.HandlerEnd+' '+$handler.CatchType)
        }
    }
    return $parts -join "`n"
}
function Bridge-Shape($method) {
    # Adding presentation fields changes Roslyn's generated member ordinals.
    # Normalize only those ordinals; retain every instruction, field, captured
    # value and lambda ordinal, and compare the lambda bodies separately below.
    $shape=Method-Shape $method
    $shape=$shape -replace '(?<=<>c__DisplayClass)\d+(?=_)','#'
    $shape=$shape -replace '(?<=<>9__)\d+(?=_)','#'
    return $shape -replace '(?<=b__)\d+(?=_)','#'
}
$previousAssembly=Read-PackagedAssembly $PreviousVersion
$currentAssembly=Read-PackagedAssembly $CurrentVersion
try {
    $criticalTypes=@('BastionTransformSync','BastionTransform','BastionController','BastionStatus','BastionEffect','BastionMeshDetail','BastionPrimary','BastionShot','BastionMain','BastionAnimation','BastionBoost','BastionSecondary','BastionSpecial','BastionPurchase')
    foreach($requestType in @('BastionRequests','BastionRequestBatch','BastionRequestRules','BastionRequestOutbox','BastionRequestWindow','BastionRequestAcks','BastionSnapshotSequence','BastionUnitStats')){
        if(@($previousAssembly.MainModule.Types | Where-Object Name -EQ $requestType).Count -and @($currentAssembly.MainModule.Types | Where-Object Name -EQ $requestType).Count){$criticalTypes+=$requestType}
    }
    # Explicit review scope for 0.20's native-channel receipts and retries. Every
    # other protected method/field, including combat, shields and revival, must
    # remain byte-for-byte identical to the preceding published package.
    $reliabilityMethods=@{
        BastionController=@('get_Requests','EnsureInitialized')
        BastionTransform=@('OnEnter','Resolve','FixedUpdate','OnExit')
        BastionStatus=@('.ctor','Serialize','Deserialize','OnReceived')
        BastionShot=@('OnEnter')
        BastionBoost=@('OnEnter','FixedUpdate','OnExit')
        BastionSecondary=@('OnEnter')
        BastionSpecial=@('OnEnter')
        BastionPurchase=@('OnEnter')
    }
    $addedMethods=@{BastionController=@('get_Requests','EnsureInitialized')}
$deliveryAuditMethods=@{BastionRequests=@('Submit','Receive','FixedUpdate','Process');BastionRequestOutbox=@('Acknowledge')}
$deliveryReviewed=0
    $removedMethods=@{BastionTransform=@('Resolve')}
    $newStatusFields=@('snapshot','noticeId','acknowledgements','notice')
    # 0.22 replaces fixed numbers and their hints with immutable host-profile
    # accessors. No request transport, receipt, renderer or protected field
    # changes are authorized by this scope.
    $balanceMethods=@{
        BastionTransformSync=@('Duration','Notice')
        BastionController=@('.ctor','Cooldown','Interval','NearGround','TryDeploymentAnchor','SetSiege','Shoot','ActivateSecondary','ActivateSpecial','FixedUpdate','ManageUnits')
        BastionShot=@('OnEnter')
        BastionSpecial=@('OnEnter')
    }
    $balanceReviewed=0
    # 0.24: native anti-gravity grants in the existing input-authority main
    # state; Aegis storage/decay and two additional status floats. No cast,
    # retry, receipt, targeting or transformation transport is in this scope.
    $aegisMethods=@{
        BastionController=@('.ctor','ActivateSecondary','FixedUpdate')
        BastionStatus=@('.ctor','Serialize','Deserialize','OnReceived')
    }
    $aegisFields=@{BastionController=@('AegisBarrier','LastAegisImpact');BastionStatus=@('aegisCapacity','aegisCharge')}
    $aegisReviewed=0;$flightReviewed=0
# 1.0: independent utility casts; original transport and defenses stay protected.
$utilityMethods=@{BastionMain=@('HandleMovements');BastionRequests=@('Receive','Process','Remaining')}
$utilityReviewed=0
$targetingReviewed=0
$upgradeReviewed=0
    if($AllowPresentationChanges){$criticalTypes=@($criticalTypes | Where-Object { $_ -ne 'BastionAnimation' })}
    $methods=0
    foreach ($name in $criticalTypes) {
        $previousType=$previousAssembly.MainModule.Types | Where-Object Name -EQ $name
        $currentType=$currentAssembly.MainModule.Types | Where-Object Name -EQ $name
        if (-not $previousType -or -not $currentType) { throw ('Missing type '+$name) }
        if(-not $AllowRequestReliabilityChanges -and $previousType.Methods.Count -ne $currentType.Methods.Count) { throw ('Method count differs: '+$name) }
        if($AllowRequestReliabilityChanges){
            foreach($newMethod in $currentType.Methods){
                if(-not @($previousType.Methods | Where-Object FullName -EQ $newMethod.FullName).Count -and $addedMethods[$name] -notcontains $newMethod.Name){throw ('Unexpected new protected method: '+$newMethod.FullName)}
            }
        }
        foreach ($method in $previousType.Methods) {
            $currentMethod=$currentType.Methods | Where-Object FullName -EQ $method.FullName
            if($AllowTargetingChanges -and (($name -eq 'BastionController' -and $method.Name -eq 'Shoot') -or ($name -eq 'BastionShot' -and $method.Name -eq 'OnEnter'))){
                if(-not $currentMethod){throw ('Missing targeting entry point: '+$method.FullName)}
                $oldCalls=@($method.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.DeclaringType.FullName+'::'+$_.Operand.Name })
                $newCalls=@($currentMethod.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.DeclaringType.FullName+'::'+$_.Operand.Name })
                if(($oldCalls -join "`n") -cne ($newCalls -join "`n")){throw ('Targeting signature update changed native call ordering: '+$method.FullName)}
                if(@($newCalls | Where-Object {$_ -eq 'IvoryBastion.BastionCombat::Select'}).Count -ne 1){throw ('Targeting entry point must retain one battery selection: '+$method.FullName)}
                $targetingReviewed++;continue
            }
if($AllowUpgradeTreeChanges -and $name -eq 'BastionController' -and @('ActivateSecondary','ActivateSpecial') -contains $method.Name){
            if(-not $currentMethod){throw ('Missing accepted-cast entry point: '+$method.FullName)}
            $calls=@($currentMethod.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.DeclaringType.Name+'::'+$_.Operand.Name })
            $required=@('NetworkServer::get_active','BastionController::get_Alive','BastionBank::Spend','BastionController::SendStatus')
            if($method.Name -eq 'ActivateSecondary'){$required+=@('BastionAegisReserve::Grant','BastionController::Init','BastionUpgradeEffects::AegisCast')}
            else{$required+=@('BastionWarheads::Launch','BastionBank::Restock','BastionBank::get_ShiftDuration','BastionUpgradeEffects::BlueShiftCast')}
            foreach($call in $required){if($calls -notcontains $call){throw ('Upgrade hook lost original host-owned action or resource call: '+$call)}}
            $upgradeReviewed++;continue
        }
        if($AllowUtilitySkillChanges -and $utilityMethods[$name] -contains $method.Name){
    if(-not $currentMethod){throw ('Missing utility bridge: '+$method.FullName)}
    $calls=@($currentMethod.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.DeclaringType.Name+'::'+$_.Operand.Name })
    if($name -eq 'BastionMain'){
        foreach($call in @('EntityState::get_isAuthority','CharacterMotor::set_gravityParameters','CharacterMotor::set_flightParameters','BastionUtility::Move')){
            if($calls -notcontains $call){throw ('Utility movement lost native flight or authority gate: '+$call)}
        }
    }elseif($method.Name -eq 'Receive'){
        if($calls -notcontains 'NetworkServer::get_active' -or $calls -notcontains 'BastionRequestWindow::Accept'){throw 'Utility receive must retain host gate and original deduplication.'}
    }elseif($method.Name -eq 'Process'){
        foreach($call in @('BastionUtility::TryCast','BastionController::Shoot','BastionController::ActivateSecondary','BastionController::ActivateSpecial','BastionRequests::Transform')){
            if($calls -notcontains $call){throw ('Utility dispatch lost an original cast bridge: '+$call)}
        }
    }elseif($calls -notcontains 'BastionUtilityReserve::Wait'){throw 'Utility cooldown must use its own reserve.'}
    $utilityReviewed++;continue
}
if($AllowDeliveryAuditChanges -and $deliveryAuditMethods[$name] -contains $method.Name){
if(-not $currentMethod){throw ('Missing delivery entry point: '+$method.FullName)}
$deliveryReviewed++;continue
}
            if(-not $currentMethod -and $AllowIndependentAegisChanges -and $name -eq 'BastionController' -and $method.Name.StartsWith('<')){
                # Added fields shift Roslyn member ordinals even though the
                # existing deployment predicate itself remains unchanged.
                $lambdaName=$method.FullName -replace '(?<=b__)\d+(?=_)','#'
                $currentMethod=$currentType.Methods | Where-Object { ($_.FullName -replace '(?<=b__)\d+(?=_)','#') -ceq $lambdaName }
            }
            if($AllowRequestReliabilityChanges -and $reliabilityMethods[$name] -contains $method.Name){
                if(-not $currentMethod -and $removedMethods[$name] -notcontains $method.Name){throw ('Missing request bridge: '+$method.FullName)}
                continue
            }
            if($AllowBalanceConfigurationChanges -and $balanceMethods[$name] -contains $method.Name){
                if(-not $currentMethod){throw ('Missing configured entry point: '+$method.FullName)}
                $calls=@($currentMethod.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.DeclaringType.Name+'::'+$_.Operand.Name })
                if(-not @($calls | Where-Object { $_ -match '^(BastionBalance|BastionDefenseRules|BastionWarheadGeometry)::' }).Count){throw ('Configured method lacks a shared balance accessor: '+$method.FullName)}
                if($name -eq 'BastionController' -and @('SetSiege','Shoot','ActivateSecondary','ActivateSpecial','FixedUpdate') -contains $method.Name){
                    if($calls -notcontains 'NetworkServer::get_active'){throw ('Missing host gate: '+$method.FullName)}
                }
                if(@('BastionShot','BastionSpecial') -contains $name){
                    if($calls -notcontains 'EntityState::get_isAuthority' -or $calls -notcontains 'BastionRequests::Submit'){throw ('Configured hint changed the authority/request bridge: '+$name)}
                }
                $balanceReviewed++;continue
            }
            if($AllowLaserLockOnChanges -and $name -eq 'BastionController' -and $method.Name -eq 'Shoot') {
                if(-not $currentMethod){throw 'Missing laser fire entry point.'}
                $calls=@($currentMethod.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.DeclaringType.Name+'::'+$_.Operand.Name })
                if($calls -notcontains 'NetworkServer::get_active' -or $calls -notcontains 'BastionLaserTracker::Find' -or $calls -notcontains 'BastionCombat::Bullet'){throw 'Authorized lock-on change lacks the server gate, target selection or native shot.'}
                continue
            }
            if($AllowFlightGravityChanges -and $name -eq 'BastionMain' -and @('HandleMovements','OnExit') -contains $method.Name){
                if(-not $currentMethod){throw ('Missing flight method: '+$method.FullName)}
                $calls=@($currentMethod.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.DeclaringType.Name+'::'+$_.Operand.Name })
                if($calls -notcontains 'CharacterMotor::set_gravityParameters' -or $calls -notcontains 'CharacterMotor::set_flightParameters'){throw ('Flight must balance both native grants: '+$method.Name)}
                if($method.Name -eq 'HandleMovements' -and $calls -notcontains 'EntityState::get_isAuthority'){throw 'Flight lost its movement authority gate.'}
                $flightReviewed++;continue
            }
            if($AllowStateResolutionChanges -and $name -eq 'BastionController' -and $method.Name -eq 'Awake'){
                if(-not $currentMethod){throw 'Missing body Awake state binding.'}
                $calls=@($currentMethod.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.DeclaringType.Name+'::'+$_.Operand.Name })
                if($calls -notcontains 'Component::GetComponent' -or $calls -notcontains 'BastionBodyStates::Bind'){throw 'Body initialization must retain its CharacterBody and bind active runtime state types.'}
                continue
            }
            if($AllowIndependentAegisChanges -and $aegisMethods[$name] -contains $method.Name){
                if(-not $currentMethod){throw ('Missing Aegis/status method: '+$method.FullName)}
                $calls=@($currentMethod.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.DeclaringType.Name+'::'+$_.Operand.Name })
                if($name -eq 'BastionController' -and @('ActivateSecondary','FixedUpdate') -contains $method.Name){
                    if($calls -notcontains 'NetworkServer::get_active'){throw ('Aegis lost its host gate: '+$method.Name)}
                    if($method.Name -eq 'ActivateSecondary' -and ($calls -notcontains 'BastionAegisReserve::Grant' -or $calls -contains 'HealthComponent::AddBarrier' -or $calls -notcontains 'BastionBank::Spend')){throw 'Aegis must spend its original charge and grant only its independent pool.'}
                    if($method.Name -eq 'FixedUpdate' -and ($calls -notcontains 'BastionAegisReserve::Tick' -or $calls -notcontains 'BastionAbilityShield::Tick')){throw 'Both independent reserve updates are required.'}
                }
                if($name -eq 'BastionStatus' -and $method.Name -eq 'OnReceived' -and ($calls -notcontains 'BastionRequests::AcceptSnapshot' -or $calls -notcontains 'BastionAegisReserve::Receive' -or $calls -notcontains 'BastionTransformSync::ReceiveReply')){throw 'Aegis snapshot lost sequence validation or transformation receipts.'}
                $aegisReviewed++;continue
            }
            if($AllowIndependentAegisChanges -and $name -eq 'BastionController'){
                if(-not $currentMethod -or (Bridge-Shape $method) -cne (Bridge-Shape $currentMethod)){throw ('Protected method differs: '+$method.FullName)}
                $methods++;continue
            }
            if (-not $currentMethod -or (Method-Shape $method) -cne (Method-Shape $currentMethod)) { throw ('Method differs: '+$method.FullName) }
            $methods++
        }
        $oldFields=@($previousType.Fields | ForEach-Object { $_.FullName+':'+$_.Attributes+':'+$_.Constant }) -join "`n"
        $fields=@($currentType.Fields)
        if($AllowIndependentAegisChanges -and $aegisFields.ContainsKey($name)){
            foreach($field in $fields){if(-not @($previousType.Fields | Where-Object FullName -EQ $field.FullName).Count -and $aegisFields[$name] -notcontains $field.Name){throw ('Unexpected defense field: '+$field.FullName)}}
            $fields=@($fields | Where-Object { $aegisFields[$name] -notcontains $_.Name })
        }
        if($AllowRequestReliabilityChanges -and $name -eq 'BastionStatus'){
            $fields=@($fields | Where-Object { $newStatusFields -notcontains $_.Name })
            foreach($field in $currentType.Fields){if(-not @($previousType.Fields | Where-Object FullName -EQ $field.FullName).Count -and $newStatusFields -notcontains $field.Name){throw ('Unexpected status field: '+$field.FullName)}}
        }
        if($AllowUtilitySkillChanges -and $name -eq 'BastionMain'){
    $extra=@($fields | Where-Object Name -EQ 'utility')
    if($extra.Count -ne 1 -or $extra[0].FieldType.FullName -ne 'IvoryBastion.BastionUtility'){throw 'Only the utility movement cache may be added to BastionMain.'}
    $fields=@($fields | Where-Object Name -NE 'utility')
}
$newFields=@($fields | ForEach-Object { $_.FullName+':'+$_.Attributes+':'+$_.Constant }) -join "`n"
        if ($oldFields -cne $newFields) { throw ('Fields differ: '+$name) }
    }
    if($AllowPresentationChanges){
        $oldAnimation=$previousAssembly.MainModule.Types | Where-Object Name -EQ 'BastionAnimation'
        $newAnimation=$currentAssembly.MainModule.Types | Where-Object Name -EQ 'BastionAnimation'
    foreach($name in @('Muzzle','MissilePort','SkyfallPort','CancelTransform')){
            $oldMethod=$oldAnimation.Methods | Where-Object Name -EQ $name
            $newMethod=$newAnimation.Methods | Where-Object Name -EQ $name
            if((Bridge-Shape $oldMethod) -cne (Bridge-Shape $newMethod)){throw ('Protected presentation bridge differs: '+$name)}
            $oldHelpers=@($oldAnimation.NestedTypes | ForEach-Object { $_.Methods } | Where-Object { $_.Name.StartsWith('<'+$name+'>') })
            $newHelpers=@($newAnimation.NestedTypes | ForEach-Object { $_.Methods } | Where-Object { $_.Name.StartsWith('<'+$name+'>') })
            if($oldHelpers.Count -ne $newHelpers.Count){throw ('Protected bridge lambda count differs: '+$name)}
            foreach($helper in $oldHelpers){
                $shape=Bridge-Shape $helper
                if(-not @($newHelpers | Where-Object { (Bridge-Shape $_) -ceq $shape }).Count){throw ('Protected bridge lambda differs: '+$helper.FullName)}
            }
        }
    }
    $scope=if($AllowPresentationChanges){'multiplayer, firing, transformation and permanent-LOD'}else{'multiplayer, firing, transformation, animation and permanent-LOD'}
    $fieldScope=if($AllowRequestReliabilityChanges -or $AllowIndependentAegisChanges -or $AllowUtilitySkillChanges){'all pre-existing fields'}else{'all fields'}
    Write-Output "PASS: $methods protected methods and $fieldScope across $($criticalTypes.Count) $scope types match $PreviousVersion exactly."
    if($AllowRequestReliabilityChanges -or $AllowDeliveryAuditChanges){
        $requests=$currentAssembly.MainModule.Types | Where-Object Name -EQ 'BastionRequests'
        $batch=$currentAssembly.MainModule.Types | Where-Object Name -EQ 'BastionRequestBatch'
        if(-not $requests -or $batch.BaseType.FullName -ne 'EntityStates.EntityState'){throw 'Missing native request transport.'}
        foreach($gate in @('Receive','FixedUpdate')){
            $entry=$requests.Methods | Where-Object Name -EQ $gate
            if(-not @($entry.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.Name -eq 'NetworkServer' -and $_.Operand.Name -eq 'get_active' }).Count){throw ('Missing host gate on '+$gate)}
        }
        $networkProject=Join-Path $root 'scripts/network_checks/Checks.csproj'
        & dotnet run --project $networkProject -c Release -- $root
        if($LASTEXITCODE -ne 0){throw 'Request reliability runtime/native regression checks failed.'}
        Write-Output 'PASS: only the listed request bridges/status additions changed; host combat, form commit, animation, shield and revival methods remain exact.'
    }
    if($AllowDeliveryAuditChanges){Write-Output "PASS: $deliveryReviewed intentional delivery/receipt methods reviewed; payloads, authority channel, controller skills, flight and status serialization remain exact."}
    if($AllowLaserLockOnChanges){Write-Output 'PASS: only the authorized Controller.Shoot laser entry point differs; its server gate, lock selection and native shot calls are present.'}
    if($AllowBalanceConfigurationChanges){Write-Output "PASS: $balanceReviewed listed balance/hint methods retain shared accessors and their host/authority gates; all protected fields and request transport remain exact."}
    if($AllowFlightGravityChanges){Write-Output "PASS: $flightReviewed intentional flight methods balance native flight/anti-gravity grants; movement authority is preserved."}
    if($AllowIndependentAegisChanges){Write-Output "PASS: $aegisReviewed listed defense/status methods and four named fields reviewed for independent Aegis; all request transport, firing and transformation methods remain protected."}
    if($AllowStateResolutionChanges){Write-Output 'PASS: only Controller.Awake intentionally adds active-assembly state binding; all movement, shield, Aegis, skill requests, retries, receipts and form commits remain exact.'}
} finally { $previousAssembly.Dispose();$currentAssembly.Dispose() }

if($AllowTargetingChanges){
    if($targetingReviewed -ne 2){throw 'Expected exactly two targeting entry-point signature updates.'}
    Write-Output 'PASS: two targeting entry-point signature updates retain identical native call ordering; original request/retry/receipt, state serialization, flight, transformation, defenses and all protected fields remain exact.'
}

if($AllowUpgradeTreeChanges){
    if($upgradeReviewed -ne 2){throw 'Expected exactly two accepted-cast upgrade hooks.'}
    Write-Output 'PASS: two intentional accepted-cast mastery hooks reviewed; all original request/retry/receipt, status serialization, flight, transformation and shield absorption methods remain protected.'
}

if($AllowUtilitySkillChanges){
    if($utilityReviewed -ne 4){throw 'Expected exactly four intentional utility bridges.'}
    & dotnet run --project (Join-Path $root 'scripts/utility_checks/Checks.csproj') -c Release -- $root
    if($LASTEXITCODE -ne 0){throw 'Utility runtime/native checks failed.'}
    Write-Output 'PASS: four intentional utility bridges reviewed. Original retry/receipt, flight, transformation, shield absorption and existing message layouts remain protected; accepted-cast mastery hooks require their separate explicit audit switch.'
}
