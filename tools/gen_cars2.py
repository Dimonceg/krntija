"""Car body v2: extruded side profile with real wheel arch cut-outs,
plus a separate greenhouse with inset glass.

Run:  python tools/gen_cars2.py
Out:  game/assets/models/cars/<id>.glb
"""

from __future__ import annotations

import math
import os

from gen_cars import Mesh, SPECS, build_glb, DEFAULT_PAINT

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "game", "assets", "models", "cars")

# All heights are relative to the chassis origin (y = 0).
WHEEL_Z = 1.45
WHEEL_Y = -0.24
ARCH_R = 0.36
SILL = -0.34
BELT = 0.34
ROOF = 0.84


# ---------------------------------------------------------------- 2D helpers

def polygon_triangles(points):
    indices = list(range(len(points)))
    triangles = []

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    def inside(p, a, b, c):
        d1 = cross(a, b, p)
        d2 = cross(b, c, p)
        d3 = cross(c, a, p)
        return (d1 >= 0 and d2 >= 0 and d3 >= 0) or (d1 <= 0 and d2 <= 0 and d3 <= 0)

    guard = 0
    while len(indices) > 3 and guard < 5000:
        guard += 1
        clipped = False

        for i in range(len(indices)):
            i0, i1, i2 = indices[i - 1], indices[i], indices[(i + 1) % len(indices)]
            a, b, c = points[i0], points[i1], points[i2]
            if cross(a, b, c) <= 1e-9:
                continue
            if any(inside(points[j], a, b, c) for j in indices if j not in (i0, i1, i2)):
                continue

            triangles.append((i0, i1, i2))
            indices.pop(i)
            clipped = True
            break

        if not clipped:
            break

    if len(indices) == 3:
        triangles.append(tuple(indices))

    return triangles


def ensure_ccw(points):
    n = len(points)
    area = sum(points[i][0] * points[(i + 1) % n][1] - points[(i + 1) % n][0] * points[i][1]
               for i in range(n))
    return points if area > 0 else list(reversed(points))


def arch_points(center_z, steps=8):
    """Wheel arch from sill level over the top and back down to sill level."""
    drop = max(-1.0, min(1.0, (SILL - WHEEL_Y) / ARCH_R))
    start = math.degrees(math.asin(drop))
    result = []
    for i in range(steps + 1):
        angle = math.radians(start + (180 - 2 * start) * i / steps)
        result.append((center_z + ARCH_R * math.cos(angle), WHEEL_Y + ARCH_R * math.sin(angle)))
    return result


def body_profile(spec):
    length = spec["length"]
    front = -length / 2
    rear = length / 2

    return [
        (front + 0.01, -0.12),
        (front + 0.01, 0.07),
        (front + 0.12, 0.10),
        (front + 0.60, 0.14),
        (-0.90, 0.16),
        (-0.90, BELT),
        (1.30, BELT),
        (1.86, 0.24),
        (rear - 0.02, 0.10),
        (rear - 0.02, -0.08),
        (rear - 0.04, SILL),
    ] + arch_points(WHEEL_Z) + [
        (-WHEEL_Z + ARCH_R * 0.95, SILL),
    ] + arch_points(-WHEEL_Z) + [
        (front + 0.04, SILL),
    ]


def width_at(z, width, length):
    t = min(1.0, abs(z) / (length / 2))
    return width * (0.98 - 0.07 * t * t)


def side_factor(y):
    """Almost flat sides: only a hint of roundness, so the car reads angular."""
    t = (y - 0.02) / 0.46
    return 1.0 - 0.04 * min(1.0, t * t)


def extrude(mesh, profile, width, length):
    profile = ensure_ccw(profile)
    left = [(width_at(z, width, length) / 2 * side_factor(y), y, z) for (z, y) in profile]
    right = [(-width_at(z, width, length) / 2 * side_factor(y), y, z) for (z, y) in profile]

    for a, b, c in polygon_triangles(profile):
        mesh.triangle(right[a], right[b], right[c])
        mesh.triangle(left[c], left[b], left[a])

    count = len(profile)
    for i in range(count):
        j = (i + 1) % count
        mesh.quad(left[i], left[j], right[j], right[i])


