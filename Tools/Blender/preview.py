"""Quick Workbench render of an FBX for visual checks:
   blender -b --factory-startup --python preview.py -- model.fbx out.png [azimuth_deg] [elevation_deg]"""
import bpy, sys, math
from mathutils import Vector
argv = sys.argv[sys.argv.index("--") + 1:]
fbx, out = argv[0], argv[1]
az = math.radians(float(argv[2])) if len(argv) > 2 else math.radians(-40)
el = math.radians(float(argv[3])) if len(argv) > 3 else math.radians(30)
bpy.ops.object.select_all(action="SELECT"); bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=fbx)
COLORS = {"shell": (0.12, 0.1, 0.07), "belly": (0.6, 0.55, 0.42), "skin": (0.2, 0.18, 0.15), "eye": (0.01, 0.01, 0.01),
          "beakspot": (0.8, 0.1, 0.05), "beak": (0.85, 0.65, 0.2), "white": (0.9, 0.9, 0.9), "grey": (0.5, 0.55, 0.6),
          "black": (0.03, 0.03, 0.03), "legs": (0.85, 0.6, 0.55), "sand": (0.75, 0.66, 0.5), "shirt": (0.1, 0.35, 0.4),
          "shorts": (0.25, 0.28, 0.38), "shoe": (0.85, 0.85, 0.82), "sole": (0.2, 0.2, 0.2), "avatar_skin": (0.78, 0.6, 0.48),
          "pebble": (0.5, 0.48, 0.45)}
for m in bpy.data.materials:
    n = m.name.lower()
    for k in sorted(COLORS, key=len, reverse=True):
        if k in n:
            m.diffuse_color = (*COLORS[k], 1)
            break
objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
for o in objs:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
center = (mn + mx) / 2; size = (mx - mn).length
print("BOUNDS", tuple(round(v, 4) for v in mn), tuple(round(v, 4) for v in mx))
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
bpy.context.scene.collection.objects.link(cam)
cam.location = center + Vector((math.cos(el) * math.sin(az), -math.cos(el) * math.cos(az), math.sin(el))) * size * 1.4
cam.rotation_euler = (center - cam.location).to_track_quat('-Z', 'Y').to_euler()
sc = bpy.context.scene
sc.camera = cam
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.color_type = "MATERIAL"
sc.display.shading.light = "STUDIO"
sc.render.resolution_x = 640; sc.render.resolution_y = 480
sc.render.filepath = out
bpy.ops.render.render(write_still=True)
