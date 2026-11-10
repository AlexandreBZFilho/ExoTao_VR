import bpy
import bmesh
import math
import os

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def create_material(name, diffuse_color, metallic=0.0, roughness=0.5, is_emissive=False, emission_color=(0,0,0,1), emission_strength=1.0):
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

def export_fbx(root_obj, export_path):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in bpy.context.scene.objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root_obj
    
    bpy.ops.export_scene.fbx(
        filepath=export_path,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True
    )
    print(f"Exported FBX to {export_path}")

# ==============================================================================
# 1. XylophoneMushroom.fbx
# ==============================================================================
def build_xylophone_mushroom(export_path):
    reset_scene()
    
    mat_stem = create_material("M_Mushroom_Stem", (0.92, 0.88, 0.80, 1.0), metallic=0.05, roughness=0.75)
    mat_cap = create_material("M_Mushroom_Cap", (0.15, 0.65, 0.75, 1.0), metallic=0.1, roughness=0.40)
    mat_gills = create_material("M_Mushroom_Gills", (0.82, 0.76, 0.65, 1.0), metallic=0.05, roughness=0.85)
    mat_glow = create_material("M_Mushroom_Glow", (0.0, 0.95, 1.0, 1.0), metallic=0.1, roughness=0.1,
                               is_emissive=True, emission_color=(0.0, 0.95, 1.0, 1.0), emission_strength=4.0)
    
    root = bpy.data.objects.new("XylophoneMushroom", None)
    bpy.context.collection.objects.link(root)
    
    # 1.1 Stem (smooth organic stalk from Z=0 to Z=0.18)
    bm_stem = bmesh.new()
    num_rings = 7
    stem_rings = []
    
    for r_idx in range(num_rings):
        frac = r_idx / (num_rings - 1)
        z = frac * 0.18
        curve_y = -0.015 * math.sin(math.pi * frac)
        radius = 0.036 * (1.0 - 0.42 * frac + 0.14 * (frac ** 2))
        
        verts_ring = []
        segments = 14
        for s in range(segments):
            angle = s * (2.0 * math.pi / segments)
            vx = math.cos(angle) * radius
            vy = curve_y + math.sin(angle) * radius
            verts_ring.append(bm_stem.verts.new((vx, vy, z)))
        stem_rings.append(verts_ring)
        
    # Cap bottom
    center_bottom = bm_stem.verts.new((0.0, 0.0, 0.0))
    for s in range(segments):
        s_next = (s + 1) % segments
        bm_stem.faces.new([center_bottom, stem_rings[0][s_next], stem_rings[0][s]])
        
    # Bridge rings
    for r_idx in range(num_rings - 1):
        for s in range(segments):
            s_next = (s + 1) % segments
            bm_stem.faces.new([
                stem_rings[r_idx][s],
                stem_rings[r_idx][s_next],
                stem_rings[r_idx + 1][s_next],
                stem_rings[r_idx + 1][s]
            ])
            
    mesh_stem = bpy.data.meshes.new("Mushroom_Stem_Mesh")
    bm_stem.to_mesh(mesh_stem)
    bm_stem.free()
    
    obj_stem = bpy.data.objects.new("Mushroom_Stem", mesh_stem)
    bpy.context.collection.objects.link(obj_stem)
    obj_stem.data.materials.append(mat_stem)
    obj_stem.parent = root
    
    # 1.2 Cap
    bm_cap = bmesh.new()
    cap_segments = 16
    cap_rings = [
        (0.165, 0.045),  # Stem connection
        (0.170, 0.138),  # Underside rim
        (0.190, 0.142),  # Outer equator
        (0.230, 0.125),  # Upper dome
        (0.258, 0.080),  # Striking plateau edge
        (0.268, 0.000)   # Plateau center
    ]
    
    cap_vert_rings = []
    for z, rad in cap_rings:
        if rad == 0.0:
            apex_v = bm_cap.verts.new((0.0, 0.0, z))
            cap_vert_rings.append([apex_v])
        else:
            ring = []
            for s in range(cap_segments):
                angle = s * (2.0 * math.pi / cap_segments)
                vx = math.cos(angle) * rad
                vy = math.sin(angle) * rad
                ring.append(bm_cap.verts.new((vx, vy, z)))
            cap_vert_rings.append(ring)
            
    for r in range(len(cap_rings) - 2):
        for s in range(cap_segments):
            s_next = (s + 1) % cap_segments
            bm_cap.faces.new([
                cap_vert_rings[r][s],
                cap_vert_rings[r][s_next],
                cap_vert_rings[r + 1][s_next],
                cap_vert_rings[r + 1][s]
            ])
    for s in range(cap_segments):
        s_next = (s + 1) % cap_segments
        bm_cap.faces.new([
            cap_vert_rings[-2][s],
            cap_vert_rings[-2][s_next],
            cap_vert_rings[-1][0]
        ])
        
    mesh_cap = bpy.data.meshes.new("Mushroom_Cap_Mesh")
    bm_cap.to_mesh(mesh_cap)
    bm_cap.free()
    
    obj_cap = bpy.data.objects.new("Mushroom_Cap", mesh_cap)
    bpy.context.collection.objects.link(obj_cap)
    obj_cap.data.materials.append(mat_cap)
    obj_cap.parent = root
    
    # 1.3 Gills
    bm_gills = bmesh.new()
    num_gills = 16
    for g in range(num_gills):
        angle = g * (2.0 * math.pi / num_gills)
        ca = math.cos(angle)
        sa = math.sin(angle)
        v0 = bm_gills.verts.new((ca * 0.044, sa * 0.044, 0.165))
        v1 = bm_gills.verts.new((ca * 0.134, sa * 0.134, 0.170))
        v2 = bm_gills.verts.new((ca * 0.130, sa * 0.130, 0.176))
        v3 = bm_gills.verts.new((ca * 0.044, sa * 0.044, 0.172))
        bm_gills.faces.new([v0, v1, v2, v3])
        
    mesh_gills = bpy.data.meshes.new("Mushroom_Gills_Mesh")
    bm_gills.to_mesh(mesh_gills)
    bm_gills.free()
    
    obj_gills = bpy.data.objects.new("Mushroom_Gills", mesh_gills)
    bpy.context.collection.objects.link(obj_gills)
    obj_gills.data.materials.append(mat_gills)
    obj_gills.parent = root
    
    # 1.4 Glow Spots
    bm_dots = bmesh.new()
    for i in range(8):
        angle = i * (2.0 * math.pi / 8.0) + 0.15
        dx = math.cos(angle) * 0.110
        dy = math.sin(angle) * 0.110
        dz = 0.226
        res = bmesh.ops.create_icosphere(bm_dots, subdivisions=1, radius=0.012)
        for v in res['verts']:
            v.co.x += dx
            v.co.y += dy
            v.co.z += dz
            
    for i in range(5):
        angle = i * (2.0 * math.pi / 5.0) + 0.35
        dx = math.cos(angle) * 0.052
        dy = math.sin(angle) * 0.052
        dz = 0.260
        res = bmesh.ops.create_icosphere(bm_dots, subdivisions=1, radius=0.009)
        for v in res['verts']:
            v.co.x += dx
            v.co.y += dy
            v.co.z += dz
            
    mesh_dots = bpy.data.meshes.new("Mushroom_GlowDots_Mesh")
    bm_dots.to_mesh(mesh_dots)
    bm_dots.free()
    
    obj_dots = bpy.data.objects.new("Mushroom_Glow_Spots", mesh_dots)
    bpy.context.collection.objects.link(obj_dots)
    obj_dots.data.materials.append(mat_glow)
    obj_dots.parent = root
    
    export_fbx(root, export_path)

