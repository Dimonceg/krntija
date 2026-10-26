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

/// <summary>Процедурные постройки на случай, если GLB-модели места нет: село, мотель, руины, ферма, блокпост, заправка.</summary>
public partial class Main
{
    private void AddVillage(Vector3 position, float heading)
    {
        var village = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };
        AddChild(village);

        var wall = new Color(0.80f, 0.76f, 0.66f);
        var roof = new Color(0.52f, 0.26f, 0.19f);
        var window = new Color(0.15f, 0.18f, 0.21f);
        var door = new Color(0.33f, 0.23f, 0.15f);
        var fence = new Color(0.42f, 0.34f, 0.24f);

        for (int i = 0; i < 3; i++)
        {
            var house = new Node3D { Position = new Vector3(i * 10f - 10f, 0, -i * 7f) };
            village.AddChild(house);

            float width = 8.0f + i * 0.5f;
            float depth = 6.4f;
            float height = 3.4f + i * 0.2f;

            Box(house, new Vector3(width, height, depth), new Vector3(0, height / 2, 0), wall);
            GableRoof(house, height, width, depth, 2.0f, roof);

            Box(house, new Vector3(0.9f, 1.1f, 0.12f), new Vector3(-1.7f, 1.8f, depth / 2), window);
            Box(house, new Vector3(0.9f, 1.1f, 0.12f), new Vector3(1.7f, 1.8f, depth / 2), window);
            Box(house, new Vector3(1.1f, 2.0f, 0.14f), new Vector3(0, 1.0f, depth / 2 + 0.02f), door);

            // труба на крыше
            Box(house, new Vector3(0.5f, 1.3f, 0.5f), new Vector3(width * 0.26f, height + 1.3f, -depth * 0.2f),
                new Color(0.46f, 0.41f, 0.37f));
        }

        // Забор вдоль улицы и столб с указателем села.
        for (int i = 0; i < 11; i++)
            Box(village, new Vector3(0.12f, 1.0f, 0.12f), new Vector3(-16 + i * 3.2f, 0.5f, 3.4f), fence);

        Box(village, new Vector3(32, 0.08f, 0.08f), new Vector3(0, 0.85f, 3.4f), fence);
        Box(village, new Vector3(32, 0.08f, 0.08f), new Vector3(0, 0.45f, 3.4f), fence);

        var sign = Box(village, new Vector3(2.4f, 0.7f, 0.08f), new Vector3(-13f, 2.3f, 3.0f),
            new Color(0.85f, 0.82f, 0.7f));
        sign.Rotation = new Vector3(0, 0.3f, 0);

