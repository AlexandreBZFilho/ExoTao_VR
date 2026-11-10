import bpy
import math
import os

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for collection in bpy.data.collections:
        bpy.data.collections.remove(collection)
    for obj in bpy.data.objects:
        bpy.data.objects.remove(obj)
    for mesh in bpy.data.meshes:
        bpy.data.meshes.remove(mesh)
    for mat in bpy.data.materials:
        bpy.data.materials.remove(mat)

def create_material(name, base_color, metallic=0.0, roughness=0.5, emission_color=None, emission_strength=1.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    if bsdf:
        if "Base Color" in bsdf.inputs:
            bsdf.inputs["Base Color"].default_value = base_color
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = metallic
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = roughness
        if emission_color is not None:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = emission_color
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat

def build_tractor_beam_emitter(output_path):
    clear_scene()
    
    mat_housing = create_material("M_Tractor_Housing", (0.12, 0.15, 0.18, 1.0), metallic=0.85, roughness=0.3)
    mat_glow = create_material("M_Tractor_CyanGlow", (0.0, 0.95, 1.0, 1.0), metallic=0.1, roughness=0.2, emission_color=(0.0, 0.95, 1.0, 1.0), emission_strength=3.5)
    mat_lens = create_material("M_Tractor_Lens", (0.05, 0.35, 0.50, 1.0), metallic=0.2, roughness=0.1, emission_color=(0.0, 0.8, 1.0, 1.0), emission_strength=2.0)

    # 1. Main outer housing collar (cylinder diameter 0.10m, height 0.025m, mounted flush at z=0 pointing downward)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=32,
        radius=0.050,  # 0.10m diameter
        depth=0.025,
        location=(0, 0, -0.0125)
    )
    collar = bpy.context.active_object
    collar.name = "Housing_Collar"
    collar.data.materials.append(mat_housing)

    # 2. Emissive cyan ring (inner recess, radius 0.042m, depth 0.008m, at z = -0.022m)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=32,
        radius=0.042,
        depth=0.008,
        location=(0, 0, -0.022)
    )
    ring = bpy.context.active_object
    ring.name = "Emissive_Ring"
    ring.data.materials.append(mat_glow)

    # 3. Concave focal lens in center (inverted hemisphere/cylinder, radius 0.028m, depth 0.007m)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=32,
        radius=0.028,
        depth=0.006,
        location=(0, 0, -0.024)
    )
    lens = bpy.context.active_object
    lens.name = "Focal_Lens"
    lens.data.materials.append(mat_lens)

    # 4. 4 Focus Calipers / Emitter nodes at 90 deg intervals around perimeter
    calipers = []
    for i in range(4):
        angle = i * (math.pi / 2.0)
        x = math.cos(angle) * 0.048
        y = math.sin(angle) * 0.048
        bpy.ops.mesh.primitive_cube_add(
            size=1.0,
            location=(x, y, -0.016)
        )
        node = bpy.context.active_object
        node.name = f"Emitter_Node_{i}"
        node.scale = (0.012, 0.012, 0.018)
        node.rotation_euler = (0, 0, angle)
        node.data.materials.append(mat_glow)
        calipers.append(node)

    # Select all and join into single unified mesh with origin at (0,0,0) (top mounting plane)
    bpy.ops.object.select_all(action='SELECT')
    bpy.context.view_layer.objects.active = collar
    bpy.ops.object.join()
    
    emitter = bpy.context.active_object
    emitter.name = "TractorBeamEmitter"
    emitter.location = (0, 0, 0)
    
    # Smooth normals
    bpy.ops.object.shade_smooth()
    
    # Export FBX
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=output_path,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_unit_scale=True,
        bake_space_transform=True
    )
    print(f"Exported TractorBeamEmitter to {output_path}")

def build_cyber_fetch_baton(output_path):
    clear_scene()

    mat_grip = create_material("M_Baton_Grip", (0.08, 0.09, 0.10, 1.0), metallic=0.1, roughness=0.85)
    mat_housing = create_material("M_Baton_Housing", (0.75, 0.80, 0.85, 1.0), metallic=0.9, roughness=0.25)
    mat_glow = create_material("M_Baton_NeonCyan", (0.0, 0.96, 1.0, 1.0), metallic=0.1, roughness=0.15, emission_color=(0.0, 0.96, 1.0, 1.0), emission_strength=3.8)
    mat_rubber_cap = create_material("M_Baton_Caps", (0.15, 0.16, 0.18, 1.0), metallic=0.05, roughness=0.9)

    # Total length: 0.22m along Z axis. Pivot at geometric center (0,0,0)
    
    # 1. Central handle grip (length 0.10m, radius 0.017m -> 0.034m diameter)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=24,
        radius=0.017,
        depth=0.10,
        location=(0, 0, 0)
    )
    grip = bpy.context.active_object
    grip.name = "Central_Grip"
    grip.data.materials.append(mat_grip)

    # 2. Ribbed grip rings on handle
    rings = []
    for z in [-0.03, -0.01, 0.01, 0.03]:
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=24,
            radius=0.0182,
            depth=0.005,
            location=(0, 0, z)
        )
        r = bpy.context.active_object
        r.data.materials.append(mat_grip)
        rings.append(r)

    # 3. Two Cyan Emissive Neon Rings flanking the grip (at z = -0.052 and +0.052)
    neon_rings = []
    for z in [-0.052, 0.052]:
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=24,
            radius=0.0188,
            depth=0.007,
            location=(0, 0, z)
        )
        nr = bpy.context.active_object
        nr.data.materials.append(mat_glow)
        neon_rings.append(nr)

    # 4. Outer tapered metallic shafts (from z=0.055 to 0.095 and z=-0.055 to -0.095)
    shafts = []
    for z_center, z_sign in [(0.076, 1.0), (-0.076, -1.0)]:
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=24,
            radius=0.016,
            depth=0.040,
            location=(0, 0, z_center)
        )
        shaft = bpy.context.active_object
        shaft.data.materials.append(mat_housing)
        shafts.append(shaft)

    # 5. Dual rounded shock-absorber end caps (spheres at z = +0.10 and -0.10, radius 0.020m -> 0.040m bulb)
    caps = []
    for z_cap in [0.098, -0.098]:
        bpy.ops.mesh.primitive_uv_sphere_add(
            segments=24,
            ring_count=16,
            radius=0.020,
            location=(0, 0, z_cap)
        )
        cap = bpy.context.active_object
        cap.scale = (1.0, 1.0, 0.70)
        cap.data.materials.append(mat_rubber_cap)
        caps.append(cap)

    # Join everything into single object
    bpy.ops.object.select_all(action='SELECT')
    bpy.context.view_layer.objects.active = grip
    bpy.ops.object.join()

    baton = bpy.context.active_object
    baton.name = "CyberFetchBaton"
    baton.location = (0, 0, 0)

    # Shade smooth
    bpy.ops.object.shade_smooth()

    # Export FBX
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=output_path,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_unit_scale=True,
        bake_space_transform=True
    )
    print(f"Exported CyberFetchBaton to {output_path}")

if __name__ == "__main__":
    base_dir = os.path.abspath("Assets/Models/Generated/Task03")
    emitter_path = os.path.join(base_dir, "TractorBeamEmitter.fbx")
    baton_path = os.path.join(base_dir, "CyberFetchBaton.fbx")

    build_tractor_beam_emitter(emitter_path)
    build_cyber_fetch_baton(baton_path)
    print("ALL TASK-03 3D MODELS GENERATED SUCCESSFULLY!")
