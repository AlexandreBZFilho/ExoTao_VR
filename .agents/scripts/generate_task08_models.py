import bpy
import mathutils
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

def build_arcade_basketball_hoop(output_path):
    clear_scene()

    # Materials
    mat_backboard = create_material("M_Hoop_Backboard", (0.10, 0.12, 0.16, 0.85), metallic=0.1, roughness=0.1)
    mat_frame_metal = create_material("M_Hoop_FrameMetal", (0.15, 0.16, 0.18, 1.0), metallic=0.85, roughness=0.3)
    mat_neon_trim = create_material("M_Hoop_NeonTrim", (0.0, 0.85, 1.0, 1.0), metallic=0.1, roughness=0.2, emission_color=(0.0, 0.85, 1.0, 1.0), emission_strength=2.5)
    mat_target_white = create_material("M_Hoop_TargetWhite", (0.95, 0.95, 0.95, 1.0), metallic=0.1, roughness=0.3, emission_color=(0.95, 0.95, 0.95, 1.0), emission_strength=1.2)
    mat_rim_orange = create_material("M_Hoop_RimOrange", (1.0, 0.35, 0.02, 1.0), metallic=0.75, roughness=0.25, emission_color=(1.0, 0.35, 0.02, 1.0), emission_strength=1.5)
    mat_net_white = create_material("M_Hoop_NetWhite", (0.92, 0.92, 0.90, 0.90), metallic=0.0, roughness=0.6)
    mat_display_glass = create_material("M_Hoop_DisplayGlass", (0.04, 0.05, 0.07, 1.0), metallic=0.3, roughness=0.15)

    created_parts = []

    # 1. Backboard Acrylic Pane: 0.70m wide (X), 0.50m high (Z), 0.025m thick (Y)
    # Centered at (0, 0.0125, 0)
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=(0, 0.0125, 0)
    )
    pane = bpy.context.active_object
    pane.name = "Backboard_Pane"
    pane.scale = (0.70, 0.025, 0.50)
    pane.data.materials.append(mat_backboard)
    created_parts.append(pane)

    # 2. Backboard Neon Outer Frame: Tubular/beveled border
    # Top frame
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.015, 0.255))
    f_top = bpy.context.active_object
    f_top.name = "Frame_Top"
    f_top.scale = (0.73, 0.035, 0.025)
    f_top.data.materials.append(mat_neon_trim)
    created_parts.append(f_top)

    # Bottom frame
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.015, -0.255))
    f_bot = bpy.context.active_object
    f_bot.name = "Frame_Bottom"
    f_bot.scale = (0.73, 0.035, 0.025)
    f_bot.data.materials.append(mat_neon_trim)
    created_parts.append(f_bot)

    # Left frame
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(-0.355, 0.015, 0))
    f_left = bpy.context.active_object
    f_left.name = "Frame_Left"
    f_left.scale = (0.025, 0.035, 0.52)
    f_left.data.materials.append(mat_neon_trim)
    created_parts.append(f_left)

    # Right frame
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.355, 0.015, 0))
    f_right = bpy.context.active_object
    f_right.name = "Frame_Right"
    f_right.scale = (0.025, 0.035, 0.52)
    f_right.data.materials.append(mat_neon_trim)
    created_parts.append(f_right)

    # 3. Target Rectangle on Backboard (inner square): 0.24m wide, 0.18m high, line width 0.015m
    # Centered above rim at Z = -0.04m, Y = 0.026m (protruding slightly in front of pane)
    z_target_center = -0.04
    # Horizontal top
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.026, z_target_center + 0.09))
    t_top = bpy.context.active_object
    t_top.name = "Target_Top"
    t_top.scale = (0.24, 0.005, 0.015)
    t_top.data.materials.append(mat_target_white)
    created_parts.append(t_top)

    # Horizontal bottom
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.026, z_target_center - 0.09))
    t_bot = bpy.context.active_object
    t_bot.name = "Target_Bottom"
    t_bot.scale = (0.24, 0.005, 0.015)
    t_bot.data.materials.append(mat_target_white)
    created_parts.append(t_bot)

    # Vertical left
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(-0.115, 0.026, z_target_center))
    t_left = bpy.context.active_object
    t_left.name = "Target_Left"
    t_left.scale = (0.015, 0.005, 0.18)
    t_left.data.materials.append(mat_target_white)
    created_parts.append(t_left)

    # Vertical right
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.115, 0.026, z_target_center))
    t_right = bpy.context.active_object
    t_right.name = "Target_Right"
    t_right.scale = (0.015, 0.005, 0.18)
    t_right.data.materials.append(mat_target_white)
    created_parts.append(t_right)

    # 4. Scoreboard Box at top of Backboard
    # Width 0.50m, Height 0.16m, Depth 0.09m, at Z = 0.35m
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.040, 0.35))
    sb_box = bpy.context.active_object
    sb_box.name = "Scoreboard_Box"
    sb_box.scale = (0.50, 0.09, 0.16)
    sb_box.data.materials.append(mat_frame_metal)
    created_parts.append(sb_box)

    # Scoreboard front glass panel (where TextMeshPro displays will sit)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.086, 0.35))
    sb_glass = bpy.context.active_object
    sb_glass.name = "Scoreboard_Screen"
    sb_glass.scale = (0.46, 0.005, 0.13)
    sb_glass.data.materials.append(mat_display_glass)
    created_parts.append(sb_glass)

    # Scoreboard top neon accent
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.040, 0.435))
    sb_neon = bpy.context.active_object
    sb_neon.name = "Scoreboard_NeonBar"
    sb_neon.scale = (0.52, 0.095, 0.015)
    sb_neon.data.materials.append(mat_neon_trim)
    created_parts.append(sb_neon)

    # 5. Hoop Mounting Bracket & Rim
    # Bracket attached at base of target: Z = -0.13m, extends from Y = 0.025 to Y = 0.14m
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.0825, -0.13))
    bracket = bpy.context.active_object
    bracket.name = "Hoop_Bracket"
    bracket.scale = (0.09, 0.115, 0.045)
    bracket.data.materials.append(mat_frame_metal)
    created_parts.append(bracket)

    # Metal Rim Torus:
    # Inner diameter 0.38m -> Major radius = (0.38 + 0.025)/2 = 0.2025m
    # Minor radius (pipe radius) = 0.0125m (pipe diameter 0.025m)
    # Rim center positioned at (X=0, Y=0.3425m, Z=-0.13m)
    rim_center_y = 0.3425
    rim_center_z = -0.13
    bpy.ops.mesh.primitive_torus_add(
        major_radius=0.2025,
        minor_radius=0.0125,
        major_segments=48,
        minor_segments=16,
        location=(0, rim_center_y, rim_center_z),
        rotation=(0, 0, 0)
    )
    rim = bpy.context.active_object
    rim.name = "Hoop_Rim"
    rim.data.materials.append(mat_rim_orange)
    created_parts.append(rim)

    # Diagonal support struts from bracket to rim sides
    for side in (-1, 1):
        p1 = mathutils.Vector((side * 0.045, 0.08, rim_center_z))
        p2 = mathutils.Vector((side * 0.15, rim_center_y - 0.05, rim_center_z))
        mid = (p1 + p2) * 0.5
        v = p2 - p1
        length = v.length
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=12,
            radius=0.007,
            depth=length,
            location=mid
        )
        strut = bpy.context.active_object
        strut.name = f"Rim_Strut_{side}"
        strut.rotation_euler = v.to_track_quat('Z', 'Y').to_euler()
        strut.data.materials.append(mat_frame_metal)
        created_parts.append(strut)

    # 6. Hoop Net:
    # Modeled as a stylized segmented nylon net with 12 vertical rib cords and 5 horizontal ring cords
    # Hanging from Z = -0.13 down to Z = -0.48 (length 0.35m)
    # Radius tapers from 0.19m at top to 0.11m at bottom
    num_ribs = 12
    net_length = 0.35
    top_z = rim_center_z
    bot_z = top_z - net_length
    top_rad = 0.195
    bot_rad = 0.115

    # Net horizontal rings (5 rings)
    for r_idx in range(5):
        frac = r_idx / 4.0
        r_z = top_z - frac * net_length
        curr_rad = top_rad + (bot_rad - top_rad) * frac
        bpy.ops.mesh.primitive_torus_add(
            major_radius=curr_rad,
            minor_radius=0.0035,
            major_segments=32,
            minor_segments=8,
            location=(0, rim_center_y, r_z)
        )
        n_ring = bpy.context.active_object
        n_ring.name = f"Net_Ring_{r_idx}"
        n_ring.data.materials.append(mat_net_white)
        created_parts.append(n_ring)

    # Net vertical ribs (12 cords)
    for i in range(num_ribs):
        angle = 2.0 * math.pi * i / num_ribs
        x1 = math.cos(angle) * top_rad
        y1 = rim_center_y + math.sin(angle) * top_rad
        x2 = math.cos(angle) * bot_rad
        y2 = rim_center_y + math.sin(angle) * bot_rad
        
        mid_x = (x1 + x2) * 0.5
        mid_y = (y1 + y2) * 0.5
        mid_z = (top_z + bot_z) * 0.5
        
        v = mathutils.Vector((x2 - x1, y2 - y1, bot_z - top_z))
        length = v.length
        
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=8,
            radius=0.003,
            depth=length,
            location=(mid_x, mid_y, mid_z)
        )
        cord = bpy.context.active_object
        cord.name = f"Net_Cord_{i}"
        cord.rotation_euler = v.to_track_quat('Z', 'Y').to_euler()
        cord.data.materials.append(mat_net_white)
        created_parts.append(cord)

    # 7. Rear Wall Mount Plates
    # 2 vertical steel rails on the back (Y = -0.015m) to anchor to the wall
    for side in (-1, 1):
        bpy.ops.mesh.primitive_cube_add(
            size=1.0,
            location=(side * 0.28, -0.015, 0.05)
        )
        rail = bpy.context.active_object
        rail.name = f"Wall_Rail_{side}"
        rail.scale = (0.04, 0.03, 0.65)
        rail.data.materials.append(mat_frame_metal)
        created_parts.append(rail)

    # Apply all transforms to parts BEFORE joining to prevent non-uniform mesh scale distortion!
    for part in created_parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = pane
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.shade_smooth()

    # Join into single comprehensive object or maintain hierarchy
    # Joining all geometry into single FBX root for clean Unity import
    bpy.ops.object.join()
    hoop_root = bpy.context.active_object
    hoop_root.name = "ArcadeBasketballHoop"
    hoop_root.location = (0, 0, 0)

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
    print(f"Exported ArcadeBasketballHoop to {output_path}")

