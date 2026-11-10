import bpy
import bmesh
import math
import os

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def create_material(name, diffuse_color, metallic=0.0, roughness=0.5, is_emissive=False, emission_color=(0,0,0,1), emission_strength=1.5):
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
                bsdf.inputs['Emission Strength'].default_value = emission_strength
            elif 'Emission' in bsdf.inputs:
                bsdf.inputs['Emission'].default_value = emission_color
    return mat

def build_atmosphere_lever(export_path):
    reset_scene()
    print("Building EnvironmentControlLever 3D model...")

    mat_chassis = create_material("M_Lever_Chassis", (0.14, 0.16, 0.19, 1.0), metallic=0.75, roughness=0.35)
    mat_metal = create_material("M_Lever_Metal", (0.85, 0.88, 0.92, 1.0), metallic=0.92, roughness=0.15)
    mat_bezel = create_material("M_Lever_Bezel", (0.08, 0.09, 0.11, 1.0), metallic=0.20, roughness=0.60)
    mat_knob = create_material("M_Lever_Knob", (0.88, 0.22, 0.12, 1.0), metallic=0.45, roughness=0.28)
    mat_ring = create_material("M_Lever_Ring", (0.0, 0.85, 1.0, 1.0), metallic=0.10, roughness=0.20, is_emissive=True, emission_color=(0.0, 0.85, 1.0, 1.0), emission_strength=2.5)
    
    mat_led_day = create_material("M_Lever_LedDay", (1.0, 0.85, 0.15, 1.0), metallic=0.10, roughness=0.20, is_emissive=True, emission_color=(1.0, 0.85, 0.15, 1.0), emission_strength=2.2)
    mat_led_sunset = create_material("M_Lever_LedSunset", (1.0, 0.45, 0.08, 1.0), metallic=0.10, roughness=0.20, is_emissive=True, emission_color=(1.0, 0.45, 0.08, 1.0), emission_strength=2.2)
    mat_led_night = create_material("M_Lever_LedNight", (0.20, 0.45, 1.0, 1.0), metallic=0.10, roughness=0.20, is_emissive=True, emission_color=(0.20, 0.45, 1.0, 1.0), emission_strength=2.2)

    root = bpy.data.objects.new("EnvironmentControlLever", None)
    bpy.context.collection.objects.link(root)

    # 1. Base Chassis (0.16m wide along X, 0.12m deep along Y, 0.05m high along Z)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.025))
    chassis = bpy.context.active_object
    chassis.name = "Lever_Chassis"
    chassis.scale = (0.16, 0.12, 0.05)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    chassis.data.materials.append(mat_chassis)
    chassis.parent = root

    bev = chassis.modifiers.new("Bevel", 'BEVEL')
    bev.width = 0.008
    bev.segments = 3

    # 2. Guide Slot Channel
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.048))
    slot = bpy.context.active_object
    slot.name = "Lever_Slot_Guide"
    slot.scale = (0.11, 0.024, 0.010)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    slot.data.materials.append(mat_bezel)
    slot.parent = root

    # 3. Indicator LEDs for the 3 notches (-30 deg Day, 0 deg Sunset, +30 deg Night)
    led_configs = [
        ("Lever_Indicator_Day", -0.042, mat_led_day),
        ("Lever_Indicator_Sunset", 0.0, mat_led_sunset),
        ("Lever_Indicator_Night", 0.042, mat_led_night)
    ]
    for led_name, x_pos, mat in led_configs:
        bpy.ops.mesh.primitive_cylinder_add(radius=0.006, depth=0.006, vertices=16, location=(x_pos, 0.035, 0.051))
        led = bpy.context.active_object
        led.name = led_name
        led.data.materials.append(mat)
        led.parent = root

    # 4. Handle Pivot Object (Single articulated mesh with pivot origin at 0, 0, 0.035)
    # Pivot is at (0, 0, 0.035) in world space
    pivot_z = 0.035

    # Shaft (length 0.12m, radius 0.007m, center at 0, 0, pivot_z + 0.060)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.007, depth=0.12, vertices=24, location=(0, 0, pivot_z + 0.060))
    shaft = bpy.context.active_object
    shaft.name = "Lever_Shaft"
    shaft.data.materials.append(mat_metal)

    # Ball Knob (center at 0, 0, pivot_z + 0.125, radius 0.022m)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.022, segments=24, ring_count=16, location=(0, 0, pivot_z + 0.125))
    knob = bpy.context.active_object
    knob.name = "Lever_Knob"
    knob.data.materials.append(mat_knob)

    # Emissive Ring around Knob
    bpy.ops.mesh.primitive_torus_add(major_radius=0.0225, minor_radius=0.003, major_segments=24, minor_segments=12, location=(0, 0, pivot_z + 0.125))
    ring = bpy.context.active_object
    ring.name = "Lever_Knob_Ring"
    ring.data.materials.append(mat_ring)

    # Join shaft, knob, ring into one cohesive Handle Mesh
    bpy.ops.object.select_all(action='DESELECT')
    shaft.select_set(True)
    knob.select_set(True)
    ring.select_set(True)
    bpy.context.view_layer.objects.active = shaft
    bpy.ops.object.join()

    handle_obj = bpy.context.active_object
    handle_obj.name = "Lever_Handle_Pivot"

    # Set handle origin precisely at pivot point (0, 0, pivot_z)
    bpy.context.scene.cursor.location = (0, 0, pivot_z)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

    handle_obj.parent = root

    # Export FBX
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
    print(f"Exported EnvironmentControlLever FBX to {export_path}")

