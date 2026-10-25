namespace Krntija.Core.WorldGen;

/// <summary>
/// Детерминированный хеш для сидов генерации. Свой, а не GetHashCode():
/// тот меняется между запусками .NET, и мир стал бы разным у игроков.
/// </summary>
public static class Hash
{
    private const ulong Golden = 0x9E3779B97F4A7C15UL;

    public static ulong Mix(ulong seed)
    {
        ulong z = seed + Golden;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public static ulong Mix(ulong seed, int index) => Mix(Mix(seed) ^ (ulong)(uint)index);

    /// <summary>Хеш строки (FNV-1a): детерминированный id → сид.</summary>
    public static ulong MixString(ulong seed, string value)
    {
        ulong hash = seed ^ 1469598103934665603UL;
        foreach (char c in value)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }

        return Mix(hash);
    }

    public static ulong Mix(ulong seed, int first, int second) =>
        Mix(Mix(seed, first) ^ ((ulong)(uint)second << 32));
}