def build_mini_basketball(output_path):
    clear_scene()

    mat_rubber = create_material("M_Basketball_Rubber", (0.88, 0.36, 0.06, 1.0), metallic=0.05, roughness=0.68)
    mat_seams = create_material("M_Basketball_Seams", (0.08, 0.08, 0.09, 1.0), metallic=0.1, roughness=0.8)

    # Main ball sphere: diameter 0.18m (radius 0.09m)
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=48,
        ring_count=32,
        radius=0.09,
        location=(0, 0, 0)
    )
    ball = bpy.context.active_object
    ball.name = "Basketball_Body"
    ball.data.materials.append(mat_rubber)

    # Basketball black seam ribs:
    # 3 orthogonal toruses slightly inset/outset with 0.003m minor radius
    seam_toruses = []
    # Equator seam (XY plane)
    bpy.ops.mesh.primitive_torus_add(
        major_radius=0.0895,
        minor_radius=0.0032,
        major_segments=48,
        minor_segments=12,
        location=(0, 0, 0),
        rotation=(0, 0, 0)
    )
    s_eq = bpy.context.active_object
    s_eq.data.materials.append(mat_seams)
    seam_toruses.append(s_eq)

    # Meridian 1 (XZ plane)
    bpy.ops.mesh.primitive_torus_add(
        major_radius=0.0895,
        minor_radius=0.0032,
        major_segments=48,
        minor_segments=12,
        location=(0, 0, 0),
        rotation=(math.radians(90), 0, 0)
    )
    s_m1 = bpy.context.active_object
    s_m1.data.materials.append(mat_seams)
    seam_toruses.append(s_m1)

    # Meridian 2 (YZ plane)
    bpy.ops.mesh.primitive_torus_add(
        major_radius=0.0895,
        minor_radius=0.0032,
        major_segments=48,
        minor_segments=12,
        location=(0, 0, 0),
        rotation=(0, math.radians(90), 0)
    )
    s_m2 = bpy.context.active_object
    s_m2.data.materials.append(mat_seams)
    seam_toruses.append(s_m2)

    # Curved side seams (parabolic ribs characteristic of basketballs)
    for side in (-1, 1):
        bpy.ops.mesh.primitive_torus_add(
            major_radius=0.075,
            minor_radius=0.0028,
            major_segments=48,
            minor_segments=12,
            location=(side * 0.048, 0, 0),
            rotation=(0, math.radians(90), 0)
        )
        s_curve = bpy.context.active_object
        s_curve.data.materials.append(mat_seams)
        seam_toruses.append(s_curve)

    # Join into single ball mesh
    for s in seam_toruses:
        s.select_set(True)
    ball.select_set(True)
    bpy.context.view_layer.objects.active = ball
    bpy.ops.object.shade_smooth()
    bpy.ops.object.join()

    ball_root = bpy.context.active_object
    ball_root.name = "MiniBasketball"
    ball_root.location = (0, 0, 0)

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
    print(f"Exported MiniBasketball to {output_path}")

