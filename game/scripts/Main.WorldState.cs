using System.Collections.Generic;
using System.Linq;
using Godot;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>
/// Сцена ↔ <see cref="WorldState"/>: что игрок изменил в мире и как это
/// показать после загрузки или новой игры.
/// </summary>
public partial class Main
{
    /// <summary>Где и что лежало при генерации: нужно, чтобы вернуть лут после загрузки.</summary>
    /// <param name="Parent">Корень куска мира: лут выгружается вместе с ним. Null — сама сцена.</param>
    private sealed record LootSpawn(string StateId, Vector3 Position, string ItemId, int Count,
        int Chunk = -1, Node3D? Parent = null);

    private readonly List<LootSpawn> _lootSpawns = new();

    private void MarkWorld(string? stateId)
    {
        if (stateId is not null)
            _car.Session.WorldState.MarkDone(stateId);
    }

    /// <summary>
    /// Привести сцену к состоянию мира из текущей сессии. Лут и шлагбаумы
    /// синхронизируются в обе стороны. Выезды и завал — только «убрать»:
    /// вернуть удалённую стену нельзя, поэтому при загрузке более старого
    /// сейва в той же сцене они остаются открытыми. Из главного меню сцена
    /// строится заново, и там всё точно.
    /// </summary>
    private void SyncWorldState()
    {
        var world = _car.Session.WorldState;

        var lootPoints = _interactions
            .Where(point => point.Kind == InteractionKind.Loot && point.StateId is not null)
            .ToDictionary(point => point.StateId!);

        foreach (var spawn in _lootSpawns)
        {
            bool taken = world.IsDone(spawn.StateId);
            bool lying = lootPoints.TryGetValue(spawn.StateId, out var point);

            if (taken && lying)
            {
                _interactions.Remove(point!);
                point!.Node?.QueueFree();
            }
            else if (!taken && !lying)
            {
                PlaceLoot(spawn);
            }
        }

        foreach (var point in _interactions)
        {
            if (point.Gate is not { } gate || point.StateId is null) continue;
            if (world.IsDone(point.StateId)) gate.Open();
            else gate.Close();
        }

        for (int i = 0; i < _exitGates.Count; i++)
        {
            var (_, barrier, wall) = _exitGates[i];
            if (!world.IsDone(WorldState.Exit(i)) || !IsInstanceValid(wall)) continue;

            if (!barrier.IsOpen) barrier.Toggle();
            wall.QueueFree();
        }

        if (world.IsDone(WorldState.Collapse(0)) && _collapseNode is not null && IsInstanceValid(_collapseNode))
        {
            _collapseNode.QueueFree();
            _collapseNode = null;
            _collapseBody = null;
        }
    }
}
