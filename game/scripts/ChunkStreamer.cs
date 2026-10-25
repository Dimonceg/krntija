using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using Krntija.Core.WorldGen;

namespace Krntija.Game;

/// <summary>
/// Держит построенными куски мира вокруг игрока и сдвигает начало координат
/// (ADR 0002, фаза 2). Постройка разбита на шаги и укладывается в бюджет кадра.
/// </summary>
public partial class ChunkStreamer : Node3D
{
    public const int Behind = 1;
    public const int Ahead = 3;

    /// <summary>Дальше этого от нуля сцены мир сдвигается обратно к нулю.</summary>
    public const float ShiftDistance = 2048f;

    public const double BudgetMs = 4.0;

    private sealed class Chunk
    {
        public required ChunkBuilder Builder;
        public Node3D? Root;
        public int NextStep;
        public bool Silent; // замер --chunk-bench: в игру не регистрируется
        public bool Done => NextStep >= ChunkBuilder.StepCount;
    }

    private readonly Dictionary<int, Chunk> _chunks = new();
    private readonly Stopwatch _clock = new();

    public ulong Seed { get; init; } = 20261025;
    public RoadCurve Curve { get; private set; } = null!;

    /// <summary>Общий материал асфальта всех кусков: погода делает его мокрым.</summary>
    public StandardMaterial3D RoadMaterial { get; } = LandscapeArt.Asphalt();

    /// <summary>Мир сдвинулся на −delta: всё, что держит позиции сцены, сдвигается так же.</summary>
    public event Action<Vector3>? OriginShifted;

    /// <summary>Кусок достроен: можно регистрировать его места, лут и точки.</summary>
    public event Action<int, Node3D, ChunkBuilder>? ChunkReady;

    /// <summary>Кусок сейчас выгрузится: снять всё, что на него ссылается.</summary>
    public event Action<int>? ChunkUnloading;

    /// <summary>Набор кусков изменился (загрузка, выгрузка): карте пора перерисоваться.</summary>
    public event Action? ChunksChanged;

    public int Shifts { get; private set; }
    public int StepsBuilt { get; private set; }
    public double PeakStepMs { get; private set; }
    public double TotalStepMs { get; private set; }
    public int LoadedChunks => _chunks.Count;
    public int CurrentChunk { get; private set; }

    /// <summary>Кусок построен и не выгружен.</summary>
    public bool IsLoaded(int index) => _chunks.TryGetValue(index, out var chunk) && chunk.Done;

    /// <summary>Отрезок дороги, который сейчас построен полностью.</summary>
    public (double StartS, double EndS) BuiltRange()
    {
        double start = double.MaxValue, end = double.MinValue;
        foreach (var chunk in _chunks.Values)
        {
            if (!chunk.Done) continue;
            start = Math.Min(start, chunk.Builder.Plan.StartS);
            end = Math.Max(end, chunk.Builder.Plan.EndS);
        }

        return start <= end ? (start, end) : (0, 0);
    }

    public override void _Ready()
    {
        Curve = WorldGenerator.Curve(Seed);
        // Слияние статичной геометрии (StaticBatcher) кусков не касается: их выгружают.
        AddToGroup(StaticBatcher.SkipGroup);
        PlaceBuilder.Preload();
    }

    // ------------------------------------------------------------ дорога в координатах сцены

    public Vector3 RoadPoint(double s, double lateral = 0)
    {
        var (x, y, z) = Curve.Offset(s, lateral);
        return WorldOrigin.ToScene(x, y, z);
    }

    public float RoadHeading(double s) => (float)Curve.Heading(s);

    /// <summary>Станция дороги, на сечении которой стоит точка сцены.</summary>
    public double StationAt(Vector3 scene)
    {
        var (x, z) = WorldOrigin.ToWorld(scene);
        return Curve.Project(x, z).S;
    }

    /// <summary>Точка на оси дороги впереди точки сцены — для автопилота.</summary>
    public Vector3 AheadOf(Vector3 scene, double meters) => RoadPoint(StationAt(scene) + meters);

    // ------------------------------------------------------------ подгрузка

    /// <summary>Построить всё вокруг станции сразу, без бюджета: старт и загрузка сейва.</summary>
    public void BuildAround(double s)
    {
        Plan(s);
        foreach (var chunk in _chunks.Values)
            while (!chunk.Done) Step(chunk);
    }

