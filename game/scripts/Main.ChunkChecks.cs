using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Krntija.Core;
using Krntija.Core.Items;
using Krntija.Core.World;
using Krntija.Core.WorldGen;

namespace Krntija.Game;

/// <summary>
/// Проверки мира кусками (ADR 0002): --chunk-bench, --chunk-run, --chunk-places.
/// Вынесены из Main.Diagnostics.cs: тот перерос 1000 строк.
/// </summary>
public partial class Main
{
    private double _runStation;
    private int _runFrames;
    private ulong _runLastUsec;
    private double _runFrameSum;
    private double _runPeakFrame;
    private int _runMisses;
    private int _runMaxLoaded;
    private double _runNodesStart;
    private int _runShifts;
    private int _runSpikes;

    /// <summary>
    /// --chunk-bench: постройка кусков 10–19 по шагам. Печатает среднее и пик на шаг;
    /// провал, если шаг заметно дольше бюджета кадра (ADR 0002: ≤ 4 мс).
    /// </summary>
    private void RunChunkBench()
    {
        var sums = new double[ChunkBuilder.StepCount];
        var peaks = new double[ChunkBuilder.StepCount];
        const int from = 10, count = 10;

        for (int index = from; index < from + count; index++)
        {
            // Два замера, минимум по шагу: проверки других моделей на той же машине
            // только добавляют время, а не отнимают — минимум ближе к цене самой постройки.
            var first = _streamer!.Bench(index);
            var second = _streamer.Bench(index);
            var times = new double[first.Length];
            for (int step = 0; step < times.Length; step++) times[step] = System.Math.Min(first[step], second[step]);
            for (int step = 0; step < times.Length; step++)
            {
                sums[step] += times[step];
                peaks[step] = System.Math.Max(peaks[step], times[step]);
            }
        }

        string[] names = { "дорога+стены", "земля 1/2", "земля 2/2", "физика 1/2", "физика 2/2", "места", "горы", "деревья", "трава+столбы" };
        double worst = 0;
        for (int step = 0; step < sums.Length; step++)
        {
            GD.Print($"[chunk-bench] {names[step],-14} среднее {sums[step] / count,5:F2} мс, пик {peaks[step],5:F2} мс");
            worst = System.Math.Max(worst, peaks[step]);
        }

        double total = 0;
        foreach (double sum in sums) total += sum;
        GD.Print($"[chunk-bench] кусок целиком: {total / count:F1} мс; худший шаг {worst:F1} мс (бюджет кадра {ChunkStreamer.BudgetMs} мс)");

        // Запас ×3: замер без окна и на разных машинах скачет, ловим только явный регресс.
        bool ok = worst <= ChunkStreamer.BudgetMs * 3;
        GD.Print(ok ? "[chunk-bench] OK" : "[chunk-bench] FAIL: шаг постройки слишком долгий");
        QuitSoon(ok ? 0 : 1);
    }

    /// <summary>
    /// --chunk-run: машину ведут по дороге 10 км по 8 м за кадр. Под машиной всегда
    /// должна быть дорога, кусков — не больше окна подгрузки, узлов — без утечки,
    /// начало координат — сдвигаться.
    /// </summary>
    private void ChunkRunStep()
    {
        if (_quitting) return;

        const double length = 10_000, step = 8;
        ulong now = Time.GetTicksUsec();

        if (_runFrames == 0)
        {
            _car.Freeze = true;
            // Не кратно шагу ленты (4 м): иначе луч идёт ровно по ребру треугольников.
            _runStation = StartStation + 1.3;
            _runShifts = _streamer!.Shifts;
        }
        else if (_streamer!.Shifts != _runShifts)
        {
            // Кадр сдвига: тела кусков переедут при следующем шаге физики, луч сейчас
            // смотрел бы в старое место. В игре машина сдвигается вместе с ними.
            _runShifts = _streamer.Shifts;
        }
        else
        {
            double frame = (now - _runLastUsec) / 1000.0;
            _runFrameSum += frame;
            // Первые кадры — компиляция шейдеров, к подгрузке отношения не имеют.
            if (_runFrames > 30) _runPeakFrame = System.Math.Max(_runPeakFrame, frame);
            if (_runFrames > 30 && frame > 30 && _runSpikes++ < 8)
                GD.Print($"[chunk-run] рывок {frame:F0} мс на {_runStation:F0} м (кусок {_streamer!.CurrentChunk}, сдвигов {_streamer.Shifts})");

            // Дорога под машиной: луч сверху вниз должен попасть в полотно.
            var from = _car.GlobalPosition + Vector3.Up * 5f;
            var query = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * 20f);
            query.Exclude = new Godot.Collections.Array<Rid> { _car.GetRid() };
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
            bool onRoad = hit.Count > 0 && PhysicsHit.Collider<Node>(hit) is { Name: var name } && name == "RoadSurface";
            if (!onRoad && _runMisses++ < 5)
                GD.PrintErr($"[chunk-run] нет дороги под машиной на {_runStation:F0} м: "
                            + (hit.Count > 0 ? $"{PhysicsHit.Collider<Node>(hit)?.GetPath()} y={PhysicsHit.Position(hit).Y:F2}, машина y={_car.GlobalPosition.Y - 1:F2}" : "пусто"));
        }

