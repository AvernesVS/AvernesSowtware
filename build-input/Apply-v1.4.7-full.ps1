$ErrorActionPreference = 'Stop'
$root = $env:SOURCE_ROOT
if ([string]::IsNullOrWhiteSpace($root) -or -not (Test-Path -LiteralPath $root)) { throw 'SOURCE_ROOT is invalid.' }
$nl = [string][char]10

function Read-Text([string]$relative) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing source file: $relative" }
    return ([IO.File]::ReadAllText($path) -replace '\r','')
}
function Write-Text([string]$relative, [string]$text) {
    [IO.File]::WriteAllText((Join-Path $root $relative), $text, (New-Object Text.UTF8Encoding($true)))
}
function Replace-Exact([string]$text, [string]$old, [string]$new, [string]$label) {
    $old = $old -replace '',''
    $new = $new -replace '',''
    $count = ([regex]::Matches($text, [regex]::Escape($old))).Count
    if ($count -ne 1) { throw "Expected exactly one match for $label; found $count" }
    return $text.Replace($old, $new)
}

$corePath = 'src/Core/AvernesSowtwareScript.cs'
$core = Read-Text $corePath
$core = Replace-Exact $core '        private readonly HashSet<int> _bodyguardDismissalDetachedHandles = new HashSet<int>();' ('        private readonly HashSet<int> _bodyguardDismissalDetachedHandles = new HashSet<int>();' + $nl + '        private readonly HashSet<int> _bodyguardDismissalGroupRemovedHandles = new HashSet<int>();') 'bodyguard dismissal phase state'

# Legacy GTA V dispatch enum: ArmyVehicle=14. Change only Army calls; keep police helicopter service 2 unchanged.
$core = Replace-Exact $core 'Function.Call((Hash)0xDC0F817884CDD856UL, 15, true);  // ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle' 'Function.Call((Hash)0xDC0F817884CDD856UL, 14, false); // ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle' 'Fort Zancudo Army dispatch disable'
$core = Replace-Exact $core 'Function.Call((Hash)0xDC0F817884CDD856UL, 15, true); // ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle' 'Function.Call((Hash)0xDC0F817884CDD856UL, 14, true); // ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle' 'Fort Zancudo Army dispatch restore'
$resourceOld = 'Function.Call((Hash)0x9B2BD3773123EA2FUL, 15, false); // BLOCK_DISPATCH_SERVICE_RESOURCE_CREATION'
$resourceMatches = [regex]::Matches($core, [regex]::Escape($resourceOld))
if ($resourceMatches.Count -ne 2) { throw "Expected 2 Army resource-block calls; found $($resourceMatches.Count)" }
$resourceRestore = 'Function.Call((Hash)0x9B2BD3773123EA2FUL, 14, false); // BLOCK_DISPATCH_SERVICE_RESOURCE_CREATION'
$resourceBlock = 'Function.Call((Hash)0x9B2BD3773123EA2FUL, 14, true); // BLOCK_DISPATCH_SERVICE_RESOURCE_CREATION'
$core = $core.Remove($resourceMatches[1].Index, $resourceOld.Length).Insert($resourceMatches[1].Index, $resourceRestore)
$core = $core.Remove($resourceMatches[0].Index, $resourceOld.Length).Insert($resourceMatches[0].Index, $resourceBlock)
$updateStart = $core.IndexOf('private void UpdateFortZancudoArmyDispatch')
$restoreStart = $core.IndexOf('private void RestoreFortZancudoArmyDispatch', $updateStart)
$updateBlock = $core.Substring($updateStart, $restoreStart - $updateStart)
$updateBlock = Replace-Exact $updateBlock '_armyVehicleDispatchSuppressed = false;' '_armyVehicleDispatchSuppressed = true;' 'Fort Zancudo suppression state'
$core = $core.Substring(0,$updateStart) + $updateBlock + $core.Substring($restoreStart)
$updateStart = $core.IndexOf('private void UpdateFortZancudoArmyDispatch')
$restoreStart = $core.IndexOf('private void RestoreFortZancudoArmyDispatch', $updateStart)
$updateBlock = $core.Substring($updateStart, $restoreStart - $updateStart)
$updateBlock = Replace-Exact $updateBlock '                // Keep Army vehicle and police helicopter dispatch enabled. Newer Wanted / 911
                // owns these response services; disabling or blocking them can leave spawned
                // emergency transports without a valid mission/driver task.' @'
                // Suppress only Army vehicle dispatch inside Fort Zancudo.
                // Legacy DT_ArmyVehicle=14; service 15 is BikerBackup.
                // Police helicopter dispatch is left entirely unchanged.
