using Godot;

namespace Krntija.Game;

/// <summary>
/// Мир кусками (ADR 0002, фаза 2): стример, место машины на дороге и сдвиг сцены
/// при переносе начала координат. Пока включается флагом --world=chunks.
/// </summary>
public partial class Main
{
    /// <summary>Где на дороге начинается игра: перед домом, не на самом краю мира.</summary>
    private const double StartStation = 40;

    private ChunkStreamer? _streamer;

    private bool UseChunkWorld => HasArg("--world=chunks") || _chunkBench || _chunkRun;

    private void BuildChunkWorld()
    {
        WorldOrigin.Reset();

        _streamer = new ChunkStreamer { Name = "Chunks" };
        AddChild(_streamer);
        _streamer.OriginShifted += ShiftScene;
        _streamer.BuildAround(StartStation);

        GD.Print($"[chunks] построено кусков: {_streamer.LoadedChunks}, шагов: {_streamer.StepsBuilt}, "
                 + $"пик шага {_streamer.PeakStepMs:F1} мс");
        _streamer.ResetStats();
    }

    private void PlaceCarOnChunkRoad(double s)
    {
        _car.GlobalPosition = _streamer!.RoadPoint(s) + Vector3.Up * 0.62f;
        _car.Rotation = new Vector3(0, -_streamer.RoadHeading(s), 0);
        _car.LinearVelocity = Vector3.Zero;
        _car.AngularVelocity = Vector3.Zero;
        _car.Path = null;
        _car.PathTarget = position => _streamer.AheadOf(position, 18);
    }

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

        if (_wolvesCenter is { } wolves) _wolvesCenter = wolves - delta;
        if (_housePosition is { } house) _housePosition = house - delta;
        if (_objectivePosition is { } objective) _objectivePosition = objective - delta;
    }
}
