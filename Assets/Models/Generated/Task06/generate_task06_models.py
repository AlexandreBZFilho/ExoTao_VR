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
                bsdf.inputs['Emission Strength'].default_value = 2.8
    return mat

def create_spool_mesh(name, hub_radius=0.0042, outer_radius=0.0105, thickness=0.006, num_teeth=6):
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bm = bmesh.new()

    segments = 32
    y_half = thickness * 0.5

    verts_front = []
    verts_back = []

    for i in range(segments):
        angle = (2 * math.pi * i) / segments
        tooth_factor = math.cos(num_teeth * angle)
        r = outer_radius + (0.0018 if tooth_factor > 0.35 else 0.0)

        x = r * math.cos(angle)
        z = r * math.sin(angle)
        vf = bm.verts.new((x, -y_half, z))
        vb = bm.verts.new((x, y_half, z))
        verts_front.append(vf)
        verts_back.append(vb)

    bm.verts.ensure_lookup_table()

    for i in range(segments):
        i_next = (i + 1) % segments
        bm.faces.new([verts_front[i], verts_front[i_next], verts_back[i_next], verts_back[i]])

    hub_front = []
    hub_back = []
    for i in range(segments):
        angle = (2 * math.pi * i) / segments
        prong = math.cos(3 * angle)
        r_inner = hub_radius - (0.0012 if prong > 0.3 else 0.0)
        x = r_inner * math.cos(angle)
        z = r_inner * math.sin(angle)
        hf = bm.verts.new((x, -y_half, z))
        hb = bm.verts.new((x, y_half, z))
        hub_front.append(hf)
        hub_back.append(hb)

    bm.verts.ensure_lookup_table()

    for i in range(segments):
        i_next = (i + 1) % segments
        bm.faces.new([hub_front[i_next], hub_front[i], hub_back[i], hub_back[i_next]])
        bm.faces.new([verts_front[i], hub_front[i], hub_front[i_next], verts_front[i_next]])
        bm.faces.new([verts_back[i_next], hub_back[i_next], hub_back[i], verts_back[i]])

    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    return obj