# ==============================================================================
# 2. RunePathLantern.fbx
# ==============================================================================
def build_rune_path_lantern(export_path):
    reset_scene()
    
    mat_stone = create_material("M_Lantern_Stone", (0.42, 0.45, 0.42, 1.0), metallic=0.05, roughness=0.85)
    mat_wood = create_material("M_Lantern_Wood", (0.32, 0.20, 0.12, 1.0), metallic=0.05, roughness=0.80)
    mat_runes = create_material("M_Lantern_Runes", (1.0, 0.72, 0.15, 1.0), metallic=0.1, roughness=0.2,
                                is_emissive=True, emission_color=(1.0, 0.75, 0.18, 1.0), emission_strength=3.5)
    mat_iron = create_material("M_Lantern_Iron", (0.18, 0.19, 0.20, 1.0), metallic=0.85, roughness=0.45)
    mat_flame = create_material("M_Lantern_Flame", (1.0, 0.60, 0.08, 1.0), metallic=0.0, roughness=0.1,
                                is_emissive=True, emission_color=(1.0, 0.65, 0.10, 1.0), emission_strength=5.0)
    
    root = bpy.data.objects.new("RunePathLantern", None)
    bpy.context.collection.objects.link(root)
    
    # 2.1 Stone Base (octagonal plinth from Z=0 to Z=0.22)
    bm_stone = bmesh.new()
    res_b = bmesh.ops.create_cone(
        bm_stone, cap_ends=True, segments=8, radius1=0.17, radius2=0.15, depth=0.14
    )
    for v in res_b['verts']:
        v.co.z += 0.07
        
    res_t = bmesh.ops.create_cone(
        bm_stone, cap_ends=True, segments=8, radius1=0.14, radius2=0.11, depth=0.08
    )
    for v in res_t['verts']:
        v.co.z += 0.18
        
    mesh_stone = bpy.data.meshes.new("Lantern_Stone_Mesh")
    bm_stone.to_mesh(mesh_stone)
    bm_stone.free()
    
    obj_stone = bpy.data.objects.new("Lantern_Stone_Base", mesh_stone)
    bpy.context.collection.objects.link(obj_stone)
    obj_stone.data.materials.append(mat_stone)
    obj_stone.parent = root
    
    # 2.2 Wooden Post (Z=0.22 to Z=0.68, width 0.13)
    bm_wood = bmesh.new()
    res_w = bmesh.ops.create_cube(bm_wood, size=1.0)
    for v in res_w['verts']:
        v.co.x *= 0.13
        v.co.y *= 0.13
        v.co.z *= 0.46
        v.co.z += 0.45
        
    mesh_wood = bpy.data.meshes.new("Lantern_Wood_Mesh")
    bm_wood.to_mesh(mesh_wood)
    bm_wood.free()
    
    obj_wood = bpy.data.objects.new("Lantern_Wood_Post", mesh_wood)
    bpy.context.collection.objects.link(obj_wood)
    obj_wood.data.materials.append(mat_wood)
    obj_wood.parent = root
    
    # 2.3 Carved Glowing Runes on Post (Z=0.34 to Z=0.58)
    bm_runes = bmesh.new()
    rune_faces = [
        (0.068, 0.0, True),
        (-0.068, 0.0, True),
        (0.0, 0.068, False),
        (0.0, -0.068, False)
    ]
    for rx, ry, is_x in rune_faces:
        for z_lvl in [0.38, 0.46, 0.54]:
            w = 0.016
            h = 0.024
            if is_x:
                v0 = bm_runes.verts.new((rx, -w, z_lvl))
                v1 = bm_runes.verts.new((rx, 0.0, z_lvl + h))
                v2 = bm_runes.verts.new((rx, w, z_lvl))
                v3 = bm_runes.verts.new((rx, 0.0, z_lvl - h))
            else:
                v0 = bm_runes.verts.new((-w, ry, z_lvl))
                v1 = bm_runes.verts.new((0.0, ry, z_lvl + h))
                v2 = bm_runes.verts.new((w, ry, z_lvl))
                v3 = bm_runes.verts.new((0.0, ry, z_lvl - h))
            bm_runes.faces.new([v0, v1, v2, v3])
            
    mesh_runes = bpy.data.meshes.new("Lantern_Runes_Mesh")
    bm_runes.to_mesh(mesh_runes)
    bm_runes.free()
    
    obj_runes = bpy.data.objects.new("Lantern_Runes", mesh_runes)
    bpy.context.collection.objects.link(obj_runes)
    obj_runes.data.materials.append(mat_runes)
    obj_runes.parent = root
    
    # 2.4 Forged Iron Lantern Cage and Roof (Clean, no index bleeding!)
    bm_iron = bmesh.new()
    # Bottom plate
    res_bp = bmesh.ops.create_cube(bm_iron, size=1.0)
    for v in res_bp['verts']:
        v.co.x *= 0.20
        v.co.y *= 0.20
        v.co.z *= 0.024
        v.co.z += 0.692
        
    # 4 Corner Struts (Z=0.70 to Z=0.88)
    strut_coords = [(-0.082, -0.082), (0.082, -0.082), (0.082, 0.082), (-0.082, 0.082)]
    for sx, sy in strut_coords:
        res_s = bmesh.ops.create_cube(bm_iron, size=1.0)
        for v in res_s['verts']:
            v.co.x = sx + v.co.x * 0.015
            v.co.y = sy + v.co.y * 0.015
            v.co.z = 0.790 + v.co.z * 0.180
            
    # Pyramidal Iron Roof (Z=0.88 to Z=0.98)
    # 4-sided pyramid with base at Z=0.88 (width 0.21) and apex at Z=0.98
    rv0 = bm_iron.verts.new((-0.105, -0.105, 0.880))
    rv1 = bm_iron.verts.new(( 0.105, -0.105, 0.880))
    rv2 = bm_iron.verts.new(( 0.105,  0.105, 0.880))
    rv3 = bm_iron.verts.new((-0.105,  0.105, 0.880))
    rapex = bm_iron.verts.new((0.0, 0.0, 0.985))
    
    bm_iron.faces.new([rv0, rv1, rv2, rv3]) # Bottom face
    bm_iron.faces.new([rv0, rv1, rapex])
    bm_iron.faces.new([rv1, rv2, rapex])
    bm_iron.faces.new([rv2, rv3, rapex])
    bm_iron.faces.new([rv3, rv0, rapex])
    
    # Perch Ring on Top (Z=0.985 to Z=1.045)
    res_r = bmesh.ops.create_cone(
        bm_iron, cap_ends=False, segments=8, radius1=0.030, radius2=0.030, depth=0.018
    )
    for v in res_r['verts']:
        v.co.z += 1.015
        
    mesh_iron = bpy.data.meshes.new("Lantern_Iron_Mesh")
    bm_iron.to_mesh(mesh_iron)
    bm_iron.free()
    
    obj_iron = bpy.data.objects.new("Lantern_Iron_Cage", mesh_iron)
    bpy.context.collection.objects.link(obj_iron)
    obj_iron.data.materials.append(mat_iron)
    obj_iron.parent = root
    
    # 2.5 Flame Mesh
    bm_flame = bmesh.new()
    res_fl = bmesh.ops.create_cone(
        bm_flame, cap_ends=True, segments=8, radius1=0.034, radius2=0.003, depth=0.10
    )
    for v in res_fl['verts']:
        v.co.z += 0.770
        
    mesh_flame = bpy.data.meshes.new("Lantern_Flame_Mesh")
    bm_flame.to_mesh(mesh_flame)
    bm_flame.free()
    
    obj_flame = bpy.data.objects.new("Lantern_Flame", mesh_flame)
    bpy.context.collection.objects.link(obj_flame)
    obj_flame.data.materials.append(mat_flame)
    obj_flame.parent = root
    
    export_fbx(root, export_path)