'@ 'Fort Zancudo dispatch comment'
$updateBlock = Replace-Exact $updateBlock '                Function.Call((Hash)0xDC0F817884CDD856UL, 2, true);   // ENABLE_DISPATCH_SERVICE: DT_PoliceHelicopter' '' 'leave police helicopter dispatch untouched'
$updateBlock = Replace-Exact $updateBlock '                Function.Call((Hash)0x9B2BD3773123EA2FUL, 2, false);  // BLOCK_DISPATCH_SERVICE_RESOURCE_CREATION' '' 'leave police helicopter resource creation untouched'
$updateBlock = Replace-Exact $updateBlock 'Log("Fort Zancudo Army and helicopter dispatch kept enabled for Newer Wanted / 911 traffic.");' 'Log("Fort Zancudo Army dispatch suppressed; police helicopter dispatch left unchanged.");' 'Fort Zancudo dispatch log'
$core = $core.Substring(0,$updateStart) + $updateBlock + $core.Substring($restoreStart)
$restoreStart = $core.IndexOf('private void RestoreFortZancudoArmyDispatch')
$militaryStart = $core.IndexOf('private void SuppressActiveMilitaryCombat', $restoreStart)
$restoreBlock = $core.Substring($restoreStart, $militaryStart - $restoreStart)
$restoreBlock = Replace-Exact $restoreBlock '                Function.Call((Hash)0xDC0F817884CDD856UL, 2, true);  // ENABLE_DISPATCH_SERVICE: DT_PoliceHelicopter' '' 'restore leaves police helicopter dispatch untouched'
$restoreBlock = Replace-Exact $restoreBlock '                Function.Call((Hash)0x9B2BD3773123EA2FUL, 2, false);  // BLOCK_DISPATCH_SERVICE_RESOURCE_CREATION' '' 'restore leaves police helicopter resource creation untouched'
$core = $core.Substring(0,$restoreStart) + $restoreBlock + $core.Substring($militaryStart)

$core = Replace-Exact $core 'now < lastFleeTaskUtc.AddSeconds(5)' 'now < lastFleeTaskUtc.AddSeconds(1.5)' 'military aircraft reassert interval'

$followStatusOld = 'IsScriptTaskActive(handle, (Hash)0x304AE42E357B8C7EUL)'
$followStatusCount = ([regex]::Matches($core, [regex]::Escape($followStatusOld))).Count
if ($followStatusCount -ne 4) { throw "Expected 4 follow-task status checks, found $followStatusCount" }
$core = $core.Replace($followStatusOld, 'IsScriptTaskActive(handle, (Hash)0x3EF867F4UL)')
$core = Replace-Exact $core 'IsScriptTaskActive(handle, (Hash)0x7CF1C3B65E9C67B1UL)' 'IsScriptTaskActive(handle, (Hash)0xAA05B492UL)' 'combat task status enum'
$oldTaskStatus = @'
                // GET_SCRIPT_TASK_STATUS: 0=WAITING_TO_START, 1=PERFORMING,
                // 2=DORMANT, 3=VACANT, 7=FINISHED. Only 0..2 still represent
                // an assigned task. Treating only status 1 as active caused
                // follow/aim tasks to be reissued every 500 ms, producing the
                // visible stop-dodge-stop / "dance in place" behaviour.
                int status = Function.Call<int>((Hash)0x77F1BEB8863288D5UL, pedHandle, (int)taskHash);
