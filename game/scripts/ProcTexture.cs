using Godot;

namespace Krntija.Game;

/// <summary>
/// Процедурные текстуры: шум в градиенте. Позволяет разбить однотонные
/// поверхности (земля, асфальт) без единого файла-ассета.
/// </summary>
public static class ProcTexture
{
    public static NoiseTexture2D Noise(float frequency, Color dark, Color light, int seed = 7, int octaves = 3, int size = 256)
    {
        var noise = new FastNoiseLite
        {
            Seed = seed,
            Frequency = frequency,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = octaves,
        };

        var gradient = new Gradient();
        gradient.SetColor(0, dark);
        gradient.SetColor(1, light);

        return new NoiseTexture2D
        {
            Width = size,
            Height = size,
            Seamless = true,
            Noise = noise,
            ColorRamp = gradient,
        };
    }
}
