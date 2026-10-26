using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Krntija.Core;
using Krntija.Core.Economy;
using Krntija.Core.Items;
using Krntija.Core.Parts;
using Krntija.Core.Vehicle;
using Krntija.Core.Workshop;
using Krntija.Core.World;

namespace Krntija.Game;

/// <summary>Меню, пауза, новая игра, сохранение и загрузка, пересборка машины.</summary>
public partial class Main
{
    /// <summary>Машина из --car=&lt;id&gt;: нужно для отладки и скриншотов разных кузовов.</summary>
    private static CarSpec SelectedCar()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--car=", StringComparison.Ordinal))
                return CarCatalog.Get(arg["--car=".Length..]);
        }

        return CarCatalog.Zastava101;
    }

    private bool HasSave() => System.IO.File.Exists(ProjectSettings.GlobalizePath(SavePath));

    /// <summary>
    /// Esc: сначала закрывает открытое окно игры (рюкзак, карту, журнал, лавку),
    /// и только из чистой игры — пауза. Меню ловит Esc раньше игры, поэтому решение здесь.
    /// </summary>
    private void OnEscape()
    {
        if (!_menu.IsOpen)
        {
            if (!CloseGameWindows()) PauseGame();
            return;
        }

        if (!_menu.InMainMode) ResumeGame();
    }

    /// <summary>Закрыть окна поверх игры и свободный курсор; false — закрывать было нечего.</summary>
    private bool CloseGameWindows()
    {
        bool closed = _backpack.IsOpen || _mapUi.IsOpen || _hud.Screen != HudScreen.None;
        if (_backpack.IsOpen) _backpack.Close();
        if (_mapUi.IsOpen) _mapUi.Toggle();
        _hud.Screen = HudScreen.None;
        SetCursorFree(false);
        return closed;
    }

    private void PauseGame()
    {
        // Пауза поверх рюкзака оставляла его и свободный курсор висеть после «Продолжить».
        CloseGameWindows();
        GetTree().Paused = true;
        _hud.Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _menu.ShowPause();
    }

    private void ResumeGame()
    {
        GetTree().Paused = false;
        _hud.Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _menu.Close();
    }

    /// <summary>
    /// Новая игра на выбранной машине: сессия собирается заново, поэтому
    /// топливо, детали, деньги, время и задача начинаются с чистого листа.
    /// Мир (дорога, террейн) остаётся, но его состояние сбрасывается:
    /// события, открытые шлагбаумы и награды за посещённые места.
    /// </summary>
    private void StartNewGame(CarSpec spec)
    {
        Respawn(GameSession.NewGame(car: spec));
        PlaceCarAtStart(rebuild: true);

        // Машина с раскладкой начинается разобранной у гаража (ADR 0003, п. 5).
        bool garage = PlaceCarAtGarage();

        EngageParkingBrake();
        ResetWorldState();

        _objectiveDone = false;
        _assemblyMissingShown = -1;
        _hud.Objective = string.Empty;

        ResumeGame();
        _hud.Toast(garage ? $"Новая игра: {spec.Name} — собери машину" : $"Новая игра: {spec.Name}");
        GD.Print($"[newgame] {spec.Id} гараж={garage}");
    }

    /// <summary>Сброс состояния мира при новой игре в той же сцене.</summary>
    private void ResetWorldState()
    {
        // Новая сессия — пустой WorldState: лут возвращается, шлагбаумы закрываются.
        SyncWorldState();
    }

    private void ContinueGame()
    {
        if (!HasSave())
        {
            _menu.ShowCarSelect();
            return;
        }

        ResumeGame();
        LoadGame();
    }

    private void GoToMainMenu()
    {
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }


    private void SaveGame()
    {
        var path = ProjectSettings.GlobalizePath(SavePath);
        var data = _car.Session.ToSave();
        data.ObjectiveDone = _objectiveDone;
        data.World = WorldKind;
        data.WorldSeed = _streamer?.Seed ?? 0;

        // Позиция — в координатах мира: начало сцены к загрузке будет другим.
        var (x, z) = WorldOrigin.ToWorld(_car.GlobalPosition);
        data.CarX = x;
        data.CarY = _car.GlobalPosition.Y;
        data.CarZ = z;
        data.CarYaw = _car.Rotation.Y;

        SaveSerializer.SaveFile(path, data);
        _hud.Toast("Сохранено");
        GD.Print($"[save] {path}");
    }

    private void LoadGame()
    {
        _loads++; // --selftest-menu: пауза не должна грузить сейв
        var path = ProjectSettings.GlobalizePath(SavePath);
        if (!System.IO.File.Exists(path))
        {
            _hud.Toast("Сейв не найден");
            return;
        }

        var data = SaveSerializer.LoadFile(path);
        // Сейв из другого мира: машина и вещи остаются, место и состояние мира — нет.
        bool samePlace = SaveWorld.Prepare(data, WorldKind, _streamer?.Seed ?? 0);
        var session = GameSession.FromSave(data);
        Respawn(session);

        _objectiveDone = data.ObjectiveDone;

        if (!samePlace)
        {
            PlaceCarAtStart(rebuild: true);
        }
        else if (_streamer is not null)
        {
            // Куски строятся заново уже с новой сессией: поднятое не появится.
            PlaceCarFromChunkSave(data);
        }
        else
        {
            _car.GlobalPosition = new Vector3((float)data.CarX, (float)data.CarY, (float)data.CarZ);
            _car.Rotation = new Vector3(0, (float)data.CarYaw, 0);
            _car.LinearVelocity = Vector3.Zero;
            _car.AngularVelocity = Vector3.Zero;
        }

        SyncWorldState();
        // Детали из сейва: тела ставятся по сохранённым мировым координатам.
        SpawnLooseParts();
        _assemblyMissingShown = -1;
        _hud.Toast(samePlace ? "Загружено" : "Загружено: сейв из другого мира, машина на старте");
        GD.Print($"[load] мир {data.World}, сид {data.WorldSeed}, место {(samePlace ? "из сейва" : "старт")}");
    }

    private void Respawn(GameSession session)
    {
        Vector3 position = _car.GlobalPosition;
        Vector3 rotation = _car.GlobalRotation;

        // Сначала убираем старую машину из дерева: QueueFree освобождает узел
        // в конце кадра, и без RemoveChild две машины успели бы побыть в мире
        // одновременно — старая ещё участвует в физике.
        RemoveChild(_car);
        _car.QueueFree();

        _car = Car.Build(session);
        AddChild(_car);
        _car.GlobalPosition = position;
        _car.GlobalRotation = rotation;
        _car.Autopilot = _smoke || _selfTest || _perf || _selfTestCars;

        _camera.Target = _car;
        _hud.Car = _car;
        if (_streamer is not null) SteerAlongChunkRoad();
    }

    /// <summary>Игрок начинает на ручнике; в автотестах ручник не мешает движению.</summary>
    private void EngageParkingBrake()
    {
        if (_smoke || _selfTest || _perf || _selfTestCars) return;
        _car.SetHandbrake(true);
    }

    /// <summary>Поставить машину на старт дороги: новую игру всегда начинаем там.</summary>
    /// <param name="rebuild">Мир кусками: собрать куски заново по плану и состоянию новой сессии.</param>
    private void PlaceCarAtStart(bool rebuild = false)
    {
        if (_streamer is not null) PlaceCarOnChunkRoad(StartStation, rebuild);
    }
}