'@
$newTaskStatus = @'
                // GET_SCRIPT_TASK_STATUS expects eScriptTaskHash, not the native
                // hash used to issue TASK_FOLLOW_TO_OFFSET_OF_ENTITY. Passing the
                // native hash always reports TASK_NOT_FOUND and reissues follow
                // every 500 ms, producing the visible stop/dodge/dance behaviour.
                // 0=WAITING_TO_START, 1=PERFORMING, 2=DORMANT.
                int status = Function.Call<int>((Hash)0x77F1BEB8863288D5UL, pedHandle, (uint)taskHash);
'@
$core = Replace-Exact $core $oldTaskStatus $newTaskStatus 'GET_SCRIPT_TASK_STATUS enum parameter'

$followHelper = @'
        private void IssueBodyguardFollowTask(int pedHandle, int playerHandle)
        {
            if (!IsPedHandleUsable(pedHandle) || !IsPedHandleUsable(playerHandle)) return;
            int formationIndex = _bodyguards.IndexOf(pedHandle);
            if (formationIndex < 0) formationIndex = _bodyguards.Count;
            int row = formationIndex / 2;
            float offsetX = (formationIndex % 2 == 0) ? -1.35f : 1.35f;
            float offsetY = -1.8f - (row * 1.35f);
            Function.Call((Hash)0x304AE42E357B8C7EUL, pedHandle, playerHandle,
                offsetX, offsetY, 0.0f, 2.0f, -1, 1.75f, true); // TASK_FOLLOW_TO_OFFSET_OF_ENTITY
            Function.Call((Hash)0x971D38760FBC02EFUL, pedHandle, true); // SET_PED_KEEP_TASK
        }

        private void ProcessBodyguardFollowing(Ped player)
'@
$core = Replace-Exact $core '        private void ProcessBodyguardFollowing(Ped player)' $followHelper 'bodyguard formation helper'
$oldSpawnFollow = @'
                Function.Call((Hash)0x304AE42E357B8C7EUL, guard, player.Handle,
                    -1.0f, -1.0f, 0.0f, 3.0f, -1, 3.0f, true); // TASK_FOLLOW_TO_OFFSET_OF_ENTITY
                Function.Call((Hash)0x971D38760FBC02EFUL, guard, true); // SET_PED_KEEP_TASK
'@
$core = Replace-Exact $core $oldSpawnFollow '                IssueBodyguardFollowTask(guard, player.Handle);' 'spawn bodyguard formation'
$oldHitFollow = @'
                            Function.Call((Hash)0x304AE42E357B8C7EUL, handle, player.Handle,
                                -1.0f, -1.0f, 0.0f, 3.0f, -1, 3.0f, true); // TASK_FOLLOW_TO_OFFSET_OF_ENTITY
                            Function.Call((Hash)0x971D38760FBC02EFUL, handle, true); // SET_PED_KEEP_TASK
'@
$core = Replace-Exact $core $oldHitFollow '                            IssueBodyguardFollowTask(handle, player.Handle);' 'player-hit follow recovery'
$oldFarFollow = @'
                            if (!IsScriptTaskActive(handle, (Hash)0x3EF867F4UL))
                                Function.Call((Hash)0x304AE42E357B8C7EUL, handle, player.Handle,
                                    -1.0f, -1.0f, 0.0f, 3.0f, -1, 3.0f, true);
'@
$newFarFollow = @'
                            if (!IsScriptTaskActive(handle, (Hash)0x3EF867F4UL))
                                IssueBodyguardFollowTask(handle, player.Handle);
'@
$farFollowCount = ([regex]::Matches($core, [regex]::Escape($oldFarFollow))).Count
if ($farFollowCount -ne 2) { throw "Expected 2 identical follow blocks, found $farFollowCount" }
$core = $core.Replace($oldFarFollow, $newFarFollow)
$oldNormalFollow = @'
                        Function.Call((Hash)0x304AE42E357B8C7EUL, handle, player.Handle,
                            -1.0f, -1.0f, 0.0f, 3.0f, -1, 3.0f, true);
'@
$core = Replace-Exact $core $oldNormalFollow '                        IssueBodyguardFollowTask(handle, player.Handle);' 'normal bodyguard follow'

