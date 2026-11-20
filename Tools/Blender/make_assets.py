"""
Same Shore - Blender asset pipeline (Blender 5.x, run headless):

  blender -b --factory-startup --python make_assets.py -- <polyhaven_download_dir> <unity_assets_dir>

1. Optimises the CC0 Poly Haven models (decimates photogrammetry, converts EXR maps to
   PNG, packs roughness into a URP metallic/smoothness map).
2. Builds the project's own models procedurally (hatchling with separate flippers, seagull
   with separate wings, PET / glass bottles, plastic bag, sandcastle).

Conventions: every model is built facing Blender +Y (head/front) with Z up, metres.
The Unity scene builder verifies orientation at import, so no manual fix-up is needed.
"""
import bpy, bmesh, math, os, random, sys, shutil
import numpy as np
from mathutils import Vector, Matrix, noise

argv = sys.argv[sys.argv.index("--") + 1:]
PH_DIR, UNITY_ASSETS = argv[0], argv[1]
ART = os.path.join(UNITY_ASSETS, "SameShore", "Art")
MODELS = os.path.join(ART, "Models")
TEX = os.path.join(ART, "Textures")
PH_OUT = os.path.join(ART, "ThirdParty", "PolyHaven")
for d in (MODELS, TEX, PH_OUT):
    os.makedirs(d, exist_ok=True)

random.seed(7)


# --------------------------------------------------------------------------- helpers
def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    return m


def assign(obj, *names):
    obj.data.materials.clear()
    for n in names:
        obj.data.materials.append(mat(n))


def apply_all(obj):
    bpy.context.view_layer.objects.active = obj
    for m in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def shade_smooth(obj, angle=60):
    for p in obj.data.polygons:
        p.use_smooth = True
    try:
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.shade_auto_smooth(angle=math.radians(angle))
        apply_all(obj)
    except Exception:
        pass


def export_fbx(path, objects):
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


