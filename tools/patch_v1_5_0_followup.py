from pathlib import Path
import sys

root = Path(sys.argv[1]).resolve()

def replace(rel, old, new, count=1):
    path = root / rel
    raw = path.read_bytes()
    newline = "\r\n" if b"\r\n" in raw else ""
    source = path.read_text(encoding="utf-8-sig")
    found = source.count(old)
    if found != count:
        raise SystemExit(f"{rel}: expected exactly {count} marker(s), found {found}: {old!r}")
    path.write_text(source.replace(old, new, count), encoding="utf-8-sig", newline=newline)

replace("src/Core/AvernesSowtwareScript.cs",
        """            UpdateFortZancudoArmyDispatch(ped, ignoreLawAndMilitary);
            ApplyMilitaryIgnore(ped, ignoreLawAndMilitary);
            if (ignoreLawAndMilitary) SuppressActiveMilitaryCombat(ped);
            if (ignoreLawAndMilitary) SuppressBolingbrokePrisonCombat(ped);""",
        """            // Fort Zancudo and Bolingbroke are local safe zones: their hostile
            // NPC/dispatch suppression is independent of the global police toggles.
            UpdateFortZancudoArmyDispatch(ped, true);
            ApplyMilitaryIgnore(ped, ignoreLawAndMilitary);
            SuppressActiveMilitaryCombat(ped);
            SuppressBolingbrokePrisonCombat(ped);""")

replace("src/Core/AvernesSowtwareScript.cs",
        "        private DateTime _nextMilitaryCombatScanUtc = DateTime.MinValue;",
        "        private DateTime _nextMilitaryCombatScanUtc = DateTime.MinValue;\n        private DateTime _nextPoliceIgnoreScanUtc = DateTime.MinValue;")
replace("src/Core/AvernesSowtwareScript.cs",
        """                ApplyPoliceIgnore(ped, _settings.CopsIgnore);
            }
            else""",
        """                ApplyPoliceIgnore(ped, _settings.CopsIgnore);
                if (_settings.CopsIgnore) SuppressActivePoliceCombat(ped);
            }
            else""")
replace("src/Core/AvernesSowtwareScript.cs",
        "        private void UpdateFortZancudoArmyDispatch(Ped player, bool ignoreLawAndMilitary)",
        """        private void SuppressActivePoliceCombat(Ped player)
        {
            if (!IsEntityUsable(player)) return;
            DateTime now = DateTime.UtcNow;
            if (now < _nextPoliceIgnoreScanUtc) return;
            _nextPoliceIgnoreScanUtc = now.AddMilliseconds(500);
            try
            {
                uint copGroup = Function.Call<uint>((Hash)0xD24D37CC275948CCUL, "COP");
                if (copGroup == 0) return;
                Ped[] nearby = World.GetNearbyPeds(player, 300.0f);
                if (nearby == null) return;
                for (int i = 0; i < nearby.Length; i++)
                {
                    Ped cop = nearby[i];
                    if (!IsEntityUsable(cop) || cop.Handle == player.Handle) continue;
                    if (Function.Call<uint>((Hash)0x7DBDD04862D95F04UL, cop.Handle) != copGroup) continue;
                    if (!Function.Call<bool>((Hash)0x4859F1FC66A6278EUL, cop.Handle, player.Handle)) continue;

                    bool inVehicle = Function.Call<bool>((Hash)0x997ABD671D25CA0BUL, cop.Handle, false);
                    if (inVehicle)
                    {
                        int vehicle = Function.Call<int>((Hash)0x9A9112A0FE9A4713UL, cop.Handle, false);
                        if (vehicle == 0 || !IsVehicleHandleUsable(vehicle)) continue;
                        int driver = Function.Call<int>((Hash)0xBB40DD2270B65366UL, vehicle, -1);
                        if (driver != cop.Handle) continue;
                        Function.Call(Hash.CLEAR_PED_TASKS, driver);
                        Function.Call((Hash)0x480142959D337D00UL, driver, vehicle, 18.0f, 786603); // TASK_VEHICLE_DRIVE_WANDER
                        Function.Call((Hash)0x971D38760FBC02EFUL, driver, true); // SET_PED_KEEP_TASK
                    }
                    else
                    {
                        Function.Call(Hash.CLEAR_PED_TASKS, cop.Handle);
                        Function.Call((Hash)0x22B0D0E37CCB840DUL, cop.Handle, player.Handle,
                            180.0f, -1, false, false); // TASK_SMART_FLEE_PED
                        Function.Call((Hash)0x971D38760FBC02EFUL, cop.Handle, true); // SET_PED_KEEP_TASK
                    }
                }
            }
            catch (Exception ex) { LogException("SuppressActivePoliceCombat", ex); }
        }

        private void UpdateFortZancudoArmyDispatch(Ped player, bool ignoreLawAndMilitary)""")

