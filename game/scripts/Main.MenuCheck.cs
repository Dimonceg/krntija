using Godot;

namespace Krntija.Game;

/// <summary>
/// --selftest-menu: Esc и пауза через настоящий ввод (баг «B, Esc, Продолжить —
/// и игра загрузилась из сейва, игрок в машине»).
/// </summary>
public partial class Main
{
    private bool _checkMenu;
    private int _loads;

    private async void RunMenuCheck()
    {
        _selfTestOk = true;

        // Без окна мышь не захватывается: этот признак проверяем только с окном.
        bool headless = DisplayServer.GetName() == "headless";
        bool Captured() => headless || Input.MouseMode == Input.MouseModeEnum.Captured;

        async System.Threading.Tasks.Task Frames(int count)
        {
            for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        void Press(Key key)
        {
            // Обёртки событий освобождаем сразу: иначе их финализирует .NET после остановки движка.
            foreach (bool pressed in new[] { true, false })
            {
                using var keyEvent = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed };
                Input.ParseInputEvent(keyEvent);
            }
        }

        // Как у игрока: главное меню, «Продолжить» из сейва, выйти из машины.
        SaveGame();
        _menu.ShowMainMenu(true);
        GetTree().Paused = true;
        await Frames(3);
        _menu.ClickButton("Продолжить");
        await Frames(3);
        Check(!_menu.IsOpen && !GetTree().Paused, "главное меню: «Продолжить» запускает игру");
        ExitCar();
        await Frames(3);
        int loads = _loads;

        Press(Key.B);
        await Frames(3);
        Check(_backpack.IsOpen, "B открывает рюкзак");

        Press(Key.Escape);
        await Frames(3);
        GD.Print($"[menu] после Esc: рюкзак {_backpack.IsOpen}, меню {_menu.IsOpen} ({_menu.Buttons}), пауза {GetTree().Paused}");
        Check(!_backpack.IsOpen, "Esc закрывает рюкзак");
        Check(!_menu.IsOpen && !GetTree().Paused, "Esc при открытом рюкзаке не открывает меню");
        Check(Captured(), "после рюкзака мышь снова крутит обзор");

        // Пауза отдельно: «Продолжить» возвращает в игру, а не грузит сейв.
        Press(Key.Escape);
        await Frames(3);
        Check(_menu.IsOpen && GetTree().Paused, $"Esc в игре — пауза: {_menu.Buttons}");
        _menu.ClickButton("Продолжить");
        await Frames(3);
        Check(!_menu.IsOpen && !GetTree().Paused, "пауза: «Продолжить» закрывает меню");
        Check(_loads == loads, $"пауза: «Продолжить» не загружает сейв (загрузок {_loads - loads})");
        Check(_onFoot, "после паузы игрок там же, пешком");

        // Пауза поверх открытого окна (карта по M) закрывает его: после «Продолжить» чистая игра.
        Press(Key.M);
        await Frames(3);
        PauseGame();
        await Frames(3);
        _menu.ClickButton("Продолжить");
        await Frames(3);
        Check(!_mapUi.IsOpen && !_backpack.IsOpen && Captured(),
            "после паузы окна закрыты, мышь захвачена");

        GD.Print(_selfTestOk ? "[menu] OK" : "[menu] FAIL");
        QuitSoon(_selfTestOk ? 0 : 1);
    }
}