    /// <summary>
    /// Перенестись в точку мира (загрузка сейва, новая игра далеко от старта):
    /// выгрузить все куски, поставить начало координат в эту точку и построить
    /// куски вокруг неё сразу. Подписчики <see cref="OriginShifted"/> двигают своё.
    /// </summary>
    public void Relocate(double x, double z)
    {
        foreach (var (index, chunk) in _chunks)
        {
            if (chunk.Done) ChunkUnloading?.Invoke(index);
            chunk.Root?.QueueFree();
        }

        _chunks.Clear();

        Vector3 scene = WorldOrigin.ToScene(x, 0, z);
        Shift(new Vector3(scene.X, 0, scene.Z));
        BuildAround(Curve.Project(x, z).S);
    }

    /// <summary>Каждый кадр: сдвиг начала, нужные куски, постройка в пределах бюджета.</summary>
    public void Focus(Vector3 scene)
    {
        if (Mathf.Abs(scene.X) > ShiftDistance || Mathf.Abs(scene.Z) > ShiftDistance)
            Shift(new Vector3(scene.X, 0, scene.Z));

        Plan(StationAt(scene));

        _clock.Restart();
        bool stepped = false;
        // Ближние куски первыми: до них игрок доберётся раньше. Хотя бы один шаг
        // за кадр делаем всегда, иначе на медленной машине мир не догонит игрока.
        for (int offset = 0; offset <= Ahead + Behind; offset++)
        {
            foreach (int index in new[] { CurrentChunk + offset, CurrentChunk - offset })
            {
                if (!_chunks.TryGetValue(index, out var chunk)) continue;
                while (!chunk.Done)
                {
                    if (stepped && _clock.Elapsed.TotalMilliseconds >= BudgetMs) return;
                    Step(chunk);
                    stepped = true;
                }
            }
        }
    }

    private void Plan(double s)
    {
        CurrentChunk = Math.Max(0, (int)Math.Floor(s / WorldGenerator.ChunkLength));

        for (int index = Math.Max(0, CurrentChunk - Behind); index <= CurrentChunk + Ahead; index++)
        {
            if (_chunks.ContainsKey(index)) continue;
            var plan = WorldGenerator.Plan(Seed, index);
            _chunks[index] = new Chunk { Builder = new ChunkBuilder(Curve, Seed, plan) };
        }

        // Выгрузка с запасом в один кусок: не строить заново, если игрок топчется на стыке.
        var stale = new List<int>();
        foreach (int index in _chunks.Keys)
            if (index < CurrentChunk - Behind - 1 || index > CurrentChunk + Ahead + 1)
                stale.Add(index);

        foreach (int index in stale)
        {
            if (_chunks[index].Done) ChunkUnloading?.Invoke(index);
            _chunks[index].Root?.QueueFree();
            _chunks.Remove(index);
        }

        if (stale.Count > 0) ChunksChanged?.Invoke();
    }

    private void Step(Chunk chunk)
    {
        long started = Stopwatch.GetTimestamp();

        if (chunk.Root is null)
        {
            chunk.Root = chunk.Builder.CreateRoot();
            AddChild(chunk.Root);
        }

        chunk.Builder.BuildStep(chunk.NextStep, chunk.Root, RoadMaterial);
        chunk.NextStep++;

        if (chunk.Done && !chunk.Silent)
        {
            ChunkReady?.Invoke(chunk.Builder.Plan.Index, chunk.Root, chunk.Builder);
            ChunksChanged?.Invoke();
        }

        double ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        StepsBuilt++;
        TotalStepMs += ms;
        PeakStepMs = Math.Max(PeakStepMs, ms);
    }

    private void Shift(Vector3 delta)
    {
        WorldOrigin.Shift(delta.X, delta.Z);
        foreach (var chunk in _chunks.Values)
            if (chunk.Root is not null) chunk.Root.Position -= delta;

        Shifts++;
        OriginShifted?.Invoke(delta);
    }

    /// <summary>Сбросить счётчики после стартовой постройки: там грузятся модели.</summary>
    public void ResetStats()
    {
        StepsBuilt = 0;
        PeakStepMs = 0;
        TotalStepMs = 0;
    }

    /// <summary>Замер для --chunk-bench: постройка куска по шагам, мс на каждый шаг.</summary>
    public double[] Bench(int index)
    {
        var chunk = new Chunk { Builder = new ChunkBuilder(Curve, Seed, WorldGenerator.Plan(Seed, index)), Silent = true };
        var times = new double[ChunkBuilder.StepCount];
        for (int step = 0; step < ChunkBuilder.StepCount; step++)
        {
            long started = Stopwatch.GetTimestamp();
            Step(chunk);
            times[step] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }

        chunk.Root?.QueueFree();
        return times;
    }
}