def build_fireworks_button(export_path):
    reset_scene()
    print("Building FireworksLaunchButton 3D model...")

    mat_chassis = create_material("M_FwBtn_Chassis", (0.12, 0.13, 0.16, 1.0), metallic=0.60, roughness=0.40)
    mat_chrome = create_material("M_FwBtn_Chrome", (0.88, 0.90, 0.94, 1.0), metallic=0.95, roughness=0.10)
    mat_dome = create_material("M_FwBtn_Dome", (0.95, 0.12, 0.10, 1.0), metallic=0.15, roughness=0.20, is_emissive=True, emission_color=(0.95, 0.12, 0.10, 1.0), emission_strength=1.8)
    mat_gold = create_material("M_FwBtn_Gold", (1.0, 0.78, 0.15, 1.0), metallic=0.88, roughness=0.22)
    mat_ring = create_material("M_FwBtn_Ring", (1.0, 0.60, 0.0, 1.0), metallic=0.10, roughness=0.25, is_emissive=True, emission_color=(1.0, 0.60, 0.0, 1.0), emission_strength=2.2)

    root = bpy.data.objects.new("FireworksLaunchButton", None)
    bpy.context.collection.objects.link(root)

    # 1. Base Housing
    bpy.ops.mesh.primitive_cylinder_add(radius=0.065, depth=0.042, vertices=8, location=(0, 0, 0.021))
    chassis = bpy.context.active_object
    chassis.name = "Button_Chassis"
    chassis.data.materials.append(mat_chassis)
    chassis.parent = root

    bev = chassis.modifiers.new("Bevel", 'BEVEL')
    bev.width = 0.005
    bev.segments = 2

    # 2. Chrome Bezel Ring
    bpy.ops.mesh.primitive_cylinder_add(radius=0.050, depth=0.012, vertices=32, location=(0, 0, 0.044))
    bezel = bpy.context.active_object
    bezel.name = "Button_Bezel_Chrome"
    bezel.data.materials.append(mat_chrome)
    bezel.parent = root

    # 3. Emissive Accent Ring
    bpy.ops.mesh.primitive_torus_add(major_radius=0.052, minor_radius=0.0025, major_segments=32, minor_segments=8, location=(0, 0, 0.042))
    glow_ring = bpy.context.active_object
    glow_ring.name = "Button_Glow_Ring"
    glow_ring.data.materials.append(mat_ring)
    glow_ring.parent = root

    # 4. Button Dome
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.038, segments=32, ring_count=16, location=(0, 0, 0.044))
    dome = bpy.context.active_object
    dome.name = "Button_Dome"
    dome.scale = (1.0, 1.0, 0.55)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    dome.data.materials.append(mat_dome)
    dome.parent = root

    # 5. Label Plate
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, -0.052, 0.022))
    label_plate = bpy.context.active_object
    label_plate.name = "Button_Label_Plate"
    label_plate.scale = (0.055, 0.006, 0.016)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    label_plate.data.materials.append(mat_gold)
    label_plate.parent = root

    # Export FBX
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
    print(f"Exported FireworksLaunchButton FBX to {export_path}")

