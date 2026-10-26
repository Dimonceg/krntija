"""Генератор низкополигональных кузовов Krntija.

Собирает кузов из сечений (лофт), добавляет стекло, бамперы, фары и выхлоп,
и пишет GLB по контракту из docs/ART.md:
  - вперёд -Z, начало координат по центру X/Z, низ кузова на Y = 0;
  - только кузов, без колёс (колёса двигает подвеска в игре).

Запуск:  python tools/gen_cars.py
Результат: assets/models/cars/<id>.glb
"""

from __future__ import annotations

import json
import math
import os
import struct

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "game", "assets", "models", "cars")


# ---------------------------------------------------------------- математика

def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0])


def norm(v):
    length = math.sqrt(v[0] ** 2 + v[1] ** 2 + v[2] ** 2) or 1.0
    return (v[0] / length, v[1] / length, v[2] / length)


def centroid(a, b, c):
    return ((a[0] + b[0] + c[0]) / 3,
            (a[1] + b[1] + c[1]) / 3,
            (a[2] + b[2] + c[2]) / 3)


# ---------------------------------------------------------------- меш

class Mesh:
    def __init__(self, center=(0, 0.7, 0)):
        self.positions = []
        self.normals = []
        self.indices = []
        self.center = center

    def triangle(self, a, b, c):
        n = norm(cross(sub(b, a), sub(c, a)))
        mid = centroid(a, b, c)
        outward = sub(mid, self.center)

        # Если нормаль смотрит внутрь — разворачиваем треугольник.
        if n[0] * outward[0] + n[1] * outward[1] + n[2] * outward[2] < 0:
            b, c = c, b
            n = (-n[0], -n[1], -n[2])

        base = len(self.positions)
        for vertex in (a, b, c):
            self.positions.append(vertex)
            self.normals.append(n)
        self.indices.extend((base, base + 1, base + 2))

    def quad(self, a, b, c, d):
        self.triangle(a, b, c)
        self.triangle(a, c, d)

    def box(self, center, size):
        cx, cy, cz = center
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        x0, x1 = cx - hx, cx + hx
        y0, y1 = cy - hy, cy + hy
        z0, z1 = cz - hz, cz + hz

        corners = [
            (x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1),
            (x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1),
        ]

        for face in ((0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4),
                     (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
            self.quad(*[corners[i] for i in face])


def ring(z, half_width, y_bottom, y_top, round_ratio=0.62):
    """Сечение кузова: скруглённый прямоугольник в плоскости X/Y."""
    inner = half_width * round_ratio
    return [
        (0.0, y_bottom, z),
        (inner, y_bottom, z),
        (half_width, y_bottom + 0.07, z),
        (half_width, y_top - 0.10, z),
        (inner, y_top, z),
        (0.0, y_top, z),
        (-inner, y_top, z),
        (-half_width, y_top - 0.10, z),
        (-half_width, y_bottom + 0.07, z),
        (-inner, y_bottom, z),
    ]


def loft(mesh, rings):
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        count = len(a)
        for j in range(count):
            k = (j + 1) % count
            mesh.quad(a[j], b[j], b[k], a[k])


def cap(mesh, ring_points, reverse=False):
    center = (
        sum(p[0] for p in ring_points) / len(ring_points),
        sum(p[1] for p in ring_points) / len(ring_points),
        sum(p[2] for p in ring_points) / len(ring_points),
    )

    for i in range(len(ring_points)):
        a = ring_points[i]
        b = ring_points[(i + 1) % len(ring_points)]
        if reverse:
            mesh.triangle(center, b, a)
        else:
            mesh.triangle(center, a, b)


# ---------------------------------------------------------------- кузов

def arch(mesh, x, z_center, y_center, inner, outer, segments=10):
    """Плоская дуга над колесом: намёк на колёсную арку."""
    for i in range(segments):
        a0 = math.pi * i / segments
        a1 = math.pi * (i + 1) / segments

        def point(angle, radius):
            return (x, y_center + radius * math.sin(angle), z_center + radius * math.cos(angle))

        mesh.quad(point(a0, inner), point(a0, outer), point(a1, outer), point(a1, inner))


def build_body(spec):
    length = spec["length"]
    width = spec["width"]
    roof = spec["roof"]
    cabin_front = spec["cabin_front"]
    cabin_back = spec["cabin_back"]

    front = -length / 2
    rear = length / 2
    half = width / 2
    sill = 0.20

    paint = Mesh(center=(0, 0.62, 0))
    glass = Mesh(center=(0, 0.95, 0))
    trim = Mesh(center=(0, 0.30, 0))
    lamp = Mesh(center=(0, 0.55, front))
    tail = Mesh(center=(0, 0.55, rear))

    # Сечения: нос, капот, лобовое со наклоном, крыша, наклон задней стойки, корма.
    round_ratio = spec.get("round", 0.62)
    nose = spec.get("nose", 0.86)

    def sec(z, half_width, y_bottom, y_top):
        return ring(z, half_width, y_bottom, y_top, round_ratio)

    rings = [
        sec(front, half * nose, 0.36, 0.58),
        sec(front + 0.22, half * 0.96, 0.30, 0.72),
        sec(front + 0.60, half, 0.26, 0.78),
        sec(cabin_front - 0.25, half, sill, 0.84),
        sec(cabin_front - 0.05, half * 0.99, sill, 0.90),
        sec(cabin_front + 0.28, half * 0.96, sill, roof * 0.93),
        sec(cabin_front + 0.55, half * 0.94, sill, roof),
        sec(cabin_back - 0.45, half * 0.94, sill, roof),
        sec(cabin_back - 0.20, half * 0.95, sill, roof * 0.97),
        sec(cabin_back + 0.10, half * 0.97, sill, roof * 0.72),
        sec(cabin_back + 0.40, half * 0.99, sill, 0.82),
        sec(rear - 0.25, half * 0.99, 0.24, 0.78),
        sec(rear, half * 0.82, 0.32, 0.62),
    ]
    loft(paint, rings)
    cap(paint, rings[0], reverse=True)
    cap(paint, rings[-1])

    # Лобовое и заднее стекло — по наклону сечений.
    glass.quad(
        (-half * 0.84, 0.88, cabin_front - 0.04),
        (half * 0.84, 0.88, cabin_front - 0.04),
        (half * 0.78, roof - 0.02, cabin_front + 0.52),
        (-half * 0.78, roof - 0.02, cabin_front + 0.52),
    )
    glass.quad(
        (half * 0.80, roof - 0.03, cabin_back - 0.16),
        (-half * 0.80, roof - 0.03, cabin_back - 0.16),
        (-half * 0.78, 0.88, cabin_back + 0.40),
        (half * 0.78, 0.88, cabin_back + 0.40),
    )

    # Боковые окна: два, с центральной стойкой.
    for side in (-1, 1):
        x = side * (half * 0.955 + 0.004)
        pairs = ((cabin_front + 0.06, cabin_front + 0.56), (cabin_front + 0.66, cabin_back - 0.02))
        for z0, z1 in pairs:
            glass.quad(
                (x, 0.77, z0),
                (x, 0.77, z1),
                (x, roof - 0.05, z1),
                (x, roof - 0.05, z0),
            )

        # Стойка между окнами.
        trim.box((side * (half * 0.96), (0.77 + roof - 0.05) / 2, cabin_front + 0.61),
                 (0.015, roof - 0.82, 0.05))

        # Молдинг по борту и колёсные арки.
        trim.box((side * (half + 0.006), 0.54, 0), (0.02, 0.05, length * 0.62))
        arch(trim, side * (half + 0.004), -1.45, 0.36, 0.38, 0.46)
        arch(trim, side * (half + 0.004), 1.45, 0.36, 0.38, 0.46)

    # Бамперы, решётка, порог, номер.
    trim.box((0, 0.36, front + 0.03), (width * 0.94, 0.16, 0.12))
    trim.box((0, 0.36, rear - 0.03), (width * 0.94, 0.16, 0.12))
    trim.box((0, 0.58, front - 0.005), (width * 0.52, 0.15, 0.05))
    trim.box((0, sill - 0.02, 0), (width * 0.96, 0.08, length * 0.72))
    trim.box((0, 0.42, front - 0.02), (0.34, 0.12, 0.02))

    # Фары и стопы.
    for side in (-1, 1):
        lamp.box((side * half * 0.60, 0.64, front - 0.004), (0.32, 0.17, 0.05))
        lamp.box((side * half * 0.84, 0.52, front - 0.004), (0.12, 0.10, 0.05))
        tail.box((side * half * 0.62, 0.62, rear + 0.004), (0.30, 0.15, 0.05))

    # По контракту начало координат — центр шасси: низ кузова на -0.15.
    shift = -0.15 - min(p[1] for p in paint.positions)
    for mesh in (paint, glass, trim, lamp, tail):
        mesh.positions = [(x, y + shift, z) for (x, y, z) in mesh.positions]

    return {"paint": paint, "glass": glass, "trim": trim, "lamp": lamp, "tail": tail}

PAINTS = {
    "zastava_101": [0.80, 0.78, 0.70, 1.0],
    "zastava_boxy": [0.80, 0.78, 0.70, 1.0],
    "zastava_rounded": [0.52, 0.12, 0.10, 1.0],
    "zastava_hatch": [0.72, 0.62, 0.30, 1.0],
    "golf_2": [0.52, 0.12, 0.10, 1.0],
    "vaz_2106": [0.72, 0.62, 0.30, 1.0],
    "zaz_968": [0.60, 0.44, 0.24, 1.0],
}



def material(color, metallic, roughness, emissive=None):
    entry = {"pbrMetallicRoughness": {
        "baseColorFactor": color,
        "metallicFactor": metallic,
        "roughnessFactor": roughness,
    }}
    if emissive is not None:
        entry["emissiveFactor"] = emissive
    return entry


MATERIALS = {
    "paint": material([0.62, 0.42, 0.22, 1.0], 0.05, 0.72),
    "glass": {**material([0.07, 0.10, 0.12, 1.0], 0.2, 0.18), "doubleSided": True},
    "trim": material([0.14, 0.14, 0.15, 1.0], 0.1, 0.85),
    "lamp": material([1.0, 0.95, 0.8, 1.0], 0.0, 0.3, [1.0, 0.9, 0.65]),
    "tail": material([0.8, 0.1, 0.08, 1.0], 0.0, 0.35, [0.6, 0.04, 0.02]),
}


# ---------------------------------------------------------------- GLB

DEFAULT_PAINT = [0.80, 0.78, 0.70, 1.0]


def build_materials(meshes, paint_color):
    result = []
    for name, mesh in meshes.items():
        if not mesh.indices:
            continue
        result.append(material(paint_color, 0.05, 0.72) if name == "paint" else MATERIALS[name])
    return result


def build_glb(meshes, paint_color=DEFAULT_PAINT):
    buffer = bytearray()
    accessors = []
    buffer_views = []
    primitives = []

    def align():
        while len(buffer) % 4:
            buffer.append(0)

    for name, mesh in meshes.items():
        if not mesh.indices:
            continue

        align()
        position_offset = len(buffer)
        for p in mesh.positions:
            buffer.extend(struct.pack("<3f", *p))
        position_length = len(buffer) - position_offset

        align()
        normal_offset = len(buffer)
        for n in mesh.normals:
            buffer.extend(struct.pack("<3f", *n))
        normal_length = len(buffer) - normal_offset

        align()
        index_offset = len(buffer)
        for i in mesh.indices:
            buffer.extend(struct.pack("<I", i))
        index_length = len(buffer) - index_offset

        buffer_views.append({"buffer": 0, "byteOffset": position_offset, "byteLength": position_length})
        position_accessor = len(accessors)
        accessors.append({
            "bufferView": len(buffer_views) - 1,
            "componentType": 5126,
            "count": len(mesh.positions),
            "type": "VEC3",
            "min": [min(p[axis] for p in mesh.positions) for axis in range(3)],
            "max": [max(p[axis] for p in mesh.positions) for axis in range(3)],
        })

        buffer_views.append({"buffer": 0, "byteOffset": normal_offset, "byteLength": normal_length})
        normal_accessor = len(accessors)
        accessors.append({
            "bufferView": len(buffer_views) - 1,
            "componentType": 5126,
            "count": len(mesh.normals),
            "type": "VEC3",
        })

        buffer_views.append({"buffer": 0, "byteOffset": index_offset, "byteLength": index_length})
        index_accessor = len(accessors)
        accessors.append({
            "bufferView": len(buffer_views) - 1,
            "componentType": 5125,
            "count": len(mesh.indices),
            "type": "SCALAR",
        })

        primitives.append({
            "attributes": {"POSITION": position_accessor, "NORMAL": normal_accessor},
            "indices": index_accessor,
            "material": len(primitives),
        })

    gltf = {
        "asset": {"version": "2.0", "generator": "krntija-gen-cars"},
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": [{"mesh": 0, "name": "car_body"}],
        "meshes": [{"primitives": primitives}],
        "materials": build_materials(meshes, paint_color),
        "accessors": accessors,
        "bufferViews": buffer_views,
        "buffers": [{"byteLength": len(buffer)}],
    }

    align()
    json_chunk = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    while len(json_chunk) % 4:
        json_chunk += b" "

    total = 12 + 8 + len(json_chunk) + 8 + len(buffer)
    glb = bytearray()
    glb.extend(struct.pack("<III", 0x46546C67, 2, total))
    glb.extend(struct.pack("<II", len(json_chunk), 0x4E4F534A))
    glb.extend(json_chunk)
    glb.extend(struct.pack("<II", len(buffer), 0x004E4942))
    glb.extend(buffer)

    return bytes(glb)


# ---------------------------------------------------------------- модели

SPECS = {
    "zastava_101": dict(length=3.96, width=1.60, roof=1.16, cabin_front=-0.95, cabin_back=1.15, round=0.95, nose=0.97),
    "golf_2": dict(length=4.02, width=1.66, roof=1.26, cabin_front=-0.70, cabin_back=1.02),
    "vaz_2106": dict(length=4.04, width=1.60, roof=1.24, cabin_front=-0.66, cabin_back=0.98),
    "zaz_968": dict(length=3.72, width=1.54, roof=1.18, cabin_front=-0.58, cabin_back=0.90),
    "zastava_boxy": dict(length=3.96, width=1.60, roof=1.16, cabin_front=-0.95, cabin_back=0.85,
                         round=0.95, nose=0.97),
    "zastava_rounded": dict(length=3.96, width=1.60, roof=1.22, cabin_front=-0.62, cabin_back=0.95,
                            round=0.62, nose=0.86),
    "zastava_hatch": dict(length=3.96, width=1.60, roof=1.18, cabin_front=-0.80, cabin_back=1.28,
                          round=0.72, nose=0.90),
}


def main():
    os.makedirs(OUT_DIR, exist_ok=True)

    for car_id, color in PAINTS.items():
        if car_id in SPECS:
            SPECS[car_id]["paint"] = color

    for car_id, spec in SPECS.items():
        if car_id in ("zastava_101", "golf_2", "vaz_2106"):
            print(f"{car_id}: authored Blender model, skipping")
            continue
        meshes = build_body(spec)
        data = build_glb(meshes, spec.get("paint", DEFAULT_PAINT))
        path = os.path.join(OUT_DIR, f"{car_id}.glb")
        with open(path, "wb") as handle:
            handle.write(data)
        print(f"{car_id}.glb  {len(data) / 1024:.0f} KB")


if __name__ == "__main__":
    main()
