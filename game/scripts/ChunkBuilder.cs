using System;
using System.Collections.Generic;
using Godot;
using Krntija.Core.World;
using Krntija.Core.WorldGen;

namespace Krntija.Game;

/// <summary>
/// Строит кусок мира по плану из ядра (ADR 0002). Всё внутри куска — в его
/// локальных координатах: корень стоит в «якоре» (ось дороги в начале куска),
/// поэтому числа маленькие при любом удалении от старта.
/// </summary>
/// <remarks>
/// Земля — сетка по мировым координатам: строки по оси Z, столбцы смещены вместе
/// с дорогой. Высота — одна функция мира (<see cref="Ground"/>), поэтому соседние
/// куски стыкуются без зазоров, а на крутом повороте земля не складывается.
/// </remarks>
public sealed class ChunkBuilder
{
    /// <summary>Шаги постройки: стример делает по нескольку за кадр в пределах бюджета.</summary>
    public const int StepCount = 9;

    private const double RowStep = 4.0;
    private const float WallOffset = 145f;

    /// <summary>Столбцы коллизии земли: реже, чем у меша. Строки — через одну (8 м).</summary>
    private static readonly float[] CollisionOffsets = Mirror(0f, 6.6f, 12f, 18f, 26f, 36f, 50f, 70f, 95f, 120f, 150f);

    /// <summary>Столбцы ближней земли: смещение по X от оси дороги, гуще у полотна.</summary>
    private static readonly float[] NearOffsets = Mirror(0f, 3.6f, 4.7f, 6.6f, 9.4f, 12f, 18f, 26f, 36f, 50f, 70f, 95f, 120f, 150f);

    /// <summary>Столбцы дальней земли с горами: первый совпадает с краем ближней.</summary>
    private static readonly float[] FarOffsets = { 150f, 175f, 220f, 290f, 380f, 500f, 650f, 850f, 1100f };

    private static FastNoiseLite? _mountainNoise;

    private static FastNoiseLite MountainNoise => _mountainNoise ??= new FastNoiseLite
    {
        Seed = 4201, Frequency = .0032f, FractalOctaves = 4, FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
    };

    private static StandardMaterial3D? _farMaterial;
    private static ShaderMaterial? _groundMaterial;
    private static StandardMaterial3D? _postMaterial;

    /// <summary>Отпустить общие ресурсы перед выходом (<see cref="StaticCaches"/>).</summary>
    internal static void ReleaseCaches()
    {
        StaticCaches.Dispose(ref _mountainNoise);
        StaticCaches.Dispose(ref _farMaterial);
        StaticCaches.Dispose(ref _groundMaterial);
        StaticCaches.Dispose(ref _postMaterial);
    }

    private readonly RoadCurve _curve;
    private readonly ulong _seed;
    private readonly ChunkPlan _plan;
    private readonly double _anchorX;
    private readonly double _anchorZ;
    private readonly (double X, double Z, float Radius)[] _clearings;
    private readonly List<PlaceBuilder.Built> _places = new();

    public ChunkBuilder(RoadCurve curve, ulong seed, ChunkPlan plan)
    {
        _curve = curve;
        _seed = seed;
        _plan = plan;
        _anchorX = curve.Lateral(plan.StartS);
        _anchorZ = -plan.StartS;

        _clearings = new (double, double, float)[plan.Places.Count];
        for (int i = 0; i < plan.Places.Count; i++)
        {
            var (x, z) = PlaceGeometry.Center(plan.Places[i], curve);
            _clearings[i] = (x, z, PlaceBuilder.ClearRadius(plan.Places[i]));
        }
    }

    public ChunkPlan Plan => _plan;

    private PlacePads? _pads;

    /// <summary>Площадки мест этого куска и соседей. Считаются при первом шаге, не в конструкторе:
    /// конструктор зовёт стример вне бюджета кадра.</summary>
    private PlacePads Pads => _pads ??= new PlacePads(_curve, _seed, _plan.Index);

    /// <summary>Высота земли этого куска: рельеф с площадками мест.</summary>
    private (float Height, float Lateral) GroundAt(double x, double z) => Ground(_curve, x, z, pads: Pads);