$core = Replace-Exact $core '            _bodyguardDismissalDetachedHandles.Remove(handle);' ('            _bodyguardDismissalDetachedHandles.Remove(handle);' + $nl + '            _bodyguardDismissalGroupRemovedHandles.Remove(handle);') 'dead bodyguard phase cleanup'
$core = Replace-Exact $core '                        _bodyguardDismissalDetachedHandles.Remove(handle);' ('                        _bodyguardDismissalDetachedHandles.Remove(handle);' + $nl + '                        _bodyguardDismissalGroupRemovedHandles.Remove(handle);') 'queued dead bodyguard phase cleanup'
$oldDismissal = @'
                    if (!_bodyguardDismissalDetachedHandles.Contains(handle))
                    {
                        // Phase 1: detach the living ped from GTA's group/task manager.
                        // Delete is intentionally deferred to a later game tick.
                        Function.Call(Hash.CLEAR_PED_TASKS, handle);
                        Function.Call((Hash)0x971D38760FBC02EFUL, handle, false); // SET_PED_KEEP_TASK
                        Function.Call((Hash)0x3DBFC55D5C9BB447UL, handle, false); // SET_PED_NEVER_LEAVES_GROUP
                        Function.Call((Hash)0xED74007FFB146BC2UL, handle); // REMOVE_PED_FROM_GROUP
                        _bodyguardDismissalDetachedHandles.Add(handle);
                        _pendingBodyguardDismissals.Enqueue(handle);
                        _nextBodyguardDismissalUtc = DateTime.UtcNow.AddMilliseconds(200);
                        return;
                    }

                    // Phase 2: after the quiet interval, release the detached ped to GTA.
                    // DELETE_ENTITY on a recently detached companion was associated with
                    // native game crashes during batch dismissal; mark it no longer needed.
                    if (IsLivingBodyguardHandle(handle))
                    {
                        Ped guard = new Ped(handle);
                        if (IsEntityUsable(guard)) guard.MarkAsNoLongerNeeded();
                    }
                    else
                    {
                        _bodyguardGroupHasDeadMember = true;
                    }
                    _bodyguardDismissalDetachedHandles.Remove(handle);
                    _queuedBodyguardDismissalHandles.Remove(handle);
'@
$newDismissal = @'
                    if (!_bodyguardDismissalDetachedHandles.Contains(handle))
                    {
                        // Phase 1: stop scripted activity and release task ownership.
                        // Group membership is changed on a later tick to avoid combining
                        // task teardown and group mutation in one native call sequence.
                        Function.Call(Hash.CLEAR_PED_TASKS, handle);
                        Function.Call((Hash)0x971D38760FBC02EFUL, handle, false); // SET_PED_KEEP_TASK
                        _bodyguardDismissalDetachedHandles.Add(handle);
                        _pendingBodyguardDismissals.Enqueue(handle);
                        _nextBodyguardDismissalUtc = DateTime.UtcNow.AddMilliseconds(650);
                        return;
                    }

                    if (!_bodyguardDismissalGroupRemovedHandles.Contains(handle))
                    {
                        // Phase 2: detach from the trainer group after the task manager
                        // has had a quiet frame to discard the previous companion task.
                        Function.Call((Hash)0x3DBFC55D5C9BB447UL, handle, false); // SET_PED_NEVER_LEAVES_GROUP
                        Function.Call((Hash)0xED74007FFB146BC2UL, handle); // REMOVE_PED_FROM_GROUP
                        _bodyguardDismissalGroupRemovedHandles.Add(handle);
                        _pendingBodyguardDismissals.Enqueue(handle);
                        _nextBodyguardDismissalUtc = DateTime.UtcNow.AddMilliseconds(650);
                        return;
                    }

                    // Phase 3: after group removal settles, release mission ownership.
                    // DELETE_ENTITY is intentionally avoided for recently detached peds.
                    if (IsLivingBodyguardHandle(handle))
                    {
                        Ped guard = new Ped(handle);
                        if (IsEntityUsable(guard)) guard.MarkAsNoLongerNeeded();
                    }
                    else
                    {
                        _bodyguardGroupHasDeadMember = true;
                    }
                    _bodyguardDismissalDetachedHandles.Remove(handle);
                    _bodyguardDismissalGroupRemovedHandles.Remove(handle);
                    _queuedBodyguardDismissalHandles.Remove(handle);
