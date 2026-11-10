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

# ==============================================================================
# 1. GlowingPineconeSeed.fbx
# ==============================================================================
def build_pinecone_seed(export_path):
    reset_scene()
    
    mat_bark = create_material("M_Pinecone_Bark", (0.34, 0.19, 0.08, 1.0), metallic=0.05, roughness=0.75)
    mat_glow = create_material("M_Pinecone_Glow", (0.10, 0.88, 0.98, 1.0), metallic=0.2, roughness=0.15,
                               is_emissive=True, emission_color=(0.12, 0.90, 1.0, 1.0), emission_strength=3.0)
    
    root = bpy.data.objects.new("GlowingPineconeSeed", None)
    bpy.context.collection.objects.link(root)
    
    # Core spindle
    bm = bmesh.new()
    bmesh.ops.create_cone(
        bm,
        cap_ends=True,
        segments=12,
        radius1=0.022,
        radius2=0.005,
        depth=0.11
    )
    for v in bm.verts:
        v.co.z += 0.055
        
    core_mesh = bpy.data.meshes.new("Pinecone_Core_Mesh")
    bm.to_mesh(core_mesh)
    bm.free()
    
    core_obj = bpy.data.objects.new("Pinecone_Core", core_mesh)
    bpy.context.collection.objects.link(core_obj)
    core_obj.data.materials.append(mat_bark)
    core_obj.parent = root
    
    tier_configs = [
        (0.018, 0.022, 6, 0.022, 0.016, 25.0, False),
        (0.038, 0.032, 8, 0.028, 0.020, 35.0, True),
        (0.058, 0.034, 8, 0.029, 0.020, 42.0, True),
        (0.078, 0.028, 7, 0.025, 0.018, 48.0, True),
        (0.096, 0.018, 5, 0.020, 0.014, 55.0, True),
    ]
    
    scale_idx = 0
    for z, r, count, slen, swid, tilt, is_glow in tier_configs:
        for i in range(count):
            scale_idx += 1
            phi = i * (2.0 * math.pi / count) + (z * 16.0)
            
            sbm = bmesh.new()
            v0 = sbm.verts.new((0, 0, 0))
            v1 = sbm.verts.new((-swid*0.5, slen*0.4, 0.003))
            v2 = sbm.verts.new((0, slen, 0.006))
            v3 = sbm.verts.new((swid*0.5, slen*0.4, 0.003))
            v4 = sbm.verts.new((0, slen*0.45, -0.004))
            
            sbm.faces.new((v0, v1, v2, v3))
            sbm.faces.new((v0, v3, v4))
            sbm.faces.new((v0, v4, v1))
            sbm.faces.new((v4, v3, v2))
            sbm.faces.new((v4, v2, v1))
            
            rot_tilt = math.radians(tilt)
            cos_t, sin_t = math.cos(rot_tilt), math.sin(rot_tilt)
            rot_phi = phi - math.pi * 0.5
            cos_p, sin_p = math.cos(rot_phi), math.sin(rot_phi)
            
            sx = math.cos(phi) * r
            sy = math.sin(phi) * r
            
            for v in sbm.verts:
                y1 = v.co.y * cos_t - v.co.z * sin_t
                z1 = v.co.y * sin_t + v.co.z * cos_t
                x1 = v.co.x
                x2 = x1 * cos_p - y1 * sin_p
                y2 = x1 * sin_p + y1 * cos_p
                z2 = z1
                v.co.x = x2 + sx
                v.co.y = y2 + sy
                v.co.z = z2 + z
                
            sbm.normal_update()
            
            smesh = bpy.data.meshes.new(f"Scale_Mesh_{scale_idx}")
            sbm.to_mesh(smesh)
            sbm.free()
            
            sobj = bpy.data.objects.new(f"Scale_{scale_idx}", smesh)
            bpy.context.collection.objects.link(sobj)
            sobj.data.materials.append(mat_glow if (is_glow and (i % 2 == 0)) else mat_bark)
            sobj.parent = root

    # Glowing crown
    tbm = bmesh.new()
    bmesh.ops.create_cone(
        tbm,
        cap_ends=True,
        segments=8,
        radius1=0.008,
        radius2=0.001,
        depth=0.02
    )
    for v in tbm.verts:
        v.co.z += 0.115
    tmesh = bpy.data.meshes.new("Crown_Mesh")
    tbm.to_mesh(tmesh)
    tbm.free()
    tobj = bpy.data.objects.new("Pinecone_Crown", tmesh)
    bpy.context.collection.objects.link(tobj)
    tobj.data.materials.append(mat_glow)
    tobj.parent = root

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
    print(f"Exported GlowingPineconeSeed FBX to {export_path}")