    /// <summary>Построенные места: точки взаимодействия и лут для регистрации в <see cref="Main"/>.</summary>
    public IReadOnlyList<PlaceBuilder.Built> Places => _places;

    /// <summary>Корень куска в координатах сцены (с учётом плавающего начала).</summary>
    public Node3D CreateRoot() => new()
    {
        Name = $"Chunk_{_plan.Index}",
        Position = WorldOrigin.ToScene(_anchorX, 0, _anchorZ),
    };

    public void BuildStep(int step, Node3D root, StandardMaterial3D roadMaterial)
    {
        switch (step)
        {
            // Площадки мест считаются в лёгком шаге дороги, а не в тяжёлом шаге земли.
            case 0: BuildRoad(root, roadMaterial); BuildWalls(root); _ = Pads; break;
            case 1: BuildNearGround(root, half: 0); break;
            case 2: BuildNearGround(root, half: 1); break;
            case 3: BuildNearCollision(root, half: 0); break;
            case 4: BuildNearCollision(root, half: 1); break;
            case 5: BuildPlaces(root); break;
            case 6: BuildFarGround(root); break;
            case 7: BuildVegetation(root); break;
            case 8: BuildGrass(root); BuildKilometerPosts(root); break;
        }
    }

    // ------------------------------------------------------------ высота земли

    /// <summary>
    /// Высота земли в точке мира. У дороги — по точному сечению полотна
    /// (<see cref="RoadCurve.Project"/>), дальше — холмы и горы.
    /// </summary>
    /// <param name="pads">Площадки мест: под ними земля ровная. Null — рельеф как есть.</param>
    public static (float Height, float Lateral) Ground(RoadCurve curve, double x, double z, bool far = false,
        PlacePads? pads = null)
    {
        var world = new Vector3((float)x, 0, (float)z);
        double s, lateral;

        if (!far)
        {
            (s, lateral) = curve.Project(x, z);
            // На внутренней стороне самого крутого поворота проекция неоднозначна.
            if (Math.Abs(s + z) > 200) far = true;
        }
        else
        {
            s = lateral = 0;
        }

        if (far)
        {
            s = -z;
            lateral = (x - curve.Lateral(s)) * Math.Cos(curve.Heading(s));
        }

        float roadY = (float)curve.Height(s);
        float height = TerrainBuilder.SurfaceHeight(roadY, (float)lateral, world);
        float distance = Mathf.Abs((float)lateral);

        // Под асфальтом земля ниже на 15 см: сетка земли и лента дороги приближают
        // одну поверхность разными треугольниками, и на уклоне в повороте земля
        // иначе прорастала бы сквозь полотно. У края — невысокий бортик.
        const float roadHalf = RoadMeshBuilder.Width * 0.5f;
        height -= 0.15f * (1 - Mathf.SmoothStep(roadHalf + 0.3f, roadHalf + 2.5f, distance));

        if (pads is not null && !far) height = pads.Apply(x, z, height, distance);

        if (distance > 140f)
        {
            float n = MountainNoise.GetNoise2D(world.X, world.Z);
            float ridge = 1 - Mathf.Abs(MountainNoise.GetNoise2D(world.X * 1.7f + 421, world.Z * 1.7f));
            height += Mathf.SmoothStep(140, 380, distance) * (22 + 45 * (n + 1))
                      + Mathf.SmoothStep(270, 850, distance) * (55 + 160 * ridge + 85 * n);
        }

        return (height, (float)lateral);
    }

    // ------------------------------------------------------------ дорога

    private void BuildRoad(Node3D root, StandardMaterial3D material)
    {
        var points = new List<Vector3>();
        var headings = new List<float>();

        // Последняя точка совпадает с первой точкой следующего куска: полотно без щели.
        for (double s = _plan.StartS; s <= _plan.EndS + 0.001; s += RowStep)
        {
            var (x, y, z) = _curve.Point(s);
            points.Add(Local(x, y, z));
            headings.Add((float)_curve.Heading(s));
        }

        // Коллизия на полметра заходит за оба конца: шов не попадает под колесо.
        var collisionPoints = new List<Vector3>(points);
        var collisionHeadings = new List<float>(headings);
        foreach (double s in new[] { _plan.StartS - 0.5, _plan.EndS + 0.5 })
        {
            var (x, y, z) = _curve.Point(s);
            int at = s < _plan.StartS ? 0 : collisionPoints.Count;
            collisionPoints.Insert(at, Local(x, y, z));
            collisionHeadings.Insert(at, (float)_curve.Heading(s));
        }

        int firstIndex = (int)Math.Round(_plan.StartS / RowStep);
        RoadMeshBuilder.BuildStrip(points, headings, root, material, firstIndex, collisionPoints, collisionHeadings);

        if (_plan.Index == 0) BuildStartBarrier(root);
    }

