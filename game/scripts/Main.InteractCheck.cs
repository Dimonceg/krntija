using System.Linq;
using Godot;

namespace Krntija.Game;

/// <summary>
/// --selftest-interact: клавиша E у дома и у заправки (баги «не сесть в машину у
/// двери», «не выйти из машины у дома», «поднять хлеб за закрытой дверью»). Позиции
/// и взгляд ставятся руками, выбор — тот же <see cref="SelectTarget"/>, что у клавиши
/// и подсказки.
/// </summary>
public partial class Main
{
    private bool _checkInteract;

    private async void RunInteractCheck()
    {
        _selfTestOk = true;

        var door = _interactions.FirstOrDefault(point => point.Kind == InteractionKind.Door && point.Door?.Name == "FrontDoor")
                   ?? _interactions.FirstOrDefault(point => point.Kind == InteractionKind.Door);
        var pump = _interactions.FirstOrDefault(point => point.Kind == InteractionKind.Pump);
        Check(door is not null && pump is not null, "у старта есть дверь дома и колонка");
        if (door is null || pump is null)
        {
            QuitSoon(1);
            return;
        }

        // Пешком у входа: машина в 2,5 м сбоку от игрока, дверь в 1,5 м перед ним.
        Vector3 ground = new(door.Position.X, _houseGroundY, door.Position.Z);
        Vector3 outward = (ground - _housePosition!.Value with { Y = _houseGroundY }).Normalized();
        Vector3 side = outward.Cross(Vector3.Up).Normalized();

        ExitCar();
        _player!.Teleport(ground + outward * 1.5f);
        _car.GlobalPosition = _player.GlobalPosition + side * 2.5f + Vector3.Up * 0.6f;
        _car.LinearVelocity = Vector3.Zero;

        string Look(Vector3 at)
        {
            Vector3 eye = (_player?.GlobalPosition ?? _car.GlobalPosition) + Vector3.Up * 1.6f;
            _camera.GlobalPosition = eye - (at - eye).Normalized() * 0.3f;
            _camera.LookAt(at, Vector3.Up);
            var target = SelectTarget();
            return target.Kind == TargetKind.Point ? target.Point!.Kind.ToString() : target.Kind.ToString();
        }

        string atDoor = Look(door.Position);
        Check(atDoor == nameof(InteractionKind.Door), $"пешком, взгляд на дверь: {atDoor}");
        UpdateFocus();
        Check(_highlight.Target == door.Door, $"обведена дверь: {_highlight.Target?.Name}");
        await Snap("interact_door", door.Door!.Center);

        // Предмет в метре за закрытой дверью: не виден, пока дверь не открыли.
        var behind = new InteractionPoint(ground - outward * 1.0f + Vector3.Up * 0.8f, InteractionKind.Loot, 2.2f);
        int doorIndex = _interactions.IndexOf(door);
        _interactions.RemoveAt(doorIndex);
        _interactions.Add(behind);

        if (door.Door!.IsOpen) door.Door.Toggle();
        await WaitPhysics();
        string closed = Look(behind.Position);
        Check(closed != nameof(InteractionKind.Loot), $"предмет за закрытой дверью не выбран: {closed}");

        door.Door.Toggle();
        await WaitPhysics();
        string open = Look(behind.Position);
        Check(open == nameof(InteractionKind.Loot), $"за открытой дверью предмет: {open}");

        door.Door.Toggle();
        await WaitPhysics();
        _interactions.Remove(behind);
        _interactions.Insert(doorIndex, door);

        CheckPointsReachable();
        await CheckWorkbenchOutline();

        string atCar = Look(_car.GlobalPosition + Vector3.Up * 0.8f);
        Check(atCar == nameof(TargetKind.EnterCar), $"пешком, взгляд на машину у двери: {atCar}");
        Check(BuildPrompt() == "[E] сесть в машину", $"подсказка совпадает с действием: «{BuildPrompt()}»");

        // В машине у дома: E — выйти, а не дверь и не сундук.
        EnterCar();
        string inCar = Look(door.Position);
        Check(inCar == nameof(TargetKind.ExitCar), $"в машине у двери: {inCar}");

        // В машине у колонки — заправка; на ходу — ничего, кроме «остановись».
        _car.GlobalPosition = pump.Position + Vector3.Up * 0.2f;
        string atPump = Look(pump.Position);
        Check(atPump == nameof(InteractionKind.Pump), $"в машине у колонки: {atPump}");

        _car.LinearVelocity = new Vector3(0, 0, -10);
        string moving = Look(pump.Position);
        Check(moving == nameof(TargetKind.StopFirst), $"в машине на ходу: {moving}");
        _car.LinearVelocity = Vector3.Zero;

        GD.Print(_selfTestOk ? "[interact] OK" : "[interact] FAIL");
        QuitSoon(_selfTestOk ? 0 : 1);
    }

