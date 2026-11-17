from pathlib import Path
import os

root = Path(os.environ["AVERNE_ROOT"])
test_path = root / "tests" / "test_1_4_2_regressions.py"
validator_path = root / "build" / "ReleaseValidator.cs"
if not test_path.is_file() or not validator_path.is_file():
    raise SystemExit("Required v1.4.2 regression or build-validator source is missing.")

# Refresh obsolete source-contract assertions without skipping any test.
data = test_path.read_bytes()
test_replacements = (
    (b'self.assertIn("IS_PED_IN_COMBAT", MAIN)',
     b'self.assertIn("0x4859F1FC66A6278EUL", MAIN)'),
    (b'0xDC0F817884CDD856UL, 14, !insideFortZancudo',
     b'0xDC0F817884CDD856UL, 14, false'),
    (b'0x9B2BD3773123EA2FUL, 14, insideFortZancudo',
     b'0x9B2BD3773123EA2FUL, 14, true'),
    (b'0xDC0F817884CDD856UL, 2, !insideFortZancudo',
     b'0xDC0F817884CDD856UL, 2, false'),
)
for old, new in test_replacements:
    count = data.count(old)
    if count != 1:
        raise SystemExit("Expected exactly one stale regression assertion; found %d for %r" % (count, old))
    data = data.replace(old, new, 1)
test_path.write_bytes(data)

# Keep pre-build validation strict, but align it with the safe 1.4.2 implementation.
validator = validator_path.read_text(encoding="utf-8-sig")
old_vehicle = '''        if (vehicleSource.IndexOf("repairVehicleOnApply", StringComparison.Ordinal) < 0)
            Error("1.3.7 vehicle repair must be gated to initial application / God Mode activation.");
'''
new_vehicle = '''        int vehicleSettingsStart = vehicleSource.IndexOf("private void ApplyVehicleSettings", StringComparison.Ordinal);
        int vehicleSettingsEnd = vehicleSettingsStart >= 0
            ? vehicleSource.IndexOf("private void RestoreVehicleProtection", vehicleSettingsStart, StringComparison.Ordinal)
            : -1;
        string vehicleSettingsBody = vehicleSettingsStart >= 0 && vehicleSettingsEnd > vehicleSettingsStart
            ? vehicleSource.Substring(vehicleSettingsStart, vehicleSettingsEnd - vehicleSettingsStart)
            : String.Empty;
        if (vehicleSettingsBody.IndexOf("SET_VEHICLE_DEFORMATION_FIXED", StringComparison.Ordinal) < 0 ||
            vehicleSettingsBody.IndexOf("0x772282EBEB95E682UL", StringComparison.Ordinal) < 0 ||
            vehicleSettingsBody.IndexOf("if (godMode)", StringComparison.Ordinal) < 0 ||
            vehicleSettingsBody.IndexOf("Function.Call(Hash.SET_VEHICLE_FIXED", StringComparison.Ordinal) >= 0)
            Error("1.4.2 vehicle God Mode must clear deformation and broken windows only while enabled, without resetting animated doors.");
'''
old_fort = '''        if (mainSource.IndexOf("ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0xDC0F817884CDD856UL, 14, !insideFortZancudo", StringComparison.Ordinal) < 0 ||
            mainSource.IndexOf("0x9B2BD3773123EA2FUL, 14, insideFortZancudo", StringComparison.Ordinal) < 0)
            Error("1.4.2 Fort Zancudo Army dispatch suppression is missing or uses the wrong dispatch ID.");
'''
new_fort = '''        int fortDispatchStart = mainSource.IndexOf("private void UpdateFortZancudoArmyDispatch", StringComparison.Ordinal);
        int fortDispatchEnd = fortDispatchStart >= 0
            ? mainSource.IndexOf("private void RestoreFortZancudoArmyDispatch", fortDispatchStart, StringComparison.Ordinal)
            : -1;
        string fortDispatchBody = fortDispatchStart >= 0 && fortDispatchEnd > fortDispatchStart
            ? mainSource.Substring(fortDispatchStart, fortDispatchEnd - fortDispatchStart)
            : String.Empty;
        if (fortDispatchBody.IndexOf("ENABLE_DISPATCH_SERVICE: DT_ArmyVehicle", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0xDC0F817884CDD856UL, 14, false", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0x9B2BD3773123EA2FUL, 14, true", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0xDC0F817884CDD856UL, 2, false", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("0x9B2BD3773123EA2FUL, 2, true", StringComparison.Ordinal) < 0 ||
            fortDispatchBody.IndexOf("AddMilliseconds(900)", StringComparison.Ordinal) < 0)
            Error("1.4.2 Fort Zancudo Army dispatch suppression and helicopter dispatch must be periodically disabled and blocked while inside the base.");
'''
for label, old, new in (
    ("vehicle God Mode validator", old_vehicle, new_vehicle),
    ("Fort Zancudo dispatch validator", old_fort, new_fort),
):
    count = validator.count(old)
    if count != 1:
        raise SystemExit("Expected exactly one obsolete %s rule; found %d" % (label, count))
    validator = validator.replace(old, new, 1)
validator_path.write_text(validator, encoding="utf-8")

