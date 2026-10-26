using System.Linq;
using Godot;

namespace Krntija.Game;

/// <summary>
/// --selftest-interact: клавиша E у дома и у заправки (баги «не сесть в машину у
/// двери» и «не выйти из машины у дома»). Позиции и взгляд ставятся руками, выбор —
/// тот же <see cref="SelectTarget"/>, что у клавиши и подсказки.
/// </summary>
public partial class Main
{
    private bool _checkInteract;

    private void RunInteractCheck()
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
}
