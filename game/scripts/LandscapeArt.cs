using System;
using System.Collections.Generic;
using Godot;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>Road materials, continuous distant ridges and batched roadside grasses.</summary>
public static class LandscapeArt
{
    private static ImageTexture? _asphalt;
    public static StandardMaterial3D Asphalt()
    {
        if (_asphalt is null)
        {
            const int size = 512;
            using var image = Image.CreateEmpty(size, size, false, Image.Format.Rgb8);
            var rng = new Pcg32(7391, sequence: 17);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Fine aggregate and restrained larger weathering.
                float grain = (float)rng.Range(-.025, .025);
                float mottling = .003f * Mathf.Sin(Mathf.Tau * x / size * 3)
                    * Mathf.Cos(Mathf.Tau * y / size * 5);
                float stone = rng.Chance(.035) ? (float)rng.Range(.025, .065) : 0;
                float v = .145f + grain + mottling + stone;
                image.SetPixel(x, y, new Color(v * .97f, v, v * 1.025f));
            }
            image.GenerateMipmaps();
            _asphalt = ImageTexture.CreateFromImage(image);
        }
        return new StandardMaterial3D
        {
            AlbedoTexture = _asphalt,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            Roughness = .95f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
    }

    private const string NoiseCode = @"
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
    vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),
        mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