# ==============================================================================
# 2. SproutingFlowerPatch.fbx (ZERO INTERMEDIATE EMPTIES - ALL DIRECT CHILDREN OF ROOT)
# ==============================================================================
def build_flower_patch(export_path):
    reset_scene()
    
    mat_mound = create_material("M_Flower_BaseMound", (0.24, 0.16, 0.09, 1.0), roughness=0.9)
    mat_foliage = create_material("M_Flower_Foliage", (0.16, 0.60, 0.14, 1.0), roughness=0.6)
    mat_center = create_material("M_Flower_Center", (1.0, 0.72, 0.05, 1.0), roughness=0.35,
                                 is_emissive=True, emission_color=(1.0, 0.65, 0.0, 1.0), emission_strength=1.6)
    mat_yellow = create_material("M_Petals_Yellow", (1.0, 0.88, 0.10, 1.0), roughness=0.3)
    mat_magenta = create_material("M_Petals_Magenta", (0.92, 0.15, 0.55, 1.0), roughness=0.3)
    mat_cyan = create_material("M_Petals_Cyan", (0.10, 0.78, 0.95, 1.0), roughness=0.3)
    
    root = bpy.data.objects.new("SproutingFlowerPatch", None)
    bpy.context.collection.objects.link(root)
    
    # Base Mound: sits flush on Z=0
    bm_mound = bmesh.new()
    bmesh.ops.create_cone(
        bm_mound,
        cap_ends=True,
        segments=16,
        radius1=0.28,
        radius2=0.22,
        depth=0.035
    )
    for v in bm_mound.verts:
        v.co.z += 0.0175
        ang = math.atan2(v.co.y, v.co.x)
        v.co.x *= (1.0 + 0.07 * math.sin(5.0 * ang))
        v.co.y *= (1.0 + 0.07 * math.cos(4.0 * ang))
        
    mound_mesh = bpy.data.meshes.new("Flower_BaseMound_Mesh")
    bm_mound.to_mesh(mound_mesh)
    bm_mound.free()
    
    mound_obj = bpy.data.objects.new("Flower_BaseMound", mound_mesh)
    bpy.context.collection.objects.link(mound_obj)
    mound_obj.data.materials.append(mat_mound)
    mound_obj.parent = root
    
    flower_configs = [
        ( 0.00,  0.00, 0.30,  0.02,  0.01, mat_yellow,  7, 1.15, "1"),
        ( 0.14,  0.08, 0.25,  0.04,  0.02, mat_magenta, 6, 1.00, "2"),
        (-0.13,  0.09, 0.27, -0.03,  0.03, mat_cyan,    7, 1.05, "3"),
        ( 0.12, -0.12, 0.23,  0.03, -0.03, mat_cyan,    6, 0.95, "4"),
        (-0.11, -0.10, 0.26, -0.03, -0.02, mat_yellow,  7, 1.00, "5"),
        (-0.02,  0.16, 0.24,  0.01,  0.04, mat_magenta, 6, 0.95, "6")
    ]
    
    for fx, fy, fheight, cx, cy, fpetal_mat, pcount, hscale, fid in flower_configs:
        # Stem: Direct child of root, positions offset by (fx, fy, 0.02)
        sbm = bmesh.new()
        n_segments = 8
        r_bot = 0.009
        r_top = 0.0055
        slices = 5
        ring_verts = []
        for s in range(slices + 1):
            t = s / float(slices)
            sz = fheight * t + 0.02
            sx = fx + cx * (t * t)
            sy = fy + cy * (t * t)
            sr = r_bot * (1.0 - t) + r_top * t
            
            curr_ring = []
            for j in range(n_segments):
                ang = j * (2.0 * math.pi / n_segments)
                vx = sx + math.cos(ang) * sr
                vy = sy + math.sin(ang) * sr
                vz = sz
                curr_ring.append(sbm.verts.new((vx, vy, vz)))
            ring_verts.append(curr_ring)
            
        for s in range(slices):
            r0 = ring_verts[s]
            r1 = ring_verts[s + 1]
            for j in range(n_segments):
                j_next = (j + 1) % n_segments
                sbm.faces.new((r0[j], r0[j_next], r1[j_next], r1[j]))
                
        for leaf_t in [0.35, 0.65]:
            lz = fheight * leaf_t + 0.02
            lx = fx + cx * (leaf_t * leaf_t)
            ly = fy + cy * (leaf_t * leaf_t)
            lang = leaf_t * 5.5
            l_cos, l_sin = math.cos(lang), math.sin(lang)
            lv0 = sbm.verts.new((lx, ly, lz))
            lv1 = sbm.verts.new((lx + l_cos * 0.035, ly + l_sin * 0.035, lz + 0.015))
            lv2 = sbm.verts.new((lx + l_cos * 0.075, ly + l_sin * 0.075, lz + 0.006))
            lv3 = sbm.verts.new((lx + (l_cos - l_sin*0.3) * 0.035, ly + (l_sin + l_cos*0.3) * 0.035, lz + 0.012))
            sbm.faces.new((lv0, lv1, lv2))
            sbm.faces.new((lv0, lv2, lv3))
            
        sbm.normal_update()
        smesh = bpy.data.meshes.new(f"Stem_Mesh_{fid}")
        sbm.to_mesh(smesh)
        sbm.free()
        
        stem_obj = bpy.data.objects.new(f"Flower_Stem_{fid}", smesh)
        bpy.context.collection.objects.link(stem_obj)
        stem_obj.data.materials.append(mat_foliage)
        stem_obj.parent = root
        
        # Center Disc: Direct child of root
        top_x = fx + cx
        top_y = fy + cy
        top_z = fheight + 0.02
        
        cbm = bmesh.new()
        bmesh.ops.create_uvsphere(
            cbm,
            u_segments=10,
            v_segments=8,
            radius=0.018 * hscale
        )
        for v in cbm.verts:
            if v.co.z < 0:
                v.co.z *= 0.25
            v.co.x += top_x
            v.co.y += top_y
            v.co.z += top_z + 0.008
        cbm.normal_update()
        cmesh = bpy.data.meshes.new(f"Center_Mesh_{fid}")
        cbm.to_mesh(cmesh)
        cbm.free()
        
        center_obj = bpy.data.objects.new(f"Flower_Center_{fid}", cmesh)
        bpy.context.collection.objects.link(center_obj)
        center_obj.data.materials.append(mat_center)
        center_obj.parent = root
        
        # Petals: Direct child of root
        pbm = bmesh.new()
        petal_len = 0.052 * hscale
        petal_w = 0.024 * hscale
        for p in range(pcount):
            p_ang = p * (2.0 * math.pi / pcount)
            ca = math.cos(p_ang)
            sa = math.sin(p_ang)
            nca = -sa
            nsa = ca
            
            pv0 = pbm.verts.new((top_x + ca * 0.015, top_y + sa * 0.015, top_z + 0.006))
            pv1 = pbm.verts.new((top_x + ca * 0.038 + nca * petal_w * 0.5, top_y + sa * 0.038 + nsa * petal_w * 0.5, top_z + 0.012))
            pv2 = pbm.verts.new((top_x + ca * (0.015 + petal_len), top_y + sa * (0.015 + petal_len), top_z + 0.008))
            pv3 = pbm.verts.new((top_x + ca * 0.038 - nca * petal_w * 0.5, top_y + sa * 0.038 - nsa * petal_w * 0.5, top_z + 0.012))
            pv_bot = pbm.verts.new((top_x + ca * 0.038, top_y + sa * 0.038, top_z - 0.002))
            
            pbm.faces.new((pv0, pv1, pv2, pv3))
            pbm.faces.new((pv0, pv3, pv_bot))
            pbm.faces.new((pv0, pv_bot, pv1))
            pbm.faces.new((pv_bot, pv3, pv2))
            pbm.faces.new((pv_bot, pv2, pv1))
            
        pbm.normal_update()
        pmesh = bpy.data.meshes.new(f"Petals_Mesh_{fid}")
        pbm.to_mesh(pmesh)
        pbm.free()
        
        petals_obj = bpy.data.objects.new(f"Flower_Petals_{fid}", pmesh)
        bpy.context.collection.objects.link(petals_obj)
        petals_obj.data.materials.append(fpetal_mat)
        petals_obj.parent = root

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
    print(f"Exported SproutingFlowerPatch FBX to {export_path}")