def build_arcade_spawn_button(output_path):
    clear_scene()

    mat_chassis = create_material("M_ArcadeBtn_Chassis", (0.10, 0.11, 0.13, 1.0), metallic=0.75, roughness=0.35)
    mat_chrome = create_material("M_ArcadeBtn_Chrome", (0.85, 0.88, 0.92, 1.0), metallic=0.95, roughness=0.1)
    mat_bezel = create_material("M_ArcadeBtn_Bezel", (0.05, 0.05, 0.06, 1.0), metallic=0.3, roughness=0.5)
    mat_dome = create_material("M_ArcadeBtn_Dome", (0.95, 0.15, 0.12, 0.95), metallic=0.1, roughness=0.15, emission_color=(0.95, 0.15, 0.12, 1.0), emission_strength=2.0)
    mat_label = create_material("M_ArcadeBtn_Label", (0.92, 0.92, 0.95, 1.0), metallic=0.2, roughness=0.4, emission_color=(0.0, 0.85, 1.0, 1.0), emission_strength=1.5)

    created_objects = []

    # 1. Base Box: 0.12m x 0.12m x 0.04m, bottom at Z = 0
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=(0, 0, 0.020)
    )
    base = bpy.context.active_object
    base.name = "Button_Base"
    base.scale = (0.12, 0.12, 0.040)
    base.data.materials.append(mat_chassis)
    created_objects.append(base)

    # 4 Chrome corner screws
    for cx in (-0.05, 0.05):
        for cy in (-0.05, 0.05):
            bpy.ops.mesh.primitive_cylinder_add(
                vertices=12,
                radius=0.005,
                depth=0.005,
                location=(cx, cy, 0.042)
            )
            bolt = bpy.context.active_object
            bolt.name = f"Corner_Bolt_{cx}_{cy}"
            bolt.data.materials.append(mat_chrome)
            created_objects.append(bolt)

    # 2. Bezel Ring: Diameter 0.096m, Height 0.012m at Z = 0.044m
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=32,
        radius=0.048,
        depth=0.012,
        location=(0, 0, 0.046)
    )
    bezel = bpy.context.active_object
    bezel.name = "Button_Bezel"
    bezel.data.materials.append(mat_bezel)
    created_objects.append(bezel)

    # 3. Label Plate: Front-facing angled nameplate "BALL"
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=(0, -0.061, 0.020)
    )
    plate = bpy.context.active_object
    plate.name = "Button_Label_Plate"
    plate.scale = (0.075, 0.004, 0.018)
    plate.data.materials.append(mat_label)
    created_objects.append(plate)

    # Join base + bezel + bolts + plate into single chassis mesh
    for obj in created_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = base
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.shade_smooth()
    bpy.ops.object.join()
    chassis = bpy.context.active_object
    chassis.name = "ArcadeButton_Chassis"

    # 4. Separate Button Dome Object (for script physical press depression animation!)
    # Domed hemisphere cylinder: Diameter 0.080m (radius 0.040m), height 0.022m
    # Pivot at local bottom (Z = 0.046m)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=32,
        radius=0.040,
        depth=0.016,
        location=(0, 0, 0.052)
    )
    dome = bpy.context.active_object
    dome.name = "Button_Dome"

    # Add rounded dome cap on top
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=32,
        ring_count=16,
        radius=0.040,
        location=(0, 0, 0.052)
    )
    dome_cap = bpy.context.active_object
    dome_cap.scale = (1.0, 1.0, 0.40)

    dome.select_set(True)
    dome_cap.select_set(True)
    bpy.context.view_layer.objects.active = dome
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.shade_smooth()
    bpy.ops.object.join()
    dome = bpy.context.active_object
    dome.data.materials.append(mat_dome)

    # Set parent: Dome is child of Chassis
    dome.parent = chassis

    # Select both for FBX export
    bpy.ops.object.select_all(action='SELECT')

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
    print(f"Exported ArcadeSpawnButton to {output_path}")

if __name__ == "__main__":
    base_dir = os.path.abspath("Assets/Models/Generated/Task08")
    hoop_path = os.path.join(base_dir, "ArcadeBasketballHoop.fbx")
    ball_path = os.path.join(base_dir, "MiniBasketball.fbx")
    btn_path = os.path.join(base_dir, "ArcadeSpawnButton.fbx")

    build_arcade_basketball_hoop(hoop_path)
    build_mini_basketball(ball_path)
    build_arcade_spawn_button(btn_path)
    print("ALL TASK-08 3D MODELS GENERATED SUCCESSFULLY!")
