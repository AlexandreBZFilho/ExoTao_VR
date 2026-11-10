import bpy
import bmesh
import math
import mathutils
import os

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def create_material(name, diffuse_color, metallic=0.0, roughness=0.5, is_emissive=False, emission_color=(0,0,0,1), emission_strength=1.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        if 'Base Color' in bsdf.inputs:
            bsdf.inputs['Base Color'].default_value = diffuse_color
        if 'Metallic' in bsdf.inputs:
            bsdf.inputs['Metallic'].default_value = metallic
        if 'Roughness' in bsdf.inputs:
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
# 1. Trophy_GoldenApple.fbx
# ==============================================================================
def build_golden_apple(export_path):
    reset_scene()
    
    mat_wood = create_material("M_Trophy_WoodDark", (0.16, 0.09, 0.05, 1.0), metallic=0.05, roughness=0.65)
    mat_gold = create_material("M_Trophy_GoldPolished", (1.0, 0.82, 0.16, 1.0), metallic=0.92, roughness=0.18,
                               is_emissive=True, emission_color=(0.75, 0.58, 0.10, 1.0), emission_strength=0.8)
    mat_plaque = create_material("M_Trophy_PlaqueBrass", (0.88, 0.72, 0.28, 1.0), metallic=0.85, roughness=0.28)
    
    root = bpy.data.objects.new("Trophy_GoldenApple", None)
    bpy.context.collection.objects.link(root)
    
    # 1.1 Stepped Octagonal Pedestal
    bm_ped = bmesh.new()
    # Bottom tier: radius 0.052m, height 0.022m
    bmesh.ops.create_cone(
        bm_ped,
        cap_ends=True,
        cap_tris=False,
        segments=8,
        radius1=0.052,
        radius2=0.050,
        depth=0.022,
        matrix=mathutils.Matrix.Translation((0, 0, 0.011))
    )
    # Top tier: radius 0.042m, height 0.028m
    bmesh.ops.create_cone(
        bm_ped,
        cap_ends=True,
        cap_tris=False,
        segments=8,
        radius1=0.044,
        radius2=0.040,
        depth=0.028,
        matrix=mathutils.Matrix.Translation((0, 0, 0.022 + 0.014))
    )
    
    mesh_ped = bpy.data.meshes.new("Pedestal_Mesh")
    bm_ped.to_mesh(mesh_ped)
    bm_ped.free()
    
    obj_ped = bpy.data.objects.new("Pedestal", mesh_ped)
    obj_ped.data.materials.append(mat_wood)
    obj_ped.parent = root
    bpy.context.collection.objects.link(obj_ped)
    
    # 1.2 Brass Engraved Plaque on front face
    bm_plaque = bmesh.new()
    bmesh.ops.create_cube(
        bm_plaque,
        size=1.0,
        matrix=mathutils.Matrix.Scale(0.046, 4, (1, 0, 0)) @
               mathutils.Matrix.Scale(0.003, 4, (0, 1, 0)) @
               mathutils.Matrix.Scale(0.016, 4, (0, 0, 1)) @
               mathutils.Matrix.Translation((0, -0.042, 0.035))
    )
    mesh_plaque = bpy.data.meshes.new("Plaque_Mesh")
    bm_plaque.to_mesh(mesh_plaque)
    bm_plaque.free()
    
    obj_plaque = bpy.data.objects.new("Plaque", mesh_plaque)
    obj_plaque.data.materials.append(mat_plaque)
    obj_plaque.parent = root
    bpy.context.collection.objects.link(obj_plaque)
    
    # 1.3 Golden Apple Body (Smooth indented sphere + stem + leaf)
    bm_apple = bmesh.new()
    # Create UV sphere for apple
    res = bmesh.ops.create_uvsphere(
        bm_apple,
        u_segments=24,
        v_segments=16,
        radius=0.042
    )
    # Deform vertices into apple shape: slightly indented top (z>0.02) and bottom (z<-0.02)
    apple_center_z = 0.050 + 0.042 # = 0.092m
    for v in bm_apple.verts:
        orig_z = v.co.z
        orig_r = math.sqrt(v.co.x**2 + v.co.y**2)
        # Indent top pole
        if orig_z > 0.025:
            factor = (orig_z - 0.025) / 0.017
            v.co.z -= 0.010 * factor * (1.0 - orig_r / 0.042)
        # Taper bottom slightly
        elif orig_z < -0.010:
            v.co.x *= 0.88
            v.co.y *= 0.88
        v.co.z += apple_center_z
        
    # Stem: curved cylinder emerging from top dip
    num_stem = 6
    stem_verts = []
    for i in range(num_stem + 1):
        t = i / float(num_stem)
        cz = 0.124 + t * 0.022
        cy = -t * 0.008
        cx = t * t * 0.005
        rad = 0.0032 * (1.0 - t * 0.25)
        ring = []
        for s in range(8):
            ang = s * (2.0 * math.pi / 8.0)
            vx = cx + rad * math.cos(ang)
            vy = cy + rad * math.sin(ang)
            ring.append(bm_apple.verts.new((vx, vy, cz)))
        stem_verts.append(ring)
    
    for i in range(num_stem):
        for s in range(8):
            s_next = (s + 1) % 8
            bm_apple.faces.new((
                stem_verts[i][s],
                stem_verts[i][s_next],
                stem_verts[i+1][s_next],
                stem_verts[i+1][s]
            ))
    bm_apple.faces.new(stem_verts[-1]) # cap
    
    # Leaf: delicate stylized polygon
    leaf_v0 = bm_apple.verts.new((0.002, -0.004, 0.134))
    leaf_v1 = bm_apple.verts.new((0.016, -0.012, 0.142))
    leaf_v2 = bm_apple.verts.new((0.028, -0.018, 0.146))
    leaf_v3 = bm_apple.verts.new((0.018, -0.006, 0.140))
    bm_apple.faces.new((leaf_v0, leaf_v1, leaf_v2, leaf_v3))
    
    mesh_apple = bpy.data.meshes.new("GoldenApple_Mesh")
    bm_apple.to_mesh(mesh_apple)
    bm_apple.free()
    
    # Set smooth shading
    for poly in mesh_apple.polygons:
        poly.use_smooth = True
        
    obj_apple = bpy.data.objects.new("GoldenApple", mesh_apple)
    obj_apple.data.materials.append(mat_gold)
    obj_apple.parent = root
    bpy.context.collection.objects.link(obj_apple)
    
    export_fbx(root, export_path)

# ==============================================================================
# 2. Trophy_LuminousBonsai.fbx
# ==============================================================================
def build_luminous_bonsai(export_path):
    reset_scene()
    
    mat_jade = create_material("M_Trophy_JadePot", (0.12, 0.48, 0.38, 1.0), metallic=0.15, roughness=0.22)
    mat_trunk = create_material("M_Trophy_BonsaiTrunk", (0.28, 0.16, 0.08, 1.0), metallic=0.05, roughness=0.82)
    mat_crystal = create_material("M_Trophy_CrystalBiolum", (0.15, 0.95, 0.85, 1.0), metallic=0.25, roughness=0.12,
                                  is_emissive=True, emission_color=(0.10, 0.95, 0.82, 1.0), emission_strength=3.5)
    
    root = bpy.data.objects.new("Trophy_LuminousBonsai", None)
    bpy.context.collection.objects.link(root)
    
    # 2.1 Hexagonal Jade Pot + Rim + Feet
    bm_pot = bmesh.new()
    # Main basin
    bmesh.ops.create_cone(
        bm_pot,
        cap_ends=True,
        cap_tris=False,
        segments=6,
        radius1=0.046,
        radius2=0.054,
        depth=0.038,
        matrix=mathutils.Matrix.Translation((0, 0, 0.024))
    )
    # Upper flared rim
    bmesh.ops.create_cone(
        bm_pot,
        cap_ends=True,
        cap_tris=False,
        segments=6,
        radius1=0.056,
        radius2=0.058,
        depth=0.008,
        matrix=mathutils.Matrix.Translation((0, 0, 0.042))
    )
    # 3 Little pot feet at base
    for ang_deg in (0, 120, 240):
        rad = math.radians(ang_deg)
        fx = 0.036 * math.cos(rad)
        fy = 0.036 * math.sin(rad)
        bmesh.ops.create_cube(
            bm_pot,
            size=1.0,
            matrix=mathutils.Matrix.Scale(0.012, 4, (1, 0, 0)) @
                   mathutils.Matrix.Scale(0.012, 4, (0, 1, 0)) @
                   mathutils.Matrix.Scale(0.006, 4, (0, 0, 1)) @
                   mathutils.Matrix.Translation((fx, fy, 0.003))
        )
    
    mesh_pot = bpy.data.meshes.new("Pot_Mesh")
    bm_pot.to_mesh(mesh_pot)
    bm_pot.free()
    
    obj_pot = bpy.data.objects.new("JadePot", mesh_pot)
    obj_pot.data.materials.append(mat_jade)
    obj_pot.parent = root
    bpy.context.collection.objects.link(obj_pot)
    
    # 2.2 Sinuous Bonsai Trunk with Branches
    bm_trunk = bmesh.new()
    trunk_pts = [
        (0.000, 0.000, 0.044, 0.014),
        (0.006, -0.004, 0.065, 0.012),
        (-0.010, 0.008, 0.090, 0.010),
        (0.012, -0.006, 0.118, 0.008),
        (0.004, 0.008, 0.144, 0.006)
    ]
    rings = []
    for px, py, pz, pr in trunk_pts:
        ring = []
        for s in range(8):
            ang = s * (2.0 * math.pi / 8.0)
            vx = px + pr * math.cos(ang)
            vy = py + pr * math.sin(ang)
            ring.append(bm_trunk.verts.new((vx, vy, pz)))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for s in range(8):
            s_next = (s + 1) % 8
            bm_trunk.faces.new((rings[i][s], rings[i][s_next], rings[i+1][s_next], rings[i+1][s]))
    bm_trunk.faces.new(rings[0])
    bm_trunk.faces.new(rings[-1])
    
    # Left Branch towards (-0.038, -0.018, 0.128)
    b1_pts = [
        (-0.008, 0.006, 0.092, 0.007),
        (-0.022, -0.004, 0.110, 0.005),
        (-0.036, -0.016, 0.126, 0.004)
    ]
    b1_rings = []
    for px, py, pz, pr in b1_pts:
        ring = []
        for s in range(6):
            ang = s * (2.0 * math.pi / 6.0)
            vx = px + pr * math.cos(ang)
            vy = py + pr * math.sin(ang)
            ring.append(bm_trunk.verts.new((vx, vy, pz)))
        b1_rings.append(ring)
    for i in range(len(b1_rings) - 1):
        for s in range(6):
            s_next = (s + 1) % 6
            bm_trunk.faces.new((b1_rings[i][s], b1_rings[i][s_next], b1_rings[i+1][s_next], b1_rings[i+1][s]))
    bm_trunk.faces.new(b1_rings[-1])
    
    # Right Branch towards (0.036, 0.018, 0.138)
    b2_pts = [
        (0.010, -0.004, 0.114, 0.006),
        (0.024, 0.008, 0.128, 0.0045),
        (0.038, 0.016, 0.138, 0.0035)
    ]
    b2_rings = []
    for px, py, pz, pr in b2_pts:
        ring = []
        for s in range(6):
            ang = s * (2.0 * math.pi / 6.0)
            vx = px + pr * math.cos(ang)
            vy = py + pr * math.sin(ang)
            ring.append(bm_trunk.verts.new((vx, vy, pz)))
        b2_rings.append(ring)
    for i in range(len(b2_rings) - 1):
        for s in range(6):
            s_next = (s + 1) % 6
            bm_trunk.faces.new((b2_rings[i][s], b2_rings[i][s_next], b2_rings[i+1][s_next], b2_rings[i+1][s]))
    bm_trunk.faces.new(b2_rings[-1])
    
    mesh_trunk = bpy.data.meshes.new("Trunk_Mesh")
    bm_trunk.to_mesh(mesh_trunk)
    bm_trunk.free()
    
    for poly in mesh_trunk.polygons:
        poly.use_smooth = True
        
    obj_trunk = bpy.data.objects.new("BonsaiTrunk", mesh_trunk)
    obj_trunk.data.materials.append(mat_trunk)
    obj_trunk.parent = root
    bpy.context.collection.objects.link(obj_trunk)
    
    # 2.3 Crystalline Bioluminescent Leaf Clusters on Branch Tips & Crown
    bm_crystal = bmesh.new()
    cluster_centers = [
        (0.004, 0.008, 0.158, 0.024),   # Apex crown
        (-0.038, -0.016, 0.132, 0.019), # Left tip
        (0.040, 0.018, 0.144, 0.018),   # Right tip
        (-0.012, 0.024, 0.142, 0.015),  # Forward sub-branch
        (0.016, -0.020, 0.148, 0.016)   # Back sub-branch
    ]
    for cx, cy, cz, crad in cluster_centers:
        bmesh.ops.create_icosphere(
            bm_crystal,
            subdivisions=1,
            radius=crad,
            matrix=mathutils.Matrix.Translation((cx, cy, cz))
        )
        # Add 3 satellite crystal facets around each cluster
        for ang in (0, 120, 240):
            rad_ang = math.radians(ang)
            sx = cx + (crad * 0.72) * math.cos(rad_ang)
            sy = cy + (crad * 0.72) * math.sin(rad_ang)
            sz = cz + 0.005 * math.sin(rad_ang * 2)
            bmesh.ops.create_cone(
                bm_crystal,
                cap_ends=True,
                segments=5,
                radius1=0.007,
                radius2=0.000,
                depth=0.016,
                matrix=mathutils.Matrix.Translation((sx, sy, sz))
            )
            
    mesh_crystal = bpy.data.meshes.new("Crystal_Mesh")
    bm_crystal.to_mesh(mesh_crystal)
    bm_crystal.free()
    
    obj_crystal = bpy.data.objects.new("CrystalLeaves", mesh_crystal)
    obj_crystal.data.materials.append(mat_crystal)
    obj_crystal.parent = root
    bpy.context.collection.objects.link(obj_crystal)
    
    export_fbx(root, export_path)

# ==============================================================================
# 3. Trophy_StarCup.fbx
# ==============================================================================
def build_star_cup(export_path):
    reset_scene()
    
    mat_marble = create_material("M_Trophy_MarbleBase", (0.10, 0.10, 0.12, 1.0), metallic=0.20, roughness=0.35)
    mat_gold = create_material("M_Trophy_GoldPolished", (1.0, 0.82, 0.16, 1.0), metallic=0.92, roughness=0.18,
                               is_emissive=True, emission_color=(0.75, 0.58, 0.10, 1.0), emission_strength=0.8)
    mat_star = create_material("M_Trophy_StarEmblem", (1.0, 0.95, 0.40, 1.0), metallic=0.88, roughness=0.12,
                               is_emissive=True, emission_color=(1.0, 0.90, 0.35, 1.0), emission_strength=2.2)
    
    root = bpy.data.objects.new("Trophy_StarCup", None)
    bpy.context.collection.objects.link(root)
    
    # 3.1 Stepped Marble Base
    bm_base = bmesh.new()
    # Bottom square: 0.08m x 0.08m x 0.020m
    bmesh.ops.create_cube(
        bm_base,
        size=1.0,
        matrix=mathutils.Matrix.Scale(0.082, 4, (1, 0, 0)) @
               mathutils.Matrix.Scale(0.082, 4, (0, 1, 0)) @
               mathutils.Matrix.Scale(0.020, 4, (0, 0, 1)) @
               mathutils.Matrix.Translation((0, 0, 0.010))
    )
    # Upper square: 0.068m x 0.068m x 0.016m
    bmesh.ops.create_cube(
        bm_base,
        size=1.0,
        matrix=mathutils.Matrix.Scale(0.068, 4, (1, 0, 0)) @
               mathutils.Matrix.Scale(0.068, 4, (0, 1, 0)) @
               mathutils.Matrix.Scale(0.016, 4, (0, 0, 1)) @
               mathutils.Matrix.Translation((0, 0, 0.028))
    )
    mesh_base = bpy.data.meshes.new("Base_Mesh")
    bm_base.to_mesh(mesh_base)
    bm_base.free()
    
    obj_base = bpy.data.objects.new("MarbleBase", mesh_base)
    obj_base.data.materials.append(mat_marble)
    obj_base.parent = root
    bpy.context.collection.objects.link(obj_base)
    
    # 3.2 Golden Cup (Collar, Stem, Chalice, Handles)
    bm_cup = bmesh.new()
    
    # Collar ring
    bmesh.ops.create_cone(
        bm_cup,
        cap_ends=True,
        segments=16,
        radius1=0.026,
        radius2=0.024,
        depth=0.008,
        matrix=mathutils.Matrix.Translation((0, 0, 0.040))
    )
    
    # Fluted stem profile: from z=0.044 to z=0.092
    stem_levels = [
        (0.044, 0.022),
        (0.054, 0.013),
        (0.068, 0.011),
        (0.082, 0.014),
        (0.092, 0.024)
    ]
    stem_rings = []
    for sz, srad in stem_levels:
        ring = []
        for s in range(16):
            ang = s * (2.0 * math.pi / 16.0)
            ring.append(bm_cup.verts.new((srad * math.cos(ang), srad * math.sin(ang), sz)))
        stem_rings.append(ring)
    for i in range(len(stem_rings) - 1):
        for s in range(16):
            s_next = (s + 1) % 16
            bm_cup.faces.new((stem_rings[i][s], stem_rings[i][s_next], stem_rings[i+1][s_next], stem_rings[i+1][s]))
            
    # Chalice / Bowl Profile: from z=0.092 to z=0.198 (flaring to radius 0.048m)
    bowl_levels = [
        (0.092, 0.024),
        (0.108, 0.038),
        (0.134, 0.046),
        (0.165, 0.047),
        (0.192, 0.049),
        (0.198, 0.052) # Flared rim
    ]
    bowl_rings = []
    for bz, brad in bowl_levels:
        ring = []
        for s in range(18):
            ang = s * (2.0 * math.pi / 18.0)
            ring.append(bm_cup.verts.new((brad * math.cos(ang), brad * math.sin(ang), bz)))
        bowl_rings.append(ring)
    for i in range(len(bowl_rings) - 1):
        for s in range(18):
            s_next = (s + 1) % 18
            bm_cup.faces.new((bowl_rings[i][s], bowl_rings[i][s_next], bowl_rings[i+1][s_next], bowl_rings[i+1][s]))
            
    # Inner hollow dip of cup rim
    inner_ring = []
    for s in range(18):
        ang = s * (2.0 * math.pi / 18.0)
        inner_ring.append(bm_cup.verts.new((0.045 * math.cos(ang), 0.045 * math.sin(ang), 0.194)))
    for s in range(18):
        s_next = (s + 1) % 18
        bm_cup.faces.new((bowl_rings[-1][s], bowl_rings[-1][s_next], inner_ring[s_next], inner_ring[s]))
    # Inner basin center
    basin_center = bm_cup.verts.new((0, 0, 0.160))
    for s in range(18):
        s_next = (s + 1) % 18
        bm_cup.faces.new((inner_ring[s], inner_ring[s_next], basin_center))
        
    # Dual Curved Handles on Left (-X) and Right (+X)
    for side in (-1.0, 1.0):
        handle_segs = 8
        h_rings = []
        for h in range(handle_segs + 1):
            ht = h / float(handle_segs)
            # Parametric arch
            hang = ht * math.pi
            hx = side * (0.048 + 0.024 * math.sin(hang))
            hz = 0.115 + ht * 0.070
            hrad = 0.0035
            hring = []
            for hs in range(6):
                hs_ang = hs * (2.0 * math.pi / 6.0)
                vx = hx + hrad * math.cos(hs_ang)
                vy = hrad * math.sin(hs_ang)
                vz = hz + hrad * 0.5 * math.cos(hs_ang)
                hring.append(bm_cup.verts.new((vx, vy, vz)))
            h_rings.append(hring)
        for h in range(handle_segs):
            for hs in range(6):
                hs_next = (hs + 1) % 6
                bm_cup.faces.new((h_rings[h][hs], h_rings[h][hs_next], h_rings[h+1][hs_next], h_rings[h+1][hs]))
                
    mesh_cup = bpy.data.meshes.new("Cup_Mesh")
    bm_cup.to_mesh(mesh_cup)
    bm_cup.free()
    
    for poly in mesh_cup.polygons:
        poly.use_smooth = True
        
    obj_cup = bpy.data.objects.new("GoldenCup", mesh_cup)
    obj_cup.data.materials.append(mat_gold)
    obj_cup.parent = root
    bpy.context.collection.objects.link(obj_cup)
    
    # 3.3 Raised 5-Point Star Emblem on Front (+Y or -Y) Face
    bm_star = bmesh.new()
    star_center_y = -0.0475
    star_center_z = 0.145
    r_outer = 0.016
    r_inner = 0.007
    num_pts = 5
    
    star_front_center = bm_star.verts.new((0, star_center_y - 0.0035, star_center_z))
    star_back_center = bm_star.verts.new((0, star_center_y, star_center_z))
    star_rim = []
    
    for p in range(num_pts * 2):
        is_outer = (p % 2 == 0)
        rad = r_outer if is_outer else r_inner
        ang = p * (math.pi / float(num_pts)) - math.pi * 0.5
        sx = rad * math.cos(ang)
        sz = star_center_z + rad * math.sin(ang)
        sy = star_center_y - 0.001
        star_rim.append(bm_star.verts.new((sx, sy, sz)))
        
    for p in range(num_pts * 2):
        p_next = (p + 1) % (num_pts * 2)
        # Front facet
        bm_star.faces.new((star_front_center, star_rim[p], star_rim[p_next]))
        # Back facet
        bm_star.faces.new((star_back_center, star_rim[p_next], star_rim[p]))
        
    mesh_star = bpy.data.meshes.new("Star_Mesh")
    bm_star.to_mesh(mesh_star)
    bm_star.free()
    
    obj_star = bpy.data.objects.new("StarEmblem", mesh_star)
    obj_star.data.materials.append(mat_star)
    obj_star.parent = root
    bpy.context.collection.objects.link(obj_star)
    
    export_fbx(root, export_path)

# ==============================================================================
# 4. Trophy_DroneCrownHat.fbx
# ==============================================================================
def build_drone_crown_hat(export_path):
    reset_scene()
    
    mat_gold = create_material("M_Trophy_GoldPolished", (1.0, 0.82, 0.16, 1.0), metallic=0.92, roughness=0.18,
                               is_emissive=True, emission_color=(0.75, 0.58, 0.10, 1.0), emission_strength=0.8)
    mat_velvet = create_material("M_Trophy_RoyalVelvet", (0.75, 0.05, 0.12, 1.0), metallic=0.08, roughness=0.85)
    mat_ruby = create_material("M_Trophy_GemRuby", (0.95, 0.08, 0.22, 1.0), metallic=0.25, roughness=0.10,
                               is_emissive=True, emission_color=(0.90, 0.05, 0.20, 1.0), emission_strength=2.5)
    mat_sapphire = create_material("M_Trophy_GemSapphire", (0.08, 0.35, 0.95, 1.0), metallic=0.25, roughness=0.10,
                                   is_emissive=True, emission_color=(0.06, 0.30, 0.92, 1.0), emission_strength=2.5)
    
    root = bpy.data.objects.new("Trophy_DroneCrownHat", None)
    bpy.context.collection.objects.link(root)
    
    # 4.1 Inner Velvet Royal Dome (Tufted cushion inside crown)
    bm_velvet = bmesh.new()
    bmesh.ops.create_uvsphere(
        bm_velvet,
        u_segments=24,
        v_segments=14,
        radius=0.066
    )
    # Scale and place dome from z=0.005 to z=0.075
    for v in bm_velvet.verts:
        if v.co.z < 0:
            v.co.z *= 0.15
        v.co.z = v.co.z * 0.85 + 0.022
        
    mesh_velvet = bpy.data.meshes.new("Velvet_Mesh")
    bm_velvet.to_mesh(mesh_velvet)
    bm_velvet.free()
    
    for poly in mesh_velvet.polygons:
        poly.use_smooth = True
        
    obj_velvet = bpy.data.objects.new("VelvetDome", mesh_velvet)
    obj_velvet.data.materials.append(mat_velvet)
    obj_velvet.parent = root
    bpy.context.collection.objects.link(obj_velvet)
    
    # 4.2 Gold Filigree Band & 6 Crown Points with Pearls
    bm_gold = bmesh.new()
    num_peaks = 6
    sub_per_peak = 4
    total_segs = num_peaks * sub_per_peak # 24 segments
    r_outer = 0.076
    r_inner = 0.068
    
    # Bottom ring & mid band
    z_bot = 0.000
    z_mid = 0.028
    
    ring_bot_out = []
    ring_bot_in = []
    ring_mid_out = []
    ring_mid_in = []
    
    for s in range(total_segs):
        ang = s * (2.0 * math.pi / float(total_segs))
        ca = math.cos(ang)
        sa = math.sin(ang)
        ring_bot_out.append(bm_gold.verts.new((r_outer * ca, r_outer * sa, z_bot)))
        ring_bot_in.append(bm_gold.verts.new((r_inner * ca, r_inner * sa, z_bot)))
        ring_mid_out.append(bm_gold.verts.new((r_outer * ca, r_outer * sa, z_mid)))
        ring_mid_in.append(bm_gold.verts.new((r_inner * ca, r_inner * sa, z_mid)))
        
    # Band faces
    for s in range(total_segs):
        s_next = (s + 1) % total_segs
        # Outer face
        bm_gold.faces.new((ring_bot_out[s], ring_bot_out[s_next], ring_mid_out[s_next], ring_mid_out[s]))
        # Inner face
        bm_gold.faces.new((ring_bot_in[s_next], ring_bot_in[s], ring_mid_in[s], ring_mid_in[s_next]))
        # Bottom rim face
        bm_gold.faces.new((ring_bot_in[s], ring_bot_in[s_next], ring_bot_out[s_next], ring_bot_out[s]))
        
    # 6 Royal Spire Peaks rising from z_mid to z=0.088
    peak_tips = []
    for p in range(num_peaks):
        s_center = p * sub_per_peak
        ang_center = s_center * (2.0 * math.pi / float(total_segs))
        px = (r_outer - 0.004) * math.cos(ang_center)
        py = (r_outer - 0.004) * math.sin(ang_center)
        pz = 0.088
        tip_v = bm_gold.verts.new((px, py, pz))
        peak_tips.append(tip_v)
        
        # Connect peak triangular facets to adjacent mid verts
        s_prev = (s_center - 1) % total_segs
        s_next = (s_center + 1) % total_segs
        bm_gold.faces.new((ring_mid_out[s_prev], ring_mid_out[s_center], tip_v))
        bm_gold.faces.new((ring_mid_out[s_center], ring_mid_out[s_next], tip_v))
        
        # Connect inner mid rim flatly across valleys
        s_far = (s_center + 2) % total_segs
        bm_gold.faces.new((ring_mid_out[s_next], ring_mid_out[s_far], ring_mid_in[s_far], ring_mid_in[s_next]))
        
        # Golden pearl sphere at each peak tip
        bmesh.ops.create_uvsphere(
            bm_gold,
            u_segments=10,
            v_segments=8,
            radius=0.0075,
            matrix=mathutils.Matrix.Translation((px, py, pz + 0.006))
        )
        
    # Apex Cross / Pinnacle Star at top of velvet dome (Z = 0.078 to 0.098)
    bmesh.ops.create_cube(
        bm_gold,
        size=1.0,
        matrix=mathutils.Matrix.Scale(0.005, 4, (1, 0, 0)) @
               mathutils.Matrix.Scale(0.005, 4, (0, 1, 0)) @
               mathutils.Matrix.Scale(0.022, 4, (0, 0, 1)) @
               mathutils.Matrix.Translation((0, 0, 0.088))
    )
    bmesh.ops.create_cube(
        bm_gold,
        size=1.0,
        matrix=mathutils.Matrix.Scale(0.018, 4, (1, 0, 0)) @
               mathutils.Matrix.Scale(0.005, 4, (0, 1, 0)) @
               mathutils.Matrix.Scale(0.005, 4, (0, 0, 1)) @
               mathutils.Matrix.Translation((0, 0, 0.091))
    )
    bmesh.ops.create_icosphere(
        bm_gold,
        subdivisions=1,
        radius=0.0055,
        matrix=mathutils.Matrix.Translation((0, 0, 0.099))
    )
    
    mesh_gold = bpy.data.meshes.new("CrownGold_Mesh")
    bm_gold.to_mesh(mesh_gold)
    bm_gold.free()
    
    for poly in mesh_gold.polygons:
        poly.use_smooth = True
        
    obj_gold = bpy.data.objects.new("CrownGoldBand", mesh_gold)
    obj_gold.data.materials.append(mat_gold)
    obj_gold.parent = root
    bpy.context.collection.objects.link(obj_gold)
    
    # 4.3 Inset Gems (3 Rubies, 3 Sapphires alternating around the gold band)
    bm_ruby = bmesh.new()
    bm_sapphire = bmesh.new()
    
    for p in range(num_peaks):
        ang = (p * sub_per_peak + 2) * (2.0 * math.pi / float(total_segs))
        gx = (r_outer + 0.001) * math.cos(ang)
        gy = (r_outer + 0.001) * math.sin(ang)
        gz = 0.014
        
        target_bm = bm_ruby if (p % 2 == 0) else bm_sapphire
        bmesh.ops.create_icosphere(
            target_bm,
            subdivisions=1,
            radius=0.0055,
            matrix=mathutils.Matrix.Scale(1.0, 4, (1, 0, 0)) @
                   mathutils.Matrix.Scale(1.0, 4, (0, 1, 0)) @
                   mathutils.Matrix.Scale(1.2, 4, (0, 0, 1)) @
                   mathutils.Matrix.Translation((gx, gy, gz))
        )
        
    mesh_ruby = bpy.data.meshes.new("Ruby_Mesh")
    bm_ruby.to_mesh(mesh_ruby)
    bm_ruby.free()
    obj_ruby = bpy.data.objects.new("CrownRubies", mesh_ruby)
    obj_ruby.data.materials.append(mat_ruby)
    obj_ruby.parent = root
    bpy.context.collection.objects.link(obj_ruby)
    
    mesh_sapphire = bpy.data.meshes.new("Sapphire_Mesh")
    bm_sapphire.to_mesh(mesh_sapphire)
    bm_sapphire.free()
    obj_sapphire = bpy.data.objects.new("CrownSapphires", mesh_sapphire)
    obj_sapphire.data.materials.append(mat_sapphire)
    obj_sapphire.parent = root
    bpy.context.collection.objects.link(obj_sapphire)
    
    export_fbx(root, export_path)

# ==============================================================================
# 5. Trophy_DisplayStand.fbx
# ==============================================================================
def build_display_stand(export_path):
    reset_scene()
    
    mat_wood = create_material("M_Trophy_WoodDark", (0.16, 0.09, 0.05, 1.0), metallic=0.05, roughness=0.65)
    mat_velvet = create_material("M_Trophy_DisplayVelvet", (0.05, 0.12, 0.28, 1.0), metallic=0.08, roughness=0.88)
    mat_plaque = create_material("M_Trophy_PlaqueBrass", (0.88, 0.72, 0.28, 1.0), metallic=0.85, roughness=0.28)
    
    root = bpy.data.objects.new("Trophy_DisplayStand", None)
    bpy.context.collection.objects.link(root)
    
    # 5.1 Main Mahogany Plinth (Length 0.52m, Width 0.14m, Height 0.045m)
    bm_plinth = bmesh.new()
    bmesh.ops.create_cube(
        bm_plinth,
        size=1.0,
        matrix=mathutils.Matrix.Scale(0.520, 4, (1, 0, 0)) @
               mathutils.Matrix.Scale(0.140, 4, (0, 1, 0)) @
               mathutils.Matrix.Scale(0.045, 4, (0, 0, 1)) @
               mathutils.Matrix.Translation((0, 0, 0.0225))
    )
    mesh_plinth = bpy.data.meshes.new("Plinth_Mesh")
    bm_plinth.to_mesh(mesh_plinth)
    bm_plinth.free()
    
    obj_plinth = bpy.data.objects.new("DisplayPlinth", mesh_plinth)
    obj_plinth.data.materials.append(mat_wood)
    obj_plinth.parent = root
    bpy.context.collection.objects.link(obj_plinth)
    
    # 5.2 4 Circular Recessed Velvet Pedestal Pads
    bm_pads = bmesh.new()
    slot_xs = [-0.18, -0.06, 0.06, 0.18]
    for sx in slot_xs:
        bmesh.ops.create_cone(
            bm_pads,
            cap_ends=True,
            segments=20,
            radius1=0.048,
            radius2=0.048,
            depth=0.005,
            matrix=mathutils.Matrix.Translation((sx, 0.008, 0.045))
        )
    mesh_pads = bpy.data.meshes.new("Pads_Mesh")
    bm_pads.to_mesh(mesh_pads)
    bm_pads.free()
    
    obj_pads = bpy.data.objects.new("VelvetPads", mesh_pads)
    obj_pads.data.materials.append(mat_velvet)
    obj_pads.parent = root
    bpy.context.collection.objects.link(obj_pads)
    
    # 5.3 4 Brass Nameplates in front of each slot
    bm_plaques = bmesh.new()
    for sx in slot_xs:
        bmesh.ops.create_cube(
            bm_plaques,
            size=1.0,
            matrix=mathutils.Matrix.Scale(0.068, 4, (1, 0, 0)) @
                   mathutils.Matrix.Scale(0.003, 4, (0, 1, 0)) @
                   mathutils.Matrix.Scale(0.016, 4, (0, 0, 1)) @
                   mathutils.Matrix.Translation((sx, -0.069, 0.024))
        )
    mesh_plaques = bpy.data.meshes.new("Plaques_Mesh")
    bm_plaques.to_mesh(mesh_plaques)
    bm_plaques.free()
    
    obj_plaques = bpy.data.objects.new("DisplayPlaques", mesh_plaques)
    obj_plaques.data.materials.append(mat_plaque)
    obj_plaques.parent = root
    bpy.context.collection.objects.link(obj_plaques)
    
    export_fbx(root, export_path)

if __name__ == "__main__":
    out_dir = os.path.dirname(os.path.abspath(__file__))
    print(f"Building Task 16 3D models in: {out_dir}")
    
    build_golden_apple(os.path.join(out_dir, "Trophy_GoldenApple.fbx"))
    build_luminous_bonsai(os.path.join(out_dir, "Trophy_LuminousBonsai.fbx"))
    build_star_cup(os.path.join(out_dir, "Trophy_StarCup.fbx"))
    build_drone_crown_hat(os.path.join(out_dir, "Trophy_DroneCrownHat.fbx"))
    build_display_stand(os.path.join(out_dir, "Trophy_DisplayStand.fbx"))
    
    print("All Task 16 models generated successfully!")
