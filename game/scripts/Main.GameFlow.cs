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

    private void OnEscape()
    {
        if (!_menu.IsOpen)
        {
            PauseGame();
            return;
        }

        if (!_menu.InMainMode) ResumeGame();
    }

    private void PauseGame()
    {
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
        PlaceCarAtStart();
        EngageParkingBrake();
        ResetWorldState();

        _objectiveDone = false;
        _hud.Objective = string.Empty;

        ResumeGame();
        _hud.Toast($"Новая игра: {spec.Name}");
        GD.Print($"[newgame] {spec.Id}");
    }

    /// <summary>Сброс состояния мира при новой игре в той же сцене.</summary>
    private void ResetWorldState()
    {
        _visitedPoi.Clear();

        foreach (var point in _interactions)
            point.Gate?.Close();

        // События хранят сработавшие точки и ссылки на старую машину:
        // проще собрать их заново, чем чинить флаги и удалённые узлы.
        if (_road is not null)
        {
            _events?.QueueFree();
            _events = new RoadEvents();
            AddChild(_events);
            _events.Setup(_road.Centerline, _road.Headings, _car, _hud);
        }
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
        data.CarX = _car.GlobalPosition.X;
        data.CarY = _car.GlobalPosition.Y;
        data.CarZ = _car.GlobalPosition.Z;
        data.CarYaw = _car.Rotation.Y;

        SaveSerializer.SaveFile(path, data);
        _hud.Toast("Сохранено");
        GD.Print($"[save] {path}");
    }

    private void LoadGame()
    {
        var path = ProjectSettings.GlobalizePath(SavePath);
        if (!System.IO.File.Exists(path))
        {
            _hud.Toast("Сейв не найден");
            return;
        }

        var data = SaveSerializer.LoadFile(path);
        var session = GameSession.FromSave(data);
        Respawn(session);

        _objectiveDone = data.ObjectiveDone;

        _car.GlobalPosition = new Vector3((float)data.CarX, (float)data.CarY, (float)data.CarZ);
        _car.Rotation = new Vector3(0, (float)data.CarYaw, 0);
        _car.LinearVelocity = Vector3.Zero;
        _car.AngularVelocity = Vector3.Zero;

        _hud.Toast("Загружено");
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

        // События держат ссылку на машину: без Rebind ямы и коровы
        // продолжали бы толкать удалённый объект после загрузки сейва.
        _events?.Rebind(_car, _hud);
    }

    /// <summary>Игрок начинает на ручнике; в автотестах ручник не мешает движению.</summary>
    private void EngageParkingBrake()
    {
        if (_smoke || _selfTest || _perf || _selfTestCars) return;
        _car.SetHandbrake(true);
    }

    /// <summary>Поставить машину на старт дороги: новую игру всегда начинаем там.</summary>
    private void PlaceCarAtStart()
    {
        // Не на самом краю полотна, иначе половина машины висит в воздухе.
        Vector3 start = _road is { Centerline.Count: > 4 } ? _road.Centerline[4] : new Vector3(0, 0.6f, 0);
        _car.GlobalPosition = start + Vector3.Up * 0.62f;
        _car.LinearVelocity = Vector3.Zero;
        _car.AngularVelocity = Vector3.Zero;
        _car.Path = _road?.Centerline;
    }
}
