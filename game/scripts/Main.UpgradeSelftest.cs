using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Krntija.Core;
using Krntija.Core.Items;
using Krntija.Core.Parts;
using Krntija.Core.Vehicle;

namespace Krntija.Game;

/// <summary>
/// --selftest-upgrades: улучшение ставится у машины где угодно (A9). Проверяем
/// подсказку второй строкой, установку по U у машины, отказ вдали от неё и
/// порядок на верстаке: сначала установка, потом починка.
/// </summary>
public partial class Main
{
    /// <summary>Прогон --selftest-upgrades: улучшения ставятся у машины (A9).</summary>
    private bool _selfTestUpgrades;

    private async void RunUpgradeSelfTest()
    {
        void Check(bool ok, string what)
        {
            if (ok) GD.Print($"[upgrade-selftest] ok: {what}");
            else
            {
                _selfTestOk = false;
                GD.PrintErr($"[upgrade-selftest] FAIL: {what}");
            }
        }

        async Task Frames(int count)
        {
            for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        // Встать в метрах от точки и посмотреть на неё: выбор цели идёт по взгляду.
        void Stand(Vector3 at, float distance)
        {
            Vector3 dir = at - _car.GlobalPosition;
            dir.Y = 0;
            if (dir.LengthSquared() < 0.01f) dir = Vector3.Forward;
            dir = dir.Normalized();

            Vector3 feet = at - dir * distance;
            _player!.Teleport(feet);
            Vector3 eye = feet + Vector3.Up * 1.5f;
            _camera.GlobalPosition = eye;
            _camera.LookAt(at, Vector3.Up);

            UpdateHands();
            UpdateFocus();
        }

        _selfTestOk = true;
        StartNewGame(CarCatalog.Zastava101);
        await Frames(4);
        _camera.ProcessMode = ProcessModeEnum.Disabled;

        // Машину — далеко от дома: A9 разрешает ставить улучшение где угодно.
        _streamer?.BuildAround(300);
        PlaceCarOnChunkRoad(300);
        ExitCar();
        await Frames(4);

        var session = _car.Session;

        // 1. Улучшение в багажнике, игрок пешком у машины: подсказка и U.
        Check(session.Trunk.TryAdd(UpgradeCatalog.RoofRack, 1), "улучшение в багажнике");
        Stand(_car.GlobalPosition, 1.4f);
        await Frames(1);

        Check(_hud.Prompt.Contains("[U] поставить: Багажник на крышу"),
            $"подсказка U: «{_hud.Prompt}»");

        int attachments = _car.BodyVisuals?.AttachmentCount ?? 0;
        InstallUpgradeNearCar();

        Check(session.Upgrades.Contains(UpgradeCatalog.RoofRack), "улучшение поставлено у машины");
        Check(session.Trunk.Count(UpgradeCatalog.RoofRack) == 0, "предмет списан из багажника");

        int now = _car.BodyVisuals?.AttachmentCount ?? 0;
        Check(now == attachments + 1, $"навес на кузове: было {attachments}, стало {now}");

        // 2. Игрок отошёл — U не ставит и подсказки нет.
        Check(session.Inventory.TryAdd(UpgradeCatalog.Headlights, 1), "второе улучшение в кармане");
        _player!.Teleport(_car.GlobalPosition + new Vector3(12, 1, 0));
        await Frames(2);
        UpdateFocus();

        Check(!_hud.Prompt.Contains("[U] поставить"), $"вдали подсказки нет: «{_hud.Prompt}»");

        InstallUpgradeNearCar();
        Check(!session.Upgrades.Contains(UpgradeCatalog.Headlights), "вдали не ставится");
        Check(session.Inventory.Count(UpgradeCatalog.Headlights) == 1, "предмет остался в кармане");

        // 3. Верстак: установка раньше починки, подсказка говорит, что будет.
        if (_interactions.FirstOrDefault(point => point.Kind == InteractionKind.Workbench) is not { } bench)
        {
            Check(false, "верстак найден");
            GD.Print(_selfTestOk ? "[upgrade-selftest] OK" : "[upgrade-selftest] FAIL");
            QuitSoon(_selfTestOk ? 0 : 1);
            return;
        }

        // 3а. Верстак без машины: подсказка про починку, улучшение не ставится.
        Stand(bench.Position, 1.0f);
        await Frames(1);

        Check(_hud.Prompt.Contains("починить"), $"верстак без машины: «{_hud.Prompt}»");

        RepairAtWorkbench();
        Check(!session.Upgrades.Contains(UpgradeCatalog.Headlights), "верстак без машины не ставит");
        Check(session.Inventory.Count(UpgradeCatalog.Headlights) == 1, "верстак без машины: предмет цел");

        // 3б. Машина рядом с верстаком: сначала установка, потом починка.
        Vector3 gaze = -_camera.GlobalBasis.Z;
        _car.GlobalPosition = _player!.GlobalPosition - gaze * 1.5f + Vector3.Up * 0.3f;

        if (session.Vehicle.Parts.Values.OrderBy(part => part.Condition).FirstOrDefault() is not { } worn)
        {
            Check(false, "есть деталь для починки");
            GD.Print(_selfTestOk ? "[upgrade-selftest] OK" : "[upgrade-selftest] FAIL");
            QuitSoon(_selfTestOk ? 0 : 1);
            return;
        }

        worn.SetCondition(0.25);
        await Frames(2);
        UpdateFocus();

        Check(PlayerToCar() <= UpgradeInstall.NearCarMeters, $"машина рядом с верстаком: {PlayerToCar():F1} м");

        // Сама подсказка верстака: E у машины «сесть» выигрывает у точки по
        // правилам выбора цели, поэтому строку берём из того же метода, что HUD.
        Check(WorkbenchPrompt().Contains("поставить"), $"подсказка верстака: «{WorkbenchPrompt()}»");

        int tape = session.Inventory.Count(ItemIds.DuctTape);
        double before = worn.Condition;
        RepairAtWorkbench();

        Check(session.Upgrades.Contains(UpgradeCatalog.Headlights), "верстак: улучшение поставлено");
        Check(Math.Abs(worn.Condition - before) < 1e-9, "верстак: сначала установка, деталь не тронута");
        Check(session.Inventory.Count(ItemIds.DuctTape) == tape, "верстак: изолента цела");

        RepairAtWorkbench();
        Check(worn.Condition > before, "верстак: потом починка");

        GD.Print(_selfTestOk ? "[upgrade-selftest] OK" : "[upgrade-selftest] FAIL");
        QuitSoon(_selfTestOk ? 0 : 1);
    }
}
