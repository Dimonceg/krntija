namespace Krntija.Core.World;

/// <summary>
/// Что игрок изменил в мире: подобранный лут, посещённые места, открытые
/// шлагбаумы и выезды, разобранные завалы. Мир строится заново по сиду,
/// а этот список говорит, что в нём уже «сделано». Попадает в сейв.
/// </summary>
/// <remarks>
/// Id стабильные и не зависят от порядка узлов в сцене:
/// <c>"&lt;что&gt;:&lt;источник&gt;:&lt;номер&gt;"</c>, например <c>loot:house:3</c>,
/// <c>poi:2</c>, <c>gate:1</c>. Конструкторы id — ниже, чтобы формат был в одном месте.
/// </remarks>
public sealed class WorldState
{
    private readonly HashSet<string> _done = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Done => _done;

    public bool IsDone(string id) => _done.Contains(id);

    /// <summary>Отметить сделанным. True — если это случилось впервые.</summary>
    public bool MarkDone(string id) => _done.Add(id);

    public void Restore(IEnumerable<string> ids)
    {
        _done.Clear();
        foreach (string id in ids)
            if (!string.IsNullOrWhiteSpace(id))
                _done.Add(id);
    }

    public static string Loot(string source, int index) => $"loot:{source}:{index}";

    public static string Place(int poiIndex) => $"poi:{poiIndex}";

    public static string Gate(int poiIndex) => $"gate:{poiIndex}";

    public static string Exit(int index) => $"exit:{index}";

    public static string Collapse(int index) => $"collapse:{index}";
}