'@
$core = Replace-Exact $core $oldDismissal $newDismissal 'three-stage bodyguard dismissal'
$core = $core.Replace('DateTime.UtcNow.AddMilliseconds(200)', 'DateTime.UtcNow.AddMilliseconds(650)')
$oldDismissCatchCleanup = @'
                    _bodyguardDismissalDetachedHandles.Remove(handle);
                    _queuedBodyguardDismissalHandles.Remove(handle);
                    LogException("Process bodyguard dismissal " + handle, ex);
'@
$newDismissCatchCleanup = @'
                    _bodyguardDismissalDetachedHandles.Remove(handle);
                    _bodyguardDismissalGroupRemovedHandles.Remove(handle);
                    _queuedBodyguardDismissalHandles.Remove(handle);
                    LogException("Process bodyguard dismissal " + handle, ex);
'@
$core = Replace-Exact $core $oldDismissCatchCleanup $newDismissCatchCleanup 'bodyguard dismissal exception cleanup'
$core = Replace-Exact $core '            _bodyguardDismissalDetachedHandles.Clear();' ('            _bodyguardDismissalDetachedHandles.Clear();' + $nl + '            _bodyguardDismissalGroupRemovedHandles.Clear();') 'bodyguard abort cleanup'

$oldParachute = '                    Function.Call((Hash)0xBF0FD6E56C964FCBUL, ped.Handle, GetHash("WEAPON_PARACHUTE"), 1, false, false); // GIVE_WEAPON_TO_PED'
$newParachute = @'
                    uint parachuteGadget = GetHash("GADGET_PARACHUTE");
                    if (!Function.Call<bool>((Hash)0x8DECB02F88F428BCUL, ped.Handle, parachuteGadget, false))
                        Function.Call((Hash)0xBF0FD6E56C964FCBUL, ped.Handle, parachuteGadget, 1, false, false); // GIVE_WEAPON_TO_PED
'@
$core = Replace-Exact $core $oldParachute $newParachute 'automatic parachute equipment'
Write-Text $corePath $core

$vehiclePath = 'src/Managers/VehicleSystems.cs'
$vehicle = Read-Text $vehiclePath
$oldInitialRepair = @'
            Function.Call(Hash.SET_VEHICLE_FIXED, vehicleHandle);
            Function.Call(Hash.SET_VEHICLE_DEFORMATION_FIXED, vehicleHandle);
            for (int windowIndex = 0; windowIndex < 8; windowIndex++)
'@
$newInitialRepair = @'
            Function.Call(Hash.SET_VEHICLE_FIXED, vehicleHandle);
            Function.Call(Hash.SET_VEHICLE_DEFORMATION_FIXED, vehicleHandle);
            Function.Call(Hash.SET_VEHICLE_DIRT_LEVEL, vehicleHandle, 0.0f);
            Function.Call((Hash)0xE91F1B65F2B48D57UL, vehicleHandle); // REMOVE_DECALS_FROM_VEHICLE
            for (int windowIndex = 0; windowIndex < 8; windowIndex++)
'@
$vehicle = Replace-Exact $vehicle $oldInitialRepair $newInitialRepair 'initial God Mode cosmetic repair'
$oldRefresh = @'
                Function.Call(Hash.SET_VEHICLE_DEFORMATION_FIXED, vehicleHandle);
                for (int windowIndex = 0; windowIndex < 8; windowIndex++)
'@
$newRefresh = @'
                // Reapply full repair only when GTA reports damage; this also
                // removes scratches/body damage that deformation-only repair misses.
                if (Function.Call<bool>((Hash)0xBCDC5017D3CE1E9EUL, vehicleHandle)) // IS_VEHICLE_DAMAGED
                    Function.Call(Hash.SET_VEHICLE_FIXED, vehicleHandle);
                Function.Call(Hash.SET_VEHICLE_DEFORMATION_FIXED, vehicleHandle);
                Function.Call(Hash.SET_VEHICLE_DIRT_LEVEL, vehicleHandle, 0.0f);
                Function.Call((Hash)0xE91F1B65F2B48D57UL, vehicleHandle); // REMOVE_DECALS_FROM_VEHICLE
                for (int windowIndex = 0; windowIndex < 8; windowIndex++)