    /// <summary>У верстака обводится сам верстак: его меш не слит с домом.</summary>
    private async System.Threading.Tasks.Task CheckWorkbenchOutline()
    {
        var bench = _interactions.FirstOrDefault(point => point.Kind == InteractionKind.Workbench);
        Check(bench?.Visual is MeshInstance3D, "у верстака свой меш для обводки");
        if (bench?.Visual is not { } visual) return;

        Vector3 away = (bench.Position - visual.GlobalPosition) with { Y = 0 };
        Vector3 stand = bench.Position + away.Normalized() * 0.6f;
        _player!.Teleport(stand with { Y = _houseGroundY });
        await WaitPhysics();

        Vector3 eye = _player.GlobalPosition + Vector3.Up * 1.6f;
        _camera.GlobalPosition = eye - (visual.GlobalPosition - eye).Normalized() * 0.3f;
        _camera.LookAt(visual.GlobalPosition, Vector3.Up);
        UpdateFocus();
        Check(_highlight.Target == visual, $"обведён верстак: {_highlight.Target?.Name}");
        await Snap("interact_workbench", visual.GlobalPosition);
    }

    /// <summary>Снимок в user://, только при запуске с окном: посмотреть обводку глазами.</summary>
    private async System.Threading.Tasks.Task Snap(string name, Vector3 target)
    {
        if (DisplayServer.GetName() == "headless") return;

        // Из-за плеча: выбор цели уже сделан, камера только показывает её.
        Vector3 eye = _player!.GlobalPosition + Vector3.Up * 1.6f;
        Vector3 back = (eye - target) with { Y = 0 };
        _camera.ProcessMode = ProcessModeEnum.Disabled;
        _camera.LookAtFromPosition(eye + back.Normalized() * 1.6f + Vector3.Up * 0.5f, target, Vector3.Up);
        for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        string path = ProjectSettings.GlobalizePath($"user://{name}.png");
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"[shot] {path}");
        _camera.ProcessMode = ProcessModeEnum.Inherit;
    }

    /// <summary>Коллизия двери меняется в _Process, а физика видит её со следующего шага.</summary>
    private async System.Threading.Tasks.Task WaitPhysics()
    {
        for (int i = 0; i < 2; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }

    /// <summary>
    /// Луч видимости не должен прятать точки, которые стоят внутри своей модели (якорь
    /// колонки, лавка): каждую загруженную точку видно хотя бы с одной стороны в её радиусе.
    /// </summary>
    private void CheckPointsReachable()
    {
        int tested = 0;
        foreach (var point in _interactions)
        {
            tested++;

            float reach = Mathf.Min(point.Radius * 0.8f, 2f);
            bool seen = false;
            Node? blocker = null;
            for (int i = 0; i < 8 && !seen; i++)
            {
                float angle = i * Mathf.Tau / 8;
                Vector3 eye = point.Position + new Vector3(Mathf.Cos(angle) * reach, 0.5f, Mathf.Sin(angle) * reach);
                blocker = SightBlocker(eye, point);
                seen = blocker is null;
            }

            if (!seen)
                Check(false, $"точку {point.Kind} не видно ни с одной стороны: {point.Position}, закрывает {blocker?.GetPath()}");
        }

        Check(tested > 0, $"точек проверено на видимость: {tested}");
    }
}