def build_cassette_tape(export_path):
    reset_scene()

    mat_shell = create_material("M_Cassette_Shell", (0.12, 0.13, 0.15, 1.0), metallic=0.1, roughness=0.35)
    mat_window = create_material("M_Cassette_Window", (0.85, 0.92, 0.98, 0.45), metallic=0.05, roughness=0.1)
    mat_label = create_material("M_Cassette_Label", (0.15, 0.45, 0.55, 1.0), metallic=0.0, roughness=0.5)
    mat_spool = create_material("M_Cassette_Spool", (0.94, 0.92, 0.88, 1.0), metallic=0.0, roughness=0.35)
    mat_ribbon = create_material("M_Cassette_TapeRibbon", (0.10, 0.08, 0.06, 1.0), metallic=0.1, roughness=0.3)

    root = bpy.data.objects.new("RetroCassetteTape", None)
    bpy.context.collection.objects.link(root)

    # 1. Main Shell: Width X = 0.100m, Thickness Y = 0.012m, Height Z = 0.064m
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0))
    shell = bpy.context.active_object
    shell.name = "Body_Shell"
    shell.scale = (0.100, 0.012, 0.064)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    shell.data.materials.append(mat_shell)
    shell.parent = root

    bev = shell.modifiers.new("Bevel", 'BEVEL')
    bev.width = 0.0018
    bev.segments = 2

    # 2. Label Sticker with Central Window Frame Cutout:
    # Top label banner
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, -0.0062, 0.020))
    lbl_top = bpy.context.active_object
    lbl_top.name = "Label_Top"
    lbl_top.scale = (0.088, 0.0004, 0.016)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    lbl_top.data.materials.append(mat_label)
    lbl_top.parent = root

    # Bottom label banner
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, -0.0062, -0.018))
    lbl_bot = bpy.context.active_object
    lbl_bot.name = "Label_Bottom"
    lbl_bot.scale = (0.088, 0.0004, 0.014)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    lbl_bot.data.materials.append(mat_label)
    lbl_bot.parent = root

    # Left & Right label borders
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(-0.034, -0.0062, 0.001))
    lbl_left = bpy.context.active_object
    lbl_left.name = "Label_Left"
    lbl_left.scale = (0.020, 0.0004, 0.024)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    lbl_left.data.materials.append(mat_label)
    lbl_left.parent = root

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.034, -0.0062, 0.001))
    lbl_right = bpy.context.active_object
    lbl_right.name = "Label_Right"
    lbl_right.scale = (0.020, 0.0004, 0.024)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    lbl_right.data.materials.append(mat_label)
    lbl_right.parent = root

    # 3. Central Acrylic Clear Window:
    # Perfectly visible through the label frame!
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.001))
    window = bpy.context.active_object
    window.name = "Window_Glass"
    window.scale = (0.048, 0.0126, 0.024)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    window.data.materials.append(mat_window)
    window.parent = root

    # 4. Tape Ribbon Rolls (cylinder along Y)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.0135, depth=0.0065, location=(-0.021, 0, 0.001))
    ribbon_left = bpy.context.active_object
    ribbon_left.rotation_euler = (math.radians(90), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    ribbon_left.name = "Ribbon_Roll_Left"
    ribbon_left.data.materials.append(mat_ribbon)
    ribbon_left.parent = root

    bpy.ops.mesh.primitive_cylinder_add(radius=0.0125, depth=0.0065, location=(0.021, 0, 0.001))
    ribbon_right = bpy.context.active_object
    ribbon_right.rotation_euler = (math.radians(90), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    ribbon_right.name = "Ribbon_Roll_Right"
    ribbon_right.data.materials.append(mat_ribbon)
    ribbon_right.parent = root

    # 5. Toothed Spools (rotatable gears inside window)
    spool_left = create_spool_mesh("Spool_Left", hub_radius=0.0040, outer_radius=0.0105, thickness=0.0070, num_teeth=6)
    spool_left.location = (-0.021, 0, 0.001)
    spool_left.data.materials.append(mat_spool)
    bpy.context.collection.objects.link(spool_left)
    spool_left.parent = root

    spool_right = create_spool_mesh("Spool_Right", hub_radius=0.0040, outer_radius=0.0105, thickness=0.0070, num_teeth=6)
    spool_right.location = (0.021, 0, 0.001)
    spool_right.data.materials.append(mat_spool)
    bpy.context.collection.objects.link(spool_right)
    spool_right.parent = root

    all_objs = [root, shell, lbl_top, lbl_bot, lbl_left, lbl_right, window, ribbon_left, ribbon_right, spool_left, spool_right]
    bpy.ops.object.select_all(action='DESELECT')
    for obj in all_objs:
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
    print(f"Exported RetroCassetteTape FBX to {export_path}")

def build_deck_slot(export_path):
    reset_scene()

    mat_chassis = create_material("M_Deck_Chassis", (0.24, 0.14, 0.09, 1.0), metallic=0.1, roughness=0.55)
    mat_metal   = create_material("M_Deck_Metal", (0.88, 0.74, 0.38, 1.0), metallic=0.9, roughness=0.25)
    mat_cavity  = create_material("M_Deck_Cavity", (0.06, 0.07, 0.08, 1.0), metallic=0.3, roughness=0.7)
    mat_eject   = create_material("M_Eject_Button", (0.92, 0.15, 0.12, 1.0), metallic=0.15, roughness=0.3)
    mat_led     = create_material("M_Deck_StatusLED", (0.0, 0.95, 1.0, 1.0), metallic=0.0, roughness=0.1, is_emissive=True, emission_color=(0, 0.95, 1, 1))

    root = bpy.data.objects.new("CassetteDeckSlot", None)
    bpy.context.collection.objects.link(root)

    # Deck Chassis: 0.20m wide (X), 0.15m deep (Y), 0.075m tall (Z)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.0375))
    chassis = bpy.context.active_object
    chassis.name = "Deck_Housing"
    chassis.scale = (0.20, 0.15, 0.075)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    chassis.data.materials.append(mat_chassis)
    chassis.parent = root

    bev = chassis.modifiers.new("Bevel", 'BEVEL')
    bev.width = 0.003
    bev.segments = 2

    # Top Brass Faceplate
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.0755))
    faceplate = bpy.context.active_object
    faceplate.name = "Deck_Faceplate"
    faceplate.scale = (0.192, 0.142, 0.002)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    faceplate.data.materials.append(mat_metal)
    faceplate.parent = root

    # Slot Bezel on top surface: tilted 18° backwards around X
    slot_tilt = math.radians(18.0)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.015, 0.075))
    slot_bezel = bpy.context.active_object
    slot_bezel.name = "Slot_Bezel"
    slot_bezel.rotation_euler = (slot_tilt, 0, 0)
    slot_bezel.scale = (0.114, 0.024, 0.014)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    slot_bezel.data.materials.append(mat_metal)
    slot_bezel.parent = root

    # Dark cavity interior
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.015, 0.068))
    slot_cavity = bpy.context.active_object
    slot_cavity.name = "Slot_Cavity"
    slot_cavity.rotation_euler = (slot_tilt, 0, 0)
    slot_cavity.scale = (0.108, 0.018, 0.035)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    slot_cavity.data.materials.append(mat_cavity)
    slot_cavity.parent = root

    # Physical EJECT button on the FRONT area of the top brass plate:
    # X = 0.035m (right), Y = -0.040m (front), Z = 0.082m (top)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.035, -0.040, 0.082))
    eject_btn = bpy.context.active_object
    eject_btn.name = "Eject_Button"
    eject_btn.scale = (0.034, 0.020, 0.012)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    eject_btn.data.materials.append(mat_eject)
    eject_btn.parent = root

    bev_btn = eject_btn.modifiers.new("Bevel", 'BEVEL')
    bev_btn.width = 0.0018
    bev_btn.segments = 2

    # Eject Button Bezel Ring
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.035, -0.040, 0.077))
    eject_ring = bpy.context.active_object
    eject_ring.name = "Eject_Bezel"
    eject_ring.scale = (0.040, 0.026, 0.004)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    eject_ring.data.materials.append(mat_metal)
    eject_ring.parent = root

    # Status LED on the FRONT-LEFT of the top plate:
    # X = -0.035m (left), Y = -0.040m (front), Z = 0.078m
    bpy.ops.mesh.primitive_cylinder_add(radius=0.005, depth=0.004, location=(-0.035, -0.040, 0.078))
    led = bpy.context.active_object
    led.name = "Status_LED"
    led.data.materials.append(mat_led)
    led.parent = root

    all_objs = [root, chassis, faceplate, slot_bezel, slot_cavity, eject_btn, eject_ring, led]
    bpy.ops.object.select_all(action='DESELECT')
    for obj in all_objs:
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
    print(f"Exported CassetteDeckSlot FBX to {export_path}")

