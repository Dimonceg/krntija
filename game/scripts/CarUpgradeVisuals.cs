using System.Collections.Generic;
using Godot;
using Krntija.Core.Items;

namespace Krntija.Game;

/// <summary>
/// Навесы установленных улучшений на кузове (docs/CAR_UPGRADES_ART.md): бак, ящик,
/// фары, кенгурятник, багажник — GLB с тем же началом, что у кузова; усиленная
/// подвеска поднимает кузов вместе с навесами, колёса остаются на месте.
/// </summary>
/// <remarks>
/// Узел — визуальный корень кузова: кузов и навесы его дети. Эффекты улучшений
/// считает ядро (<c>GameSession.ApplyUpgrade</c>), здесь только показ.
/// </remarks>
public partial class CarUpgradeVisuals : Node3D
{
    /// <summary>Улучшения, у которых есть навес. Двигатель под капотом — его не видно.</summary>
    private static readonly string[] Attached =
    {
        UpgradeCatalog.FuelTank, UpgradeCatalog.Toolbox, UpgradeCatalog.Headlights,
        UpgradeCatalog.Bullbar, UpgradeCatalog.RoofRack,
    };

    private readonly Dictionary<string, Node3D> _shown = new();
    private string _specId = string.Empty;
    private int _syncedCount = -1;

    /// <summary>Подъём кузова от рессор, метры: из JSON арта, 0 — рессор нет.</summary>
    public float Lift => Position.Y;

    /// <summary>Сколько навесов на машине: для проверок.</summary>
    public int AttachmentCount => _shown.Count;

    /// <param name="specId">Машина; навесы есть только у тех, для кого их сделал арт.
    /// Пусто — кузов подменён (<c>--model=</c>), навесы к нему не подходят.</param>
    public static CarUpgradeVisuals Create(string specId) => new() { Name = "Body", _specId = specId };

    /// <summary>Показать то, что установлено. Повторный вызов без новых улучшений ничего не стоит.</summary>
    public void Sync(IReadOnlySet<string> upgrades)
    {
        if (upgrades.Count == _syncedCount) return;
        _syncedCount = upgrades.Count;

        foreach (string id in Attached)
        {
            if (_shown.ContainsKey(id) || !upgrades.Contains(id)) continue;
            if (ModelLibrary.Instantiate($"res://assets/models/cars/{_specId}_{id}.glb") is not { } model) continue;

            // Контракт арта: начало навеса = начало кузова, без сдвига и масштаба.
            model.Name = id;
            AddChild(model);
            _shown[id] = model;
        }

        Position = new Vector3(0, upgrades.Contains(UpgradeCatalog.Springs) ? SpringsLift(_specId) : 0f, 0);
    }

    /// <summary><c>{ "lift": 0.04 }</c> из assets/models/cars/&lt;spec&gt;_upgrade_springs.json.</summary>
    private static float SpringsLift(string specId)
    {
        string path = $"res://assets/models/cars/{specId}_{UpgradeCatalog.Springs}.json";
        if (!FileAccess.FileExists(path)) return 0f;

        using var json = Json.ParseString(FileAccess.GetFileAsString(path));
        if (json.VariantType != Variant.Type.Dictionary) return 0f;

        using var data = json.AsGodotDictionary();
        return data.TryGetValue("lift", out var lift) ? (float)lift.AsDouble() : 0f;
    }
}
