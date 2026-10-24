using Godot;
using Godot.Collections;

namespace Krntija.Game;

/// <summary>
/// Поля результата IntersectRay/IntersectShape. Каждое обращение <c>hit["collider"]</c>
/// создаёт <see cref="Variant"/> со ссылкой на объект; если его не освободить, финализатор
/// срабатывает уже после остановки движка и роняет Godot на выходе (0xC0000005).
/// Поэтому читаем поля только через эти методы.
/// </summary>
public static class PhysicsHit
{
    public static T? Collider<T>(Dictionary hit) where T : GodotObject
    {
        using var value = hit["collider"];
        return value.As<GodotObject>() as T;
    }

    public static Vector3 Position(Dictionary hit)
    {
        using var value = hit["position"];
        return value.AsVector3();
    }
}
