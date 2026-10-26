using Godot;
using Krntija.Core;

namespace Krntija.Game;

/// <summary>
/// Что видит игрок для клавиши E: цель за стеной или закрытой дверью не выбирается
/// (баг «у двери подсказка поднять хлеб за ней»), выбранная цель обводится.
/// </summary>
public partial class Main
{
    /// <summary>Препятствие ближе этого к точке её не закрывает: точка лежит на столе, в шкафу, на полке.</summary>
    private const float SightSlack = 0.3f;

    private readonly InteractionHighlight _highlight = new();

    /// <summary>Подсказка и обводка каждый кадр — от одного выбора цели.</summary>
    private void UpdateFocus()
    {
        var target = SelectTarget();
        _hud.Prompt = BuildPrompt(target);

        // С деталью в руках обводки нет: E про деталь, её место показывает магнит.
        _highlight.Show(_held is not null ? null : target.Kind switch
        {
            TargetKind.Point => target.Point!.Body,
            TargetKind.LoosePart => target.Body,
            TargetKind.CarPart => _car.Assembly?.PartNode(PartSlotIds.Of(target.Slot!.Value)),
            _ => null,
        });
    }

    /// <summary>
    /// Луч от глаз до точки. Закрывает всё, кроме самой цели (её модель и коллизия под
    /// <see cref="InteractionPoint.Body"/>), игрока и машины; закрытая чужая дверь
    /// закрывает всегда, даже если точка сразу за ней.
    /// </summary>
    private bool InSight(Vector3 eye, InteractionPoint point) => SightBlocker(eye, point) is null;

    /// <summary>Что закрывает точку от глаз; null — видно.</summary>
    private Node? SightBlocker(Vector3 eye, InteractionPoint point)
    {
        var exclude = new Godot.Collections.Array<Rid> { _car.GetRid() };
        if (_onFoot && _player is not null) exclude.Add(_player.GetRid());

        var query = new PhysicsRayQueryParameters3D
        {
            From = eye,
            To = SightTarget(point),
            CollisionMask = 1,
            Exclude = exclude,
        };

        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return null;

        var collider = PhysicsHit.Collider<Node>(hit);
        if (collider is null) return null;
        if (point.Body is { } body && (body == collider || body.IsAncestorOf(collider))) return null;
        if (DoorOf(collider) is not null) return collider;

        return PhysicsHit.Position(hit).DistanceTo(SightTarget(point)) <= SightSlack ? null : collider;
    }

    /// <summary>Куда смотреть: у двери точка стоит у порога, рельеф закрывает её — смотрим в полотно.</summary>
    private static Vector3 SightTarget(InteractionPoint point) => point.Door?.Center ?? point.Position;

    /// <summary>Дверь, которой принадлежит коллизия: Door → Hinge → DoorBody.</summary>
    private static Door? DoorOf(Node collider) => collider.GetParent()?.GetParent() as Door;
}
