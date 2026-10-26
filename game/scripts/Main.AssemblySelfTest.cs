using System.Linq;
using Godot;
using Krntija.Core;
using Krntija.Core.Parts;
using Krntija.Core.Vehicle;

namespace Krntija.Game;

/// <summary>
/// --selftest-assembly (ADR 0003, A5): руки, магнит и сборка машины без рук игрока.
/// Снять капот и двигатель, положить, взять, примагнитить, поставить; с закрытым
/// капотом двигатель не ставится; без аккумулятора не заводится; старт в гараже —
/// собрать машину целиком и уехать; сейв посреди сборки.
/// </summary>
public partial class Main
{
    private bool _selfTestAssembly;

    private async void RunAssemblySelfTest()
    {
        _selfTestOk = true;

        async System.Threading.Tasks.Task Frames(int count)
        {
            for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        // Встать рядом с точкой так, чтобы камера смотрела на неё: иначе SelectTarget
        // выбирает по взгляду и проверка была бы не про то.
        void StandAt(Vector3 at)
        {
            Vector3 away = at - _car.GlobalPosition;
            away.Y = 0;
            if (away.LengthSquared() < 0.01f) away = Vector3.Forward;
            away = away.Normalized();

            _player!.Teleport(at + away * 0.6f + Vector3.Up * 0.1f);
            Vector3 eye = _player.GlobalPosition + Vector3.Up * 1.5f;
            Vector3 dir = (at - eye).Normalized();
            _camera.GlobalPosition = eye - dir * 0.2f;
            _camera.LookAt(at, Vector3.Up);

            UpdateHands();
            UpdateFocus();
        }

        RigidBody3D? LooseBody(string defId)
        {
            var loose = _car.Session.LooseParts.Items.FirstOrDefault(part => part.DefId == defId);
            if (loose is null) return null;

            return _loosePartBodies.FirstOrDefault(body => body.GetMeta("part_id").AsString() == loose.Id);
        }

        bool TakePart(string defId)
        {
            if (LooseBody(defId) is not { } body) return false;

            StandAt(body.GlobalPosition);
            if (SelectTarget().Kind != TargetKind.LoosePart) return false;

            // Подсказка на экране — та, что видит игрок, а не отдельный вызов.
            if (!_hud.Prompt.Contains("взять"))
            {
                Check(false, $"подсказка у детали: «{_hud.Prompt}»");
                return false;
            }

            Interact();
            return _held is not null && _held.DefId == defId;
        }

        bool PutPart(PartSlot slot)
        {
            if (SocketPosition(slot) is not { } at) return false;

            StandAt(at);
            if (_snapSlot != slot) return false;

            if (!_hud.Prompt.Contains("установить"))
            {
                Check(false, $"подсказка с деталью у гнезда: «{_hud.Prompt}»");
                return false;
            }

            Interact();
            return _held is null && _car.Session.Vehicle.Parts.ContainsKey(slot);
        }

        bool RemovePart(PartSlot slot)
        {
            if (SocketPosition(slot) is not { } at) return false;

            StandAt(at);
            TakeAimedPart();
            return _held is not null;
        }

        bool IsOpen(PartSlot slot) => AssemblyRules.IsOpen(_car.Session.Vehicle, slot);

        // С окном — снимки для ревью (user://assembly_*.png); камера остаётся под проверкой.
        async System.Threading.Tasks.Task Shot(string name, Vector3 at)
        {
            await Snap(name, at);
            _camera.ProcessMode = ProcessModeEnum.Disabled;
        }

        StartNewGame(CarCatalog.Get("zastava_101"));
        await Frames(6);
        UpdateAssemblyObjective();

        Check(StartScenario.NeedsAssembly(_car.Session.Vehicle), "старт в гараже: машина разобрана");
        Check(_car.Session.LooseParts.Count == 6, $"деталей в мире: {_car.Session.LooseParts.Count}");
        Check(_hud.Objective.Contains("Собери"), $"задача в HUD: «{_hud.Objective}»");

        // Сейв и загрузка посреди сборки: пустые гнёзда и детали на месте.
        SaveGame();
        LoadGame();
        await Frames(4);
        Check(_car.Session.LooseParts.Count == 6, $"после загрузки деталей: {_car.Session.LooseParts.Count}");
        Check(StartScenario.MissingCount(_car.Session.Vehicle) == 6, "после загрузки машина так же разобрана");
        UpdateAssemblyObjective();
        Check(_hud.Objective.Contains("Собери"), $"задача после загрузки: «{_hud.Objective}»");

        ExitCar();
        await Frames(4);
        _camera.ProcessMode = ProcessModeEnum.Disabled;
        await Shot("assembly_start", _car.GlobalPosition);

        // 1. Капот: взять, положить (гнездо далеко, магнита нет), взять и поставить.
        Check(TakePart("hood_101"), "капот в руках");
        Check(_snapSlot is null, $"у лежащего капота магнита нет: {_snapSlot}");
        Check(BuildPrompt().Contains("положить"), $"подсказка: {BuildPrompt()}");

        Interact();
        Check(_held is null && _loosePartBodies.Count == 6, "капот положен");

        Check(TakePart("hood_101"), "капот снова в руках");
        if (SocketPosition(PartSlot.Hood) is { } hoodGhost)
        {
            StandAt(hoodGhost);
            await Shot("assembly_hood_magnet", hoodGhost);
        }

        Check(PutPart(PartSlot.Hood), "капот поставлен магнитом");
        Check(_car.Session.LooseParts.Count == 5, $"в мире деталей: {_car.Session.LooseParts.Count}");

        // 2. Снять капот в руки (R) и поставить обратно.
        Check(RemovePart(PartSlot.Hood) && _held?.DefId == "hood_101", "капот снят в руки (R)");
        Check(PutPart(PartSlot.Hood), "капот снова на машине");
        Check(!IsOpen(PartSlot.Hood), "поставленная панель закрыта");

        // 3. С закрытым капотом двигатель не ставится: E кладёт его на землю.
        Check(TakePart("engine_1_6"), "двигатель в руках");
        var engineSocket = SocketPosition(PartSlot.Engine);
        Check(engineSocket is not null, "у машины есть гнездо двигателя");
        if (engineSocket is { } engineAt) StandAt(engineAt);

        Check(_snapSlot is null, $"с закрытым капотом магнита нет: {_snapSlot}");
        Interact();
        Check(_held is null && !_car.Session.Vehicle.Parts.ContainsKey(PartSlot.Engine),
            "с закрытым капотом двигатель не поставлен");

        // 4. Открыть капот (E на панели) и поставить двигатель.
        var hoodSocket = SocketPosition(PartSlot.Hood);
        Check(hoodSocket is not null, "у машины есть гнездо капота");
        if (hoodSocket is { } hoodAt)
        {
            StandAt(hoodAt);
            var aim = SelectTarget();
            Check(aim.Kind == TargetKind.CarPart && aim.Slot == PartSlot.Hood,
                $"E смотрит на капот: {aim.Kind}/{aim.Slot}");
            Interact();
            Check(IsOpen(PartSlot.Hood), "капот открыт");
        }

        Check(TakePart("engine_1_6"), "двигатель снова в руках");
        if (SocketPosition(PartSlot.Engine) is { } engineGhost)
        {
            StandAt(engineGhost);
            await Shot("assembly_engine_magnet", engineGhost);
        }

        Check(PutPart(PartSlot.Engine), "двигатель поставлен магнитом");

        Check(RemovePart(PartSlot.Engine) && _held?.DefId == "engine_1_6", "двигатель снят в руки (R)");
        Check(PutPart(PartSlot.Engine), "двигатель снова на машине");

        // 5. Без аккумулятора не заводится, с ним — можно.
        Check(!AssemblyRules.CanStart(_car.Session.Vehicle), "без аккумулятора не завестись");
        Check(TakePart("battery_std"), "аккумулятор в руках");
        Check(PutPart(PartSlot.Battery), "аккумулятор поставлен");
        Check(AssemblyRules.CanStart(_car.Session.Vehicle), "с двигателем и аккумулятором завестись можно");

        // 6. Сейв посреди сборки: три детали на машине, три в мире, капот открыт.
        EnterCar();
        await Frames(3);
        SaveGame();
        LoadGame();
        await Frames(4);
        Check(_car.Session.Vehicle.Parts.ContainsKey(PartSlot.Engine)
              && _car.Session.Vehicle.Parts.ContainsKey(PartSlot.Battery)
              && _car.Session.Vehicle.Parts.ContainsKey(PartSlot.Hood), "после загрузки детали на машине");
        Check(_car.Session.LooseParts.Count == 3, $"после загрузки в мире деталей: {_car.Session.LooseParts.Count}");
        Check(IsOpen(PartSlot.Hood), "открытый капот остался открытым");

        ExitCar();
        await Frames(3);
        _camera.ProcessMode = ProcessModeEnum.Disabled;

        // 7. Собрать остальное: радиатор, крышку багажника, левую дверь.
        Check(TakePart("radiator_std") && PutPart(PartSlot.Radiator), "радиатор поставлен");
        Check(TakePart("trunk_101") && PutPart(PartSlot.TrunkLid), "крышка багажника поставлена");
        Check(TakePart("door_fl_101") && PutPart(PartSlot.DoorFrontLeft), "дверь поставлена");

        Check(!StartScenario.NeedsAssembly(_car.Session.Vehicle), "машина собрана целиком");
        await Shot("assembly_done", _car.GlobalPosition);
        Check(_car.Session.LooseParts.Count == 0, $"деталей в мире: {_car.Session.LooseParts.Count}");
        await Frames(2);
        Check(!_hud.Objective.Contains("Собери"), $"задача сборки снята: «{_hud.Objective}»");

        // 8. Завестись и уехать.
        EnterCar();
        await Frames(3);
        _car.SetHandbrake(false);
        _car.ForcedInput = new DriveInput(Ignition: true, Throttle: 0.7);
        await Frames(90);
        Check(_car.Session.Vehicle.EngineRunning, "двигатель завёлся");
        Check(_car.SpeedMps > 1f, $"машина поехала: {_car.SpeedMps:F2} м/с");
        _car.ForcedInput = null;
        _car.SetHandbrake(true);

        GD.Print(_selfTestOk ? "[assembly-selftest] OK" : "[assembly-selftest] FAIL");
        QuitSoon(_selfTestOk ? 0 : 1);
    }
}
