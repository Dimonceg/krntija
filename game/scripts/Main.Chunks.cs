using System.Collections.Generic;
using Godot;
using Krntija.Core;
using Krntija.Core.World;
using Krntija.Core.WorldGen;

namespace Krntija.Game;

/// <summary>
/// Мир кусками (ADR 0002, фаза 2): стример, место машины на дороге, места и лут
/// из плана, карта и сдвиг сцены при переносе начала координат.
/// Пока включается флагом --world=chunks.
/// </summary>
public partial class Main
{
    /// <summary>Где на дороге начинается игра: у края мира, дом (s = 24) впереди слева.</summary>
    private const double StartStation = 8;

    /// <summary>Место из плана в сцене: для награды за посещение и для карты.</summary>
    private sealed record ChunkPlace(PlacePlan Plan, Vector3 Position, int Chunk);

    private ChunkStreamer? _streamer;
    private readonly List<ChunkPlace> _chunkPlaces = new();

    /// <summary>Мир кусками — по умолчанию; старый мир на 2,5 км — до конца фазы 3.</summary>
    private bool UseChunkWorld => !HasArg("--world=legacy");

    private string WorldKind => _streamer is null ? SaveWorld.Legacy : SaveWorld.Chunks;

    private void BuildChunkWorld()
    {
        WorldOrigin.Reset();

        _streamer = new ChunkStreamer { Name = "Chunks" };
        AddChild(_streamer);
        _streamer.OriginShifted += ShiftScene;
        _streamer.ChunkReady += OnChunkReady;
        _streamer.ChunkUnloading += OnChunkUnloading;
        _streamer.ChunksChanged += RefreshChunkMap;
        _streamer.BuildAround(StartStation);

        GD.Print($"[chunks] построено кусков: {_streamer.LoadedChunks}, шагов: {_streamer.StepsBuilt}, "
                 + $"пик шага {_streamer.PeakStepMs:F1} мс; мест {_chunkPlaces.Count}, точек {_interactions.Count}");
        _streamer.ResetStats();
    }

    /// <summary>Поставить машину на дорогу; если вокруг станции куски не построены — перенестись.</summary>
    private void PlaceCarOnChunkRoad(double s)
    {
        if (!_streamer!.IsLoaded((int)(s / WorldGenerator.ChunkLength)))
        {
            var (x, _, z) = _streamer.Curve.Point(s);
            _streamer.Relocate(x, z);
        }

        _car.GlobalPosition = _streamer!.RoadPoint(s) + Vector3.Up * 0.62f;
        _car.Rotation = new Vector3(0, -_streamer.RoadHeading(s), 0);
        _car.LinearVelocity = Vector3.Zero;
        _car.AngularVelocity = Vector3.Zero;
        SteerAlongChunkRoad();
    }

    /// <summary>Автопилот (проверки) едет по кривой: нужно и после пересборки машины.</summary>
    private void SteerAlongChunkRoad()
    {
        _car.Path = null;
        _car.PathTarget = position => _streamer!.AheadOf(position, 18);
    }

    /// <summary>Машина из сейва в мире кусками: перенестись к ней и поставить её туда же.</summary>
    private void PlaceCarFromChunkSave(SaveData data)
    {
        _streamer!.Relocate(data.CarX, data.CarZ);
        _car.GlobalPosition = WorldOrigin.ToScene(data.CarX, data.CarY, data.CarZ);
        _car.Rotation = new Vector3(0, (float)data.CarYaw, 0);
        _car.LinearVelocity = Vector3.Zero;
        _car.AngularVelocity = Vector3.Zero;
        SteerAlongChunkRoad();
    }

    // ------------------------------------------------------------ места из плана

    /// <summary>
    /// Кусок достроен: зарегистрировать его места, точки и лут. Стартовые куски
    /// строятся до машины и сессии — для них уже сделанное убирает
    /// <see cref="SyncWorldState"/>, для остальных проверяем здесь.
    /// </summary>
    private void OnChunkReady(int index, Node3D root, ChunkBuilder builder)
    {
        WorldState? world = _car?.Session.WorldState;

        foreach (var place in builder.Places)
        {
            _chunkPlaces.Add(new ChunkPlace(place.Plan, root.ToGlobal(place.Local), index));
            if (place.Plan.Kind == PlaceKind.House)
            {
                _housePosition = root.ToGlobal(place.Local);
                _houseNode = root.FindChild("PlayerHouse", true, false) as Node3D;
                if (_houseNode is not null) _houseGroundY = _houseNode.GlobalPosition.Y;
            }

            foreach (var use in place.Uses)
            {
                if (UseKind(use.Kind) is not { } mapped) continue;

                _interactions.Add(new InteractionPoint(root.ToGlobal(use.Local), mapped.Kind, mapped.Radius,
                    Gate: use.Gate, Door: use.Door, StateId: use.StateId, Chunk: index));

                if (use.Gate is { } gate && use.StateId is { } id && world?.IsDone(id) == true) gate.Open();
            }

            foreach (var loot in place.Loot)
            {
                _lootSpawns.Add(new LootSpawn(loot.StateId, root.ToGlobal(loot.Local), loot.ItemId, loot.Count,
                    index, root));
                if (world?.IsDone(loot.StateId) != true) PlaceLoot(_lootSpawns[^1]);
            }
        }
    }

