using System;
using System.Linq;
using Godot;
using Krntija.Core;
using Krntija.Core.Parts;
using Krntija.Core.Vehicle;

namespace Krntija.Game;

/// <summary>
/// --selftest-assembly (ADR 0003, A5): руки, магнит и сборка машины без рук игрока.
/// Старт в сборочном боксе (A6): машина на якоре, детали на местах, ворота закрыты;
/// после сборки ворота открываются (E) и машина выезжает. Снять капот и двигатель,
/// положить, взять, примагнитить, поставить; с закрытым капотом двигатель не
/// ставится; без аккумулятора не заводится; сейв посреди сборки.
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

        // Пол бокса: у якоря машины, иначе пол дома.
        float FloorY()
        {
            if (HouseNode() is not { } house) return _houseGroundY;

            return house.FindChild("GarageCarAnchor", true, false) is Node3D anchor
                ? anchor.GlobalPosition.Y
                : _houseGroundY;
        }

        // Встать рядом с точкой на полу бокса и смотреть на неё: иначе SelectTarget
        // выбирает по взгляду и проверка была бы не про то. У детали в мире — со
        // стороны машины, у детали машины и створки ворот — снаружи от неё.
        void Stand(Vector3 at, bool awayFromCar, float distance = 0.6f)
        {
            // Проверка сама не должна оставаться за рулём: руки работают только пешком.
            if (_player is null) ExitCar();

            Vector3 dir = at - _car.GlobalPosition;
            dir.Y = 0;
            if (dir.LengthSquared() < 0.01f) dir = Vector3.Forward;
            dir = dir.Normalized();
            if (!awayFromCar) dir = -dir;

            Vector3 feet = new(at.X + dir.X * distance, FloorY() + 0.05f, at.Z + dir.Z * distance);
            _player!.Teleport(feet);

            Vector3 eye = feet + Vector3.Up * 1.5f;
            Vector3 look = (at - eye).Normalized();
            _camera.GlobalPosition = eye - look * 0.2f;
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

            // Деталь на столе стоит выше пола: от стола отходим дальше, иначе игрок
            // оказывается внутри столешницы и не видит деталь.
            float distance = body.GlobalPosition.Y > FloorY() + 0.8f ? 1.2f : 0.6f;
            Stand(body.GlobalPosition, awayFromCar: false, distance);
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

            Stand(at, awayFromCar: true);
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

            Stand(at, awayFromCar: true);
            TakeAimedPart();
            return _held is not null;
        }

        bool IsOpen(PartSlot slot) => AssemblyRules.IsOpen(_car.Session.Vehicle, slot);

        // Створка ворот: E на двери, как у игрока.
        bool ToggleGate(string name)
        {
            var point = _interactions.FirstOrDefault(p => p.Door?.Name == name);
            if (point?.Door is not { } door) return false;

            Stand(door.Center, awayFromCar: false);
            if (SelectTarget().Point?.Door != door) return false;

            Interact();
            return door.IsOpen;
        }

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

        // A6: машина стоит в боксе по якорю, детали — по местам, ворота закрыты.
        var house = HouseNode();
        Check(house?.FindChild("GarageCarAnchor", true, false) is Node3D, "в доме есть GarageCarAnchor");
        if (house is not null)
        {
            Vector3 localCar = house.ToLocal(_car.GlobalPosition);
            Check(Mathf.Abs(localCar.X - 9f) < 0.5f && Mathf.Abs(localCar.Z + 1f) < 0.5f,
                $"машина на якоре бокса: {localCar}");
        }

        foreach (string name in new[] { "GarageBayLeft", "GarageBayRight" })
            Check(_interactions.Any(p => p.Door?.Name == name && !p.Door.IsOpen),
                $"ворота {name} закрыты на старте");

        if (house is not null)
        {
            var spots = SpotPoses(house);
            Check(spots.Count == 8, $"мест PartSpot_* в боксе: {spots.Count}");

            foreach (var placement in StartScenario.ZastavaGarage.LooseParts)
            {
                Check(spots.Any(spot => spot.Spot == placement.Spot), $"есть место {placement.Spot}");
                var pose = spots.FirstOrDefault(spot => spot.Spot == placement.Spot);
                var loose = _car.Session.LooseParts.Items.FirstOrDefault(part => part.DefId == placement.DefId);
                Check(loose is not null, $"{placement.DefId} лежит в боксе");
                if (loose is null) continue;

                double dx = loose.X - pose.X;
                double dz = loose.Z - pose.Z;
                Check(Math.Sqrt(dx * dx + dz * dz) < 1.0 && Math.Abs(loose.Y - pose.Y) < 1.2,
                    $"{placement.DefId} на месте {placement.Spot}");
            }
        }

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
            Stand(hoodGhost, awayFromCar: true);
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
        if (engineSocket is { } engineAt) Stand(engineAt, awayFromCar: true);

        Check(_snapSlot is null, $"с закрытым капотом магнита нет: {_snapSlot}");
        Interact();
        Check(_held is null && !_car.Session.Vehicle.Parts.ContainsKey(PartSlot.Engine),
            "с закрытым капотом двигатель не поставлен");

        // 4. Открыть капот (E на панели) и поставить двигатель.
        var hoodSocket = SocketPosition(PartSlot.Hood);
        Check(hoodSocket is not null, "у машины есть гнездо капота");
        if (hoodSocket is { } hoodAt)
        {
            Stand(hoodAt, awayFromCar: true);
            var aim = SelectTarget();
            Check(aim.Kind == TargetKind.CarPart && aim.Slot == PartSlot.Hood,
                $"E смотрит на капот: {aim.Kind}/{aim.Slot}");
            Interact();
            Check(IsOpen(PartSlot.Hood), "капот открыт");
        }

        Check(TakePart("engine_1_6"), "двигатель снова в руках");
        if (SocketPosition(PartSlot.Engine) is { } engineGhost)
        {
            Stand(engineGhost, awayFromCar: true);
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

        // Обновление HUD идёт в _Process, а кадры проверки — физические: зовём то же
        // обновление сами, иначе проверка гоняется с кадром.
        UpdateAssemblyObjective();
        Check(!_hud.Objective.Contains("Собери"), $"задача сборки снята: «{_hud.Objective}»");

        // 8. Открыть ворота бокса (E на каждой створке), завестись и выехать наружу.
        Check(ToggleGate("GarageBayLeft"), "левая створка ворот открыта (E)");
        Check(ToggleGate("GarageBayRight"), "правая створка ворот открыта (E)");

        EnterCar();
        await Frames(3);
        _car.SetHandbrake(false);
        _car.ForcedInput = new DriveInput(Ignition: true, Throttle: 0.7);
        await Frames(180);
        Check(_car.Session.Vehicle.EngineRunning, "двигатель завёлся");
        Check(_car.SpeedMps > 1f, $"машина поехала: {_car.SpeedMps:F2} м/с");

        if (HouseNode() is { } houseNow)
        {
            // Ворота дома в локальных координатах на z = −5,5; выехали — значит дальше.
            float z = houseNow.ToLocal(_car.GlobalPosition).Z;
            Check(z < -6.2f, $"машина выехала из бокса: z={z:F1}");
        }

        _car.ForcedInput = null;
        _car.SetHandbrake(true);

        GD.Print(_selfTestOk ? "[assembly-selftest] OK" : "[assembly-selftest] FAIL");
        QuitSoon(_selfTestOk ? 0 : 1);
    }
}
