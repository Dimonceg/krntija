using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Krntija.Core;
using Krntija.Core.Economy;
using Krntija.Core.Items;
using Krntija.Core.Parts;
using Krntija.Core.Vehicle;
using Krntija.Core.Workshop;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>Лут и рюкзак: раскладка предметов, курсор, наведение, подбор, перекладывание.</summary>
public partial class Main
{
    private LootItem? _hoveredLoot;
    private bool _cursorFree;

    /// <summary>Клик в рюкзаке: использовать, переложить в багажник, домой или забрать.</summary>
    private void OnBackpackClick(string itemId, string source)
    {
        var session = _car.Session;
        string name = Core.Items.ItemCatalog.Get(itemId).Name;

        switch (source)
        {
            case "trunk":
            case "home":
                if (!session.Inventory.TryAdd(itemId))
                {
                    _hud.Toast("В кармане нет места");
                    return;
                }

                (source == "trunk" ? session.Trunk : session.Home).Remove(itemId, 1);
                _hud.Toast($"{(source == "trunk" ? "Из багажника" : "Из дома")}: {name}");
                break;

            case "to_home":
                if (!session.Home.TryAdd(itemId))
                {
                    _hud.Toast("Дома нет места");
                    return;
                }

                session.Inventory.Remove(itemId, 1);
                _hud.Toast($"Домой: {name}");
                break;

            default:
                if (Core.Items.ItemUseService.TryUse(session, itemId, out string message))
                {
                    _hud.Toast(message);
                }
                else if (session.Trunk.TryAdd(itemId))
                {
                    session.Inventory.Remove(itemId, 1);
                    _hud.Toast($"В багажник: {name}");
                }
                else
                {
                    _hud.Toast("Багажник полон");
                }

                break;
        }

        RefreshBackpack();
    }

    /// <summary>Рюкзак: открыть/закрыть, заодно отпустить или захватить мышь.</summary>
    private void ToggleBackpack()
    {
        _backpack.Toggle(_car.Session.Inventory, _car.Session.Trunk, AtHome ? _car.Session.Home : null);
        SetCursorFree(_backpack.IsOpen);
    }

    /// <summary>Игрок рядом с домом: тогда в рюкзаке виден склад.</summary>
    private bool AtHome => _housePosition is { } home
                           && _car.GlobalPosition.DistanceTo(home) < 45f;

    private void RefreshBackpack()
    {
        _backpack.Refresh(_car.Session.Inventory, _car.Session.Trunk,
            AtHome ? _car.Session.Home : null);
    }

    private bool _orbitBeforeFree;

    /// <summary>Свободный курсор: обзор не крутится, мышь видна.</summary>
    private void SetCursorFree(bool free)
    {
        if (_cursorFree == free) return;

        _cursorFree = free;
        Input.MouseMode = free ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;

        if (free)
        {
            _orbitBeforeFree = _camera.OrbitWithMouse;
            _camera.OrbitWithMouse = false;
        }
        else
        {
            _camera.OrbitWithMouse = _orbitBeforeFree;
            ClearHover();
        }
    }

    /// <summary>Луч из камеры под курсор: что за предмет, подсветить и запомнить.</summary>
    private void UpdateHover(Vector2 screenPosition)
    {
        var camera = GetViewport().GetCamera3D();
        var space = GetWorld3D()?.DirectSpaceState;
        if (camera is null || space is null) return;

        Vector3 from = camera.ProjectRayOrigin(screenPosition);
        Vector3 to = from + camera.ProjectRayNormal(screenPosition) * 9f;

        var query = new PhysicsRayQueryParameters3D { From = from, To = to, CollisionMask = 2 };
        var hit = space.IntersectRay(query);

        LootItem? found = null;
        if (hit.Count > 0 && hit["collider"].As<GodotObject>() is Node3D body)
            found = body.GetParent() as LootItem;

        if (found == _hoveredLoot) return;

        ClearHover();
        _hoveredLoot = found;

        if (found is not null)
        {
            found.Scale = Vector3.One * 1.45f;
            _hud.Toast($"{Core.Items.ItemCatalog.Get(found.ItemId).Name} — ЛКМ поднять");
        }
    }

    private void ClearHover()
    {
        if (_hoveredLoot is not null) _hoveredLoot.Scale = Vector3.One;
        _hoveredLoot = null;
    }

    private void PickHoveredLoot()
    {
        if (_hoveredLoot is null) return;

        var point = _interactions.FirstOrDefault(p => p.Node == _hoveredLoot);
        if (point is not null) PickUpLoot(point);

        ClearHover();
    }

    /// <summary>Подобрать предмет: в инвентарь, узел убрать, точку взаимодействия снять.</summary>
    private void PickUpLoot(InteractionPoint point)
    {
        if (point.LootId is null || point.Node is null) return;

        int count = point.Node is LootItem item ? item.Count : 1;
        string name = Core.Items.ItemCatalog.Get(point.LootId).Name;

        if (!_car.Session.Inventory.TryAdd(point.LootId, count))
        {
            _hud.Toast("Инвентарь полон");
            return;
        }

        _hud.Toast($"Подобрано: {name} ×{count}");
        Sfx.PlayUi();
        _interactions.Remove(point);
        point.Node.QueueFree();
    }

    /// <summary>Разложить лут у точек интереса и внутри дома.</summary>
    private void SpawnLoot()
    {
        if (_road is null) return;

        int placed = 0;
        foreach (var (position, itemId, count) in LootSpawner.PlanLandmarks(_road.Points))
        {
            AddLoot(position, itemId, count);
            placed++;
        }

        int inHouse = 0;
        if (GetNodeOrNull<Node3D>("PlayerHouse") is { } house)
            foreach (var (position, itemId, count) in LootSpawner.PlanHouse(house))
            {
                AddLoot(position, itemId, count);
                inHouse++;
            }

        // Лут у построек карты: у руин металлолом, у дома — кассеты, у сарая — запчасти.
        int inMap = 0;
        foreach (var place in _mapPlaces)
        {
            string itemId = place.Kind switch
            {
                "ruins" => "scrap_metal",
                "barn" => "spark_plug",
                "house" or "abandoned" => "cassette",
                "well" => "bottle_cap",
                _ => "duct_tape",
            };

            Vector3 spot = place.Position + new Vector3(0, 0.15f, 1.8f);
            AddLoot(spot, itemId, 1 + place.Index % 2);
            inMap++;
        }

        GD.Print($"[loot] предметов: {placed} у мест, {inHouse} в доме, {inMap} на карте");
    }

    private void AddLoot(Vector3 position, string itemId, int count)
    {
        var item = LootItem.Create(itemId, count, position);
        AddChild(item);

        _interactions.Add(new InteractionPoint(position + Vector3.Up * 0.25f, InteractionKind.Loot, 2.2f,
            LootId: itemId, Node: item));
    }
}
