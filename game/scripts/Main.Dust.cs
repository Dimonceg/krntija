using Godot;

namespace Krntija.Game;

/// <summary>
/// Пыль из-под колёс (локальная LLM, конвейер tools/llm_art): узел живёт рядом
/// с машиной, при её смене (новая игра, загрузка) переключается на новую.
/// </summary>
public partial class Main
{
    private CarDust? _dust;

    /// <summary>Завести пыль и отдать ей актуальную машину.</summary>
    private void SyncDust()
    {
        if (_dust is null || !IsInstanceValid(_dust))
        {
            _dust = new CarDust { Name = "CarDust" };
            AddChild(_dust);
        }

        _dust.Target = _car;
    }
}
