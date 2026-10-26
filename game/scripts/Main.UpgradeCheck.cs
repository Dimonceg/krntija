using System.Linq;
using Godot;
using Krntija.Core;
using Krntija.Core.Items;
using Krntija.Core.Vehicle;

namespace Krntija.Game;

/// <summary>
/// --check-car-upgrades: навесы улучшений на каждой машине, у которой они есть
/// (docs/CAR_UPGRADES_ART.md): все пять на месте, рессоры поднимают кузов, а не
/// колёса, после сохранения и загрузки навесы снова на машине.
/// </summary>
public partial class Main
{
    private bool _checkCarUpgrades;

    private async void RunCarUpgradesCheck()
    {
        _selfTestOk = true;
        int checkedCars = 0;

        foreach (var spec in CarCatalog.All)
        {
            if (!ResourceLoader.Exists($"res://assets/models/cars/{spec.Id}_{UpgradeCatalog.Bullbar}.glb")) continue;
            checkedCars++;

            StartNewGame(spec);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            var body = _car.BodyVisuals;
            Check(body is not null && body.AttachmentCount == 0, $"{spec.Id}: новая машина без навесов");

            foreach (string id in UpgradeCatalog.All.Keys) _car.Session.ApplyUpgrade(id);
            for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            Check(body?.AttachmentCount == 5, $"{spec.Id}: все пять навесов на кузове ({body?.AttachmentCount})");
            Check(body is not null && Mathf.IsEqualApprox(body.Lift, 0.04f, 0.005f), $"{spec.Id}: рессоры поднимают кузов на 4 см ({body?.Lift:F3})");
            Check(_car.WheelsOutsideBody, $"{spec.Id}: колёса не поднимаются вместе с кузовом");

            SaveGame();
            LoadGame();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            Check(_car.BodyVisuals?.AttachmentCount == 5, $"{spec.Id}: после загрузки навесы на месте ({_car.BodyVisuals?.AttachmentCount})");
        }

        Check(checkedCars >= 2, $"машин с навесами проверено: {checkedCars}");
        GD.Print(_selfTestOk ? "[upgrades] OK" : "[upgrades] FAIL");
        QuitSoon(_selfTestOk ? 0 : 1);
    }
}
