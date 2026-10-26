using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Krntija.Core.Vehicle;

namespace Krntija.Game;

/// <summary>
/// --check-zastava-detail: игровая приёмка детализации Zastava (docs/ZASTAVA_DETAIL.md).
/// Смотрим то, что видит игрок: PBR-материалы на месте и не подменены одним общим,
/// стекло прозрачное, салон в кузове есть, обводка ставится на модель арта.
/// </summary>
public partial class Main
{
    private bool _checkZastavaDetail;

    private async void RunZastavaDetailCheck()
    {
        void Check(bool ok, string what)
        {
            if (ok) GD.Print($"[zastava] ok: {what}");
            else
            {
                _selfTestOk = false;
                GD.PrintErr($"[zastava] FAIL: {what}");
            }
        }

        async Task Frames(int count)
        {
            for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        _selfTestOk = true;
        StartNewGame(CarCatalog.Zastava101);
        await Frames(3);
        _camera.ProcessMode = ProcessModeEnum.Disabled;

        var meshes = new List<MeshInstance3D>();
        CollectMeshes(_car, meshes);

        var byMaterial = new Dictionary<string, int>();
        int surfaces = 0, overridden = 0, zasta = 0;
        int paintTextured = 0, glassAlpha = 0, glassCull = 0, interior = 0, glass = 0;

        foreach (var mesh in meshes)
        {
            if (mesh.MaterialOverride is not null) overridden++;

            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                surfaces++;

                if ((mesh.GetSurfaceOverrideMaterial(i) ?? mesh.Mesh.SurfaceGetMaterial(i)) is not StandardMaterial3D material)
                    continue;

                string name = material.ResourceName;
                byMaterial[name] = byMaterial.GetValueOrDefault(name) + 1;

                // Краска, металл и салон обязаны быть с атласом: это новая детализация.
                if (name.StartsWith("Zastava_"))
                {
                    zasta++;
                    if (material.AlbedoTexture is not null) paintTextured++;
                }

                if (name == "Zastava_Interior") interior++;
                if (name == "Zastava_Glass") glass++;

                if (material.Transparency == BaseMaterial3D.TransparencyEnum.Alpha)
                {
                    if (material.AlbedoColor.A < 0.5f) glassAlpha++;
                    if (material.CullMode == BaseMaterial3D.CullModeEnum.Disabled) glassCull++;
                }
            }
        }

        foreach (var (name, count) in byMaterial.OrderBy(pair => pair.Key))
            GD.Print($"[zastava] материал «{name}»: {count}");

        GD.Print($"[zastava] мешей {meshes.Count}, поверхностей {surfaces}, Zastava-поверхностей {zasta}, "
                 + $"с атласом {paintTextured}, прозрачных с alpha<0.5 {glassAlpha}, override {overridden}");

        // Числа из docs/ZASTAVA_DETAIL.md: было 7 поверхностей и 0 прозрачных.
        Check(surfaces >= 20, $"поверхностей больше семи: {surfaces}");
        Check(zasta >= 20, $"новых Zastava-поверхностей: {zasta}");
        Check(paintTextured + glass == zasta, $"краска/металл/салон с атласом: {paintTextured} из {zasta - glass} (стёкол {glass} без атласа — так и надо)");
        Check(glassAlpha >= 4, $"стёкол с альфой меньше 0.5: {glassAlpha}");
        Check(glassCull == glassAlpha, $"стёкла без отсечения задних граней: {glassCull} из {glassAlpha}");
        Check(interior >= 1, $"салон в кузове: поверхность «Zastava_Interior» — {interior}");
        Check(overridden == 0, $"PBR не подменяется общим материалом, override: {overridden}");

        // Обводка: смотрим на дверь, цель фокуса — модель арта, а не заглушка.
        // Берём заднюю правую: на старте в боксе левая передняя снята (её нет в сборке).
        Vector3 rearDoor = _car.ToGlobal(new Vector3(0.95f, 0.55f, 0.55f));
        Vector3 doorSide = _car.ToGlobal(new Vector3(1.75f, 0.1f, 0.55f));
        _player = _player ?? new Player { Name = "Player" };
        if (_player.GetParent() is null) AddChild(_player);
        _player.Teleport(doorSide);
        _onFoot = true;
        Vector3 eye = doorSide + Vector3.Up * 1.4f;
        _camera.GlobalPosition = eye;
        _camera.LookAt(rearDoor, Vector3.Up);
        await Frames(2);
        UpdateHands();
        UpdateFocus();

        Check(_hud.Prompt.Contains("дверь"), $"подсказка про дверь: «{_hud.Prompt}»");
        Check(_highlight.Target is not null, $"обводка ставится на модель: {_highlight.Target?.Name}");

        GD.Print(_selfTestOk ? "[zastava] OK" : "[zastava] FAIL");
        QuitSoon(_selfTestOk ? 0 : 1);
    }

    private static void CollectMeshes(Node node, List<MeshInstance3D> into)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is MeshInstance3D mesh && mesh.Mesh is not null) into.Add(mesh);
            CollectMeshes(child, into);
        }
    }
}