# ==============================================================================
# 3. BouncyMushroomCluster.fbx (ZERO INTERMEDIATE EMPTIES - ALL DIRECT CHILDREN OF ROOT)
# ==============================================================================
def build_mushroom_cluster(export_path):
    reset_scene()
    
    mat_soil = create_material("M_Mushroom_Base", (0.20, 0.38, 0.14, 1.0), roughness=0.85)
    mat_stem = create_material("M_Mushroom_Stem", (0.92, 0.88, 0.78, 1.0), roughness=0.55)
    mat_gills = create_material("M_Mushroom_Gills", (0.85, 0.78, 0.65, 1.0), roughness=0.7)
    mat_cap = create_material("M_Mushroom_Cap_Red", (0.88, 0.12, 0.14, 1.0), roughness=0.25)
    mat_spots = create_material("M_Mushroom_Spots", (0.98, 0.98, 0.95, 1.0), roughness=0.35)
    
    root = bpy.data.objects.new("BouncyMushroomCluster", None)
    bpy.context.collection.objects.link(root)
    
    # Base Mound flush to Z=0
    bm_base = bmesh.new()
    bmesh.ops.create_cone(
        bm_base,
        cap_ends=True,
        segments=16,
        radius1=0.22,
        radius2=0.16,
        depth=0.03
    )
    for v in bm_base.verts:
        v.co.z += 0.015
        ang = math.atan2(v.co.y, v.co.x)
        v.co.x *= (1.0 + 0.07 * math.sin(4.0 * ang))
        v.co.y *= (1.0 + 0.07 * math.cos(3.0 * ang))
        
    base_mesh = bpy.data.meshes.new("Mushroom_Base_Mesh")
    bm_base.to_mesh(base_mesh)
    bm_base.free()
    
    base_obj = bpy.data.objects.new("Mushroom_BaseMound", base_mesh)
    bpy.context.collection.objects.link(base_obj)
    base_obj.data.materials.append(mat_soil)
    base_obj.parent = root
    
    mush_configs = [
        ("Mushroom_Large",   0.02,  0.02, 0.18, 0.038, 0.028, 0.11, 0.075,  0.015, -0.010, 10),
        ("Mushroom_Medium", -0.11, -0.05, 0.13, 0.028, 0.020, 0.08, 0.055, -0.020, -0.015,  7),
        ("Mushroom_Small",   0.10, -0.06, 0.09, 0.020, 0.015, 0.055,0.038,  0.015, -0.015,  5)
    ]
    
    for mname, mx, my, sh, rbot, rtop, cap_r, cap_h, cx, cy, spot_n in mush_configs:
        # Stem: direct child of root
        sbm = bmesh.new()
        n_seg = 12
        slices = 5
        ring_verts = []
        for s in range(slices + 1):
            t = s / float(slices)
            sz = sh * t + 0.015
            sx = mx + cx * (t * t)
            sy = my + cy * (t * t)
            sr = (rbot * (1.0 - t) + rtop * t)
            if t < 0.35:
                sr *= (1.0 + (0.35 - t) * 0.5)
                
            curr_ring = []
            for j in range(n_seg):
                ang = j * (2.0 * math.pi / n_seg)
                vx = sx + math.cos(ang) * sr
                vy = sy + math.sin(ang) * sr
                vz = sz
                curr_ring.append(sbm.verts.new((vx, vy, vz)))
            ring_verts.append(curr_ring)
            
        for s in range(slices):
            r0 = ring_verts[s]
            r1 = ring_verts[s + 1]
            for j in range(n_seg):
                j_next = (j + 1) % n_seg
                sbm.faces.new((r0[j], r0[j_next], r1[j_next], r1[j]))
                
        sbm.normal_update()
        smesh = bpy.data.meshes.new(f"{mname}_Stem_Mesh")
        sbm.to_mesh(smesh)
        sbm.free()
        
        stem_obj = bpy.data.objects.new(f"{mname}_Stem", smesh)
        bpy.context.collection.objects.link(stem_obj)
        stem_obj.data.materials.append(mat_stem)
        stem_obj.parent = root
        
        top_x = mx + cx
        top_y = my + cy
        top_z = sh + 0.015
        
        # 1. Domed Red Cap: direct child of root
        cbm = bmesh.new()
        bmesh.ops.create_uvsphere(
            cbm,
            u_segments=16,
            v_segments=12,
            radius=cap_r
        )
        for v in cbm.verts:
            if v.co.z < 0:
                v.co.z *= 0.15
            else:
                v.co.z *= (cap_h / cap_r)
            v.co.x += top_x
            v.co.y += top_y
            v.co.z += top_z + cap_h * 0.45
            
        cbm.normal_update()
        cmesh = bpy.data.meshes.new(f"{mname}_Cap_Mesh")
        cbm.to_mesh(cmesh)
        cbm.free()
        
        cap_obj = bpy.data.objects.new(f"{mname}_Cap", cmesh)
        bpy.context.collection.objects.link(cap_obj)
        cap_obj.data.materials.append(mat_cap)
        cap_obj.parent = root
        
        # 2. Underside Gills: direct child of root
        gbm = bmesh.new()
        bmesh.ops.create_cone(
            gbm,
            cap_ends=True,
            segments=14,
            radius1=cap_r * 0.90,
            radius2=rtop * 1.1,
            depth=0.014
        )
        for v in gbm.verts:
            v.co.x += top_x
            v.co.y += top_y
            v.co.z += top_z + 0.007
        gbm.normal_update()
        gmesh = bpy.data.meshes.new(f"{mname}_Gills_Mesh")
        gbm.to_mesh(gmesh)
        gbm.free()
        
        gills_obj = bpy.data.objects.new(f"{mname}_Gills", gmesh)
        bpy.context.collection.objects.link(gills_obj)
        gills_obj.data.materials.append(mat_gills)
        gills_obj.parent = root
        
        # 3. Raised White Spots: direct child of root
        spbm = bmesh.new()
        golden_angle = 2.39996
        for s in range(spot_n):
            theta = (s + 1) * golden_angle
            r_ratio = math.sqrt((s + 1) / (spot_n + 1)) * 0.75 + 0.12
            sx = math.cos(theta) * (cap_r * r_ratio)
            sy = math.sin(theta) * (cap_r * r_ratio)
            r_norm = min(0.96, math.sqrt(sx*sx + sy*sy) / cap_r)
            sz = math.sqrt(max(0.01, 1.0 - r_norm*r_norm)) * cap_h + cap_h * 0.45
            spot_rad = (0.016 - 0.006 * r_ratio) * (cap_r / 0.10)
            
            res_sphere = bmesh.ops.create_uvsphere(
                spbm,
                u_segments=8,
                v_segments=6,
                radius=spot_rad
            )
            for v in res_sphere['verts']:
                v.co.z *= 0.35
                v.co.x += top_x + sx
                v.co.y += top_y + sy
                v.co.z += top_z + sz
                
        spbm.normal_update()
        spmesh = bpy.data.meshes.new(f"{mname}_Spots_Mesh")
        spbm.to_mesh(spmesh)
        spbm.free()
        
        spots_obj = bpy.data.objects.new(f"{mname}_Spots", spmesh)
        bpy.context.collection.objects.link(spots_obj)
        spots_obj.data.materials.append(mat_spots)
        spots_obj.parent = root

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
    print(f"Exported BouncyMushroomCluster FBX to {export_path}")

