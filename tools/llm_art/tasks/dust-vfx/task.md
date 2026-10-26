---
slug: dust-vfx
writes: game/scripts/CarDust.cs
refs: game/scripts/RainFx.cs, game/scripts/Car.cs:55-120, docs/ART.md:1-35
build: dotnet build game\Krntija.Game.csproj -v q --nologo
check: --selftest
shot: --shot-drive --at=40 --shot-name=llm_dust
---

# Задание: пыль из-под колёс

## Цель

Когда машина едет по земле, из-под задних колёс поднимается пыль: облачко
частиц позади машины, тем плотнее и длиннее, чем выше скорость. На месте и
в воздухе (колёса не на земле) пыли нет. Это чистая графика: правил игры,
физики и урона не трогаем.

## Что сделать

Новый файл `game/scripts/CarDust.cs`: `public partial class CarDust : Node3D`
в пространстве имён `Krntija.Game`.

Требования:

1. `public Car? Target { get; set; }` — машину ставит `Main` (меняется при
   смене машины). `Target` может быть `null` — тогда ничего не делать.
2. В `_Ready` создать **два** источника пыли (задние колёса), каждый —
   `GpuParticles3D` с `ParticleProcessMaterial`. Больше двух источников не нужно.
3. Каждый кадр (`_Process`) держать источники в точках контакта задних колёс:
   - задняя ось — `+Z` в осях машины (перед машины — `−Z`);
   - `float halfBase = (float)Target.Session.Vehicle.Spec.WheelbaseM / 2f;`
   - `float halfTrack = (float)Target.Session.Vehicle.Spec.TrackM / 2f;`
   - позиция колеса в мире: `Target.ToGlobal(new Vector3(±halfTrack, 0, halfBase))`,
     опущенная на радиус колеса: `… - Vector3.Up * (float)Target.Session.Vehicle.Spec.WheelRadiusM`.
4. Плотность от скорости: `Target.SpeedMps`. До 1 м/с — не emitting; дальше
   `AmountRatio = Mathf.Clamp((speed - 1f) / 12f, 0.05f, 1f)`, `Emitting` —
   по тому же признаку. Колёса не на земле (`Target.GroundedWheels < 2`) — не emitting.
5. Вид пыли: цвет `new Color(0.62f, 0.55f, 0.42f, 0.35f)`, `Unshaded`,
   `Transparency = Alpha`, `BillboardMode = Enabled`, `CullMode = Disabled`.
   Частица — `QuadMesh` размером примерно `0.5 × 0.5` м.
6. Движение частиц: вверх и назад относительно машины (`Direction` в осях
   материала, `Spread` ≈ 25°), `InitialVelocity` 1.5–3 м/с, гравитация
   слабая вверх (`new Vector3(0, 0.4f, 0)`), `ScaleMin/Max` 0.6–1.4,
   `Lifetime` ≈ 0.9 с, `Amount` ≤ 32 на колесо.
7. Материал и меш создаются один раз в `_Ready` и переиспользуются обоими
   источниками (никаких новых материалов каждый кадр).
8. Ничего не читать через `GetNode` в `_Process`, ничего не аллоцировать
   списками/строками. Никаких статических полей с ресурсами Godot.
9. Не менять другие файлы: `Car.cs`, `Main*` и всё в `core/` — не твои.

## Проверка

- `dotnet build` без предупреждений;
- `--selftest` (машина едет сама) остаётся зелёным;
- снимок `--shot-drive` — на нём должна быть видна пыль позади машины.
