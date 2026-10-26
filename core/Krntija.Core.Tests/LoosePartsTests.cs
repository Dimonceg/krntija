using Krntija.Core.Parts;
using Xunit;

namespace Krntija.Core.Tests;

/// <summary>Детали в мире: стабильные id и состояние (ADR 0003, п. 3).</summary>
public class LoosePartsTests
{
    [Fact]
    public void IdsAreStablePerSource()
    {
        var parts = new LooseParts();

        var first = parts.Add("hood_101", 1, 1, 0, 2, 0, LooseParts.Garage);
        var second = parts.Add("door_fl_101", 1, 1, 0, 3, 0, LooseParts.Garage);
        var dropped = parts.Add("wheel_steel", 1, 1, 0, 4, 0);

        Assert.Equal("part:garage:0", first.Id);
        Assert.Equal("part:garage:1", second.Id);
        Assert.Equal("part:world:0", dropped.Id);
        Assert.Equal(3, parts.Count);
    }

    [Fact]
    public void RemovedPartDoesNotShiftIds()
    {
        var parts = new LooseParts();
        var first = parts.Add("hood_101", 1, 0, 0, 0, 0, LooseParts.Garage);
        var second = parts.Add("trunk_101", 1, 0, 1, 0, 0, LooseParts.Garage);

        Assert.True(parts.Remove(first.Id));
        Assert.False(parts.Remove(first.Id), "второй раз убирать нечего");
        Assert.Null(parts.Find(first.Id));
        Assert.NotNull(parts.Find(second.Id));

        // Номер — следующий за самым большим в источнике, соседи не сдвигаются.
        var next = parts.Add("door_fl_101", 1, 0, 2, 0, 0, LooseParts.Garage);
        Assert.Equal("part:garage:2", next.Id);
    }

    [Fact]
    public void FindAndAdd()
    {
        var parts = new LooseParts();
        var part = new LoosePart("part:world:5", "engine_1_6", 0.6, 1, 2, 3, 0.5);

        Assert.True(parts.Add(part));
        Assert.False(parts.Add(part), "один и тот же id дважды не добавить");
        Assert.Same(part, parts.Find("part:world:5"));

        parts.Clear();
        Assert.Equal(0, parts.Count);
    }

    [Fact]
    public void PartKeepsItsWearWhenItGoesToTheCar()
    {
        var loose = new LoosePart("part:garage:0", "engine_1_6", 0.42, 0, 0, 0, 0);

        var part = loose.ToPart();

        Assert.NotNull(part);
        Assert.Equal(0.42, part!.Condition, 3);
        Assert.Equal(PartSlot.Engine, part.Slot);
        Assert.Equal("engine_1_6", part.Definition.Id);
    }

    [Fact]
    public void UnknownPartAndBadCondition()
    {
        var unknown = new LoosePart("part:world:0", "нет_такой_детали", 1, 0, 0, 0, 0);
        Assert.Null(unknown.Definition);
        Assert.Null(unknown.ToPart());

        var worn = new LoosePart("part:world:1", "hood_101", 7.5, 0, 0, 0, 0);
        Assert.Equal(1.0, worn.Condition, 3);

        var broken = new LoosePart("part:world:2", "hood_101", -1, 0, 0, 0, 0);
        Assert.Equal(0.0, broken.Condition, 3);
    }

    [Fact]
    public void EmptyIdIsAnError()
    {
        Assert.Throws<ArgumentException>(() => new LoosePart("", "hood_101", 1, 0, 0, 0, 0));
        Assert.Throws<ArgumentException>(() => new LoosePart("  ", "hood_101", 1, 0, 0, 0, 0));
    }
}