# ==============================================================================
# 3. LowPolyForestBird.fbx (Clean geometry, natural folded wings!)
# ==============================================================================
def build_low_poly_forest_bird(export_path):
    reset_scene()
    
    mat_body = create_material("M_Bird_Body", (0.18, 0.48, 0.85, 1.0), metallic=0.05, roughness=0.55)
    mat_chest = create_material("M_Bird_Chest", (0.95, 0.52, 0.12, 1.0), metallic=0.05, roughness=0.65)
    mat_wings = create_material("M_Bird_Wings", (0.12, 0.32, 0.65, 1.0), metallic=0.05, roughness=0.60)
    mat_beak = create_material("M_Bird_Beak", (0.98, 0.75, 0.05, 1.0), metallic=0.10, roughness=0.30)
    mat_eyes = create_material("M_Bird_Eyes", (0.04, 0.04, 0.04, 1.0), metallic=0.90, roughness=0.10)
    
    root = bpy.data.objects.new("LowPolyForestBird", None)
    bpy.context.collection.objects.link(root)
    
    # 3.1 Main Bird Body (Torso, head, tail, feet)
    bm_body = bmesh.new()
    
    # Plump torso centered at Y=0.010, Z=0.085
    res_torso = bmesh.ops.create_icosphere(bm_body, subdivisions=2, radius=0.052)
    for v in res_torso['verts']:
        v.co.x *= 0.85
        v.co.y *= 1.28
        v.co.z *= 0.95
        v.co.y += 0.010
        v.co.z += 0.082
        
    # Head sphere centered at Y=0.048, Z=0.135
    res_head = bmesh.ops.create_icosphere(bm_body, subdivisions=2, radius=0.036)
    for v in res_head['verts']:
        v.co.y += 0.048
        v.co.z += 0.135
        
    # Tail feathers (extending backward -Y and down)
    res_tail = bmesh.ops.create_cone(
        bm_body, cap_ends=True, segments=4, radius1=0.026, radius2=0.008, depth=0.08
    )
    for v in res_tail['verts']:
        v.co.x *= 1.35
        v.co.y *= 0.30
        # Rotate tail backward-downward:
        ry = v.co.y * math.cos(-0.40) - v.co.z * math.sin(-0.40)
        rz = v.co.y * math.sin(-0.40) + v.co.z * math.cos(-0.40)
        v.co.y = -0.085 + ry
        v.co.z = 0.075 + rz
        
    # Flat perching feet resting at Z=0.0
    for foot_x in [-0.022, 0.022]:
        # Ankle / leg post
        res_leg = bmesh.ops.create_cube(bm_body, size=1.0)
        for v in res_leg['verts']:
            v.co.x = foot_x + v.co.x * 0.007
            v.co.y = -0.005 + v.co.y * 0.007
            v.co.z = 0.020 + v.co.z * 0.038
            
        # 3 perching toes resting on Z=0.0
        for toe_angle in [-0.28, 0.0, 0.28]:
            res_toe = bmesh.ops.create_cube(bm_body, size=1.0)
            for v in res_toe['verts']:
                v.co.x = foot_x + math.sin(toe_angle) * 0.010 + v.co.x * 0.005
                v.co.y = 0.014 + math.cos(toe_angle) * 0.010 + v.co.y * 0.012
                v.co.z = 0.003 + v.co.z * 0.006
                
    mesh_body = bpy.data.meshes.new("Bird_Body_Mesh")
    bm_body.to_mesh(mesh_body)
    bm_body.free()
    
    obj_body = bpy.data.objects.new("Bird_Body", mesh_body)
    bpy.context.collection.objects.link(obj_body)
    obj_body.data.materials.append(mat_body)
    obj_body.parent = root
    
    # 3.2 Beak (golden forward cone)
    bm_beak = bmesh.new()
    res_bk = bmesh.ops.create_cone(
        bm_beak, cap_ends=True, segments=4, radius1=0.013, radius2=0.002, depth=0.025
    )
    for v in res_bk['verts']:
        old_z = v.co.z
        old_y = v.co.y
        v.co.y = 0.086 + old_z
        v.co.z = 0.135 - old_y
        
    mesh_beak = bpy.data.meshes.new("Bird_Beak_Mesh")
    bm_beak.to_mesh(mesh_beak)
    bm_beak.free()
    
    obj_beak = bpy.data.objects.new("Bird_Beak", mesh_beak)
    bpy.context.collection.objects.link(obj_beak)
    obj_beak.data.materials.append(mat_beak)
    obj_beak.parent = root
    
    # 3.3 Eyes
    bm_eyes = bmesh.new()
    for eye_x in [-0.025, 0.025]:
        res_e = bmesh.ops.create_icosphere(bm_eyes, subdivisions=1, radius=0.006)
        for v in res_e['verts']:
            v.co.x += eye_x
            v.co.y += 0.062
            v.co.z += 0.142
            
    mesh_eyes = bpy.data.meshes.new("Bird_Eyes_Mesh")
    bm_eyes.to_mesh(mesh_eyes)
    bm_eyes.free()
    
    obj_eyes = bpy.data.objects.new("Bird_Eyes", mesh_eyes)
    bpy.context.collection.objects.link(obj_eyes)
    obj_eyes.data.materials.append(mat_eyes)
    obj_eyes.parent = root
    
    # 3.4 Left Wing (Wing_L) - Folded neatly along left flank pointing backward (-Y)
    # Shoulder Pivot at (-0.042, 0.010, 0.100)
    shoulder_l = (-0.042, 0.010, 0.100)
    bm_wing_l = bmesh.new()
    
    # Construct wing relative to shoulder origin (0, 0, 0)
    # Wing extends backward from 0.0 to -0.090 along Y, and down from 0.0 to -0.040 along Z
    wv0 = bm_wing_l.verts.new(( 0.000,  0.010,  0.000)) # Shoulder front
    wv1 = bm_wing_l.verts.new((-0.006, -0.035,  0.005)) # Wing top crest
    wv2 = bm_wing_l.verts.new((-0.004, -0.088, -0.032)) # Wing tip (folded back)
    wv3 = bm_wing_l.verts.new((-0.005, -0.045, -0.028)) # Wing bottom edge
    wv4 = bm_wing_l.verts.new(( 0.000, -0.005, -0.020)) # Shoulder bottom
    
    bm_wing_l.faces.new([wv0, wv1, wv2, wv3, wv4])
    
    mesh_wing_l = bpy.data.meshes.new("Bird_Wing_L_Mesh")
    bm_wing_l.to_mesh(mesh_wing_l)
    bm_wing_l.free()
    
    obj_wing_l = bpy.data.objects.new("Wing_L", mesh_wing_l)
    obj_wing_l.location = shoulder_l
    bpy.context.collection.objects.link(obj_wing_l)
    obj_wing_l.data.materials.append(mat_wings)
    obj_wing_l.parent = root
    
    # 3.5 Right Wing (Wing_R) - Folded neatly along right flank pointing backward (-Y)
    # Shoulder Pivot at (+0.042, 0.010, 0.100)
    shoulder_r = (0.042, 0.010, 0.100)
    bm_wing_r = bmesh.new()
    
    rwv0 = bm_wing_r.verts.new(( 0.000,  0.010,  0.000))
    rwv1 = bm_wing_r.verts.new(( 0.006, -0.035,  0.005))
    rwv2 = bm_wing_r.verts.new(( 0.004, -0.088, -0.032))
    rwv3 = bm_wing_r.verts.new(( 0.005, -0.045, -0.028))
    rwv4 = bm_wing_r.verts.new(( 0.000, -0.005, -0.020))
    
    bm_wing_r.faces.new([rwv0, rwv1, rwv2, rwv3, rwv4])
    
    mesh_wing_r = bpy.data.meshes.new("Bird_Wing_R_Mesh")
    bm_wing_r.to_mesh(mesh_wing_r)
    bm_wing_r.free()
    
    obj_wing_r = bpy.data.objects.new("Wing_R", mesh_wing_r)
    obj_wing_r.location = shoulder_r
    bpy.context.collection.objects.link(obj_wing_r)
    obj_wing_r.data.materials.append(mat_wings)
    obj_wing_r.parent = root
    
    export_fbx(root, export_path)

if __name__ == '__main__':
    out_dir = r"D:\Alunos\Alexandre\Projeto IC\Assets\Models\Generated\Task13"
    os.makedirs(out_dir, exist_ok=True)
    
    build_xylophone_mushroom(os.path.join(out_dir, "XylophoneMushroom.fbx"))
    build_rune_path_lantern(os.path.join(out_dir, "RunePathLantern.fbx"))
    build_low_poly_forest_bird(os.path.join(out_dir, "LowPolyForestBird.fbx"))
    print("All Task 13 3D models regenerated cleanly with Blender 5.1 CLI.")