";

    public static ShaderMaterial GroundMaterial() => new()
    {
        Shader = new Shader { Code = @"shader_type spatial;
render_mode cull_disabled;
varying vec3 ground;
" + NoiseCode + @"
void vertex() { ground=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
void fragment() {
    vec2 p=ground.xz;
    float broad=noise(p*.045);
    float grass=noise(p*1.6)*.65+noise(p*8.0)*.35;
    float gravel=noise(p*15.0);
    float shoulder=1.0-smoothstep(4.2,6.5,abs(UV.x));
    vec3 meadow=COLOR.rgb*mix(.77,1.16,broad)*mix(.85,1.12,grass);
    vec3 stones=mix(vec3(.25,.235,.195),vec3(.43,.40,.33),gravel);
    ALBEDO=pow(mix(meadow,stones,shoulder),vec3(2.2));
    ROUGHNESS=1.0;
}" }
    };

    public static void Mountains(List<Vector3> road, List<float> headings, Node3D parent)
    {
        if (road.Count < 4) return;
        float[] distances = { 140, 175, 220, 290, 380, 500, 650, 850, 1100 };
        var noise = new FastNoiseLite { Seed = 4201, Frequency = .0032f,
            FractalOctaves = 4, FractalType = FastNoiseLite.FractalTypeEnum.Fbm };
        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true, Roughness = 1,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        for (int side = -1; side <= 1; side += 2)
        {
            var tool = new SurfaceTool(); tool.Begin(Mesh.PrimitiveType.Triangles);
            var rows = new List<Vector3[]>();
            // Match the near terrain's boundary vertices exactly: no isolated domes.
            for (int i = 0; i < road.Count; i += 2)
            {
                var right = new Vector3(Mathf.Cos(headings[i]), 0, Mathf.Sin(headings[i]));
                var row = new Vector3[distances.Length];
                for (int j = 0; j < distances.Length; j++)
                {
                    float d = distances[j];
                    var p = road[i] + right * (d * side);
                    float edge = TerrainBuilder.SurfaceHeight(road[i].Y, side * 140, road[i] + right * (side * 140));
                    float n = noise.GetNoise2D(p.X, p.Z);
                    float ridge = 1 - Mathf.Abs(noise.GetNoise2D(p.X * 1.7f + 421, p.Z * 1.7f));
                    float foothill = Mathf.SmoothStep(140, 380, d) * (22 + 45 * (n + 1));
                    float mountain = Mathf.SmoothStep(270, 850, d) * (55 + 160 * ridge + 85 * n);
                    p.Y = edge + foothill + mountain;
                    row[j] = p;
                }
                rows.Add(row);
            }
            for (int i = 0; i < rows.Count - 1; i++)
            for (int j = 0; j < distances.Length - 1; j++)
            {
                Facet(tool, rows[i][j], rows[i + 1][j], rows[i][j + 1], j);
                Facet(tool, rows[i][j + 1], rows[i + 1][j], rows[i + 1][j + 1], j);
            }
            parent.AddChild(new MeshInstance3D { Name = side < 0 ? "MountainRangeWest" : "MountainRangeEast",
                Mesh = tool.Commit(), MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
    }

    private static void Facet(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c, int band)
    {
        var normal = (b - a).Cross(c - a).Normalized();
        if (normal.Y > 0) (b, c) = (c, b);
        else normal = -normal;
        float rock = Mathf.Clamp((1 - normal.Y) * 2.6f + band * .075f, 0, .85f);
        var color = new Color(.24f, .31f, .16f).Lerp(new Color(.43f, .42f, .36f), rock);
        color = color.Lerp(new Color(.35f, .40f, .43f), Mathf.Clamp((band - 3) * .12f, 0, .45f));
        foreach (var p in new[] { a, b, c })
        {
            tool.SetNormal(normal); tool.SetColor(color.SrgbToLinear()); tool.AddVertex(p);
        }
    }

    public static void Grass(List<Vector3> road, List<float> headings, Node3D parent)
    {
        if (road.Count < 12) return;
        var tool = new SurfaceTool(); tool.Begin(Mesh.PrimitiveType.Triangles);
        for (int blade = 0; blade < 5; blade++)
        {
            float angle = blade * 2.4f;
            var right = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var foot = right * .11f;
            var tip = foot + Vector3.Up * (.30f + blade * .045f) + right * .12f;
            var normal = right.Cross(Vector3.Up);
            foreach (var p in new[] { foot - right * .035f, foot + right * .035f, tip })
            {
                tool.SetNormal(normal); tool.SetColor(p.Y > .1f ? new Color(.56f,.59f,.28f) : new Color(.23f,.30f,.10f));
                tool.AddVertex(p);
            }
        }
        var mesh = tool.Commit();
        var material = new ShaderMaterial { Shader = new Shader { Code = @"shader_type spatial;
render_mode cull_disabled;
void vertex() {
    vec3 p=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;
    VERTEX.x+=sin(TIME*1.6+p.x*.6+p.z*.25)*.06*VERTEX.y;
}
void fragment() { ALBEDO=COLOR.rgb; ROUGHNESS=1.0; }
" } };
        var rng = new Pcg32(9117, sequence: 13);
        Vector3 home = road[10] - new Vector3(Mathf.Cos(headings[10]), 0, Mathf.Sin(headings[10])) * 15;
        int count = 0;
        for (int start = 2; start < road.Count - 3; start += 24)
        {
            var positions = new List<Transform3D>();
            for (int i = start; i < Math.Min(start + 24, road.Count - 3); i++)
            for (int side = -1; side <= 1; side += 2)
            for (int n = 0; n < 10; n++)
            {
                float t = (float)rng.NextDouble();
                var center = road[i].Lerp(road[i + 1], t);
                float heading = Mathf.LerpAngle(headings[i], headings[i + 1], t);
                var right = new Vector3(Mathf.Cos(heading), 0, Mathf.Sin(heading));
                float d = (float)rng.Range(5.8, 29) * side;
                var p = center + right * d;
                if (WorldArtGround.IsPlot(p, 1) || new Vector2(p.X - home.X, p.Z - home.Z).LengthSquared() < 169) continue;
                if (Mathf.Sin(p.X * .25f) * Mathf.Cos(p.Z * .18f) < -.35f) continue;
                p.Y = TerrainBuilder.SurfaceHeight(center.Y, d, p) - .035f;
                var scale = new Vector3((float)rng.Range(.7,1.5), (float)rng.Range(.55,1.3), 1);
                positions.Add(new Transform3D(new Basis(Vector3.Up, (float)rng.Range(0,Mathf.Tau)).Scaled(scale),p));
            }
            if (positions.Count == 0) continue;
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true, Mesh = mesh, InstanceCount = positions.Count };
            Aabb bounds = default;
            for (int i = 0; i < positions.Count; i++)
            {
                mm.SetInstanceTransform(i, positions[i]);
                float tint = (float)rng.Range(.8, 1.12);
                mm.SetInstanceColor(i, new Color(tint, tint, tint * .9f));
                var box = (positions[i] * mesh.GetAabb()).Grow(.12f);
                bounds = i == 0 ? box : bounds.Merge(box);
            }
            parent.AddChild(new MultiMeshInstance3D { Name = $"RoadGrass_{start}", Multimesh = mm,
                MaterialOverride = material, CustomAabb = bounds, VisibilityRangeEnd = 95,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            count += positions.Count;
        }
        GD.Print($"[landscape] grass: {count} clumps in spatial batches");
    }
}
