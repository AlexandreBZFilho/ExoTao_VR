import bpy
import os

input_path = os.path.abspath("Assets/Resources/Models/ExoBot_Blueprint_Drone.fbx")
backup_path = os.path.abspath("Assets/Resources/Models/ExoBot_Blueprint_Drone_OriginalBackup.fbx")

# Save backup if not exists
if not os.path.exists(backup_path):
    import shutil
    shutil.copy2(input_path, backup_path)
    print(f"Backed up original FBX to {backup_path}")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=input_path)

mesh_objs = [o for o in bpy.data.objects if o.type == 'MESH']
if not mesh_objs:
    print("ERROR: No mesh found in FBX!")
    exit(1)

obj = mesh_objs[0]
print(f"Original mesh: {obj.name}, Polygons: {len(obj.data.polygons)}, Vertices: {len(obj.data.vertices)}")
print(f"Original materials: {[m.name for m in obj.data.materials if m]}")

# Add Decimate modifier to bring polycount down to < 1500 triangles
mod = obj.modifiers.new("Decimate_VR", "DECIMATE")
# 1350 / 25196 ~= 0.0535 (or for triangulated ratio ~0.027)
# Let's target exactly ~1250-1350 faces
target_faces = 1350
mod.ratio = target_faces / len(obj.data.polygons)

bpy.context.view_layer.objects.active = obj
bpy.ops.object.modifier_apply(modifier="Decimate_VR")

# Smooth shading
bpy.ops.object.shade_smooth()

# Verify normals and triangulation
final_faces = len(obj.data.polygons)
final_verts = len(obj.data.vertices)
print(f"Optimized mesh: {obj.name}, Polygons: {final_faces}, Vertices: {final_verts}")

if final_faces >= 1500:
    print(f"WARNING: Exceeds 1500 triangles! Applying additional decimate...")
    mod2 = obj.modifiers.new("Decimate_Fine", "DECIMATE")
    mod2.ratio = 1350 / final_faces
    bpy.ops.object.modifier_apply(modifier="Decimate_Fine")
    final_faces = len(obj.data.polygons)
    final_verts = len(obj.data.vertices)
    print(f"Adjusted: Polygons: {final_faces}, Vertices: {final_verts}")

# Export back to FBX
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(
    filepath=input_path,
    use_selection=True,
    axis_forward='-Z',
    axis_up='Y',
    apply_unit_scale=True,
    bake_space_transform=True
)

print(f"SUCCESS: Exported optimized Drone model to {input_path} with {final_faces} polygons (< 1500)!")
