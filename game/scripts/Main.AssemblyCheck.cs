using System.Linq;
using Godot;
using Krntija.Core.Vehicle;

namespace Krntija.Game;

/// <summary>
/// --check-car-assembly: кузов из деталей (ADR 0003, задание C1). Гнёзда на месте и
/// дают мировые трансформы, панели открываются, снятая деталь не видна, без колеса
/// машина стоит на трёх. Машина без раскладки живёт как прежде.
/// </summary>
public partial class Main
{
    private bool _checkCarAssembly;

    private async void RunCarAssemblyCheck()
    {
        _selfTestOk = true;

        async System.Threading.Tasks.Task Seconds(float seconds)
        {
            ulong until = Time.GetTicksMsec() + (ulong)(seconds * 1000);
            while (Time.GetTicksMsec() < until) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        StartNewGame(CarCatalog.Get("zastava_101"));
        _car.SetHandbrake(true);
        await Seconds(1.5f);

        var assembly = _car.Assembly;
        Check(assembly is not null, "zastava_101: кузов из деталей");
        if (assembly is null)
        {
            QuitSoon(1);
            return;
        }

        var sockets = CarAssemblyVisual.Sockets.Concat(CarAssemblyVisual.WheelSockets).ToArray();

        // Id гнёзд в сцене — те же, что в раскладке ядра (PartSlotIds): иначе
        // механика рук спросит у Car гнездо, которого нет.
        var coreIds = Krntija.Core.CarLayout.Zastava101.Slots
            .Select(slot => Krntija.Core.PartSlotIds.Of(slot.Slot)).OrderBy(id => id).ToArray();
        Check(coreIds.SequenceEqual(sockets.OrderBy(id => id)),
            $"гнёзда сцены = раскладка ядра ({string.Join(", ", coreIds)})");
        var missing = sockets.Where(id => _car.SocketGlobal(id) is null).ToArray();
        Check(missing.Length == 0, $"все {sockets.Length} гнёзд дают трансформ (нет: {string.Join(", ", missing)})");

        // Гнёзда едут с машиной: капот спереди (−Z машины), багажник сзади.
        Vector3 local(string id) => _car.ToLocal(_car.SocketGlobal(id)!.Value.Origin);
        Check(local("hood").Z < 0 && local("trunk").Z > 0 && local("door_fl").X < 0 && local("door_fr").X > 0,
            "гнёзда стоят по своим местам кузова");

        // Детали от арта (B2) стоят в гнёздах вместо коробок-заглушек.
        foreach (string id in new[] { "engine", "radiator", "battery" })
        {
            bool model = assembly.PartNode(id)?.FindChildren("*", nameof(MeshInstance3D), true, false)
                .OfType<MeshInstance3D>().Any(mesh => mesh.Mesh is not BoxMesh) == true;
            Check(model, $"{id}: в гнезде модель, а не заглушка");
        }

        var state = new SceneAssemblyState();
        assembly.State = state;
        state.Open.Add("hood");
        state.Open.Add("door_fl");
        await Seconds(0.8f);
        Check(assembly.OpenWeight("hood") > 0.99f && assembly.OpenWeight("door_fl") > 0.99f, "капот и дверь открылись");

        Transform3D hoodOpen = assembly.PartNode("hood")!.Transform;
        state.Open.Remove("hood");
        await Seconds(0.8f);
        Check(assembly.OpenWeight("hood") < 0.01f && !assembly.PartNode("hood")!.Transform.IsEqualApprox(hoodOpen),
            "капот закрылся");

        state.Removed.Add("engine");
        state.Removed.Add("wheel_fl");
        await Seconds(1.5f);
        Check(assembly.PartNode("engine") is null, "снятый двигатель не виден");
        Check(_car.GroundedWheels == 3, $"без колеса машина стоит на трёх: {_car.GroundedWheels}");

        state.Removed.Clear();
        await Seconds(1.0f);
        Check(_car.GroundedWheels == 4 && assembly.PartNode("engine") is not null, "детали вернулись на место");

        // Без раскладки — как раньше: цельный кузов, гнёзд нет.
        StartNewGame(CarCatalog.Get("vaz_2106"));
        await Seconds(0.5f);
        Check(_car.Assembly is null && _car.SocketGlobal("hood") is null, "vaz_2106 без раскладки — цельный кузов");

        GD.Print(_selfTestOk ? "[assembly] OK" : "[assembly] FAIL");
        QuitSoon(_selfTestOk ? 0 : 1);
    }
}
