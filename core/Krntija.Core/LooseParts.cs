using Krntija.Core.Parts;

namespace Krntija.Core;

/// <summary>
/// Деталь, лежащая в мире: на земле, на полу гаража или в руках игрока
/// (ADR 0003, п. 3). Это состояние мира, поэтому оно сохраняется: id, что за
/// деталь, её износ и где она — в мировых координатах, как машина в сейве v3.
/// </summary>
public sealed class LoosePart
{
    public LoosePart(string id, string defId, double condition, double x, double y, double z, double yaw)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("у детали в мире должен быть стабильный id", nameof(id));

        Id = id;
        DefId = defId;
        Condition = Math.Clamp(condition, 0, 1);
        X = x;
        Y = y;
        Z = z;
        Yaw = yaw;
    }

    /// <summary>Стабильный id: <c>part:garage:0</c>, <c>part:world:3</c>.</summary>
    public string Id { get; }

    public string DefId { get; }
    public double Condition { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double Yaw { get; set; }

    /// <summary>Описание из каталога; null — такого id нет (битый сейв).</summary>
    public PartDefinition? Definition =>
        PartCatalog.All.TryGetValue(DefId, out var def) ? def : null;

    /// <summary>Деталь для установки в гнездо: с тем же износом. null — нет в каталоге.</summary>
    public Part? ToPart()
    {
        if (Definition is not { } def) return null;

        var part = new Part(def);
        part.SetCondition(Condition);
        return part;
    }
}

/// <summary>
/// Детали, лежащие в мире или в руках. Порядок — как в сейве; id стабильные:
/// <c>part:&lt;источник&gt;:&lt;номер&gt;</c>, как у <see cref="World.WorldState"/>.
/// Номер берётся следующий свободный в своём источнике, поэтому брошенная
/// деталь не занимает чужой id.
/// </summary>
public sealed class LooseParts
{
    /// <summary>Источники id. Стартовые детали гаража — <see cref="Garage"/> (ADR 0003, п. 5).</summary>
    public const string World = "world";
    public const string Garage = "garage";

    private readonly List<LoosePart> _items = new();

    public IReadOnlyList<LoosePart> Items => _items;

    public int Count => _items.Count;

    /// <summary>Бросить деталь в мир: id выдаётся по источнику.</summary>
    public LoosePart Add(string defId, double condition, double x, double y, double z, double yaw,
        string source = World)
    {
        var part = new LoosePart(NextId(source), defId, condition, x, y, z, yaw);
        _items.Add(part);
        return part;
    }

    /// <summary>Добавить готовую деталь (загрузка сейва). False — такой id уже есть.</summary>
    public bool Add(LoosePart part)
    {
        if (Find(part.Id) is not null) return false;

        _items.Add(part);
        return true;
    }

    public LoosePart? Find(string id) =>
        _items.FirstOrDefault(part => string.Equals(part.Id, id, StringComparison.Ordinal));

    /// <summary>Убрать деталь из мира: её взяли в руки или поставили на машину.</summary>
    public bool Remove(string id)
    {
        var part = Find(id);
        return part is not null && _items.Remove(part);
    }

    public void Clear() => _items.Clear();

    /// <summary>Следующий свободный id источника: <c>part:world:3</c>.</summary>
    public string NextId(string source)
    {
        string prefix = $"part:{source}:";
        int next = 0;

        foreach (var part in _items)
        {
            if (!part.Id.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (int.TryParse(part.Id.AsSpan(prefix.Length), out int number) && number >= next)
                next = number + 1;
        }

        return prefix + next;
    }
}
