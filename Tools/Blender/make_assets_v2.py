"""
Same Shore - second generation of the project's own models (Blender 5.x, headless):

  blender -b --factory-startup --python make_assets_v2.py -- <unity_assets_dir>

  Hatchling.fbx   sea turtle hatchling: serrated carapace with scutes, pale plastron, hooked beak,
                  big eyes, long front paddles with claws (separate flipper objects, origin at shoulder)
  Seagull.fbx     herring gull: tapered body, hooked beak with red spot, fan tail, pink legs and
                  two-segment wings (WingX_In from the shoulder, WingX_Out from the wrist) for a real flap
  NestMound.fbx   excavated sand mound with a crater and loose clumps
  HumanBody.fbx   segments of a first-person body (pivots at the proximal joint, built along +Y)
  Pebbles.fbx     6 low-poly pebbles (unit size) scattered around the hatchling at runtime

Conventions as in make_assets.py: built facing Blender +Y (front) and +Z up, metres.
"""
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix, noise

argv = sys.argv[sys.argv.index("--") + 1:]
UNITY_ASSETS = argv[0]
MODELS = os.path.join(UNITY_ASSETS, "SameShore", "Art", "Models")
TEX = os.path.join(UNITY_ASSETS, "SameShore", "Art", "Textures")
os.makedirs(MODELS, exist_ok=True)
os.makedirs(TEX, exist_ok=True)


# ----------------------------------------------------------------------------- helpers
def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for block in (bpy.data.meshes, bpy.data.materials):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def mat(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)


def assign(o, *names):
    o.data.materials.clear()
    for n in names:
        o.data.materials.append(mat(n))


def smooth(o):
    for p in o.data.polygons:
        p.use_smooth = True


