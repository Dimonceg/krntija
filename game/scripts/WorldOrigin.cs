using Godot;

namespace Krntija.Game;

/// <summary>
/// Плавающее начало координат. Godot считает сцену во <c>float</c>: дальше ~10 км
/// дрожат физика и камера. Поэтому сцена всегда рядом с нулём, а настоящее место
/// в мире — это координаты сцены плюс <see cref="X"/>, <see cref="Z"/> (в <c>double</c>).
/// Ядро считает в координатах мира и о сдвиге не знает.
/// </summary>
/// <remarks>
/// Шейдеры, которые берут шум от мировой позиции вершины, прибавляют глобальный
/// параметр <c>world_origin</c> — иначе узор травы и земли перескакивал бы при сдвиге.
/// </remarks>
public static class WorldOrigin
{
    public const string ShaderParameter = "world_origin";

    public static double X { get; private set; }
    public static double Z { get; private set; }

    public static Vector3 ToScene(double x, double y, double z) => new((float)(x - X), (float)y, (float)(z - Z));

    public static (double X, double Z) ToWorld(Vector3 scene) => (scene.X + X, scene.Z + Z);

    public static void Reset()
    {
        X = 0;
        Z = 0;
        Push();
    }

    /// <summary>Сдвинуть начало на (dx, dz): всё в сцене должно сдвинуться на (−dx, −dz).</summary>
    public static void Shift(double dx, double dz)
    {
        X += dx;
        Z += dz;
        Push();
    }

    private static void Push() =>
        RenderingServer.GlobalShaderParameterSet(ShaderParameter, new Vector3((float)X, 0, (float)Z));
}