replace("src/Core/AvernesSowtwareScript.cs",
        """                    if (_militaryAircraftFleeIssuedUtc.TryGetValue(npc.Handle, out lastFleeTaskUtc) &&
                            now < lastFleeTaskUtc.AddSeconds(1.5)) continue;""",
        """                    if (_militaryAircraftFleeIssuedUtc.TryGetValue(npc.Handle, out lastFleeTaskUtc) &&
                            now < lastFleeTaskUtc.AddSeconds(1.5) && !attackingPlayer) continue;""")
replace("src/Core/AvernesSowtwareScript.cs",
        """                    if (responseAlreadyIssued && now < lastResponse.AddSeconds(1.5)) continue;""",
        """                    if (responseAlreadyIssued && now < lastResponse.AddSeconds(1.5) && !attackingPlayer) continue;""")
replace("src/Core/AvernesSowtwareScript.cs",
        """                    if (_prisonNpcFleeIssuedUtc.TryGetValue(npc.Handle, out lastIssued) &&
                        now < lastIssued.AddSeconds(1.5)) continue;""",
        """                    if (_prisonNpcFleeIssuedUtc.TryGetValue(npc.Handle, out lastIssued) &&
                        now < lastIssued.AddSeconds(1.5) && !attackingPlayer) continue;""")

replace("src/Core/AvernesSowtwareScript.cs",
        "Function.Call((Hash)0x4D9CA1009AFBD057UL, handle, 1); // SET_PED_COMBAT_MOVEMENT: defensive/steady",
        "Function.Call((Hash)0x4D9CA1009AFBD057UL, handle, 0); // SET_PED_COMBAT_MOVEMENT: stationary; no cover-dodging/strafe movement")

replace("src/Core/AvernesSowtwareScript.cs",
        "        private void ProcessBodyguardFollowing(Ped player)",
        """        private int FindPedAttackingPlayer(Ped player)
        {
            if (!IsEntityUsable(player)) return 0;
            try
            {
                Ped[] nearby = World.GetNearbyPeds(player, 45.0f);
                if (nearby == null) return 0;
                int bestHandle = 0;
                float bestDistance = Single.MaxValue;
                for (int i = 0; i < nearby.Length; i++)
                {
                    Ped candidate = nearby[i];
                    if (!IsEntityUsable(candidate) || candidate.Handle == player.Handle || _bodyguards.Contains(candidate.Handle)) continue;
                    if (Function.Call<bool>((Hash)0x12534C348C6CB68BUL, candidate.Handle)) continue;
                    if (!Function.Call<bool>((Hash)0x4859F1FC66A6278EUL, candidate.Handle, player.Handle)) continue;
                    Vector3 position = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, candidate.Handle, true);
                    float distance = Function.Call<float>(Hash.GET_DISTANCE_BETWEEN_COORDS,
                        position.X, position.Y, position.Z, player.Position.X, player.Position.Y, player.Position.Z, true);
                    if (distance < bestDistance) { bestDistance = distance; bestHandle = candidate.Handle; }
                }
                return bestHandle;
            }
            catch (Exception ex) { LogException("FindPedAttackingPlayer", ex); return 0; }
        }

        private void ProcessBodyguardFollowing(Ped player)""")
replace("src/Core/AvernesSowtwareScript.cs",
        """            if (playerInVehicle) _bodyguardPlayerVehicleHandle = playerVehicleHandle;

            for (int i = _bodyguards.Count - 1; i >= 0; i--)""",
        """            if (playerInVehicle) _bodyguardPlayerVehicleHandle = playerVehicleHandle;
            bool playerInCombat = Function.Call<bool>((Hash)0x4859F1FC66A6278EUL, player.Handle, 0);
            int playerThreatHandle = playerInCombat ? FindPedAttackingPlayer(player) : 0;

            for (int i = _bodyguards.Count - 1; i >= 0; i--)""")