        _runLastUsec = now;
        _runFrames++;

        // Узлы считаем после первого километра: к этому времени окно подгрузки заполнено.
        // Узлы кусков не в счёт: в селе их больше, чем у колодца, это не утечка.
        // Утечка — то, что растёт вне кусков (лут, точки, сироты), а сами куски держит окно.
        if (_runNodesStart == 0 && _runStation >= StartStation + 1000)
            _runNodesStart = NodesOutsideChunks();
        _runMaxLoaded = System.Math.Max(_runMaxLoaded, _streamer!.LoadedChunks);

        if (_runStation >= StartStation + length)
        {
            double nodes = NodesOutsideChunks();
            double growth = nodes / _runNodesStart - 1;
            int stale = StaleChunkRefs();
            GD.Print($"[chunk-run] {length / 1000:F0} км за {_runFrames} кадров; кадр в среднем {_runFrameSum / (_runFrames - 1):F2} мс, "
                     + $"пик {_runPeakFrame:F1} мс; сдвигов начала {_streamer.Shifts}; кусков максимум {_runMaxLoaded}; "
                     + $"узлов вне кусков {_runNodesStart:F0} → {nodes:F0} ({growth * 100:+0;-0}%); промахов мимо дороги {_runMisses}; "
                     + $"точек {_interactions.Count}, ссылок на выгруженные куски {stale}; "
                     + $"шагов постройки {_streamer.StepsBuilt}, пик шага {_streamer.PeakStepMs:F1} мс");

            bool ok = _runMisses == 0
                      && _streamer.Shifts >= 4
                      && _runMaxLoaded <= ChunkStreamer.Behind + ChunkStreamer.Ahead + 3
                      && growth < 0.25
                      && stale == 0;
            GD.Print(ok ? "[chunk-run] OK" : "[chunk-run] FAIL");
            QuitSoon(ok ? 0 : 1);
            return;
        }