'@
$vehicle = Replace-Exact $vehicle $oldRefresh $newRefresh 'recurring God Mode damage repair'
$oldNitroCap = '                    Function.Call((Hash)0xBAA045B4E42F3C06UL, handle, maximumNitroSpeed); // SET_VEHICLE_MAX_SPEED (m/s)' + $nl
$newNitroCap = @'
                    // Avoid a second native speed cap here; it can impose a ceiling
                    // around 333 km/h on some vehicles. Transmission top speed is
                    // already adjusted above and the forward-speed assist is bounded.
'@
$vehicle = Replace-Exact $vehicle $oldNitroCap $newNitroCap 'remove secondary Nitro speed cap'
Write-Text $vehiclePath $vehicle

$legacyTestPath = 'tests/test_1_4_6_regressions.py'
$legacyTests = Read-Text $legacyTestPath
$legacyTests = Replace-Exact $legacyTests 'self.assertIn("IsScriptTaskActive(handle, (Hash)0x304AE42E357B8C7EUL)", body)' 'self.assertIn("IsScriptTaskActive(handle, (Hash)0x3EF867F4UL)", body)' 'legacy follow task enum'
$legacyTests = Replace-Exact $legacyTests 'self.assertIn("IsScriptTaskActive(handle, (Hash)0x7CF1C3B65E9C67B1UL)", body)' 'self.assertIn("IsScriptTaskActive(handle, (Hash)0xAA05B492UL)", body)' 'legacy combat task enum'
Write-Text $legacyTestPath $legacyTests