replace("src/Core/AvernesSowtwareScript.cs",
        """                    bool playerInCombat = Function.Call<bool>((Hash)0x4859F1FC66A6278EUL, player.Handle, 0);
                    if (playerInCombat)
                    {
                        if (!IsScriptTaskActive(handle, (Hash)0xAA05B492UL))
                            Function.Call((Hash)0x7CF1C3B65E9C67B1UL, handle, 45.0f, 0); // TASK_COMBAT_HATED_TARGETS_AROUND_PED
                        continue;
                    }""",
        """                    if (playerInCombat)
                    {
                        if (playerThreatHandle != 0)
                        {
                            if (!Function.Call<bool>((Hash)0x4859F1FC66A6278EUL, handle, playerThreatHandle))
                            {
                                Function.Call((Hash)0xF166E48407BAC484UL, handle, playerThreatHandle, 0, 16); // TASK_COMBAT_PED
                                ApplyBodyguardCombatTactics(handle);
                                Function.Call((Hash)0x971D38760FBC02EFUL, handle, true); // SET_PED_KEEP_TASK
                            }
                        }
                        else if (!IsScriptTaskActive(handle, (Hash)0xAA05B492UL))
                            Function.Call((Hash)0x7CF1C3B65E9C67B1UL, handle, 45.0f, 0); // TASK_COMBAT_HATED_TARGETS_AROUND_PED
                        continue;
                    }""")

replace("src/Managers/VehicleSystems.cs",
        "        private DateTime _nextVehicleGodModeRepairUtc = DateTime.MinValue;",
        "        private DateTime _nextVehicleGodModeRepairUtc = DateTime.MinValue;\n        private DateTime _nextNitroCollisionRequestUtc = DateTime.MinValue;")
replace("src/Managers/VehicleSystems.cs",
        """                    Function.Call((Hash)0xB59E4BD37AE292DBUL, handle, isToreador ? 1.25f : (aircraft ? 1.45f : 1.35f)); // SET_VEHICLE_CHEAT_POWER_INCREASE
                    ApplyNitroVelocityAssist(handle, maximumNitroSpeed, effectiveNitroPower);""",
        """                    Function.Call((Hash)0xB59E4BD37AE292DBUL, handle, isToreador ? 1.25f : (aircraft ? 1.45f : 1.35f)); // SET_VEHICLE_CHEAT_POWER_INCREASE
                    RequestNitroAheadCollision(handle);
                    ApplyNitroVelocityAssist(handle, maximumNitroSpeed, effectiveNitroPower);""")
replace("src/Managers/VehicleSystems.cs",
        "        // Preserve the original internal entry point for existing callers/reflection-based integrations.",
        """        private void RequestNitroAheadCollision(int vehicleHandle)
        {
            if (vehicleHandle == 0 || !IsVehicleHandleUsable(vehicleHandle) || DateTime.UtcNow < _nextNitroCollisionRequestUtc) return;
            _nextNitroCollisionRequestUtc = DateTime.UtcNow.AddMilliseconds(150);
            try
            {
                float speed = Function.Call<float>(Hash.GET_ENTITY_SPEED, vehicleHandle);
                if (Single.IsNaN(speed) || Single.IsInfinity(speed) || speed < 70.0f) return;
                Vector3 position = Function.Call<Vector3>(Hash.GET_ENTITY_COORDS, vehicleHandle, true);
                Vector3 forward = Function.Call<Vector3>(Hash.GET_ENTITY_FORWARD_VECTOR, vehicleHandle);
                int vehicleClass = Function.Call<int>((Hash)0x29439776AAA00A62UL, vehicleHandle);
                bool aircraft = vehicleClass == 15 || vehicleClass == 16;
                float length = aircraft
                    ? (float)Math.Sqrt(forward.X * forward.X + forward.Y * forward.Y + forward.Z * forward.Z)
                    : (float)Math.Sqrt(forward.X * forward.X + forward.Y * forward.Y);
                if (!aircraft && length < 0.15f)
                {
                    float heading = Function.Call<float>((Hash)0xE83D4F9BA2A38914UL, vehicleHandle) * (float)(Math.PI / 180.0);
                    forward.X = -(float)Math.Sin(heading);
                    forward.Y = (float)Math.Cos(heading);
                    forward.Z = 0.0f;
                    length = 1.0f;
                }
                if (length < 0.001f) return;
                float distance = ClampFloat(speed * 0.65f, 80.0f, 240.0f);
                float targetX = position.X + (forward.X / length) * distance;
                float targetY = position.Y + (forward.Y / length) * distance;
                float targetZ = position.Z + (aircraft ? forward.Z / length : 0.0f) * distance;
                Function.Call((Hash)0x07503F7948F491A7UL, targetX, targetY, targetZ); // REQUEST_COLLISION_AT_COORD
                Function.Call((Hash)0xC9156DC11411A9EAUL, targetX, targetY, targetZ); // REQUEST_ADDITIONAL_COLLISION_AT_COORD
            }
            catch (Exception ex) { LogException("RequestNitroAheadCollision " + vehicleHandle, ex); }
        }

        // Preserve the original internal entry point for existing callers/reflection-based integrations.""")