def uv_sphere(name, radius=1.0, seg=32, rings=16, loc=(0, 0, 0), scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=radius, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return o


def set_origin(obj, point):
    """Move the object origin to a world point without moving the geometry."""
    offset = Vector(point) - obj.location
    obj.data.transform(Matrix.Translation(-offset))
    obj.location = Vector(point)


def lathe(name, profile, seg=40):
    """profile: list of (radius, height) from bottom to top; returns closed revolved mesh."""
    bm = bmesh.new()
    rings = []
    for r, h in profile:
        ring = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a), h)))
        rings.append(ring)
    for a, b in zip(rings[:-1], rings[1:]):
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
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
    bpy.ops.uv.cylinder_project()
    bpy.ops.object.mode_set(mode="OBJECT")
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def save_png(path, rgba, non_color=False):
    h, w, _ = rgba.shape
    img = bpy.data.images.new(os.path.basename(path), width=w, height=h, alpha=True, float_buffer=False)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    img.pixels.foreach_set(np.ascontiguousarray(rgba, dtype=np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    print("TEX", path)


def load_pixels(path):
    img = bpy.data.images.load(path)
    img.colorspace_settings.name = "Non-Color"
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(h, w, 4)


# --------------------------------------------------------------------------- Poly Haven
PH_TARGET_TRIS = {
    "coast_rocks_01": 9000, "sand_rocks_small_01": 9000, "can_rusted": 1500,
    "small_plastic_torch": 4000, "lambis_shell": 1500, "dead_quiver_branch_01": 2500,
    "grass_medium_01": 6000, "street_lamp_01": 6000, "plastic_container": 1500,
}


def tri_count(objs):
    return sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs if o.type == "MESH")


def process_polyhaven(aid):
    src_dir = os.path.join(PH_DIR, aid)
    fbx = [f for f in os.listdir(src_dir) if f.endswith(".fbx")][0]
    clear_scene()
    bpy.ops.import_scene.fbx(filepath=os.path.join(src_dir, fbx))
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    target = PH_TARGET_TRIS.get(aid, 5000)
    total = tri_count(objs)
    if total > target:
        ratio = max(0.002, target / total)
        for o in objs:
            m = o.modifiers.new("dec", "DECIMATE")
            m.ratio = ratio
            apply_all(o)
    print(f"PH {aid}: {total} -> {tri_count(objs)} tris, {len(objs)} meshes")
    out_dir = os.path.join(PH_OUT, aid)
    os.makedirs(out_dir, exist_ok=True)
    roots = [o for o in bpy.context.scene.objects if o.parent is None]
    export_fbx(os.path.join(out_dir, aid + ".fbx"), list(bpy.context.scene.objects))

    tdir = os.path.join(src_dir, "textures")
    for f in os.listdir(tdir):
        p = os.path.join(tdir, f)
        if "_diff_" in f or "_alpha_" in f or "_opacity_" in f:
            shutil.copy(p, os.path.join(out_dir, f))
        elif "_nor_gl_" in f:
            px = load_pixels(p)
            px[..., 3] = 1.0
            save_png(os.path.join(out_dir, f.replace(".exr", ".png").replace(".jpg", ".png")),
                     np.clip(px, 0, 1), non_color=True)
    # metallic (R) + smoothness (A) for URP Lit
    rough = [f for f in os.listdir(tdir) if "_rough_" in f]
    if rough:
        r = load_pixels(os.path.join(tdir, rough[0]))[..., 0]
        metal_f = [f for f in os.listdir(tdir) if "_metal_" in f]
        m = load_pixels(os.path.join(tdir, metal_f[0]))[..., 0] if metal_f else np.zeros_like(r)
        if m.shape != r.shape:
            m = np.zeros_like(r)
        ms = np.zeros(r.shape + (4,), dtype=np.float32)
        ms[..., 0] = np.clip(m, 0, 1)
        ms[..., 3] = np.clip(1.0 - r, 0, 1)
        save_png(os.path.join(out_dir, f"{aid}_ms_1k.png"), ms, non_color=True)


# --------------------------------------------------------------------------- hatchling
def scute_texture(path, size=512):
    """Carapace albedo: dark olive-brown scutes with paler seams, top-projected UVs (0..1)."""
    yy, xx = np.mgrid[0:size, 0:size] / size
    seeds = []
    for i in range(5):                       # vertebral row
        seeds.append((0.5, 0.18 + i * 0.16))
    for i in range(4):                       # costal pairs
        seeds += [(0.30, 0.25 + i * 0.16), (0.70, 0.25 + i * 0.16)]
    for i in range(13):                      # marginals around the rim
        a = math.pi * (i / 12.0)
        seeds += [(0.5 + 0.45 * math.sin(a), 0.5 - 0.45 * math.cos(a)),
                  (0.5 - 0.45 * math.sin(a), 0.5 - 0.45 * math.cos(a))]
    seeds = np.array(seeds)
    d = np.sqrt((xx[..., None] - seeds[:, 0]) ** 2 + (yy[..., None] - seeds[:, 1]) ** 2)
    ds = np.sort(d, axis=-1)
    edge = np.clip((ds[..., 1] - ds[..., 0]) / 0.018, 0, 1)
    idx = np.argmin(d, axis=-1)
    rng = np.random.default_rng(3)
    tint = rng.uniform(0.75, 1.15, len(seeds))[idx]
    grain = rng.uniform(0.9, 1.1, (size, size))
    base = np.array([0.085, 0.075, 0.055])
    seam = np.array([0.20, 0.18, 0.14])
    col = base[None, None, :] * (tint * grain)[..., None]
    col = col * edge[..., None] + seam[None, None, :] * (1 - edge[..., None])
    rgba = np.concatenate([np.clip(col, 0, 1), np.ones((size, size, 1))], axis=-1)
    save_png(path, rgba[::-1])


def build_hatchling():
    clear_scene()
    parts = []
    # carapace: flattened, tapered ellipsoid with a central keel
    shell = uv_sphere("Shell", 1.0, 48, 24, scale=(0.021, 0.028, 0.0095))
    for v in shell.data.vertices:
        x, y, z = v.co
        if z < 0:
            v.co.z = z * 0.35                                # flat plastron
        taper = 1.0 - 0.25 * max(0.0, -y / 0.028)           # narrower at the tail
        v.co.x = x * taper
        if z > 0:
            v.co.z += 0.0016 * math.exp(-(x / 0.004) ** 2) * (z / 0.0095)  # keel
            v.co.z += 0.0004 * noise.noise(Vector((x, y, z)) * 400)
    # top projection UVs
    uvl = shell.data.uv_layers.active or shell.data.uv_layers.new()
    for poly in shell.data.polygons:
        for li in poly.loop_indices:
            co = shell.data.vertices[shell.data.loops[li].vertex_index].co
            uvl.data[li].uv = (co.x / 0.044 + 0.5, co.y / 0.058 + 0.5)
    assign(shell, "Hatchling_Shell")
    shade_smooth(shell, 80)
    parts.append(shell)

    head = uv_sphere("Head", 1.0, 24, 12, loc=(0, 0.033, 0.0035), scale=(0.0062, 0.0085, 0.0055))
    for v in head.data.vertices:
        if v.co.y > 0:
            v.co.z -= 0.0012 * (v.co.y / 0.0085) ** 2   # beak-like slope
    assign(head, "Hatchling_Skin")
    parts.append(head)
    for sx in (-1, 1):
        eye = uv_sphere(f"Eye{'L' if sx < 0 else 'R'}", 0.0016, 12, 8, loc=(sx * 0.0048, 0.0365, 0.0052))
        assign(eye, "Hatchling_Eye")
        parts.append(eye)
    neck = uv_sphere("Neck", 1.0, 16, 8, loc=(0, 0.026, 0.002), scale=(0.006, 0.006, 0.0045))
    assign(neck, "Hatchling_Skin")
    parts.append(neck)
    tail = uv_sphere("Tail", 1.0, 12, 6, loc=(0, -0.029, 0.0), scale=(0.0022, 0.005, 0.0016))
    assign(tail, "Hatchling_Skin")
    parts.append(tail)

    # flippers: separate objects, origin at the shoulder so Unity can rotate them
    def flipper(name, side, pivot, length, width, sweep):
        f = uv_sphere(name, 1.0, 24, 12, scale=(length * 0.5, width * 0.5, 0.0011))
        for v in f.data.vertices:
            t = (v.co.x / (length * 0.5) + 1) * 0.5          # 0 at root, 1 at tip
            v.co.y *= (1.0 - 0.55 * t)                         # tapered blade
            v.co.y -= 0.35 * width * t * t                     # trailing curve
            v.co.z *= (1.0 - 0.5 * t)
        f.data.transform(Matrix.Translation((length * 0.5, 0, 0)))
        f.data.transform(Matrix.Rotation(math.radians(-sweep), 4, "Z"))
        if side < 0:
            f.data.transform(Matrix.Scale(-1, 4, Vector((1, 0, 0))))
            f.data.flip_normals()
        f.location = pivot
        assign(f, "Hatchling_Skin")
        shade_smooth(f, 80)
        return f

    parts.append(flipper("FlipperFL", -1, (-0.0155, 0.015, -0.0005), 0.042, 0.013, 28))
    parts.append(flipper("FlipperFR", 1, (0.0155, 0.015, -0.0005), 0.042, 0.013, 28))
    parts.append(flipper("FlipperBL", -1, (-0.012, -0.019, -0.001), 0.016, 0.009, 40))
    parts.append(flipper("FlipperBR", 1, (0.012, -0.019, -0.001), 0.016, 0.009, 40))
    for p in parts:
        if p.name in ("Head", "Neck", "Tail") or p.name.startswith("Eye"):
            shade_smooth(p, 80)
    export_fbx(os.path.join(MODELS, "Hatchling.fbx"), parts)
    scute_texture(os.path.join(TEX, "Hatchling_Shell.png"))


# --------------------------------------------------------------------------- seagull
def build_seagull():
    clear_scene()
    parts = []
    body = uv_sphere("Body", 1.0, 32, 16, scale=(0.075, 0.2, 0.08))
    for v in body.data.vertices:
        if v.co.y < 0:
            v.co.x *= 1 - 0.5 * (-v.co.y / 0.2)
            v.co.z *= 1 - 0.4 * (-v.co.y / 0.2)
    assign(body, "Gull_White")
    parts.append(body)
    head = uv_sphere("Head", 0.055, 24, 12, loc=(0, 0.2, 0.07))
    assign(head, "Gull_White")
    parts.append(head)
    bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=0.014, radius2=0.002, depth=0.07,
                                    location=(0, 0.275, 0.06), rotation=(math.radians(-95), 0, 0))
    beak = bpy.context.active_object
    beak.name = "Beak"
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    assign(beak, "Gull_Beak")
    parts.append(beak)
    for sx in (-1, 1):
        eye = uv_sphere("Eye", 0.007, 10, 6, loc=(sx * 0.04, 0.225, 0.088))
        assign(eye, "Gull_Black")
        parts.append(eye)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -0.24, 0.02))
    tail = bpy.context.active_object
    tail.name = "Tail"
    tail.scale = (0.08, 0.1, 0.012)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    for v in tail.data.vertices:
        if v.co.y < 0:
            v.co.x *= 1.4
    assign(tail, "Gull_White")
    parts.append(tail)
    for sx in (-1, 1):
        for name, y in (("Leg", 0.0),):
            bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.006, depth=0.12,
                                                location=(sx * 0.03, y, -0.12))
            leg = bpy.context.active_object
            leg.name = f"Leg{'L' if sx < 0 else 'R'}"
            assign(leg, "Gull_Beak")
            parts.append(leg)

    def wing(name, side):
        bm = bmesh.new()
        # planform: shoulder chord 0.2 -> tip chord 0.06, span 0.6
        span, n = 0.6, 8
        top, bot = [], []
        for i in range(n + 1):
            t = i / n
            x = span * t
            chord = 0.2 * (1 - t) + 0.05 * t
            lead = 0.05 * math.sin(t * math.pi) - 0.04 * t
            thick = 0.012 * (1 - t) + 0.002
            top.append((bm.verts.new((x, lead, thick)), bm.verts.new((x, lead - chord, thick * 0.4))))
            bot.append((bm.verts.new((x, lead, -thick * 0.3)), bm.verts.new((x, lead - chord, 0))))
        for i in range(n):
            a0, a1 = top[i], top[i + 1]
            b0, b1 = bot[i], bot[i + 1]
            bm.faces.new((a0[0], a1[0], a1[1], a0[1]))
            bm.faces.new((b0[1], b1[1], b1[0], b0[0]))
            bm.faces.new((a0[0], b0[0], b1[0], a1[0]))
            bm.faces.new((a1[1], b1[1], b0[1], a0[1]))
        bm.faces.new((top[-1][0], bot[-1][0], bot[-1][1], top[-1][1]))
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        o = bpy.data.objects.new(name, me)
        bpy.context.collection.objects.link(o)
        o.data.materials.append(mat("Gull_Grey"))
        o.data.materials.append(mat("Gull_Black"))
        for p in o.data.polygons:
            cx = sum(o.data.vertices[v].co.x for v in p.vertices) / len(p.vertices)
            p.material_index = 1 if cx > 0.45 else 0
        if side < 0:
            o.data.transform(Matrix.Scale(-1, 4, Vector((1, 0, 0))))
            o.data.flip_normals()
        o.location = (side * 0.06, 0.06, 0.04)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.object.mode_set(mode="OBJECT")
        return o

    parts.append(wing("WingL", -1))
    parts.append(wing("WingR", 1))
    for p in parts:
        if p.name not in ("WingL", "WingR", "Tail", "Beak"):
            shade_smooth(p, 80)
    export_fbx(os.path.join(MODELS, "Seagull.fbx"), parts)


