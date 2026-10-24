namespace Krntija.Core.WorldGen;

/// <summary>
/// Геометрия мест: где стоит место на карте и пересекаются ли два места.
/// Нужно, чтобы проверять расстановку формой, а не «слоты по станции».
/// </summary>
public static class PlaceGeometry
{
    /// <summary>Мировой центр места: смещение от оси дороги с учётом станции.</summary>
    public static (double X, double Z) Center(PlacePlan place, RoadCurve curve)
    {
        var point = curve.Offset(place.S, place.Lateral);
        return (point.X, point.Z);
    }

    /// <summary>Углы прямоугольника места в мировых координатах (XZ).</summary>
    public static (double X, double Z)[] Corners(PlacePlan place, RoadCurve curve)
    {
        var (cx, cz) = Center(place, curve);

        double halfWidth = place.Footprint.HalfWidth;
        double halfDepth = place.Footprint.HalfDepth;

        // Ось места: вперёд (глубина) по курсу места, вбок (ширина) — вправо.
        double forwardX = Math.Sin(place.Yaw);
        double forwardZ = Math.Cos(place.Yaw);
        double rightX = Math.Cos(place.Yaw);
        double rightZ = -Math.Sin(place.Yaw);

        return new[]
        {
            (cx + rightX * halfWidth + forwardX * halfDepth, cz + rightZ * halfWidth + forwardZ * halfDepth),
            (cx + rightX * halfWidth - forwardX * halfDepth, cz + rightZ * halfWidth - forwardZ * halfDepth),
            (cx - rightX * halfWidth + forwardX * halfDepth, cz - rightZ * halfWidth + forwardZ * halfDepth),
            (cx - rightX * halfWidth - forwardX * halfDepth, cz - rightZ * halfWidth - forwardZ * halfDepth),
        };
    }

    /// <summary>Пересекаются ли два места (по осям, с отступом margin).</summary>
    public static bool Overlaps(PlacePlan first, PlacePlan second, RoadCurve curve, double margin = 2.0)
    {
        var a = Corners(first, curve);
        var b = Corners(second, curve);

        // Разделяющая ось: нормали к сторонам обоих прямоугольников.
        var axes = new List<(double X, double Z)>();
        for (int shape = 0; shape < 2; shape++)
        {
            var corners = shape == 0 ? a : b;
            for (int i = 0; i < 2; i++)
            {
                double dx = corners[i + 1].X - corners[i].X;
                double dz = corners[i + 1].Z - corners[i].Z;
                double length = Math.Sqrt(dx * dx + dz * dz);
                if (length < 1e-9) continue;
                axes.Add((-dz / length, dx / length));
            }
        }

        foreach (var axis in axes)
        {
            var (minA, maxA) = Project(a, axis);
            var (minB, maxB) = Project(b, axis);

            if (maxA + margin <= minB || maxB + margin <= minA) return false;
        }

        return true;
    }

    private static (double Min, double Max) Project((double X, double Z)[] corners, (double X, double Z) axis)
    {
        double min = double.MaxValue;
        double max = double.MinValue;

        foreach (var corner in corners)
        {
            double value = corner.X * axis.X + corner.Z * axis.Z;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        return (min, max);
    }
}
