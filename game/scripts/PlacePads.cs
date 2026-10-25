using System;
using System.Collections.Generic;
using Godot;
using Krntija.Core.WorldGen;

namespace Krntija.Game;

/// <summary>
/// Ровные площадки под местами (ADR 0002, фаза 3): внутри <see cref="Footprint"/>
/// земля плоская на средней высоте рельефа, дальше за <see cref="Blend"/> метров
/// плавно возвращается к холмам. Иначе дом на склоне висит одним углом и тонет другим.
/// </summary>
/// <remarks>
/// Площадки куска k берутся из планов кусков k−1…k+1: место у края куска меняет
/// землю соседа, и на общем шве оба куска должны считать одинаково. Места дальше
/// соседнего куска до шва не достают (кусок 256 м, площадка с краем ≤ 30 м).
/// </remarks>
public sealed class PlacePads
{
    /// <summary>Ширина перехода от площадки к рельефу, метры.</summary>
    public const float Blend = 10f;

    /// <summary>Запас вокруг габарита: дорожка вокруг дома тоже ровная.</summary>
    private const double Margin = 1.5;

    private readonly record struct Pad(
        double X, double Z, double RightX, double RightZ, double HalfWidth, double HalfDepth,
        float Height, double S, double OffsetX, double ExtentX);

    private readonly List<Pad> _pads = new();

    public PlacePads(RoadCurve curve, ulong seed, int chunkIndex)
    {
        for (int index = Math.Max(0, chunkIndex - 1); index <= chunkIndex + 1; index++)
        {
            foreach (var place in WorldGenerator.Plan(seed, index).Places)
            {
                if (place.OnRoad) continue; // блокпост стоит на полотне — там своя высота

                var (x, z) = PlaceGeometry.Center(place, curve);
                double rightX = Math.Cos(place.Yaw), rightZ = Math.Sin(place.Yaw);
                double halfWidth = place.Footprint.HalfWidth + Margin;
                double halfDepth = place.Footprint.HalfDepth + Margin;

                // Высота площадки — среднее рельефа по габариту: меньше всего срезать и насыпать.
                float sum = 0;
                int count = 0;
                for (double u = -1; u <= 1.001; u += 0.5)
                for (double v = -1; v <= 1.001; v += 0.5)
                {
                    double px = x + rightX * u * halfWidth + rightZ * v * halfDepth;
                    double pz = z + rightZ * u * halfWidth - rightX * v * halfDepth;
                    sum += ChunkBuilder.Ground(curve, px, pz).Height;
                    count++;
                }

                // Насколько площадка тянется по X сцены: столбцы сетки земли идут по X.
                double extentX = Math.Abs(rightX) * halfWidth + Math.Abs(rightZ) * halfDepth;
                _pads.Add(new Pad(x, z, rightX, rightZ, halfWidth, halfDepth, sum / count,
                    place.S, x - curve.Lateral(-z), extentX));
            }
        }
    }

    public int Count => _pads.Count;

    /// <summary>Высота земли с площадками. <paramref name="roadDistance"/> — от оси дороги: у полотна не трогаем.</summary>
    public float Apply(double x, double z, float height, float roadDistance)
    {
        const float roadHalf = RoadMeshBuilder.Width * 0.5f;
        float nearRoad = Mathf.SmoothStep(roadHalf + 1.5f, roadHalf + 5f, roadDistance);
        if (nearRoad <= 0) return height;

        foreach (var pad in _pads)
        {
            double dx = x - pad.X, dz = z - pad.Z;
            // Оси места: ширина — вправо (cos, sin), глубина — вдоль дороги (sin, −cos).
            double across = Math.Abs(dx * pad.RightX + dz * pad.RightZ) - pad.HalfWidth;
            double along = Math.Abs(dx * pad.RightZ - dz * pad.RightX) - pad.HalfDepth;
            if (across > Blend || along > Blend) continue;

            double outside = Math.Sqrt(Math.Pow(Math.Max(across, 0), 2) + Math.Pow(Math.Max(along, 0), 2));
            float weight = (1 - Mathf.SmoothStep(0, Blend, (float)outside)) * nearRoad;
            height = Mathf.Lerp(height, pad.Height, weight);
        }

        return height;
    }

    /// <summary>
    /// Дополнительные столбцы сетки земли у площадок куска: базовые столбцы на
    /// 36–95 м идут через 14–25 м и площадку бы не заметили.
    /// </summary>
    /// <param name="blendColumns">Столбцы и в переходе (меш). Коллизии хватает краёв площадки:
    /// каждый столбец — 33–64 вызова <see cref="ChunkBuilder.Ground"/>, а шаг ограничен 4 мс.</param>
    public float[] Columns(float[] baseOffsets, double startS, double endS, bool blendColumns = true)
    {
        var columns = new List<float>(baseOffsets);
        foreach (var pad in _pads)
        {
            double reach = pad.ExtentX + Blend;
            if (pad.S < startS - reach || pad.S > endS + reach) continue;

            // Внутри площадки земля плоская — хватит краёв; в переходе — середина и конец.
            foreach (double side in new[] { -1.0, 1.0 })
                foreach (double edge in blendColumns ? new[] { pad.ExtentX, pad.ExtentX + Blend * 0.5, reach } : new[] { pad.ExtentX })
                    TryAdd(columns, (float)(pad.OffsetX + side * edge));
        }

        columns.Sort();
        return columns.ToArray();
    }

    private static void TryAdd(List<float> columns, float offset)
    {
        // У дороги сетка и так густая; за краем ближней земли — дальние горы.
        if (Math.Abs(offset) < 10f || Math.Abs(offset) > 148f) return;
        foreach (float existing in columns)
            if (Math.Abs(existing - offset) < 1.5f) return;
        columns.Add(offset);
    }
}