def build_mascot_bobblehead(export_path):
    reset_scene()
    print("Building ExoMascotBobblehead 3D model...")

    mat_pedestal = create_material("M_Bobble_Pedestal", (0.13, 0.14, 0.17, 1.0), metallic=0.70, roughness=0.35)
    mat_armor = create_material("M_Bobble_Armor", (0.05, 0.42, 0.65, 1.0), metallic=0.65, roughness=0.25)
    mat_body_trim = create_material("M_Bobble_Trim", (0.85, 0.88, 0.92, 1.0), metallic=0.85, roughness=0.20)
    mat_spring = create_material("M_Bobble_Spring", (0.75, 0.77, 0.80, 1.0), metallic=0.95, roughness=0.15)
    mat_visor = create_material("M_Bobble_Visor", (0.0, 0.95, 1.0, 1.0), metallic=0.10, roughness=0.15, is_emissive=True, emission_color=(0.0, 0.95, 1.0, 1.0), emission_strength=2.6)
    mat_ears = create_material("M_Bobble_Ears", (1.0, 0.55, 0.05, 1.0), metallic=0.50, roughness=0.30, is_emissive=True, emission_color=(1.0, 0.55, 0.05, 1.0), emission_strength=1.8)

    root = bpy.data.objects.new("ExoMascotBobblehead", None)
    bpy.context.collection.objects.link(root)

    # 1. Base Pedestal
    bpy.ops.mesh.primitive_cylinder_add(radius=0.045, depth=0.018, vertices=32, location=(0, 0, 0.009))
    pedestal = bpy.context.active_object
    pedestal.name = "Bobble_Pedestal"
    pedestal.data.materials.append(mat_pedestal)
    pedestal.parent = root

    bev = pedestal.modifiers.new("Bevel", 'BEVEL')
    bev.width = 0.003
    bev.segments = 2

    # Pedestal accent ring
    bpy.ops.mesh.primitive_torus_add(major_radius=0.046, minor_radius=0.002, major_segments=32, minor_segments=8, location=(0, 0, 0.012))
    ped_ring = bpy.context.active_object
    ped_ring.name = "Bobble_Pedestal_Ring"
    ped_ring.data.materials.append(mat_visor)
    ped_ring.parent = root

    # 2. Torso
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.048))
    torso = bpy.context.active_object
    torso.name = "Bobble_Torso"
    torso.scale = (0.052, 0.040, 0.056)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    torso.data.materials.append(mat_armor)
    torso.parent = root

    bev_t = torso.modifiers.new("Bevel", 'BEVEL')
    bev_t.width = 0.008
    bev_t.segments = 3

    # Chestplate reactor
    bpy.ops.mesh.primitive_cylinder_add(radius=0.010, depth=0.006, vertices=16, location=(0, -0.021, 0.052))
    chest = bpy.context.active_object
    chest.name = "Bobble_Chest_Reactor"
    chest.rotation_euler = (math.radians(90.0), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    chest.data.materials.append(mat_visor)
    chest.parent = root

    # Chibi arms
    for side, sign in [("L", -1), ("R", 1)]:
        bpy.ops.mesh.primitive_cylinder_add(radius=0.007, depth=0.032, vertices=16, location=(sign * 0.030, 0, 0.042))
        arm = bpy.context.active_object
        arm.name = f"Bobble_Arm_{side}"
        arm.rotation_euler = (0, sign * math.radians(18.0), 0)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        arm.data.materials.append(mat_body_trim)
        arm.parent = root

    # 3. Neck Spring
    bpy.ops.mesh.primitive_cylinder_add(radius=0.009, depth=0.018, vertices=16, location=(0, 0, 0.084))
    spring = bpy.context.active_object
    spring.name = "Bobble_Neck_Spring"
    spring.data.materials.append(mat_spring)
    spring.parent = root

    # 4. Chibi Head (Combined mesh with pivot at neck center 0, 0, 0.088)
    neck_pivot_z = 0.088

    # Helmet
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, neck_pivot_z + 0.038))
    helmet = bpy.context.active_object
    helmet.name = "Head_Helmet"
    helmet.scale = (0.074, 0.070, 0.068)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    helmet.data.materials.append(mat_armor)

    bev_h = helmet.modifiers.new("Bevel", 'BEVEL')
    bev_h.width = 0.016
    bev_h.segments = 4

    # Visor
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, -0.036, neck_pivot_z + 0.038))
    visor = bpy.context.active_object
    visor.name = "Head_Visor"
    visor.scale = (0.058, 0.005, 0.030)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    visor.data.materials.append(mat_visor)

    head_parts = [helmet, visor]

    # EarPods & Antennae
    for side, sign in [("L", -1), ("R", 1)]:
        bpy.ops.mesh.primitive_cylinder_add(radius=0.012, depth=0.010, vertices=16, location=(sign * 0.039, 0, neck_pivot_z + 0.038))
        ear = bpy.context.active_object
        ear.name = f"Head_EarPod_{side}"
        ear.rotation_euler = (0, math.radians(90.0), 0)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        ear.data.materials.append(mat_body_trim)
        head_parts.append(ear)

        bpy.ops.mesh.primitive_cone_add(radius1=0.004, radius2=0.001, depth=0.024, vertices=12, location=(sign * 0.040, 0, neck_pivot_z + 0.055))
        ant = bpy.context.active_object
        ant.name = f"Head_Antenna_{side}"
        ant.rotation_euler = (0, sign * math.radians(-25.0), 0)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        ant.data.materials.append(mat_ears)
        head_parts.append(ant)

    # Apply modifiers on helmet before joining
    bpy.context.view_layer.objects.active = helmet
    bpy.ops.object.modifier_apply(modifier="Bevel")

    # Join all head parts into one cohesive Bobble_Head_Pivot mesh
    bpy.ops.object.select_all(action='DESELECT')
    for part in head_parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = helmet
    bpy.ops.object.join()

    head_obj = bpy.context.active_object
    head_obj.name = "Bobble_Head_Pivot"

    # Set origin at neck joint (0, 0, neck_pivot_z)
    bpy.context.scene.cursor.location = (0, 0, neck_pivot_z)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

    head_obj.parent = root

    # Export FBX
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
    print(f"Exported ExoMascotBobblehead FBX to {export_path}")

if __name__ == '__main__':
    out_dir = r"D:\Alunos\Alexandre\Projeto IC\Assets\Models\Generated\Task09"
    os.makedirs(out_dir, exist_ok=True)

    build_atmosphere_lever(os.path.join(out_dir, "EnvironmentControlLever.fbx"))
    build_fireworks_button(os.path.join(out_dir, "FireworksLaunchButton.fbx"))
    build_mascot_bobblehead(os.path.join(out_dir, "ExoMascotBobblehead.fbx"))
    print("All Task09 3D FBX models regenerated successfully with unified pivot meshes!")