# ==============================================================================
# 4. SeedNestPedestal.fbx
# ==============================================================================
def build_pedestal(export_path):
    reset_scene()
    
    mat_bark = create_material("M_Pedestal_Bark", (0.35, 0.22, 0.12, 1.0), roughness=0.85)
    mat_moss = create_material("M_Pedestal_Moss", (0.22, 0.52, 0.15, 1.0), roughness=0.7)
    mat_rune = create_material("M_Pedestal_RuneGlow", (0.15, 0.85, 0.95, 1.0), roughness=0.2,
                               is_emissive=True, emission_color=(0.15, 0.85, 0.95, 1.0), emission_strength=2.2)
    
    root = bpy.data.objects.new("SeedNestPedestal", None)
    bpy.context.collection.objects.link(root)
    
    tbm = bmesh.new()
    bmesh.ops.create_cone(
        tbm,
        cap_ends=True,
        segments=16,
        radius1=0.17,
        radius2=0.13,
        depth=0.65
    )
    for v in tbm.verts:
        v.co.z += 0.325
        ang = math.atan2(v.co.y, v.co.x)
        v.co.x *= (1.0 + 0.05 * math.sin(6.0 * ang))
        v.co.y *= (1.0 + 0.05 * math.cos(5.0 * ang))
        
    tmesh = bpy.data.meshes.new("Pedestal_Trunk_Mesh")
    tbm.to_mesh(tmesh)
    tbm.free()
    
    trunk_obj = bpy.data.objects.new("Pedestal_Trunk", tmesh)
    bpy.context.collection.objects.link(trunk_obj)
    trunk_obj.data.materials.append(mat_bark)
    trunk_obj.parent = root
    
    mbm = bmesh.new()
    bmesh.ops.create_cone(
        mbm,
        cap_ends=True,
        segments=16,
        radius1=0.135,
        radius2=0.132,
        depth=0.035
    )
    for v in mbm.verts:
        v.co.z += 0.65 + 0.0175
        if v.co.z > 0.66:
            r = math.sqrt(v.co.x*v.co.x + v.co.y*v.co.y)
            if r < 0.08:
                dep = (1.0 - r / 0.08)
                v.co.z -= dep * dep * 0.025
                
    mmesh = bpy.data.meshes.new("Pedestal_Moss_Mesh")
    mbm.to_mesh(mmesh)
    mbm.free()
    
    moss_obj = bpy.data.objects.new("Pedestal_MossCap", mmesh)
    bpy.context.collection.objects.link(moss_obj)
    moss_obj.data.materials.append(mat_moss)
    moss_obj.parent = root
    
    rbm = bmesh.new()
    bmesh.ops.create_cone(
        rbm,
        cap_ends=False,
        segments=16,
        radius1=0.055,
        radius2=0.048,
        depth=0.005
    )
    for v in rbm.verts:
        v.co.z += 0.654
    rmesh = bpy.data.meshes.new("Pedestal_Rune_Mesh")
    rbm.to_mesh(rmesh)
    rbm.free()
    
    rune_obj = bpy.data.objects.new("Pedestal_RuneRing", rmesh)
    bpy.context.collection.objects.link(rune_obj)
    rune_obj.data.materials.append(mat_rune)
    rune_obj.parent = root
    
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
    print(f"Exported SeedNestPedestal FBX to {export_path}")

if __name__ == '__main__':
    out_dir = r"D:\Alunos\Alexandre\Projeto IC\Assets\Models\Generated\Task12"
    os.makedirs(out_dir, exist_ok=True)
    
    build_pinecone_seed(os.path.join(out_dir, "GlowingPineconeSeed.fbx"))
    build_flower_patch(os.path.join(out_dir, "SproutingFlowerPatch.fbx"))
    build_mushroom_cluster(os.path.join(out_dir, "BouncyMushroomCluster.fbx"))
    build_pedestal(os.path.join(out_dir, "SeedNestPedestal.fbx"))
    print("All Task 12 3D FBX models regenerated successfully with Blender 5.1 CLI.")