# Refresh release notes so they describe the follow-up implementation, not the first draft.
changelog = """# AvernesSowtware 1.4.2 — дополнительные исправления после игрового тестирования

## Исправления
- **Телохранители:** нейтральное отношение к игроку; разрешены наведение, захват цели и обычный урон. Настройка неуязвимости телохранителей продолжает учитываться.
- **Удаление телохранителей:** массовое снятие выполняется поэтапно; NPC отсоединяются от группы и передаются игре. Удаление больше не уничтожает группу принудительно и не вызывает немедленное удаление сущностей. Убийство одного телохранителя сохраняет исправленное безопасное поведение.
- **Nitro X8:** добавлена физическая помощь скорости с целевым пределом 1200 км/ч поверх ограничений трансмиссии. При отключении Nitro и смене машины временные параметры сбрасываются. Фактическая скорость зависит от игровой физики и требует проверки.
- **Режим бога автомобиля:** отключается отображение новых повреждений; периодически исправляется деформация кузова и восстанавливаются стёкла. Полный SET_VEHICLE_FIXED не используется при посадке, чтобы не сбрасывать анимацию дверей.
- **Двери:** сохранены отдельная задача открытия двери и обычная анимация посадки вместо телепортации NPC в салон.
- **Fort Zancudo:** пока игрок находится на базе и активен Never Wanted или Cops Ignore, повторно блокируются армейский dispatch 14 и вертолётный dispatch 2. Дополнительно снимаются боевые задачи у уже созданных армейских пилотов/истребителей, атакующих игрока.
- **Логи:** сохраняются диагностические файлы и маркер сессии; при следующем старте незакрытый маркер отмечается как возможный аварийный выход.

## Сохранённые функции
Парашют, спавн транспорта, Max Tuning, номера, тонировка, сохранение тюнинга, погода, оружие, функции игрока, телепортация, меню, настройки и пользовательские данные остаются в составе программы.

## Каталог транспорта — открытый пункт
В JSON-пакете 480 основных и 85 дополнительных записей; после пересечений это 545 уникальных моделей до runtime-фильтра. В игровом тесте отображалось 542. Цель 619 не достигнута и не объявляется достигнутой: подтверждённого совместимого набора недостающих моделей нет. Произвольные model ID не добавлялись.

Сборка и статические тесты не заменяют проверку в игре. Окончательное подтверждение исправлений возможно только после повторного игрового теста.
"""
report = """# AvernesSowtware 1.4.2 — отчёт проверок

## Автоматические проверки
- 28 регрессионных и source-contract тестов.
- Компиляция DLL под Windows с ScriptHookVDotNet2 API 2.
- Pre-build validation, проверка C# 4 / .NET Framework 4.0 API.
- Проверка установщика и повторной установки.
- Проверка структуры ZIP, DLL и JSON.

Результат CI: все этапы сборки и проверки пакета прошли. Игровой runtime не запускался.

## Повторное ручное тестирование
1. **Телохранители:** проверить наведение и попадание обычным оружием; проверить убийство одного NPC; выполнить Remove All Bodyguards при 1, 4 и максимальном количестве. Убедиться, что игра не вылетает.
2. **Nitro X8:** проверить движение и показания скорости. Цель — 1200 км/ч. Затем отключить Nitro и сменить автомобиль: штатные параметры должны восстановиться.
3. **Режим бога машины:** проверить, что после столкновений не появляются вмятины, разбитые стёкла и другие визуальные повреждения.
4. **Двери:** проверить обычную анимацию открытия, посадки и выхода, включая посадку телохранителей.
5. **Fort Zancudo:** с Never Wanted или Cops Ignore провести на базе не менее нескольких минут; проверить, что не появляются новые истребители и уже находящиеся в воздухе не атакуют игрока.
6. **Логирование:** проверить создание папки scripts\\AvernesSowtware и файлов журнала; после аварийного завершения проверить отметку незакрытой сессии при следующем старте.
7. **Каталог:** записать фактическое число машин и проверить недоступные модели, включая lm87. Текущий тестовый результат — 542; цель 619 остаётся открытой.
8. **Регрессии:** перепроверить парашют, спавн транспорта, Max Tuning, номера, тонировку, сохранение тюнинга, World/погоду, оружие, функции игрока и телепортацию.

## Ограничения
Успешная компиляция и статические тесты не подтверждают поведение в самой игре. До повторной ручной проверки телохранителей, Nitro, режима бога, дверей и Fort Zancudo нельзя объявлять все игровые дефекты окончательно закрытыми. Каталог 619 также не достигнут.
"""
(root / "docs" / "CHANGELOG_1_4_2_RU.md").write_text(changelog, encoding="utf-8")
(root / "docs" / "TEST_REPORT_1_4_2_RU.md").write_text(report, encoding="utf-8")
for path, markers in (
    (root / "docs" / "CHANGELOG_1_4_2_RU.md", ("Nitro X8", "массовое снятие", "Fort Zancudo", "542", "619")),
    (root / "docs" / "TEST_REPORT_1_4_2_RU.md", ("28 регрессионных", "1200 км/ч", "542", "619")),
):
    text = path.read_text(encoding="utf-8")
    if any(marker not in text for marker in markers):
        raise SystemExit("Updated release documentation failed its content check: " + str(path))

print("Updated four stale test assertions and two strict build-validator contracts; no tests were skipped.")