        AddBusStop(village, new Vector3(3.5f, 0, 7.5f));
        AddMotel(village, new Vector3(-6f, 0, 9.5f));
    }

    /// <summary>Мотель у села: переночевать за плату, проснуться утром.</summary>
    private void AddMotel(Node3D parent, Vector3 position)
    {
        var motel = new Node3D { Position = position, Rotation = new Vector3(0, 0.18f, 0) };
        parent.AddChild(motel);

        var wall = new Color(0.72f, 0.66f, 0.56f);
        var roof = new Color(0.44f, 0.30f, 0.22f);
        var window = new Color(0.15f, 0.18f, 0.21f);

        Box(motel, new Vector3(8.8f, 3.6f, 6.2f), new Vector3(0, 1.8f, 0), wall);
        GableRoof(motel, 3.6f, 8.8f, 6.2f, 1.9f, roof);

        for (int i = -1; i <= 1; i++)
            Box(motel, new Vector3(0.9f, 0.9f, 0.12f), new Vector3(i * 2f, 1.9f, 2.52f), window);

        Box(motel, new Vector3(1.1f, 2f, 0.14f), new Vector3(0, 1f, 2.53f), new Color(0.30f, 0.22f, 0.16f));

        var sign = Box(motel, new Vector3(2.8f, 0.62f, 0.1f), new Vector3(0, 3.35f, 2.45f),
            new Color(0.86f, 0.83f, 0.70f));
        sign.Rotation = new Vector3(0, 0, 0);

        motel.AddChild(new Label3D
        {
            Text = "МОТЕЛЬ",
            FontSize = 56,
            PixelSize = 0.0045f,
            Position = new Vector3(0, 3.35f, 2.52f),
            Modulate = new Color(0.16f, 0.15f, 0.14f),
        });

        _interactions.Add(new InteractionPoint(
            motel.ToGlobal(new Vector3(0, 0, 3.4f)), InteractionKind.Sleep, 4.5f));
    }

    /// <summary>Автобусная остановка: бетонный навес, лавка и табличка.</summary>
    private static void AddBusStop(Node3D parent, Vector3 position)
    {
        var stop = new Node3D { Position = position, Rotation = new Vector3(0, -0.25f, 0) };
        parent.AddChild(stop);

        var concrete = new Color(0.66f, 0.64f, 0.58f);
        var bench = new Color(0.45f, 0.32f, 0.2f);

        Box(stop, new Vector3(5.0f, 0.18f, 2.2f), new Vector3(0, 2.6f, 0), concrete);
        Box(stop, new Vector3(4.4f, 1.3f, 0.14f), new Vector3(0, 1.75f, -0.95f), concrete);
        Box(stop, new Vector3(0.14f, 2.45f, 2.0f), new Vector3(-2.15f, 1.22f, 0), concrete);
        Box(stop, new Vector3(0.14f, 2.45f, 2.0f), new Vector3(2.15f, 1.22f, 0), concrete);

        Box(stop, new Vector3(3.4f, 0.12f, 0.5f), new Vector3(0, 0.55f, -0.5f), bench);
        Box(stop, new Vector3(3.4f, 0.45f, 0.1f), new Vector3(0, 0.82f, -0.72f), bench);

        stop.AddChild(new Label3D
        {
            Text = "АУТОБУС",
            FontSize = 64,
            PixelSize = 0.004f,
            Position = new Vector3(0, 2.05f, -0.88f),
            Modulate = new Color(0.95f, 0.92f, 0.8f),
            OutlineSize = 6,
        });
    }

    private void AddRuins(Vector3 position, float heading)
    {
        var ruins = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };
        AddChild(ruins);

        var stone = new Color(0.61f, 0.58f, 0.51f);
        var beam = new Color(0.29f, 0.22f, 0.16f);

        Box(ruins, new Vector3(10.5f, 3.2f, 0.45f), new Vector3(0, 1.6f, 0), stone);
        Box(ruins, new Vector3(0.45f, 4.2f, 7), new Vector3(4.9f, 2.1f, 0), stone);
        Box(ruins, new Vector3(0.45f, 1.6f, 4), new Vector3(-4.2f, 0.8f, 0), stone);
        Box(ruins, new Vector3(3.4f, 1.0f, 0.45f), new Vector3(-2.4f, 0.5f, 0.1f), stone);

        var collapsed = Box(ruins, new Vector3(5.4f, 0.22f, 0.22f), new Vector3(0.6f, 1.6f, 1.2f), beam);
        collapsed.Rotation = new Vector3(0, 0, 0.38f);

        var collapsed2 = Box(ruins, new Vector3(4.2f, 0.2f, 0.2f), new Vector3(-1.1f, 0.95f, -0.9f), beam);
        collapsed2.Rotation = new Vector3(0, 0, -0.52f);

        AddWreck(ruins, new Vector3(6.4f, 0, 3.4f));
    }

    /// <summary>Брошенная машина у руин: кузов, крыша и колёса, слегка завалена.</summary>
    private static void AddWreck(Node3D parent, Vector3 position)
    {
        var wreck = new Node3D { Position = position, Rotation = new Vector3(0.02f, 0.8f, 0.05f) };
        parent.AddChild(wreck);

        var rust = new Color(0.44f, 0.29f, 0.19f);
        var dark = new Color(0.13f, 0.13f, 0.13f);

        Box(wreck, new Vector3(1.6f, 0.5f, 3.7f), new Vector3(0, 0.45f, 0), rust);
        Box(wreck, new Vector3(1.42f, 0.42f, 1.7f), new Vector3(0, 0.88f, 0.15f), rust);
        Box(wreck, new Vector3(1.36f, 0.28f, 1.5f), new Vector3(0, 0.92f, 0.15f), dark);

        for (int side = -1; side <= 1; side += 2)
        {
            for (int axle = -1; axle <= 1; axle += 2)
            {
                wreck.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { Height = 0.22f, TopRadius = 0.3f, BottomRadius = 0.3f, RadialSegments = 10 },
                    Position = new Vector3(side * 0.78f, 0.3f, axle * 1.3f),
                    Rotation = new Vector3(0, 0, Mathf.Pi / 2),
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.19f, 0.18f), Roughness = 1f },
                });
            }
        }
    }

    private void AddFarm(Vector3 position, float heading)
    {
        var farm = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };
        AddChild(farm);

        var wall = new Color(0.64f, 0.52f, 0.38f);
        var roof = new Color(0.42f, 0.30f, 0.22f);
        var fence = new Color(0.42f, 0.34f, 0.24f);

        Box(farm, new Vector3(12.5f, 4.6f, 8.5f), new Vector3(0, 2.3f, 0), wall);
        GableRoof(farm, 4.6f, 12.5f, 8.5f, 2.7f, roof);
        Box(farm, new Vector3(2.4f, 2.8f, 0.14f), new Vector3(0, 1.4f, 3.55f), new Color(0.29f, 0.21f, 0.15f));

        var silo = new MeshInstance3D
        {
            Mesh = new CylinderMesh { Height = 9, TopRadius = 1.9f, BottomRadius = 2.1f, RadialSegments = 12 },
            Position = new Vector3(8.5f, 4.5f, -2),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.74f, 0.72f, 0.66f), Roughness = 0.85f },
        };
        farm.AddChild(silo);

        farm.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1.95f, Height = 1.6f },
            Position = new Vector3(8.5f, 9.2f, -2),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.5f, 0.48f), Roughness = 0.8f },
        });

        for (int i = 0; i < 6; i++)
            Box(farm, new Vector3(0.14f, 1.1f, 0.14f), new Vector3(-6 + i * 2.4f, 0.55f, 5.2f), fence);

        Box(farm, new Vector3(14, 0.1f, 0.1f), new Vector3(0, 0.95f, 5.2f), fence);
    }

    /// <summary>Двускатная крыша: две наклонённые плиты с коньком по центру.</summary>
    private static void GableRoof(Node3D parent, float baseY, float width, float depth, float height, Color color)
    {
        float half = width / 2 + 0.45f;
        float slant = Mathf.Sqrt(half * half + height * height);
        float angle = Mathf.Atan2(height, half);

        for (int side = -1; side <= 1; side += 2)
        {
            var slab = Box(parent, new Vector3(slant, 0.18f, depth + 0.9f),
                new Vector3(side * half / 2, baseY + height / 2, 0), color);
            slab.Rotation = new Vector3(0, 0, -side * angle);
        }
    }

    private void AddCheckpoint(Vector3 position, float heading, Vector3 roadPoint, int poiIndex, bool buildBooth = true)
    {
        if (buildBooth)
        {
            var checkpoint = new Node3D { Position = position, Rotation = new Vector3(0, -heading, 0) };
            AddChild(checkpoint);

            // Будка смотрителя и козырёк на обочине.
            Box(checkpoint, new Vector3(2.4f, 2.6f, 2.4f), new Vector3(0, 1.3f, 0), new Color(0.72f, 0.7f, 0.6f));
            Box(checkpoint, new Vector3(3.0f, 0.2f, 3.0f), new Vector3(0, 2.7f, 0), new Color(0.4f, 0.38f, 0.34f));
        }

        // Сам шлагбаум стоит поперёк дороги и нужен всегда — это механика.
        var gate = CheckpointGate.Create(roadPoint, heading);
        AddChild(gate);

        _interactions.Add(new InteractionPoint(
            roadPoint + new Vector3(0, 1.2f, 0), InteractionKind.Gate, 9f, gate, StateId: WorldState.Gate(poiIndex)));
    }

    /// <summary>Заправка: навес, колонка и магазинчик у дороги.</summary>
    private void AddGasStation(Vector3 origin, float heading)
    {
        var concrete = new StandardMaterial3D { AlbedoColor = new Color(0.7f, 0.68f, 0.62f), Roughness = 0.9f };
        var red = new StandardMaterial3D { AlbedoColor = new Color(0.66f, 0.22f, 0.18f), Roughness = 0.7f };
        var yellow = new StandardMaterial3D { AlbedoColor = new Color(0.83f, 0.74f, 0.4f), Roughness = 0.6f };

        var station = new Node3D { Position = origin, Rotation = new Vector3(0, -heading, 0) };
        AddChild(station);

        station.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(12, 0.35f, 8) },
            Position = new Vector3(0, 4.3f, 0),
            MaterialOverride = red,
        });

        foreach (float dx in new[] { -5f, 5f })
        foreach (float dz in new[] { -3f, 3f })
        {
            station.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.3f, 4.3f, 0.3f) },
                Position = new Vector3(dx, 2.15f, dz),
                MaterialOverride = concrete,
            });
        }

        // Колонка: визуал + коллизия, чтобы в неё можно было въехать.
        var pumpBody = new StaticBody3D { Position = new Vector3(0, 0.8f, 0) };
        pumpBody.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.8f, 1.6f, 0.6f) } });
        pumpBody.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.8f, 1.6f, 0.6f) },
            MaterialOverride = yellow,
        });
        station.AddChild(pumpBody);

        // Магазин.
        var shop = new StaticBody3D { Position = new Vector3(0, 1.7f, -10) };
        shop.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(10, 3.4f, 6) } });
        shop.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(10, 3.4f, 6) },
            MaterialOverride = concrete,
        });
        station.AddChild(shop);

        AddGasStationInteractions(origin, heading, model: null);
    }

    /// <summary>
    /// Колонка и вход в лавку — считаются от точки POI. Колонка арт-модели — её
    /// обводка, а её коллизия не закрывает точку, стоящую внутри неё.
    /// </summary>
    private void AddGasStationInteractions(Vector3 origin, float heading, Node3D? model)
    {
        Vector3 pump = origin + new Vector3(0, 1f, 0).Rotated(Vector3.Up, -heading);
        Vector3 shop = origin + new Vector3(0, 1.6f, -6.4f).Rotated(Vector3.Up, -heading);

        _interactions.Add(new InteractionPoint(pump, InteractionKind.Pump, 4.5f,
            Visual: model?.FindChild("pump*", true, false) as Node3D));
        _interactions.Add(new InteractionPoint(shop, InteractionKind.Shop, 4.5f));
    }
}