regression = root / "tests/test_1_5_0_regressions.py"
source = regression.read_text(encoding="utf-8-sig")
marker = "if __name__ == \"__main__\": unittest.main()"
extra = '''    def test_fort_and_prison_safe_zones_do_not_depend_on_global_police_toggles(self):
        apply = CORE[CORE.index("private void ApplyPlayerSettings"):CORE.index("private void ApplyPoliceIgnore")]
        self.assertIn("UpdateFortZancudoArmyDispatch(ped, true)", apply)
        self.assertIn("SuppressActiveMilitaryCombat(ped);", apply)
        self.assertIn("SuppressBolingbrokePrisonCombat(ped);", apply)
        self.assertNotIn("if (ignoreLawAndMilitary) Suppress", apply)

    def test_police_ignore_releases_active_pursuit_without_global_dispatch_native(self):
        apply = CORE[CORE.index("private void ApplyPlayerSettings"):CORE.index("private void ApplyPoliceIgnore")]
        police = CORE[CORE.index("private void SuppressActivePoliceCombat"):CORE.index("private void UpdateFortZancudoArmyDispatch")]
        self.assertIn("SuppressActivePoliceCombat(ped)", apply)
        self.assertIn("TASK_VEHICLE_DRIVE_WANDER", police)
        self.assertIn("TASK_SMART_FLEE_PED", police)
        self.assertNotIn("SET_POLICE_IGNORE_PLAYER", police)

    def test_bodyguards_release_aim_target_and_do_not_use_evasive_combat_movement(self):
        aim = CORE[CORE.index("private void ProcessBodyguardAim"):CORE.index("private void RemoveBodyguardBlip")]
        tactics = CORE[CORE.index("private void ApplyBodyguardCombatTactics"):CORE.index("private bool IsLivingBodyguardHandle")]
        self.assertIn("previousTarget", aim)
        self.assertIn("IssueBodyguardFollowTask(guard, player.Handle)", aim)
        self.assertIn("SET_PED_CAN_EVASIVE_DIVE", tactics)
        self.assertIn("handle, 0); // SET_PED_COMBAT_MOVEMENT: stationary", tactics)

    def test_bodyguards_explicitly_protect_player_from_actual_attacker(self):
        self.assertIn("FindPedAttackingPlayer", CORE)
        self.assertIn("TASK_COMBAT_PED", CORE)
        self.assertIn("playerThreatHandle", CORE)

    def test_nitro_requests_collision_ahead_without_teleporting(self):
        method = VEHICLE[VEHICLE.index("private void RequestNitroAheadCollision"):VEHICLE.index("// Preserve the original internal entry point", VEHICLE.index("private void RequestNitroAheadCollision"))]
        self.assertIn("REQUEST_COLLISION_AT_COORD", method)
        self.assertIn("REQUEST_ADDITIONAL_COLLISION_AT_COORD", method)
        self.assertIn("ClampFloat(speed * 0.65f, 80.0f, 240.0f)", method)
        self.assertNotIn("SET_ENTITY_COORDS", method)
        self.assertNotIn("SET_ENTITY_VELOCITY", method)

'''
if marker not in source:
    raise SystemExit("test_1_5_0_regressions.py: insertion marker missing")
regression.write_text(source.replace(marker, extra + marker), encoding="utf-8-sig")

changelog = root / "docs/CHANGELOG_1_5_0_RU.md"
text = changelog.read_text(encoding="utf-8-sig")
text += """
- Fort Zancudo и Bolingbroke: локальная защита зон теперь работает независимо от глобальных переключателей Police Ignore / Never Wanted; атакующие NPC получают повторную команду отхода.
- Police Ignore: только активные полицейские атакующие игрока получают команду прекращения преследования; водитель полицейского автомобиля возвращается к обычному дорожному AI. Глобальное отключение dispatch не используется.
- Телохранители: после прекращения прицеливания освобождают цель и возвращаются к сопровождению; при реальной атаке на игрока получают конкретную цель для защиты. В бою отключено защитное перемещение/уклонение.
- Nitro X8: при высокой скорости запрашивается коллизия на участке впереди по направлению движения; запрос не телепортирует транспорт и не меняет его скорость.
"""
changelog.write_text(text, encoding="utf-8-sig")

report = root / "docs/TEST_REPORT_1_5_0_RU.md"
text = report.read_text(encoding="utf-8-sig")
text += """

Дополнительные регрессии проверяют независимость зон Fort Zancudo/Bolingbroke от глобальных переключателей, адресное снятие полицейского преследования, возврат телохранителей к сопровождению/защиту игрока и запрос коллизии перед быстрым транспортом. Они проверяют контракт исходного кода, а не заменяют игровой прогон.
"""
report.write_text(text, encoding="utf-8-sig")