# --------------------------------------------------------------------------- trash
def build_bottles():
    clear_scene()
    pet_profile = [(0.0, 0.0), (0.028, 0.0), (0.033, 0.006), (0.033, 0.03), (0.030, 0.04), (0.033, 0.05),
                   (0.033, 0.12), (0.031, 0.135), (0.024, 0.16), (0.014, 0.185), (0.0125, 0.195),
                   (0.0125, 0.2)]
    pet = lathe("PET_Body", pet_profile)
    assign(pet, "Trash_PET")
    cap = lathe("PET_Cap", [(0, 0.199), (0.0145, 0.199), (0.0145, 0.215), (0.0, 0.215)], 20)
    assign(cap, "Trash_Cap")
    label = lathe("PET_Label", [(0.0335, 0.06), (0.0335, 0.11)], 40)
    assign(label, "Trash_Label")
    export_fbx(os.path.join(MODELS, "Bottle_PET.fbx"), [pet, cap, label])

    clear_scene()
    pet = lathe("PET_Crushed", pet_profile, 32)
    for v in pet.data.vertices:
        v.co.x *= 0.55 + 0.12 * noise.noise(v.co * 40)
        v.co.y *= 1.0 + 0.08 * noise.noise(v.co * 35 + Vector((3, 1, 2)))
        v.co.z *= 0.9
    assign(pet, "Trash_PET")
    cap = lathe("PET_CapC", [(0, 0.179), (0.0145, 0.179), (0.0145, 0.195), (0.0, 0.195)], 20)
    cap.location.z = -0.02
    assign(cap, "Trash_Cap")
    export_fbx(os.path.join(MODELS, "Bottle_PET_Crushed.fbx"), [pet, cap])

    clear_scene()
    glass = lathe("Glass_Body", [(0.0, 0.0), (0.029, 0.0), (0.031, 0.004), (0.031, 0.14), (0.028, 0.16),
                                 (0.014, 0.19), (0.012, 0.225), (0.0135, 0.23), (0.0135, 0.236),
                                 (0.0105, 0.236), (0.0, 0.236)])
    assign(glass, "Trash_GlassBrown")
    export_fbx(os.path.join(MODELS, "Bottle_Glass.fbx"), [glass])