def sphere(name, scale, loc=(0, 0, 0), seg=32, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=1, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    smooth(o)
    return o


def link_mesh(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    smooth(o)
    return o


def loft(name, sections, closed_ends=True):
    """sections: list of rings (list of (x,y,z)) with the same vertex count."""
    bm = bmesh.new()
    rings = [[bm.verts.new(p) for p in ring] for ring in sections]
    n = len(sections[0])
    for a, b in zip(rings[:-1], rings[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if closed_ends:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    return link_mesh(name, bm)


def ellipse_ring(cx, cy, cz, rx, rz, n=16, axis="y"):
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        if axis == "y":
            pts.append((cx + rx * math.cos(a), cy, cz + rz * math.sin(a)))
        else:
            pts.append((cx + rx * math.cos(a), cy + rz * math.sin(a), cz))
    return pts


def limb(name, length, r0, r1, n=14, segs=8, bulge=0.12, flat=1.0, material="Skin", splits=None):
    """Tapered limb along +Y from the origin (proximal joint) to `length`."""
    sections = []
    for s in range(segs + 1):
        t = s / segs
        r = (r0 * (1 - t) + r1 * t) * (1 + bulge * math.sin(math.pi * t))
        y = length * t
        # rounded caps
        if s == 0:
            r *= 0.75
        if s == segs:
            r *= 0.8
        sections.append(ellipse_ring(0, y, 0, r, r * flat, n))
    o = loft(name, sections)
    if splits:
        # splits: list of (y_threshold, material) - faces above threshold take material
        o.data.materials.clear()
        names = [material] + [m for _, m in splits]
        for nme in names:
            o.data.materials.append(mat(nme))
        for p in o.data.polygons:
            cy = sum(o.data.vertices[v].co.y for v in p.vertices) / len(p.vertices)
            idx = 0
            for k, (thr, _) in enumerate(splits):
                if (cy / length) < thr:
                    idx = k + 1
            p.material_index = idx
    else:
        assign(o, material)
    return o


def export(path, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=True, axis_forward="-Z", axis_up="Y",
        use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
        bake_anim=False, path_mode="STRIP", embed_textures=False)
    print("EXPORTED", path)


def save_png(path, rgba):
    h, w, _ = rgba.shape
    img = bpy.data.images.new(os.path.basename(path), width=w, height=h, alpha=True, float_buffer=False)
    img.pixels.foreach_set(np.ascontiguousarray(rgba, dtype=np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    print("TEX", path)


# ----------------------------------------------------------------------------- hatchling
def carapace_texture(path, size=1024):
    yy, xx = np.mgrid[0:size, 0:size] / size
    seeds = []
    for i in range(5):
        seeds.append((0.5, 0.2 + i * 0.15))
    for i in range(4):
        seeds += [(0.29, 0.26 + i * 0.155), (0.71, 0.26 + i * 0.155)]
    for i in range(12):
        a = math.pi * ((i + 0.5) / 12.0)
        seeds += [(0.5 + 0.43 * math.sin(a), 0.5 - 0.44 * math.cos(a)), (0.5 - 0.43 * math.sin(a), 0.5 - 0.44 * math.cos(a))]
    seeds = np.array(seeds)
    d = np.sqrt((xx[..., None] - seeds[:, 0]) ** 2 + (yy[..., None] - seeds[:, 1]) ** 2)
    ds = np.sort(d, axis=-1)
    edge = np.clip((ds[..., 1] - ds[..., 0]) / 0.012, 0, 1)
    idx = np.argmin(d, axis=-1)
    rng = np.random.default_rng(8)
    tint = rng.uniform(0.8, 1.2, len(seeds))[idx]
    # radial growth rings inside each scute
    rings = 0.5 + 0.5 * np.sin(ds[..., 0] * 260.0)
    grain = rng.normal(1.0, 0.06, (size, size))
    base = np.array([0.075, 0.07, 0.055])
    seam = np.array([0.17, 0.16, 0.13])
    col = base[None, None, :] * (tint * grain * (0.9 + 0.12 * rings))[..., None]
    col = col * edge[..., None] + seam[None, None, :] * (1 - edge[..., None])
    rgba = np.concatenate([np.clip(col, 0, 1), np.ones((size, size, 1))], axis=-1)
    save_png(path, rgba[::-1])


def build_hatchling():
    clear()
    parts = []
    L, W, H = 0.030, 0.023, 0.0105    # half length / half width / dome height
    bm = bmesh.new()
    nu, nv = 48, 18
    grid = []
    for j in range(nv + 1):
        v = j / nv                       # 0 = rim, 1 = apex
        row = []
        for i in range(nu):
            a = 2 * math.pi * i / nu
            ca, sa = math.cos(a), math.sin(a)
            # heart-ish outline: wider at the front, pointed tail, serrated rim
            rx = W * (1.0 + 0.08 * sa) * (1 - 0.18 * max(0.0, -sa) ** 2)
            ry = L * (1.0 - 0.06 * max(0.0, sa))
            serr = 1.0 + (0.035 * max(0.0, math.cos(a * 13)) if v < 0.05 and sa < 0.3 else 0.0)
            k = math.cos(v * math.pi / 2)
            x = rx * ca * k * serr
            y = ry * sa * k * serr
            z = H * math.sin(v * math.pi / 2) ** 0.8
            z += 0.0014 * math.exp(-(x / 0.0035) ** 2) * v                    # central keel
            z += 0.0006 * math.exp(-((abs(x) - 0.011) / 0.003) ** 2) * v       # lateral ridges
            row.append(bm.verts.new((x, y, z)))
        grid.append(row)
    for j in range(nv):
        for i in range(nu):
            k = (i + 1) % nu
            bm.faces.new((grid[j][i], grid[j][k], grid[j + 1][k], grid[j + 1][i]))
    # plastron
    bottom = [bm.verts.new((v.co.x * 0.93, v.co.y * 0.93, -0.0035)) for v in grid[0]]
    for i in range(nu):
        k = (i + 1) % nu
        bm.faces.new((grid[0][k], grid[0][i], bottom[i], bottom[k]))
    bm.faces.new(list(reversed(bottom)))
    shell = link_mesh("Shell", bm)
    shell.data.materials.append(mat("Hatchling_Shell"))
    shell.data.materials.append(mat("Hatchling_Belly"))
    for p in shell.data.polygons:
        p.material_index = 1 if p.center.z < -0.0005 else 0
    uvl = shell.data.uv_layers.new()
    for poly in shell.data.polygons:
        for li in poly.loop_indices:
            co = shell.data.vertices[shell.data.loops[li].vertex_index].co
            uvl.data[li].uv = (co.x / (2.2 * W) + 0.5, co.y / (2.2 * L) + 0.5)
    parts.append(shell)

    neck = sphere("Neck", (0.0062, 0.008, 0.0048), (0, 0.029, 0.0012), 20, 10)
    assign(neck, "Hatchling_Skin")
    parts.append(neck)
    head = sphere("Head", (0.0068, 0.0092, 0.0058), (0, 0.0375, 0.0028), 28, 14)
    for v in head.data.vertices:
        f = max(0.0, v.co.y - 0.0375) / 0.0092
        v.co.z -= 0.0022 * f * f                       # sloping snout
        v.co.x *= 1 - 0.25 * f
    assign(head, "Hatchling_Skin")
    parts.append(head)
    beak = sphere("Beak", (0.0026, 0.0028, 0.0018), (0, 0.0458, 0.0012), 12, 8)
    assign(beak, "Hatchling_Beak")
    parts.append(beak)
    for sx in (-1, 1):
        e = sphere("Eye" + ("L" if sx < 0 else "R"), (0.0021, 0.0021, 0.0021), (sx * 0.0052, 0.0405, 0.0042), 14, 8)
        assign(e, "Hatchling_Eye")
        parts.append(e)
    tail = sphere("Tail", (0.0022, 0.006, 0.0015), (0, -0.031, -0.0008), 12, 6)
    assign(tail, "Hatchling_Skin")
    parts.append(tail)

    def flipper(name, side, pivot, length, width, sweep, claw=True):
        segs, n = 12, 12
        sections = []
        for s in range(segs + 1):
            t = s / segs
            chord = width * (0.55 + 0.9 * math.sin(math.pi * min(1, t * 1.2)) ** 0.6) * (1 - 0.75 * t ** 1.6)
            thick = 0.0016 * (1 - 0.6 * t) + 0.0003
            x = length * t
            y = -0.35 * width * t * t                       # trailing curve back
            ring = []
            for i in range(n):
                a = 2 * math.pi * i / n
                ring.append((x, y + chord * 0.5 * math.cos(a) + 0.15 * chord * math.cos(a) ** 2, thick * math.sin(a)))
            sections.append(ring)
        o = loft(name, sections)
        assign(o, "Hatchling_Skin")
        if claw:
            c = sphere(name + "_Claw", (0.0012, 0.0016, 0.0006), (length * 0.32, width * 0.45, 0.0006), 8, 6)
            assign(c, "Hatchling_Beak")
            bpy.ops.object.select_all(action="DESELECT")
            c.select_set(True)
            o.select_set(True)
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.join()
        o.data.transform(Matrix.Rotation(math.radians(-sweep), 4, "Z"))
        if side < 0:
            o.data.transform(Matrix.Scale(-1, 4, Vector((1, 0, 0))))
            o.data.flip_normals()
        o.location = pivot
        return o

    parts.append(flipper("FlipperFL", -1, (-0.0165, 0.017, -0.0003), 0.046, 0.0125, 24))
    parts.append(flipper("FlipperFR", 1, (0.0165, 0.017, -0.0003), 0.046, 0.0125, 24))
    parts.append(flipper("FlipperBL", -1, (-0.0125, -0.020, -0.0008), 0.017, 0.009, 42, False))
    parts.append(flipper("FlipperBR", 1, (0.0125, -0.020, -0.0008), 0.017, 0.009, 42, False))
    export(os.path.join(MODELS, "Hatchling.fbx"), parts)
    carapace_texture(os.path.join(TEX, "Hatchling_Shell.png"))


# ----------------------------------------------------------------------------- seagull
def build_gull():
    clear()
    parts = []
    # body: lofted teardrop along Y (tail -Y, head +Y)
    sections = []
    prof = [(-0.20, 0.012, 0.010), (-0.15, 0.04, 0.035), (-0.08, 0.065, 0.062), (0.0, 0.075, 0.075),
            (0.08, 0.068, 0.07), (0.14, 0.05, 0.055), (0.18, 0.032, 0.04)]
    for y, rx, rz in prof:
        sections.append(ellipse_ring(0, y, 0.01 * (y > 0) * y * 2, rx, rz, 18))
    body = loft("Body", sections)
    body.data.materials.append(mat("Gull_White"))
    body.data.materials.append(mat("Gull_Grey"))
    for p in body.data.polygons:
        # grey mantle on the back between the wings
        p.material_index = 1 if (p.center.z > 0.045 and -0.12 < p.center.y < 0.1) else 0
    parts.append(body)
    head = sphere("Head", (0.046, 0.052, 0.044), (0, 0.215, 0.052), 24, 12)
    assign(head, "Gull_White")
    parts.append(head)
    # hooked beak
    bm = bmesh.new()
    pts = []
    for s in range(7):
        t = s / 6
        y = 0.255 + 0.06 * t
        z = 0.045 - 0.006 * t - (0.012 * max(0, t - 0.75) / 0.25 if t > 0.75 else 0)
        w = 0.011 * (1 - 0.7 * t)
        hgt = 0.012 * (1 - 0.4 * t)
        pts.append([bm.verts.new((w * math.cos(a), y, z + hgt * 0.5 * math.sin(a))) for a in np.linspace(0, 2 * math.pi, 10, endpoint=False)])
    for a, b in zip(pts[:-1], pts[1:]):
        for i in range(10):
            j = (i + 1) % 10
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(list(reversed(pts[0])))
    bm.faces.new(pts[-1])
    beak = link_mesh("Beak", bm)
    beak.data.materials.append(mat("Gull_Beak"))
    beak.data.materials.append(mat("Gull_BeakSpot"))
    for p in beak.data.polygons:
        p.material_index = 1 if (p.center.y > 0.295 and p.center.z < 0.04) else 0
    parts.append(beak)
    for sx in (-1, 1):
        e = sphere("Eye", (0.0075, 0.0075, 0.0075), (sx * 0.036, 0.235, 0.066), 10, 6)
        assign(e, "Gull_Eye")
        parts.append(e)
    # fan tail
    bm = bmesh.new()
    v0 = bm.verts.new((0, -0.17, 0.012))
    fan = [bm.verts.new((0.07 * math.sin(a), -0.17 - 0.11 * math.cos(a), 0.006)) for a in np.linspace(-0.9, 0.9, 7)]
    for a, b in zip(fan[:-1], fan[1:]):
        bm.faces.new((v0, a, b))
    tailo = link_mesh("Tail", bm)
    solid = tailo.modifiers.new("solid", "SOLIDIFY")
    solid.thickness = 0.006
    bpy.context.view_layer.objects.active = tailo
    bpy.ops.object.modifier_apply(modifier="solid")
    assign(tailo, "Gull_White")
    parts.append(tailo)
    for sx in (-1, 1):
        leg = limb("Leg" + ("L" if sx < 0 else "R"), 0.11, 0.0065, 0.005, 8, 4, 0.0, 1.0, "Gull_Legs")
        leg.data.transform(Matrix.Rotation(math.radians(180), 4, "X"))
        leg.location = (sx * 0.03, 0.0, -0.05)
        parts.append(leg)
        foot = sphere("Foot", (0.02, 0.028, 0.004), (sx * 0.03, 0.015, -0.162), 10, 6)
        assign(foot, "Gull_Legs")
        parts.append(foot)

    def wing(name, side, x0, span, chord0, chord1, sweep_back, tip=False):
        bm = bmesh.new()
        n = 6
        top, bot = [], []
        for i in range(n + 1):
            t = i / n
            x = span * t
            chord = chord0 * (1 - t) + chord1 * t
            if tip:
                chord *= (1 - 0.55 * t ** 2)
            lead = 0.015 * math.sin(t * math.pi) - sweep_back * t
            thick = 0.010 * (1 - t) + 0.002
            top.append((bm.verts.new((x, lead, thick)), bm.verts.new((x, lead - chord, thick * 0.3))))
            bot.append((bm.verts.new((x, lead, -thick * 0.4)), bm.verts.new((x, lead - chord, -0.001))))
        for i in range(n):
            a0, a1, b0, b1 = top[i], top[i + 1], bot[i], bot[i + 1]
            bm.faces.new((a0[0], a1[0], a1[1], a0[1]))
            bm.faces.new((b0[1], b1[1], b1[0], b0[0]))
            bm.faces.new((a0[0], b0[0], b1[0], a1[0]))
            bm.faces.new((a1[1], b1[1], b0[1], a0[1]))
        bm.faces.new((top[0][0], top[0][1], bot[0][1], bot[0][0]))
        bm.faces.new((top[-1][0], bot[-1][0], bot[-1][1], top[-1][1]))
        o = link_mesh(name, bm)
        o.data.materials.append(mat("Gull_Grey"))
        o.data.materials.append(mat("Gull_Black"))
        o.data.materials.append(mat("Gull_White"))
        for p in o.data.polygons:
            cx = p.center.x / span
            if tip and cx > 0.55:
                p.material_index = 1
                if 0.78 < cx < 0.86 and p.center.y < -0.03:
                    p.material_index = 2   # white "mirror" spot
        if side < 0:
            o.data.transform(Matrix.Scale(-1, 4, Vector((1, 0, 0))))
            o.data.flip_normals()
        o.location = (side * x0, 0.07, 0.05)
        return o

    for sx, tag in ((-1, "L"), (1, "R")):
        inner = wing("Wing" + tag + "_In", sx, 0.05, 0.28, 0.19, 0.15, 0.02)
        outer = wing("Wing" + tag + "_Out", sx, 0.05 + 0.28, 0.36, 0.15, 0.07, 0.08, tip=True)
        outer.location.y += -0.02
        parts += [inner, outer]
    export(os.path.join(MODELS, "Seagull.fbx"), parts)


# ----------------------------------------------------------------------------- nest
def build_nest():
    clear()
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=48, y_subdivisions=48, size=2.2)
    mound = bpy.context.active_object
    mound.name = "NestMound"
    for v in mound.data.vertices:
        r = math.hypot(v.co.x, v.co.y)
        h = 0.07 * math.exp(-(r / 0.62) ** 2)                 # low mound
        h -= 0.05 * math.exp(-(r / 0.24) ** 2)                # crater where the clutch was dug
        h += 0.008 * noise.noise(Vector((v.co.x * 9, v.co.y * 9, 0.3)))
        h *= max(0.0, 1 - (r / 1.1) ** 4)                     # blend into the beach
        v.co.z = h - 0.01
    assign(mound, "SandFill")
    smooth(mound)
    parts = [mound]
    for i in range(14):
        a = i * 2.39
        r = 0.18 + 0.32 * ((i * 37) % 10) / 10
        c = sphere("Clump%d" % i, (0.025 + 0.01 * (i % 3), 0.02 + 0.008 * (i % 2), 0.012), (r * math.cos(a), r * math.sin(a), 0.02), 8, 5)
        for v in c.data.vertices:
            v.co += Vector((noise.noise(v.co * 60), noise.noise(v.co * 60 + Vector((2, 0, 0))), 0)) * 0.004
        assign(c, "SandFill")
        parts.append(c)
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = mound
    bpy.ops.object.join()
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.cube_project(cube_size=0.6)
    bpy.ops.object.mode_set(mode="OBJECT")
    export(os.path.join(MODELS, "NestMound.fbx"), [mound])


# ----------------------------------------------------------------------------- human body
def build_body():
    clear()
    parts = []
    # torso from the hips (0) to the base of the neck (0.56), chest wider than waist
    prof = [(0.0, 0.165, 0.10), (0.06, 0.17, 0.105), (0.16, 0.15, 0.095), (0.26, 0.155, 0.1), (0.36, 0.175, 0.11),
            (0.44, 0.19, 0.115), (0.5, 0.18, 0.105), (0.54, 0.12, 0.08), (0.56, 0.06, 0.05)]
    sections = [ellipse_ring(0, y, 0, rx, rz, 20) for y, rx, rz in prof]
    torso = loft("Torso", sections)
    torso.data.materials.append(mat("Avatar_Shirt"))
    torso.data.materials.append(mat("Avatar_Shorts"))
    for p in torso.data.polygons:
        p.material_index = 1 if p.center.y < 0.1 else 0
    parts.append(torso)
    parts.append(limb("Thigh", 0.44, 0.078, 0.052, 16, 8, 0.1, 1.0, "Avatar_Skin", [(0.55, "Avatar_Shorts")]))
    parts.append(limb("Shin", 0.43, 0.052, 0.036, 14, 8, 0.15, 1.0, "Avatar_Skin"))
    parts.append(limb("UpperArm", 0.29, 0.046, 0.037, 14, 6, 0.08, 1.0, "Avatar_Skin", [(0.45, "Avatar_Shirt")]))
    parts.append(limb("Forearm", 0.26, 0.036, 0.027, 14, 6, 0.1, 1.0, "Avatar_Skin"))
    # foot: sneaker from the ankle (origin) down and forward (+Z in Unity = +Y here after export)
    foot_sections = []
    for s in range(9):
        t = s / 8
        y = -0.05 + 0.26 * t
        w = 0.042 * (0.85 + 0.25 * math.sin(math.pi * min(1, t * 1.3)))
        h = 0.045 * (1 - 0.35 * t) + 0.012
        foot_sections.append([(w * math.cos(a), y, -0.07 + h * 0.5 + h * 0.5 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 14, endpoint=False)])
    foot = loft("Foot", foot_sections)
    foot.data.materials.append(mat("Avatar_Shoe"))
    foot.data.materials.append(mat("Avatar_Sole"))
    for p in foot.data.polygons:
        p.material_index = 1 if p.center.z < -0.062 else 0
    parts.append(foot)
    # mitten hand with thumb (used when no tracked controller hand is shown)
    hand = limb("Hand", 0.17, 0.03, 0.022, 12, 6, 0.25, 0.45, "Avatar_Skin")
    thumb = limb("Thumb", 0.07, 0.012, 0.009, 8, 4, 0.1, 1.0, "Avatar_Skin")
    thumb.data.transform(Matrix.Rotation(math.radians(-35), 4, "Z"))
    thumb.location = (0.025, 0.04, 0.005)
    bpy.ops.object.select_all(action="DESELECT")
    thumb.select_set(True)
    hand.select_set(True)
    bpy.context.view_layer.objects.active = hand
    bpy.ops.object.join()
    parts.append(hand)
    export(os.path.join(MODELS, "HumanBody.fbx"), parts)


# ----------------------------------------------------------------------------- pebbles
def build_pebbles():
    clear()
    parts = []
    for i in range(6):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.5, location=(i * 1.5, 0, 0))
        o = bpy.context.active_object
        o.name = "Pebble%d" % i
        sx, sy, sz = 1.0, 0.6 + 0.08 * i, 0.38 + 0.05 * (i % 3)
        for v in o.data.vertices:
            p = v.co.copy()
            d = 1 + 0.18 * noise.noise(p * 3.1 + Vector((i * 7.0, 0, 0)))
            v.co = Vector((p.x * sx * d, p.y * sy * d, p.z * sz * d))
        o.data.transform(Matrix.Translation((-i * 1.5, 0, 0)))
        o.location = (0, 0, 0)
        assign(o, "Pebble")
        smooth(o)
        parts.append(o)
    export(os.path.join(MODELS, "Pebbles.fbx"), parts)


build_hatchling()
build_gull()
build_nest()
build_body()
build_pebbles()
print("V2 DONE")
