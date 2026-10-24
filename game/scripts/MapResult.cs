using System.Collections.Generic;
using Godot;

namespace Krntija.Game;

/// <summary>Запись на карте: что за место и где оно (для миникарты и лута).</summary>
public readonly record struct MapPlace(int Index, float Side, Vector3 Position, string Kind, string Label);

/// <summary>Точка взаимодействия, которую карта просит зарегистрировать.</summary>
public readonly record struct MapUse(Vector3 Position, string Kind, string? ItemId = null, int Count = 1);

/// <summary>Итог генерации: места, дороги, точки взаимодействия.</summary>
public sealed class MapResult
{
    public List<MapPlace> Places { get; } = new();
    public List<List<Vector3>> Branches { get; } = new();
    public List<MapUse> Uses { get; } = new();
    public List<Door> Doors { get; } = new();
    public List<int> ClosedExits { get; } = new();
    public Vector3? Settlement { get; set; }
}
