import bpy
import bmesh
import math
import os

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def create_material(name, diffuse_color, metallic=0.0, roughness=0.5, is_emissive=False, emission_color=(0,0,0,1)):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = diffuse_color
        bsdf.inputs['Metallic'].default_value = metallic
        bsdf.inputs['Roughness'].default_value = roughness
        if is_emissive:
            if 'Emission Color' in bsdf.inputs:
                bsdf.inputs['Emission Color'].default_value = emission_color
                bsdf.inputs['Emission Strength'].default_value = 2.5
            elif 'Emission' in bsdf.inputs:
                bsdf.inputs['Emission'].default_value = emission_color
    return mat

def build_balloon_gun(export_path):
    reset_scene()
    print("Building BalloonGunDispenser 3D model...")

    # Materials
    mat_body = create_material("M_BalloonGun_Body", (0.92, 0.93, 0.95, 1.0), metallic=0.08, roughness=0.32)
    mat_grip = create_material("M_BalloonGun_Grip", (0.13, 0.14, 0.16, 1.0), metallic=0.02, roughness=0.75)
    mat_canister = create_material("M_BalloonGun_Canister", (0.00, 0.46, 0.72, 1.0), metallic=0.85, roughness=0.18)
    mat_accent = create_material("M_BalloonGun_Accent", (0.00, 0.71, 0.85, 1.0), metallic=0.30, roughness=0.25, is_emissive=True, emission_color=(0.0, 0.75, 1.0, 1.0))
    mat_nozzle = create_material("M_BalloonGun_Nozzle", (0.20, 0.22, 0.25, 1.0), metallic=0.70, roughness=0.40)
    mat_gauge = create_material("M_BalloonGun_Gauge", (0.96, 0.97, 0.98, 1.0), metallic=0.05, roughness=0.25)
    mat_brass = create_material("M_BalloonGun_Brass", (0.85, 0.65, 0.20, 1.0), metallic=0.80, roughness=0.30)

    # Root object (Grip Center of mass at 0, 0, 0)
    root = bpy.data.objects.new("BalloonGunDispenser", None)
    bpy.context.collection.objects.link(root)

    # 1. Ergonomic Grip (Angled back 16 degrees, centered around origin)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, -0.015, -0.045))
    grip = bpy.context.active_object
    grip.name = "Gun_Grip"
    grip.rotation_euler = (math.radians(-16.0), 0, 0)
    grip.scale = (0.030, 0.036, 0.105)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    grip.data.materials.append(mat_grip)
    grip.parent = root

    bev_grip = grip.modifiers.new("Bevel", 'BEVEL')
    bev_grip.width = 0.006
    bev_grip.segments = 3

    # Grip finger indentations / pads
    for i in range(3):
        y_ind = -0.004 - i * 0.006
        z_ind = -0.025 - i * 0.022
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, y_ind, z_ind))
        indent = bpy.context.active_object
        indent.name = f"Grip_Pad_{i}"
        indent.rotation_euler = (math.radians(-16.0), 0, 0)
        indent.scale = (0.031, 0.012, 0.012)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        indent.data.materials.append(mat_accent)
        indent.parent = root

    # 2. Trigger Guard & Trigger
    bpy.ops.mesh.primitive_cylinder_add(radius=0.018, depth=0.010, vertices=16, location=(0, 0.022, -0.012))
    guard = bpy.context.active_object
    guard.name = "Trigger_Guard"
    guard.rotation_euler = (0, math.radians(90.0), 0)
    guard.scale = (1.0, 1.4, 1.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    guard.data.materials.append(mat_nozzle)
    guard.parent = root

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.018, -0.008))
    trigger = bpy.context.active_object
    trigger.name = "Gun_Trigger"
    trigger.rotation_euler = (math.radians(22.0), 0, 0)
    trigger.scale = (0.008, 0.012, 0.024)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    trigger.data.materials.append(mat_accent)
    trigger.parent = root

    # 3. Main Receiver / Gun Body
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.015, 0.038))
    body = bpy.context.active_object
    body.name = "Gun_Receiver"
    body.scale = (0.046, 0.130, 0.048)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    body.data.materials.append(mat_body)
    body.parent = root

    bev_body = body.modifiers.new("Bevel", 'BEVEL')
    bev_body.width = 0.005
    bev_body.segments = 2

    # Top heat-sink / ventilation fins
    for i in range(4):
        y_fin = -0.025 + i * 0.022
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, y_fin, 0.064))
        fin = bpy.context.active_object
        fin.name = f"Receiver_Fin_{i}"
        fin.scale = (0.034, 0.007, 0.006)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        fin.data.materials.append(mat_accent)
        fin.parent = root

    # 4. Front Barrel & Muzzle Nozzle
    bpy.ops.mesh.primitive_cylinder_add(radius=0.018, depth=0.065, vertices=24, location=(0, 0.095, 0.040))
    barrel = bpy.context.active_object
    barrel.name = "Gun_Barrel"
    barrel.rotation_euler = (math.radians(90.0), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    barrel.data.materials.append(mat_body)
    barrel.parent = root

    bpy.ops.mesh.primitive_cylinder_add(radius=0.015, depth=0.030, vertices=24, location=(0, 0.135, 0.040))
    nozzle = bpy.context.active_object
    nozzle.name = "Gun_Nozzle"
    nozzle.rotation_euler = (math.radians(90.0), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    nozzle.data.materials.append(mat_nozzle)
    nozzle.parent = root

    bpy.ops.mesh.primitive_cylinder_add(radius=0.017, depth=0.008, vertices=24, location=(0, 0.150, 0.040))
    rim = bpy.context.active_object
    rim.name = "Nozzle_Rim"
    rim.rotation_euler = (math.radians(90.0), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    rim.data.materials.append(mat_brass)
    rim.parent = root

    # Muzzle Socket (Child Empty Transform exactly at the tip center)
    muzzle_socket = bpy.data.objects.new("Muzzle_Socket", None)
    muzzle_socket.location = (0, 0.155, 0.040)
    bpy.context.collection.objects.link(muzzle_socket)
    muzzle_socket.parent = root

    # 5. Lateral Helium Canister & Brackets
    bpy.ops.mesh.primitive_cylinder_add(radius=0.016, depth=0.095, vertices=20, location=(0.036, 0.010, 0.032))
    canister = bpy.context.active_object
    canister.name = "Helium_Canister"
    canister.rotation_euler = (math.radians(90.0), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    canister.data.materials.append(mat_canister)
    canister.parent = root

    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.016, segments=16, ring_count=12, location=(0.036, -0.037, 0.032))
    cap_back = bpy.context.active_object
    cap_back.name = "Canister_Cap_Back"
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    cap_back.data.materials.append(mat_canister)
    cap_back.parent = root

    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.016, segments=16, ring_count=12, location=(0.036, 0.057, 0.032))
    cap_front = bpy.context.active_object
    cap_front.name = "Canister_Cap_Front"
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    cap_front.data.materials.append(mat_canister)
    cap_front.parent = root

    for band_y in [-0.015, 0.035]:
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.024, band_y, 0.032))
        band = bpy.context.active_object
        band.name = f"Canister_Band_{band_y}"
        band.scale = (0.016, 0.008, 0.034)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        band.data.materials.append(mat_nozzle)
        band.parent = root

    bpy.ops.mesh.primitive_cylinder_add(radius=0.004, depth=0.022, vertices=12, location=(0.026, 0.057, 0.036))
    pipe = bpy.context.active_object
    pipe.name = "Canister_Valved_Pipe"
    pipe.rotation_euler = (0, math.radians(90.0), 0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    pipe.data.materials.append(mat_brass)
    pipe.parent = root

    # 6. Analog Pressure Gauge Dial
    bpy.ops.mesh.primitive_cylinder_add(radius=0.013, depth=0.008, vertices=20, location=(-0.012, 0.020, 0.066))
    gauge_body = bpy.context.active_object
    gauge_body.name = "Pressure_Gauge_Bezel"
    gauge_body.rotation_euler = (math.radians(-25.0), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    gauge_body.data.materials.append(mat_brass)
    gauge_body.parent = root

    bpy.ops.mesh.primitive_cylinder_add(radius=0.011, depth=0.002, vertices=20, location=(-0.012, 0.021, 0.069))
    gauge_face = bpy.context.active_object
    gauge_face.name = "Pressure_Gauge_Face"
    gauge_face.rotation_euler = (math.radians(-25.0), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    gauge_face.data.materials.append(mat_gauge)
    gauge_face.parent = root

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(-0.010, 0.022, 0.071))
    needle = bpy.context.active_object
    needle.name = "Pressure_Gauge_Needle"
    needle.rotation_euler = (math.radians(-25.0), 0, math.radians(45.0))
    needle.scale = (0.002, 0.008, 0.001)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    needle.data.materials.append(mat_accent)
    needle.parent = root

    bpy.ops.object.select_all(action='DESELECT')
    for obj in bpy.context.scene.objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root

    bpy.ops.export_scene.fbx(
        filepath=export_path,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True
    )
    print(f"Exported BalloonGunDispenser FBX to {export_path}")

def build_inflatable_balloon(export_path):
    reset_scene()
    print("Building InflatableBalloon 3D model (Vertical Along Z in Blender -> Upright in Unity)...")

    mat_balloon = create_material("M_Balloon_Base", (0.95, 0.12, 0.25, 1.0), metallic=0.05, roughness=0.12)
    mat_knot = create_material("M_Balloon_Knot", (0.80, 0.08, 0.18, 1.0), metallic=0.02, roughness=0.35)

    root = bpy.data.objects.new("InflatableBalloon", None)
    bpy.context.collection.objects.link(root)

    # In Blender, +Z is vertical UP!
    # Profile: (z_height, radius)
    # Knot at z=0.0m, expanding upward to dome at z=0.30m
    mesh = bpy.data.meshes.new("Balloon_Mesh")
    balloon_obj = bpy.data.objects.new("Balloon_Body", mesh)
    bpy.context.collection.objects.link(balloon_obj)
    balloon_obj.parent = root

    bm = bmesh.new()

    # Perfil v4 (TASK-07.B, 2026-11-25): domo/ventre ELIPSOIDAL de verdade (balão de festa redondo, nada de cone),
    # pescoço em curva de Hermite com tangente contínua e nó com colarinho enrolado. Altura nominal 0,30 m.
    def _hermite(p0, p1, m0, m1, t):
        t2, t3 = t * t, t * t * t
        h00, h10, h01, h11 = 2*t3 - 3*t2 + 1, t3 - 2*t2 + t, -2*t3 + 3*t2, t3 - t2
        return tuple(h00 * p0[i] + h10 * m0[i] + h01 * p1[i] + h11 * m1[i] for i in range(2))

    zc, rh, rv = 0.175, 0.120, 0.125          # centro e semi-eixos (horizontal 0,12 -> 0,24 de diâmetro)
    theta_end = math.radians(122.0)
    profile_top = []                           # do ápice (excluído) até a transição para o pescoço
    for deg in (11, 22, 33, 44, 55, 66, 77, 88, 99, 110, 122):
        th = math.radians(deg)
        profile_top.append((zc + rv * math.cos(th), rh * math.sin(th)))
    # Pescoço: da transição (tangente da elipse) até o início do pescoço (tangente vertical)
    p0 = (zc + rv * math.cos(theta_end), rh * math.sin(theta_end))
    m0 = (-rv * math.sin(theta_end) * 0.9, rh * math.cos(theta_end) * 0.9)   # d(z,r)/dθ escalada
    p1 = (0.026, 0.0115)
    m1 = (-0.06, 0.0)
    neck = [_hermite(p0, p1, m0, m1, t) for t in (0.25, 0.5, 0.72, 0.88, 1.0)]
    knot = [(0.017, 0.0072), (0.011, 0.0050), (0.007, 0.0100), (0.003, 0.0120), (0.000, 0.0090)]
    # Ordem de baixo para cima, terminando no ápice (r = 0)
    profile = list(reversed(knot)) + list(reversed(neck)) + list(reversed(profile_top)) + [(zc + rv, 0.0)]

    segments = 20  # 20 x 18 anéis ~ 720 tris (objeto médio VR); sombreamento smooth
    ring_verts = []

    for z_pos, radius in profile[:-1]:
        ring = []
        for s in range(segments):
            angle = (2.0 * math.pi * s) / segments
            x = radius * math.cos(angle)
            y = radius * math.sin(angle)
            v = bm.verts.new((x, y, z_pos))
            ring.append(v)
        ring_verts.append(ring)

    apex_vert = bm.verts.new((0.0, 0.0, profile[-1][0]))

    bm.verts.ensure_lookup_table()

    for r in range(len(ring_verts) - 1):
        r1 = ring_verts[r]
        r2 = ring_verts[r + 1]
        for s in range(segments):
            s_next = (s + 1) % segments
            bm.faces.new([r1[s], r1[s_next], r2[s_next], r2[s]])

    top_ring = ring_verts[-1]
    for s in range(segments):
        s_next = (s + 1) % segments
        bm.faces.new([top_ring[s], top_ring[s_next], apex_vert])

    bottom_center = bm.verts.new((0.0, 0.0, 0.0))
    bm.verts.ensure_lookup_table()
    bottom_ring = ring_verts[0]
    for s in range(segments):
        s_next = (s + 1) % segments
        bm.faces.new([bottom_ring[s_next], bottom_ring[s], bottom_center])

    for face in bm.faces:
        face.smooth = True

    # Garante normais para fora em todas as faces (zero partes invisíveis com backface culling)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

    balloon_obj.data.materials.append(mat_balloon)

    # (Subsurf removido: quadruplicava os triângulos de um item que pode ter 8 cópias simultâneas)

    # Tied knot bead detail at base
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.009, segments=10, ring_count=6, location=(0.0, 0.0, 0.010))
    knot_obj = bpy.context.active_object
    knot_obj.name = "Knot_Bead"
    knot_obj.scale = (1.25, 1.1, 0.8)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    knot_obj.data.materials.append(mat_knot)
    knot_obj.parent = root

    string_anchor = bpy.data.objects.new("String_Anchor", None)
    string_anchor.location = (0.0, 0.0, 0.0)
    bpy.context.collection.objects.link(string_anchor)
    string_anchor.parent = root

    bpy.ops.object.select_all(action='DESELECT')
    for obj in bpy.context.scene.objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root

    bpy.ops.export_scene.fbx(
        filepath=export_path,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True
    )
    print(f"Exported InflatableBalloon FBX to {export_path}")

if __name__ == '__main__':
    out_dir = r"D:\Alunos\Alexandre\Projeto IC\Assets\Models\Generated\Task07"
    os.makedirs(out_dir, exist_ok=True)

    import sys
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not args or "gun" in args:
        build_balloon_gun(os.path.join(out_dir, "BalloonGunDispenser.fbx"))
    if not args or "balloon" in args:
        build_inflatable_balloon(os.path.join(out_dir, "InflatableBalloon.fbx"))
    print("All Task07 3D FBX models regenerated successfully!")