    /// <summary>Отбойник за домом: дальше назад мира нет.</summary>
    private void BuildStartBarrier(Node3D root)
    {
        const double s = 2.0;
        var (x, y, z) = _curve.Point(s);
        float heading = (float)_curve.Heading(s);

        var barrier = new Node3D { Name = "Boundary_Начало", Position = Local(x, y, z), Rotation = new Vector3(0, -heading, 0) };
        root.AddChild(barrier);

        var concrete = new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.6f, 0.56f), Roughness = 0.95f };
        barrier.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(26f, 1.15f, 0.7f) },
            Position = new Vector3(0, 0.58f, 0),
            MaterialOverride = concrete,
        });

        var body = new StaticBody3D { Name = "Boundary" };
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(2 * WallOffset, 6f, 1.5f) },
            Position = new Vector3(0, 2.5f, 0.9f),
        });
        barrier.AddChild(body);
    }

    /// <summary>Невидимые стены по краям полосы: за ними мир не построен.</summary>
    private void BuildWalls(Node3D root)
    {
        var faces = new List<Vector3>();
        for (double s = _plan.StartS; s < _plan.EndS - 0.001; s += RowStep * 4)
        {
            double next = Math.Min(s + RowStep * 4, _plan.EndS);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 a = Local(_curve.Lateral(s) + side * WallOffset, _curve.Height(s) - 20, -s);
                Vector3 b = Local(_curve.Lateral(next) + side * WallOffset, _curve.Height(next) - 20, -next);
                Vector3 up = Vector3.Up * 60f;
                faces.AddRange(new[] { a, b, a + up, a + up, b, b + up });
            }
        }

        var body = new StaticBody3D { Name = "ChunkWalls" };
        body.AddChild(new CollisionShape3D
        {
            Shape = new ConcavePolygonShape3D { Data = faces.ToArray(), BackfaceCollision = true },
        });
        root.AddChild(body);
    }

    // ------------------------------------------------------------ земля

    /// <summary>
    /// Ближняя земля — двумя шагами по половине строк: со столбцами у площадок мест
    /// целый кусок не укладывается в бюджет кадра. Средняя строка общая, считается
    /// той же функцией в обеих половинах — шва нет.
    /// </summary>
    private void BuildNearGround(Node3D root, int half)
    {
        int allRows = (int)Math.Round((_plan.EndS - _plan.StartS) / RowStep) + 1;
        int middle = allRows / 2;
        int firstRow = half == 0 ? 0 : middle;
        int rows = half == 0 ? middle + 1 : allRows - middle;

        float[] offsets = Pads.Columns(NearOffsets, _plan.StartS, _plan.EndS);
        int columns = offsets.Length;
        var vertices = new Vector3[rows * columns];
        var laterals = new float[rows * columns];
        var colors = new Color[rows * columns];

        for (int r = 0; r < rows; r++)
        {
            double s = _plan.StartS + (firstRow + r) * RowStep;
            double axis = _curve.Lateral(s);
            for (int c = 0; c < columns; c++)
            {
                double x = axis + offsets[c];
                double z = -s;
                var (height, lateral) = GroundAt(x, z);
                int i = r * columns + c;
                vertices[i] = Local(x, height, z);
                laterals[i] = lateral;
                colors[i] = TerrainBuilder.GroundColor(lateral, new Vector3((float)x, height, (float)z));
            }
        }

        // Шов с соседним куском — только у крайних строк куска, не у средней.
        SealSeams(vertices, rows, offsets, NearOffsets, first: half == 0, last: half == 1);

        var (mesh, _) = Grid(vertices, rows, columns, colors, laterals);
        root.AddChild(new MeshInstance3D
        {
            Name = $"LandscapeGround{half}",
            Mesh = mesh,
            MaterialOverride = _groundMaterial ??= LandscapeArt.GroundMaterial(),
        });
    }

    /// <summary>
    /// Коллизия земли — отдельным шагом и реже меша (строки 8 м, 21 столбец): форма из
    /// тысяч треугольников строится дольше бюджета кадра. У дороги земля ровная, там
    /// совпадение точное; на холмах расхождение с картинкой — сантиметры.
    /// Двумя шагами по половине строк, как и меш: со столбцами у площадок мест иначе
    /// шаг выходил за бюджет кадра.
    /// </summary>
    private void BuildNearCollision(Node3D root, int half)
    {
        int allRows = (int)Math.Round((_plan.EndS - _plan.StartS) / (RowStep * 2)) + 1;
        int middle = allRows / 2;
        int firstRow = half == 0 ? 0 : middle;
        int rows = half == 0 ? middle + 1 : allRows - middle;
        float[] offsets = Pads.Columns(CollisionOffsets, _plan.StartS, _plan.EndS, blendColumns: false);
        int columns = offsets.Length;
        var vertices = new Vector3[rows * columns];

        for (int r = 0; r < rows; r++)
        {
            double s = _plan.StartS + (firstRow + r) * RowStep * 2;
            double axis = _curve.Lateral(s);
            for (int c = 0; c < columns; c++)
            {
                double x = axis + offsets[c];
                var (height, _) = GroundAt(x, -s);
                vertices[r * columns + c] = Local(x, height, -s);
            }
        }

        SealSeams(vertices, rows, offsets, CollisionOffsets, first: half == 0, last: half == 1);

        var faces = new Vector3[(rows - 1) * (columns - 1) * 6];
        int k = 0;
        for (int r = 0; r < rows - 1; r++)
        for (int c = 0; c < columns - 1; c++)
        {
            int a = r * columns + c, b = a + 1, d = a + columns, e = d + 1;
            foreach (int index in new[] { a, d, b, b, d, e }) faces[k++] = vertices[index];
        }

        var body = new StaticBody3D { Name = $"LandscapeGroundCollision{half}" };
        body.AddChild(new CollisionShape3D
        {
            // Двусторонняя коллизия: иначе, заехав в склон, машина проваливается.
            Shape = new ConcavePolygonShape3D { Data = faces, BackfaceCollision = true },
        });
        root.AddChild(body);
    }

    private void BuildFarGround(Node3D root)
    {
        int rows = (int)Math.Round((_plan.EndS - _plan.StartS) / RowStep) + 1;
        int columns = FarOffsets.Length;

        for (int side = -1; side <= 1; side += 2)
        {
            var vertices = new Vector3[rows * columns];
            var colors = new Color[rows * columns];
            var laterals = new float[rows * columns];

            for (int r = 0; r < rows; r++)
            {
                double s = _plan.StartS + r * RowStep;
                double axis = _curve.Lateral(s);
                for (int c = 0; c < columns; c++)
                {
                    double x = axis + side * FarOffsets[c];
                    double z = -s;
                    // Первый столбец — край ближней земли: та же функция, шва нет.
                    var (height, lateral) = Ground(_curve, x, z, far: c > 0);
                    int i = r * columns + c;
                    vertices[i] = Local(x, height, z);
                    laterals[i] = lateral;
                    colors[i] = new Color(.24f, .31f, .16f);
                }
            }

            // Цвет по крутизне: склоны каменные, дальние полосы синеют от дымки.
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++)
            {
                int i = r * columns + c;
                var across = vertices[r * columns + Math.Min(c + 1, columns - 1)] - vertices[r * columns + Math.Max(c - 1, 0)];
                var along = vertices[Math.Min(r + 1, rows - 1) * columns + c] - vertices[Math.Max(r - 1, 0) * columns + c];
                float up = Mathf.Abs(across.Cross(along).Normalized().Y);
                float rock = Mathf.Clamp((1 - up) * 2.6f + c * .075f, 0, .85f);
                colors[i] = new Color(.24f, .31f, .16f).Lerp(new Color(.43f, .42f, .36f), rock)
                    .Lerp(new Color(.35f, .40f, .43f), Mathf.Clamp((c - 3) * .12f, 0, .45f));
            }

            var (mesh, _) = Grid(vertices, rows, columns, colors, laterals);
            root.AddChild(new MeshInstance3D
            {
                Name = side < 0 ? "MountainsWest" : "MountainsEast",
                Mesh = mesh,
                MaterialOverride = _farMaterial ??= new StandardMaterial3D
                {
                    VertexColorUseAsAlbedo = true,
                    Roughness = 1,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
    }

    /// <summary>Сетка строк × столбцов → меш с нормалями и треугольники для коллизии.</summary>
    private static (ArrayMesh Mesh, Vector3[] Faces) Grid(Vector3[] vertices, int rows, int columns,
        Color[] colors, float[] laterals)
    {
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < columns; c++)
        {
            int i = r * columns + c;
            var across = vertices[r * columns + Math.Min(c + 1, columns - 1)] - vertices[r * columns + Math.Max(c - 1, 0)];
            var along = vertices[Math.Min(r + 1, rows - 1) * columns + c] - vertices[Math.Max(r - 1, 0) * columns + c];
            var normal = across.Cross(along).Normalized();
            normals[i] = normal.Y < 0 ? -normal : normal;
            uvs[i] = new Vector2(laterals[i], 0);
        }

        var indices = new int[(rows - 1) * (columns - 1) * 6];
        var faces = new Vector3[indices.Length];
        int k = 0;
        for (int r = 0; r < rows - 1; r++)
        for (int c = 0; c < columns - 1; c++)
        {
            int a = r * columns + c, b = a + 1, d = a + columns, e = d + 1;
            foreach (int index in new[] { a, d, b, b, d, e })
            {
                indices[k] = index;
                faces[k] = vertices[index];
                k++;
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return (mesh, faces);
    }

    // ------------------------------------------------------------ растительность

    private void BuildVegetation(Node3D root)
    {
        if (!WorldVegetation.TryLoadAssets(out _, out _)) return;

        var rng = new Pcg32(Hash.Mix(_seed, _plan.Index), sequence: 99);
        var groups = new List<WorldVegetation.Spot>[WorldVegetation.Ids.Length];
        for (int i = 0; i < groups.Length; i++) groups[i] = new List<WorldVegetation.Spot>();

        for (double s = _plan.StartS + 2; s < _plan.EndS; s += RowStep * 2)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                // Рощи и прогалы, а не деревья через равные промежутки.
                double density = 0.55 + 0.25 * Math.Sin(s / RowStep * 0.035 + side * 2);
                if (rng.Chance(density))
                {
                    var spot = Spot(s, rng.Range(13, 47) * side, -0.08f);
                    int kind = rng.Chance(0.56) ? 0 : rng.Chance(0.5) ? 1 : 2;
                    float tint = (float)rng.Range(0.83, 1.12);
                    var tree = new WorldVegetation.Spot(spot, (float)rng.Range(0.75, 1.35),
                        (float)rng.Range(0, Mathf.Tau), new Color(tint, tint, tint * 0.94f, 1));
                    if (!InClearing(spot.X + _anchorX, spot.Z + _anchorZ)) groups[kind].Add(tree);
                }

                if (rng.Chance(0.42))
                {
                    var bush = new WorldVegetation.Spot(Spot(s, rng.Range(8, 25) * side, -0.05f),
                        (float)rng.Range(0.55, 1.20), (float)rng.Range(0, Mathf.Tau), Colors.White);
                    if (!InClearing(bush.Position.X + _anchorX, bush.Position.Z + _anchorZ)) groups[3].Add(bush);
                }
            }
        }

        WorldVegetation.AddBatches(root, "Vegetation", groups);

        // Дворовый декор у мест — в этом же лёгком шаге (план — в ядре, свой поток ГПСЧ).
        YardDecor.Build(root, _curve, _seed, _plan, (x, z) => GroundAt(x, z).Height, Local);
    }

    private void BuildGrass(Node3D root)
    {
        var rng = new Pcg32(Hash.Mix(_seed, _plan.Index), sequence: 13);
        var positions = new List<Transform3D>();

        for (int n = 0; n < 900; n++)
        {
            double s = rng.Range(_plan.StartS, _plan.EndS);
            double lateral = rng.Range(5.8, 29) * (rng.Chance(0.5) ? 1 : -1);
            var (x, _, z) = _curve.Offset(s, lateral);
            if (Mathf.Sin((float)x * .25f) * Mathf.Cos((float)z * .18f) < -.35f) continue;
            if (InClearing(x, z)) continue;

            var (height, _) = GroundAt(x, z);
            var scale = new Vector3((float)rng.Range(.7, 1.5), (float)rng.Range(.55, 1.3), 1);
            positions.Add(new Transform3D(new Basis(Vector3.Up, (float)rng.Range(0, Mathf.Tau)).Scaled(scale),
                Local(x, height - .035f, z)));
        }

        if (positions.Count > 0) LandscapeArt.AddGrassBatch(root, "RoadGrass", positions, rng);
    }

    /// <summary>Километровые столбики каждые 200 м: по ним видно, сколько проехал.</summary>
    private void BuildKilometerPosts(Node3D root)
    {
        const double every = 200;
        double first = Math.Ceiling(_plan.StartS / every) * every;
        for (double s = Math.Max(first, every); s < _plan.EndS; s += every)
        {
            var (x, y, z) = _curve.Offset(s, RoadMeshBuilder.Width * 0.5 + 1.1);
            Vector3 position = Local(x, y, z);

            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.18f, 1.1f, 0.18f) },
                Position = position + Vector3.Up * 0.55f,
                MaterialOverride = _postMaterial ??= new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.88f, 0.87f, 0.82f), Roughness = 0.9f,
                },
            });

            root.AddChild(new Label3D
            {
                Text = $"{s / 1000.0:F1}",
                FontSize = 72,
                PixelSize = 0.0032f,
                Position = position + Vector3.Up * 1.25f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Modulate = new Color(0.95f, 0.94f, 0.88f),
                OutlineSize = 6,
            });
        }
    }

    // ------------------------------------------------------------ места

    private void BuildPlaces(Node3D root)
    {
        foreach (var place in _plan.Places)
            _places.Add(PlaceBuilder.Build(_curve, place, root, Local, (x, z) => GroundAt(x, z).Height));
    }

    /// <summary>Точка внутри места: там не растут деревья и трава.</summary>
    private bool InClearing(double x, double z)
    {
        foreach (var (cx, cz, radius) in _clearings)
            if ((x - cx) * (x - cx) + (z - cz) * (z - cz) < radius * radius)
                return true;
        return false;
    }

    /// <summary>
    /// Шов с соседним куском: у соседа на крайней строке только базовые столбцы.
    /// Добавленные столбцы на крайних строках кладём на прямую между базовыми —
    /// иначе между кусками была бы щель (Т-образный стык).
    /// </summary>
    private static void SealSeams(Vector3[] vertices, int rows, float[] offsets, float[] baseOffsets,
        bool first = true, bool last = true)
    {
        int columns = offsets.Length;
        if (columns == baseOffsets.Length) return;

        foreach (int r in new[] { 0, rows - 1 })
        {
            if ((r == 0 && !first) || (r == rows - 1 && !last)) continue;

            int left = -1;
            for (int c = 0; c < columns; c++)
            {
                if (Array.IndexOf(baseOffsets, offsets[c]) < 0) continue;
                if (left >= 0 && c - left > 1)
                {
                    Vector3 a = vertices[r * columns + left], b = vertices[r * columns + c];
                    for (int m = left + 1; m < c; m++)
                    {
                        float t = (offsets[m] - offsets[left]) / (offsets[c] - offsets[left]);
                        vertices[r * columns + m].Y = Mathf.Lerp(a.Y, b.Y, t);
                    }
                }

                left = c;
            }
        }
    }

    // ------------------------------------------------------------ помощники

    private Vector3 Spot(double s, double lateral, float sink)
    {
        var (x, _, z) = _curve.Offset(s, lateral);
        var (height, _) = GroundAt(x, z);
        return Local(x, height + sink, z);
    }

    private Vector3 Local(double x, double y, double z) => new((float)(x - _anchorX), (float)y, (float)(z - _anchorZ));

    private static float[] Mirror(params float[] half)
    {
        var list = new List<float>();
        for (int i = half.Length - 1; i > 0; i--) list.Add(-half[i]);
        list.AddRange(half);
        return list.ToArray();
    }
}