def build_bag():
    clear_scene()
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=28, y_subdivisions=34, size=1)
    bag = bpy.context.active_object
    bag.name = "PlasticBag"
    bag.scale = (0.30, 0.38, 1)
    bpy.ops.object.transform_apply(scale=True)
    me = bag.data
    for v in me.vertices:
        x, y, _ = v.co
        # crumple: folds + noise, handles lifted at one end
        z = 0.012 * noise.noise(Vector((x * 14, y * 14, 0.3)))
        z += 0.006 * noise.noise(Vector((x * 40, y * 40, 1.7)))
        z += 0.02 * max(0.0, math.cos(x * 9)) * max(0.0, y - 0.05) * 3
        v.co.x = x * (1 - 0.25 * max(0.0, y) / 0.19)
        v.co.z = z + 0.012
    # cut-out handle hole
    bm = bmesh.new()
    bm.from_mesh(me)
    kill = [f for f in bm.faces if abs(f.calc_center_median().x) < 0.05 and f.calc_center_median().y > 0.13]
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bm.to_mesh(me)
    bm.free()
    assign(bag, "Trash_Bag")
    shade_smooth(bag, 80)
    export_fbx(os.path.join(MODELS, "PlasticBag.fbx"), [bag])


def build_sandcastle():
    clear_scene()
    parts = []

    def frustum(name, r1, r2, h, loc, sides=14):
        bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=r1, radius2=r2, depth=h,
                                        location=(loc[0], loc[1], loc[2] + h / 2))
        o = bpy.context.active_object
        o.name = name
        for v in o.data.vertices:
            v.co += Vector((noise.noise(v.co * 30), noise.noise(v.co * 30 + Vector((5, 0, 0))), 0)) * 0.006
        assign(o, "Sandcastle")
        return o

    parts.append(frustum("Keep", 0.13, 0.09, 0.16, (0, 0, 0)))
    for i, (x, y) in enumerate(((0.16, 0.1), (-0.15, 0.12), (0.12, -0.14), (-0.13, -0.11))):
        parts.append(frustum(f"Tower{i}", 0.06, 0.045, 0.12, (x, y, 0)))
    parts.append(frustum("Top", 0.07, 0.05, 0.07, (0, 0, 0.16)))
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    castle = bpy.context.active_object
    castle.name = "Sandcastle"
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.cube_project(cube_size=0.4)
    bpy.ops.object.mode_set(mode="OBJECT")
    export_fbx(os.path.join(MODELS, "Sandcastle.fbx"), [castle])


# --------------------------------------------------------------------------- main
for aid in sorted(os.listdir(PH_DIR)):
    if os.path.isdir(os.path.join(PH_DIR, aid)):
        try:
            process_polyhaven(aid)
        except Exception as e:
            print("PH FAILED", aid, e)

build_hatchling()
build_seagull()
build_bottles()
build_bag()
build_sandcastle()
print("ALL DONE")