    /// <summary>Кусок выгружается: его узлы освободит стример, здесь — ссылки на них.</summary>
    private void OnChunkUnloading(int index)
    {
        _interactions.RemoveAll(point => point.Chunk == index);
        _lootSpawns.RemoveAll(spawn => spawn.Chunk == index);

        foreach (var place in _chunkPlaces)
            if (place.Chunk == index && place.Plan.Kind == PlaceKind.House)
            {
                _housePosition = null;
                _houseNode = null;
            }
        _chunkPlaces.RemoveAll(place => place.Chunk == index);

        ClearHover(); // наведённый предмет мог лежать в этом куске
    }

    /// <summary>Вид точки из плана → действие и радиус подсказки.</summary>
    private static (InteractionKind Kind, float Radius)? UseKind(string kind) => kind switch
    {
        "pump" => (InteractionKind.Pump, 4.5f),
        "shop" => (InteractionKind.Shop, 4.5f),
        "sell" => (InteractionKind.SellAll, 5f),
        "sleep" => (InteractionKind.Sleep, 4.5f),
        "gate" => (InteractionKind.Gate, 9f),
        "drink" => (InteractionKind.Drink, 3.5f),
        "door" => (InteractionKind.Door, 3.2f),
        "chest" => (InteractionKind.Chest, 3f),
        "workbench" => (InteractionKind.Workbench, 3f),
        "sleep_home" => (InteractionKind.SleepHome, 3f),
        _ => null,
    };

    /// <summary>Первое посещение заметного места — награда, один раз за игру.</summary>
    private void RewardChunkPlaces()
    {
        foreach (var place in _chunkPlaces)
        {
            if (_car.GlobalPosition.DistanceTo(place.Position) > 25f) continue;
            if (!WorldActions.TryRewardPlace(_car.Session, place.Plan)) continue;
            _hud.Toast($"{PlaceTitle(place.Plan.Kind)}: +{Core.Economy.Money.Format(WorldActions.PlaceReward)}");
        }
    }

    private static string PlaceTitle(PlaceKind kind) => kind switch
    {
        PlaceKind.GasStation => "Заправка",
        PlaceKind.Village => "Село",
        PlaceKind.Checkpoint => "Блокпост",
        PlaceKind.Ruins => "Руины",
        PlaceKind.Farm => "Ферма",
        _ => "Место",
    };

    // ------------------------------------------------------------ карта

    /// <summary>Карта по построенному отрезку дороги: ось из кривой, точки мест из плана.</summary>
    private void RefreshChunkMap()
    {
        if (_streamer is null || _mapUi is null || _miniMap is null) return;

        var (start, end) = _streamer.BuiltRange();
        var road = new List<Vector3>();
        for (double s = start; s <= end; s += 8) road.Add(_streamer.RoadPoint(s));

        var places = new List<MapPlace>();
        foreach (var place in _chunkPlaces)
            places.Add(new MapPlace(places.Count, Mathf.Sign((float)place.Plan.Lateral), place.Position,
                place.Plan.Kind.ToString().ToLowerInvariant(), PlaceTitle(place.Plan.Kind)));

        var none = new List<List<Vector3>>();
        _mapUi.SetData(road, none, places, new List<int>());
        _miniMap.SetData(road, none, places, new List<int>());
    }

    // ------------------------------------------------------------ сдвиг начала

    /// <summary>
    /// Мир сдвинулся на −delta. Куски стример двигает сам; здесь — всё остальное,
    /// что хранит позиции сцены. Новое поле с позицией в Main — добавить сюда.
    /// </summary>
    private void ShiftScene(Vector3 delta)
    {
        _car.GlobalPosition -= delta;
        if (_player is not null) _player.GlobalPosition -= delta;
        _camera.GlobalPosition -= delta;

        for (int i = 0; i < _interactions.Count; i++)
            _interactions[i] = _interactions[i] with { Position = _interactions[i].Position - delta };

        for (int i = 0; i < _lootSpawns.Count; i++)
            _lootSpawns[i] = _lootSpawns[i] with { Position = _lootSpawns[i].Position - delta };

        for (int i = 0; i < _chunkPlaces.Count; i++)
            _chunkPlaces[i] = _chunkPlaces[i] with { Position = _chunkPlaces[i].Position - delta };

        if (_wolvesCenter is { } wolves) _wolvesCenter = wolves - delta;
        if (_housePosition is { } house) _housePosition = house - delta;
        if (_objectivePosition is { } objective) _objectivePosition = objective - delta;

        RefreshChunkMap();
    }
}
