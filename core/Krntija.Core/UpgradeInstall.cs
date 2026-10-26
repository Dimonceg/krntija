using Krntija.Core.Items;

namespace Krntija.Core;

/// <summary>Почему улучшение нельзя поставить сейчас (A9).</summary>
public enum UpgradeInstallResult
{
    Ok,

    /// <summary>Улучшения нет ни в кармане, ни в багажнике.</summary>
    NotCarried,

    /// <summary>Такое улучшение уже стоит на машине.</summary>
    AlreadyInstalled,

    /// <summary>Игрок не у машины (или машины нет рядом).</summary>
    NotNear,

    /// <summary>Такого улучшения нет в каталоге.</summary>
    Unknown,
}

/// <summary>
/// Установка улучшения у машины, где угодно (A9). Временное правило до ADR 0004,
/// когда улучшения станут деталями и будут ставиться руками, как детали сборки:
/// эффект по-прежнему даёт <see cref="GameSession.ApplyUpgrade"/> и
/// <see cref="UpgradeEffects"/>, здесь только «можно ли» и списание предмета.
/// </summary>
public static class UpgradeInstall
{
    /// <summary>Дальше этого от машины улучшение не поставить, метры.</summary>
    public const double NearCarMeters = 4.0;

    /// <summary>
    /// Можно ли поставить улучшение <paramref name="id"/> сейчас.
    /// <paramref name="distanceToCar"/> — от игрока до машины, метры (считает сцена).
    /// </summary>
    public static UpgradeInstallResult CanInstall(GameSession session, string id, double distanceToCar)
    {
        if (!UpgradeCatalog.TryGet(id, out _)) return UpgradeInstallResult.Unknown;
        if (session.Upgrades.Contains(id)) return UpgradeInstallResult.AlreadyInstalled;
        if (distanceToCar < 0 || distanceToCar > NearCarMeters) return UpgradeInstallResult.NotNear;
        if (!Carried(session, id)) return UpgradeInstallResult.NotCarried;

        return UpgradeInstallResult.Ok;
    }

    /// <summary>
    /// Поставить улучшение: списать предмет из кармана, иначе из багажника, и
    /// применить его эффект. Причина отказа — как у <see cref="CanInstall"/>.
    /// </summary>
    public static UpgradeInstallResult Install(GameSession session, string id, double distanceToCar)
    {
        var result = CanInstall(session, id, distanceToCar);
        if (result != UpgradeInstallResult.Ok) return result;

        if (session.Inventory.Count(id) > 0)
            session.Inventory.Remove(id, 1);
        else
            session.Trunk.Remove(id, 1);

        session.ApplyUpgrade(id);
        return UpgradeInstallResult.Ok;
    }

    /// <summary>
    /// Что можно поставить: первое по каталогу, что лежит с собой и ещё не стоит.
    /// null — ставить нечего.
    /// </summary>
    public static string? Pending(GameSession session)
    {
        foreach (string id in UpgradeCatalog.All.Keys)
        {
            if (session.Upgrades.Contains(id)) continue;
            if (!Carried(session, id)) continue;

            return id;
        }

        return null;
    }

    /// <summary>Название улучшения для подсказки.</summary>
    public static string Name(string id) => UpgradeCatalog.TryGet(id, out var def) ? def.Name : id;

    private static bool Carried(GameSession session, string id) =>
        session.Inventory.Count(id) > 0 || session.Trunk.Count(id) > 0;
}
