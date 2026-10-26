using System.Collections.Generic;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Лёгкая обводка вокруг того, с чем сделает действие клавиша E. Вешает на меши
/// цели второй проход (<see cref="GeometryInstance3D.MaterialOverlay"/>) со стенсил-
/// обводкой Godot; саму модель и её материалы не трогает.
/// </summary>
/// <remarks>
/// Цель должна иметь свои меши: то, что слил <see cref="StaticBatcher"/>, обвести
/// нельзя. Поэтому мебель-точки дома лежат в <see cref="StaticBatcher.SkipGroup"/>.
/// </remarks>
public sealed class InteractionHighlight
{
    private static StandardMaterial3D? _outline;

    private readonly List<GeometryInstance3D> _lit = new();
    private Node3D? _target;

    /// <summary>Сейчас обведено: для проверок.</summary>
    public Node3D? Target => _target;

    /// <summary>Обвести цель; null — снять обводку. Повторный вызов с той же целью ничего не стоит.</summary>
    public void Show(Node3D? target)
    {
        if (target is not null && !GodotObject.IsInstanceValid(target)) target = null;
        if (target == _target) return;

        Clear();
        _target = target;
        if (target is null) return;

        Collect(target);
        foreach (var mesh in _lit) mesh.MaterialOverlay = Outline;
    }

    private void Clear()
    {
        foreach (var mesh in _lit)
            if (GodotObject.IsInstanceValid(mesh)) mesh.MaterialOverlay = null;
        _lit.Clear();
        _target = null;
    }

    private void Collect(Node node)
    {
        if (node is MeshInstance3D { Visible: true } mesh) _lit.Add(mesh);
        foreach (var child in node.GetChildren()) Collect(child);
    }

    /// <summary>
    /// Сам проход невидим и только метит пиксели цели в стенсиле; обводку рисует
    /// встроенный в Godot следующий проход вокруг метки. Стенсил, а не вывернутая
    /// оболочка: дверь стоит заподлицо со стеной, и стена закрывала оболочку.
    /// </summary>
    private static StandardMaterial3D Outline => _outline ??= CreateOutline();

    private static StandardMaterial3D CreateOutline()
    {
        var mark = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1f, 1f, 1f, 0f),
            StencilMode = BaseMaterial3D.StencilModeEnum.Outline,
            StencilColor = new Color(1f, 0.93f, 0.72f, 0.75f),
            StencilOutlineThickness = 0.025f,
            // Метка — на весь силуэт, даже за игроком: иначе кольцо зальёт то, что стоит перед целью.
            NoDepthTest = true,
        };

        // Проход обводки создаёт сам Godot; без проверки глубины её не прячет стена
        // вокруг двери, а на саму цель она не ложится — там метка в стенсиле.
        if (mark.NextPass is BaseMaterial3D ring) ring.NoDepthTest = true;
        return mark;
    }

    internal static void ReleaseCaches() => StaticCaches.Dispose(ref _outline);
}
