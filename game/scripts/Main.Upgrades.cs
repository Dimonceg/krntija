using Godot;
using Krntija.Core;
using Krntija.Core.Economy;
using Krntija.Core.Items;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>
/// Установка улучшений у машины (A9, временно до ADR 0004): пешком рядом с
/// машиной клавиша U ставит одно улучшение из кармана или багажника, навес
/// появляется сразу. Правило — в ядре (<see cref="UpgradeInstall"/>), здесь
/// клавиша, подсказка и тост.
/// </summary>
public partial class Main
{
    /// <summary>От игрока до машины, метры; пешком нет — бесконечно далеко.</summary>
    private double PlayerToCar() =>
        _player is null || !_onFoot ? double.MaxValue : _player.GlobalPosition.DistanceTo(_car.GlobalPosition);

    /// <summary>
    /// Вторая строка подсказки: что поставит U. Пусто — ставить нечего или
    /// игрок не у машины.
    /// </summary>
    private string UpgradeHint()
    {
        if (_hud.Screen != HudScreen.None) return string.Empty;
        if (UpgradeInstall.Pending(_car.Session) is not { } id) return string.Empty;
        if (PlayerToCar() > UpgradeInstall.NearCarMeters) return string.Empty;

        return $"[U] поставить: {UpgradeInstall.Name(id)}";
    }

    /// <summary>Подсказка верстака: что сделает E — поставит улучшение или починит.</summary>
    private string WorkbenchPrompt()
    {
        var session = _car.Session;
        if (UpgradeInstall.Pending(session) is { } id
            && UpgradeInstall.CanInstall(session, id, PlayerToCar()) == UpgradeInstallResult.Ok)
            return $"[E] верстак: поставить {UpgradeInstall.Name(id)}";

        return $"[E] верстак: починить деталь ({Money.Format(WorldActions.WorkbenchPrice)} + изолента)";
    }

    /// <summary>Клавиша U: поставить одно улучшение, если игрок пешком у машины.</summary>
    private void InstallUpgradeNearCar()
    {
        var session = _car.Session;

        if (UpgradeInstall.Pending(session) is not { } id)
        {
            _hud.Toast("Ставить нечего");
            return;
        }

        switch (UpgradeInstall.Install(session, id, PlayerToCar()))
        {
            case UpgradeInstallResult.Ok:
                InstalledUpgrade(id);
                break;
            case UpgradeInstallResult.NotNear:
                _hud.Toast("Улучшение ставится у машины: подойди ближе");
                break;
            case UpgradeInstallResult.NotCarried:
                _hud.Toast("Улучшения нет с собой");
                break;
        }
    }

    /// <summary>Конец установки: тост, навес на кузове, звук.</summary>
    private void InstalledUpgrade(string id)
    {
        _hud.Toast($"{UpgradeInstall.Name(id)}: поставлено");
        _car.BodyVisuals?.Sync(_car.Session.Upgrades);
        Sfx.PlayDoor(_car.GlobalPosition);
    }
}