        _runStation += step;
        _car.GlobalPosition = _streamer.RoadPoint(_runStation) + Vector3.Up * 1.0f;
        _car.Rotation = new Vector3(0, -_streamer.RoadHeading(_runStation), 0);
    }

    /// <summary>
    /// --chunk-places: места из плана в мире кусками. Дом, заправка и блокпост
    /// на месте; оплаченный шлагбаум и поднятый лут переживают выгрузку куска
    /// и постройку его заново (id из плана, ADR 0002).
    /// </summary>
    private void RunChunkPlacesCheck()
    {
        _selfTestOk = true;
        var world = _car.Session.WorldState;
        int Count(InteractionKind kind) => _interactions.Count(point => point.Kind == kind);

        Check(_housePosition is not null, "дом игрока построен");
        Check(Count(InteractionKind.Chest) == 1 && Count(InteractionKind.Workbench) == 1
              && Count(InteractionKind.SleepHome) == 1, "в доме сундук, верстак и кровать");
        Check(Count(InteractionKind.Door) >= 3, $"двери дома: {Count(InteractionKind.Door)}");
        Check(Count(InteractionKind.Pump) >= 1 && Count(InteractionKind.Shop) >= 1, "на заправке колонка и лавка");
        Check(_chunkPlaces.Count >= 6, $"мест в стартовых кусках: {_chunkPlaces.Count}");

        // Начало мира закрыто: луч назад вдоль дороги и по полю упирается в стену.
        foreach (double lateral in new[] { 0.0, -80.0, 120.0 })
        {
            Vector3 from = _streamer!.RoadPoint(6, lateral) + Vector3.Up * 1.5f;
            Vector3 back = _streamer.RoadPoint(0, lateral) - _streamer.RoadPoint(6, lateral);
            var query = PhysicsRayQueryParameters3D.Create(from, from + back.Normalized() * 15f);
            query.Exclude = new Godot.Collections.Array<Rid> { _car.GetRid() };
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
            string? name = hit.Count > 0 ? PhysicsHit.Collider<Node>(hit)?.Name : null;
            Check(name == "Boundary", $"за началом мира стена (вбок {lateral:F0} м): {name ?? "пусто"}");
        }

        CheckPads("старт");

        // Дворовый декор не на точках действий и не на луте. Позиции — из плана, как их
        // ставит YardDecor: без видеокарты MultiMesh не хранит данные экземпляров.
        var decor = _chunkPlaces
            .SelectMany(place => WorldGenerator.Decor(_streamer!.Seed, place.Plan, _streamer.Curve)
                .Select(item => WorldGenerator.PlaceToWorld(place.Plan, _streamer.Curve, item.OffsetX, item.OffsetZ)))
            .Select(world => WorldOrigin.ToScene(world.X, 0, world.Z))
            .ToList();
        int decorNodes = _streamer!.FindChildren("Decor_*", "MultiMeshInstance3D", true, false).Count;
        float closest = decor.Count == 0 || _interactions.Count == 0 ? float.MaxValue
            : decor.Min(item => _interactions.Min(point => new Vector2(point.Position.X - item.X, point.Position.Z - item.Z).Length()));
        Check(decor.Count > 0 && decorNodes > 0 && closest > 1.2f,
              $"дворовый декор: {decor.Count} шт. в {decorNodes} пачках, ближе всех к точке {closest:F1} м");

        // Дорожные угрозы стоят у дороги, где их поставил план, а не со сдвигом на позицию куска.
        var hazards = _streamer!.FindChildren("Hazard_*", "", true, false).OfType<Node3D>().ToList();
        float worstHazard = hazards.Count == 0 ? 0 : hazards.Max(node =>
        {
            Vector3 position = node.GlobalPosition;
            return position.DistanceTo(_streamer.RoadPoint(_streamer.StationAt(position)));
        });
        Check(worstHazard < 130f, $"угрозы у дороги: {hazards.Count} шт., дальше всех {worstHazard:F0} м");

        var gate = _interactions.FirstOrDefault(point => point.Kind == InteractionKind.Gate);
        Check(gate is { Gate.IsOpen: false, StateId: "gate:c2/p0/u0" }, $"шлагбаум закрыт, id {gate?.StateId}");

        var loot = _interactions.FirstOrDefault(point => point.Kind == InteractionKind.Loot
                                                         && point.StateId?.StartsWith("loot:c1/") == true);
        Check(loot is not null, "лут у заправки лежит");
        if (gate is null || loot is null)
        {
            QuitSoon(1);
            return;
        }

        _car.Session.Wallet.Earn(100);
        PayForGate(gate);
        string lootId = loot.StateId!;
        PickUpLoot(loot);
        Check(world.IsDone("gate:c2/p0/u0") && world.IsDone(lootId), $"оплата и подбор записаны: {lootId}");

        // Закрытый выезд у блокпоста: починка убирает заграждение и пишется в мир.
        var exit = _interactions.FirstOrDefault(point => point.Kind == InteractionKind.RepairExit && point.Chunk == 2);
        Check(exit is { Node: not null, StateId: not null }, $"выезд у блокпоста: {exit?.StateId}");
        string? exitId = exit?.StateId;
        if (exit is not null)
        {
            _car.Session.Wallet.Earn(WorldActions.ExitRepairPrice);
            _car.Session.Inventory.TryAdd(Krntija.Core.Items.ItemIds.DuctTape);
            var barrier = exit.Node!;
            Use(exit);
            Check(world.IsDone(exitId!) && barrier.IsQueuedForDeletion() && !_interactions.Contains(exit),
                  "выезд починен: заграждение убрано, точка снята");
        }

        // Уехать далеко (стартовые куски выгружаются) и вернуться: куски строятся заново.
        _streamer!.BuildAround(StartStation + 3000);
        Check(_housePosition is null && !_streamer.IsLoaded(0), "дальние куски выгружены, дом снят");
        Check(StaleChunkRefs() == 0, "нет ссылок на выгруженные куски");
        _streamer.BuildAround(StartStation);

        var rebuiltGate = _interactions.FirstOrDefault(point => point.Kind == InteractionKind.Gate);
        Check(rebuiltGate?.Gate is { IsOpen: true }, "шлагбаум после возврата открыт");
        Check(_interactions.All(point => point.StateId != lootId), "поднятый лут не вернулся");
        Check(exitId is not null && _interactions.All(point => point.StateId != exitId)
              && _streamer.IsLoaded(2), "починенный выезд после возврата открыт");
        Check(_interactions.Any(point => point.Kind == InteractionKind.Loot && point.Chunk == 1),
              "остальной лут у заправки на месте");
        Check(_housePosition is not null && StaleChunkRefs() == 0, "дом снова на месте, ссылки чистые");

        // Ящик в брошенном доме: ищем первый по плану, открываем, второй раз — пуст.
        var abandoned = Enumerable.Range(3, 200)
            .SelectMany(index => WorldGenerator.Plan(_streamer.Seed, index).Places)
            .FirstOrDefault(place => place.Uses.Any(use => use.Kind == "crate"));
        Check(abandoned is not null, $"брошенный дом с ящиком в плане: {abandoned?.Id}");
        if (abandoned is not null)
        {
            PlaceCarOnChunkRoad(abandoned.S);
            var crate = _interactions.FirstOrDefault(point => point.Kind == InteractionKind.Crate
                                                              && point.StateId?.Contains(abandoned.Id + "/") == true);
            Check(crate is not null, $"ящик зарегистрирован: {crate?.StateId}");
            if (crate is not null)
            {
                int Carried() => _car.Session.Inventory.Items.Sum(item => item.Count);
                int before = Carried();
                Use(crate);
                int opened = Carried();
                Use(crate);
                Check(world.IsDone(crate.StateId!) && opened > before && Carried() == opened,
                      $"ящик открыт один раз: {before} → {opened} → {Carried()}");
            }
        }

        // Самый крутой рельеф под селом на первых 300 кусках (4,4 м до площадок).
        PlaceCarOnChunkRoad(58 * WorldGenerator.ChunkLength + 30);
        CheckPads("село c58");

        // Попутчик платит один раз за игру: после выгрузки куска и возврата — уже нет.
        var rider = Enumerable.Range(3, 300)
            .SelectMany(index => WorldGenerator.Plan(_streamer.Seed, index).Places)
            .SelectMany(place => place.Hazards.Select(hazard => (place, hazard)))
            .FirstOrDefault(pair => pair.hazard.Kind == HazardKind.Hitchhiker);
        Check(rider.hazard is not null, $"попутчик в плане: {rider.hazard?.Id}");
        if (rider.hazard is not null)
        {
            string riderId = WorldState.Use("hitchhiker", rider.hazard.Id);
            HazardNode? FindRider() => _streamer.FindChildren("Hazard_*", "", true, false).OfType<HazardNode>()
                .FirstOrDefault(node => node.StateId == riderId);

            PlaceCarOnChunkRoad(rider.place.S, rebuild: true);
            double money = _car.Session.Wallet.Money;
            FindRider()?.Trigger(_car);
            double paid = _car.Session.Wallet.Money - money;

            PlaceCarOnChunkRoad(rider.place.S + 3000);
            PlaceCarOnChunkRoad(rider.place.S);
            money = _car.Session.Wallet.Money;
            FindRider()?.Trigger(_car);
            Check(System.Math.Abs(paid - rider.hazard.Amount) < 0.001 && _car.Session.Wallet.Money == money
                  && _car.Session.WorldState.IsDone(riderId),
                  $"попутчик заплатил {paid:F0} $ один раз ({riderId})");
        }

        // Сейв далеко от старта (сейв v3): позиция в координатах мира, начало сцены сдвинуто.
        const double far = 6000;
        PlaceCarOnChunkRoad(far);
        var farLoot = _interactions.FirstOrDefault(point => point.Kind == InteractionKind.Loot && point.Chunk >= 0);
        string? farLootId = farLoot?.StateId;
        if (farLoot is not null) PickUpLoot(farLoot);
        Check(System.Math.Abs(WorldOrigin.Z) > far - 500, $"начало сцены перенесено: Z={WorldOrigin.Z:F0}");
        SaveGame();

        PlaceCarOnChunkRoad(StartStation);
        Check(_streamer.IsLoaded(0) && !_streamer.IsLoaded((int)(far / WorldGenerator.ChunkLength)), "вернулись на старт");
        LoadGame();

        double station = _streamer.StationAt(_car.GlobalPosition);
        Check(System.Math.Abs(station - far) < 5, $"после загрузки машина на {station:F0} м (ждём {far:F0})");
        Check(_car.GlobalPosition.Length() < 50, $"машина у начала сцены: {_car.GlobalPosition.Length():F0} м");
        Check(farLootId is not null && _car.Session.WorldState.IsDone(farLootId)
              && _interactions.All(point => point.StateId != farLootId), $"поднятый вдали лут не вернулся: {farLootId}");
        Check(StaleChunkRefs() == 0, "после загрузки ссылки чистые");

        GD.Print(_selfTestOk ? "[chunk-places] OK" : "[chunk-places] FAIL");
        QuitSoon(_selfTestOk ? 0 : 1);
    }

    /// <summary>Площадки мест построенных кусков: земля под габаритом ровная.</summary>
    private void CheckPads(string where)
    {
        // Площадки мест: земля под габаритом ровная (луч до коллизии земли, сквозь постройки).
        float worstPad = 0;
        string worstPlace = "";
        foreach (var place in _chunkPlaces)
        {
            if (place.Plan.OnRoad) continue;
            var footprint = place.Plan.Footprint;
            var basis = new Basis(Vector3.Up, -(float)place.Plan.Yaw);
            for (float u = -0.8f; u <= 0.81f; u += 0.4f)
            for (float v = -0.8f; v <= 0.81f; v += 0.4f)
            {
                Vector3 at = place.Position + basis * new Vector3(u * (float)footprint.HalfWidth, 0, v * (float)footprint.HalfDepth);
                float deviation = Mathf.Abs(LandscapeY(at) - place.Position.Y);
                if (deviation <= worstPad) continue;
                worstPad = deviation;
                worstPlace = $"{place.Plan.Id} {place.Plan.Kind}";
            }
        }

        Check(worstPad < 0.25f, $"площадки мест ровные ({where}): худшее отклонение {worstPad:F2} м ({worstPlace})");
    }

    /// <summary>Высота коллизии земли под точкой: постройки и машину пропускаем.</summary>
    private float LandscapeY(Vector3 at)
    {
        var exclude = new Godot.Collections.Array<Rid> { _car.GetRid() };
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * 60f, at + Vector3.Down * 60f);
            query.Exclude = exclude;
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (hit.Count == 0) return float.NaN;
            if (PhysicsHit.Collider<Node>(hit) is { Name: var name } && name.ToString().StartsWith("LandscapeGroundCollision"))
            {
                return PhysicsHit.Position(hit).Y;
            }
            exclude.Add(PhysicsHit.Collider<CollisionObject3D>(hit)!.GetRid());
        }

        return float.NaN;
    }

    /// <summary>Точки, лут и места, чей кусок уже выгружен: их забыли снять.</summary>
    private int StaleChunkRefs()
    {
        bool Stale(int chunk) => chunk >= 0 && !_streamer!.IsLoaded(chunk);
        return _interactions.Count(point => Stale(point.Chunk))
               + _lootSpawns.Count(spawn => Stale(spawn.Chunk))
               + _chunkPlaces.Count(place => Stale(place.Chunk));
    }

    private double NodesOutsideChunks()
    {
        static int Count(Node node)
        {
            int count = 1;
            foreach (var child in node.GetChildren()) count += Count(child);
            return count;
        }

        return Performance.GetMonitor(Performance.Monitor.ObjectNodeCount) - Count(_streamer!);
    }
}
