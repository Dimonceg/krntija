using System;
using Godot;

namespace Krntija.Game;

/// <summary>
/// Общие ресурсы Godot в статических полях (материалы, меши, кеш моделей) надо
/// отпустить, пока движок жив. Иначе их обёртки финализирует .NET уже после
/// остановки движка, и Godot падает при выходе с 0xC0000005.
/// </summary>
/// <remarks>
/// Новое статическое поле с ресурсом Godot — добавить его освобождение сюда
/// (через метод <c>ReleaseCaches</c> своего класса). Ленивые геттеры создадут
/// ресурс заново, если после освобождения он всё-таки понадобится.
/// </remarks>
public static class StaticCaches
{
    public static void Release()
    {
        ChunkBuilder.ReleaseCaches();
        InteractionHighlight.ReleaseCaches();
        LandscapeArt.ReleaseCaches();
        RoadMeshBuilder.ReleaseCaches();
        WorldVegetation.ReleaseCaches();
        YardDecor.ReleaseCaches();
        ModelLibrary.Clear();

        // Финализаторы оставшихся обёрток — сейчас, пока движок жив.
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    internal static void Dispose<T>(ref T? resource) where T : GodotObject
    {
        resource?.Dispose();
        resource = null;
    }
}
