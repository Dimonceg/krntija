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

def build_body(spec):
    """Возвращает словарь: имя материала -> список (mesh, material)."""
    length = spec["length"]
    width = spec["width"]
    roof = spec["roof"]
    cabin_back = spec["cabin_back"]
    cabin_front = spec["cabin_front"]

    front = -length / 2
    rear = length / 2
    half = width / 2

    paint = Mesh(center=(0, 0.6, 0))
    glass = Mesh(center=(0, 0.9, 0))
    trim = Mesh(center=(0, 0.3, 0))
    lamp = Mesh(center=(0, 0.5, front))
    tail = Mesh(center=(0, 0.5, rear))

    # Сечения вдоль кузова: нос, капот, лобовое, крыша, заднее стекло, багажник.
    rings = [
        ring(front, half * 0.82, 0.30, 0.58),
        ring(front + 0.28, half * 0.95, 0.24, 0.72),
        ring(front + 0.75, half, 0.20, 0.80),
        ring(cabin_front - 0.10, half, 0.18, 0.86),
        ring(cabin_front, half * 0.97, 0.18, roof * 0.86),
        ring(cabin_front + 0.18, half * 0.94, 0.18, roof),
        ring(cabin_back - 0.18, half * 0.94, 0.18, roof),
        ring(cabin_back, half * 0.96, 0.18, roof * 0.82),
        ring(cabin_back + 0.25, half, 0.18, 0.84),
        ring(rear - 0.30, half * 0.98, 0.22, 0.76),
        ring(rear, half * 0.82, 0.30, 0.62),
    ]

    loft(paint, rings)
    cap(paint, rings[0], reverse=True)
    cap(paint, rings[-1])

    # Стёкла: лобовое, заднее и два боковых, чуть вынесены наружу.
    glass.quad(
        (-half * 0.80, 0.86, cabin_front - 0.10),
        (half * 0.80, 0.86, cabin_front - 0.10),
        (half * 0.72, roof - 0.02, cabin_front + 0.20),
        (-half * 0.72, roof - 0.02, cabin_front + 0.20),
    )
    glass.quad(
        (half * 0.80, 0.86, cabin_back + 0.24),
        (-half * 0.80, 0.86, cabin_back + 0.24),
        (-half * 0.72, roof - 0.02, cabin_back - 0.18),
        (half * 0.72, roof - 0.02, cabin_back - 0.18),
    )
    for side in (-1, 1):
        x = side * (half * 0.95 + 0.004)
        glass.quad(
            (x, 0.90, cabin_front + 0.02),
            (x, 0.90, cabin_back - 0.02),
            (x, roof - 0.06, cabin_back - 0.05),
            (x, roof - 0.06, cabin_front + 0.05),
        )

    # Бамперы, решётка, порог.
    trim.box((0, 0.34, front + 0.02), (width * 0.92, 0.16, 0.12))
    trim.box((0, 0.34, rear - 0.02), (width * 0.92, 0.16, 0.12))
    trim.box((0, 0.56, front - 0.01), (width * 0.55, 0.14, 0.05))
    trim.box((0, 0.16, 0), (width * 0.94, 0.10, length * 0.78))

    # Фары и стопы.
    for side in (-1, 1):
        lamp.box((side * half * 0.62, 0.62, front - 0.005), (0.30, 0.16, 0.04))
        tail.box((side * half * 0.64, 0.60, rear + 0.005), (0.28, 0.14, 0.04))

    # Номерной знак.
    trim.box((0, 0.40, front - 0.02), (0.34, 0.12, 0.02))

    return {"paint": paint, "glass": glass, "trim": trim, "lamp": lamp, "tail": tail}


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

def build_glb(meshes):
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
        "materials": [MATERIALS[name] for name in meshes if meshes[name].indices],
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
    "zastava_101": dict(length=3.96, width=1.58, roof=1.34, cabin_front=-0.42, cabin_back=0.86),
    "golf_2": dict(length=4.02, width=1.66, roof=1.38, cabin_front=-0.50, cabin_back=0.92),
    "vaz_2106": dict(length=4.04, width=1.60, roof=1.36, cabin_front=-0.46, cabin_back=0.88),
    "zaz_968": dict(length=3.72, width=1.54, roof=1.30, cabin_front=-0.38, cabin_back=0.80),
}


def main():
    os.makedirs(OUT_DIR, exist_ok=True)

    for car_id, spec in SPECS.items():
        meshes = build_body(spec)
        data = build_glb(meshes)
        path = os.path.join(OUT_DIR, f"{car_id}.glb")
        with open(path, "wb") as handle:
            handle.write(data)
        print(f"{car_id}.glb  {len(data) / 1024:.0f} KB")


if __name__ == "__main__":
    main()