def build_cabin(paint, glass, spec):
    length = spec["length"]
    width = spec["width"] * 0.92

    profile = ensure_ccw([(-0.90, BELT), (-0.42, ROOF), (0.86, ROOF), (1.30, BELT)])

    def half_at(z, y):
        taper = 0.96 if y > BELT + 0.01 else 1.0
        return width_at(z, width, length) / 2 * taper * side_factor(y)

    left = [(half_at(z, y), y, z) for (z, y) in profile]
    right = [(-half_at(z, y), y, z) for (z, y) in profile]

    # Sides are the windows.
    glass.quad(right[0], right[1], right[2], right[3])
    glass.quad(left[3], left[2], left[1], left[0])

    # Windscreen and rear glass.
    glass.quad(left[0], right[0], right[1], left[1])
    glass.quad(right[3], left[3], left[2], right[2])

    # Roof.
    paint.quad(left[1], right[1], right[2], left[2])

    # Pillars: strips in the same plane as the glass, so they never poke out.
    for side in (-1, 1):
        for z0 in (-0.87, 0.04, 1.10):
            z1 = z0 + 0.07
            xb = side * (half_at(z0, BELT) + 0.002)
            xr = side * (half_at(z0, ROOF) + 0.002)
            paint.quad((xb, BELT, z0), (xb, BELT, z1), (xr, ROOF, z1), (xr, ROOF, z0))


def build_body(spec):
    length = spec["length"]
    width = spec["width"]
    front = -length / 2
    rear = length / 2
    half = width / 2

    paint = Mesh(center=(0, 0.3, 0))
    glass = Mesh(center=(0, 0.6, 0))
    trim = Mesh(center=(0, 0.0, 0))
    lamp = Mesh(center=(0, 0.1, front))
    tail = Mesh(center=(0, 0.1, rear))

    extrude(paint, body_profile(spec), width, length)
    build_cabin(paint, glass, spec)

    # Bumpers.
    trim.box((0, -0.11, front + 0.03), (width * 0.94, 0.11, 0.12))
    trim.box((0, -0.11, rear - 0.03), (width * 0.94, 0.11, 0.12))

    # Grille and plates.
    trim.box((0, -0.01, front - 0.008), (width * 0.36, 0.11, 0.03))
    trim.box((0, -0.20, front + 0.02), (0.34, 0.09, 0.02))
    trim.box((0, -0.02, rear + 0.005), (0.34, 0.11, 0.02))

    for k in range(-2, 3):
        trim.box((k * 0.09, -0.01, front - 0.02), (0.016, 0.10, 0.012))

    # Hatch seam and handle on the tailgate.
    trim.box((0, 0.31, rear - 0.05), (width * 0.62, 0.02, 0.02))
    trim.box((0, 0.16, rear - 0.03), (0.26, 0.03, 0.02))

    # Lights.
    for side in (-1, 1):
        lamp.box((side * half * 0.56, 0.00, front - 0.006), (0.32, 0.14, 0.05))
        lamp.box((side * half * 0.82, -0.075, front - 0.006), (0.11, 0.07, 0.05))
        tail.box((side * half * 0.58, 0.00, rear + 0.006), (0.26, 0.14, 0.05))

    # Door seams, handles, mirrors, side moulding.
    for side in (-1, 1):
        x = side * (half * 0.995)
        for z in (-0.36, 0.44, 1.06):
            trim.box((x, 0.05, z), (0.012, 0.26, 0.014))

        trim.box((side * (half + 0.012), 0.13, -0.06), (0.02, 0.03, 0.13))
        trim.box((side * (half + 0.012), 0.13, 0.74), (0.02, 0.03, 0.13))
        trim.box((side * (half + 0.10), 0.31, -0.82), (0.16, 0.09, 0.05))
        trim.box((side * (half + 0.004), 0.06, 0.32), (0.014, 0.035, 1.34))

    return {"paint": paint, "glass": glass, "trim": trim, "lamp": lamp, "tail": tail}


def main():
    os.makedirs(OUT_DIR, exist_ok=True)

    for car_id, spec in SPECS.items():
        if car_id in ("zastava_101", "golf_2"):
            print(f"{car_id}: authored Blender model, skipping")
            continue
        meshes = build_body(spec)
        data = build_glb(meshes, spec.get("paint", DEFAULT_PAINT))
        with open(os.path.join(OUT_DIR, f"{car_id}.glb"), "wb") as handle:
            handle.write(data)
        print(f"{car_id}.glb  {len(data) / 1024:.0f} KB")


if __name__ == "__main__":
    main()