def build_organizer_rack(export_path):
    reset_scene()

    mat_wood  = create_material("M_Rack_Wood", (0.24, 0.14, 0.09, 1.0), metallic=0.08, roughness=0.6)
    mat_metal = create_material("M_Rack_Trim", (0.88, 0.74, 0.38, 1.0), metallic=0.9, roughness=0.25)

    root = bpy.data.objects.new("CassetteOrganizerRack", None)
    bpy.context.collection.objects.link(root)

    # Stepped Wooden Rack Base: 0.14m wide (X), 0.14m deep (Y), 0.035m tall (Z)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.0175))
    base = bpy.context.active_object
    base.name = "Rack_Base"
    base.scale = (0.14, 0.14, 0.035)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    base.data.materials.append(mat_wood)
    base.parent = root

    bev = base.modifiers.new("Bevel", 'BEVEL')
    bev.width = 0.002
    bev.segments = 2

    # 5 Stepped Dividers tilted 18° backwards around X
    tilt_rad = math.radians(18.0)
    y_positions = [-0.048, -0.022, 0.004, 0.030, 0.056]
    z_heights   = [0.035,  0.042,  0.049, 0.056, 0.063]
    for i in range(5):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, y_positions[i], z_heights[i]))
        div = bpy.context.active_object
        div.name = f"Divider_{i}"
        div.rotation_euler = (tilt_rad, 0, 0)
        div.scale = (0.132, 0.004, 0.024)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        div.data.materials.append(mat_metal)
        div.parent = root

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
    print(f"Exported CassetteOrganizerRack FBX to {export_path}")

if __name__ == '__main__':
    out_dir = r"D:\Alunos\Alexandre\Projeto IC\Assets\Models\Generated\Task06"
    os.makedirs(out_dir, exist_ok=True)

    build_cassette_tape(os.path.join(out_dir, "RetroCassetteTape.fbx"))
    build_deck_slot(os.path.join(out_dir, "CassetteDeckSlot.fbx"))
    build_organizer_rack(os.path.join(out_dir, "CassetteOrganizerRack.fbx"))
    print("All Task06 3D models updated and generated successfully!")