$validatorPath = 'build/ReleaseValidator.cs'
$validator = Read-Text $validatorPath
$oldFortValidator = @'
        if (fortDispatchBody.IndexOf("0xDC0F817884CDD856UL, 15, true", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0x9B2BD3773123EA2FUL, 15, false", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0xDC0F817884CDD856UL, 2, true", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0x9B2BD3773123EA2FUL, 2, false", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("AddMilliseconds(900)", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("TASK_VEHICLE_DRIVE_WANDER", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("if (pedInVehicle)", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("_armyVehicleDispatchSuppressed = true;", StringComparison.Ordinal) < 0)
            Error("1.4.5 Fort Zancudo must preserve Newer Wanted / 911 dispatch and avoid clearing tasks from emergency vehicle occupants.");
'@
$newFortValidator = @'
        if (fortDispatchBody.IndexOf("0xDC0F817884CDD856UL, 14, false", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0x9B2BD3773123EA2FUL, 14, true", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0xDC0F817884CDD856UL, 2,", StringComparison.Ordinal) >= 0 ||
            fortDispatchBody.IndexOf("0x9B2BD3773123EA2FUL, 2,", StringComparison.Ordinal) >= 0 ||
            fortDispatchBody.IndexOf("AddMilliseconds(900)", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("TASK_VEHICLE_DRIVE_WANDER", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("if (pedInVehicle)", StringComparison.Ordinal) < 0)
            Error("1.4.7 Fort Zancudo must suppress Army service 14 while leaving police helicopter dispatch untouched.");
'@
$validator = Replace-Exact $validator $oldFortValidator $newFortValidator 'Fort Zancudo validator'
$oldTaskValidator = @'
        if (mainSource.IndexOf("IsScriptTaskActive(handle, (Hash)0x304AE42E357B8C7EUL)", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("IsScriptTaskActive(handle, (Hash)0x7CF1C3B65E9C67B1UL)", StringComparison.Ordinal) < 0)
            Error("1.4.6 bodyguard combat and follow tasks must not be restarted on every update.");
'@
$newTaskValidator = @'
        if (mainSource.IndexOf("IsScriptTaskActive(handle, (Hash)0x3EF867F4UL)", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("IsScriptTaskActive(handle, (Hash)0xAA05B492UL)", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("private void IssueBodyguardFollowTask", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("_bodyguardDismissalGroupRemovedHandles", StringComparison.Ordinal) < 0)
            Error("1.4.7 bodyguard follow/combat task enums or staged dismissal are incomplete.");
'@
$validator = Replace-Exact $validator $oldTaskValidator $newTaskValidator 'bodyguard task validator'
$validator = Replace-Exact $validator 'mainSource.IndexOf("AddMilliseconds(200)"' 'mainSource.IndexOf("AddMilliseconds(650)"' 'bodyguard dismissal interval validator'
Write-Text $validatorPath $validator

$readmePath = 'README_RU.md'
$readme = Read-Text $readmePath
$readmeStart = $readme.IndexOf('## Изменения 1.4.7')
$readmeEnd = $readme.IndexOf('## Изменения 1.4.6', $readmeStart)
if ($readmeStart -lt 0 -or $readmeEnd -lt 0) { throw 'Could not locate README 1.4.7 section.' }
$newReadmeSection = @'
## Изменения 1.4.7

- Runtime-логи закреплены в scripts/AvernesSowtware_Logs: install.log, session.log, error.log, crash.log; повторные попытки записи сохранены.
- Max Tuning блокируется в воздухе; неподдерживаемый native вторичного цвета исключён.
- Телохранители: исправлен eScriptTaskHash для Follow/Combat; задачи больше не переотправляются из-за неверного task ID. Построение — две колонки.
- Dismiss All Bodyguards разбит на три безопасных этапа с паузами между операциями.
- Fort Zancudo: отключается только DT_ArmyVehicle (service 14); police helicopter service 2 не изменяется.
- Автораскрытие парашюта использует GADGET_PARACHUTE перед принудительным раскрытием.
- Vehicle God Mode повторно устраняет подтверждённые повреждения, деформацию, грязь и decals.
- Nitro: удалён дополнительный native speed cap, способный ограничивать скорость около 333 км/ч; импульс в воздухе отключён, Toreador исключён из Nitro.
- Добавлены отдельная мойка автомобиля и переключатель High-Speed Stability.
- Franklin Ability в Toreador сохраняет отдельное включение/разблокировку/заполнение индикатора.

Игровые тесты GTA V для этой сборки не выполнялись. Оконный режим GTA V задаётся настройками самой игры/лаунчера, а не трейнером.

'@
$readme = $readme.Substring(0,$readmeStart) + $newReadmeSection + $readme.Substring($readmeEnd)
Write-Text $readmePath $readme

$changelog = @'
# AvernesSowtware 1.4.7 — полный корректирующий выпуск

## Исправления по коду
- Исправлены task IDs для GET_SCRIPT_TASK_STATUS: используются значения eScriptTaskHash для FollowToOffset и CombatHatedTargetsAroundPed. Это предотвращает повторную постановку задач телохранителям каждые 500 мс.
- Следование телохранителей использует разнесённое двухколонное построение.
- Dismiss All Bodyguards выполняет остановку задач, снятие из группы и освобождение mission ownership в трёх отдельных игровых тиках с паузой 650 мс.
- Fort Zancudo использует индекс DT_ArmyVehicle=14. Service 15 (BikerBackup) не затрагивается; police helicopter service 2 остаётся без изменений.
- Повторная задача отхода для уже находящихся рядом военных самолётов/вертолётов подтверждается чаще, если AI заменяет её.
- Автораскрытие парашюта выдаёт GADGET_PARACHUTE, если предмет отсутствует, затем вызывает раскрытие.
- Vehicle God Mode очищает грязь/decals; периодический цикл вызывает полный ремонт только при обнаруженном повреждении.
- Удалён дополнительный _SET_VEHICLE_MAX_SPEED из активного Nitro-цикла, чтобы не создавать второй скоростной предел около 333 км/ч.
- Сохранены Max Tuning airborne guard, защита от missing-native, Wash Vehicle, High-Speed Stability и отключение Nitro для Toreador.

## Не относится к коду трейнера
- Маленькое окно GTA V исправляется в настройках графики GTA V/параметрах запуска. Трейнер не меняет разрешение или режим экрана.

## Проверка
- Регрессионные тесты и Windows-компиляция выполняются в CI.
- Реальные игровые сценарии GTA V не выполняются в CI и требуют проверки пользователем.
'@
Write-Text 'docs/CHANGELOG_1_4_7_RU.md' $changelog
Write-Host 'Applied full corrective patch to v1.4.7 baseline.'
