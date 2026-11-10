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

def build_apple_mesh(is_golden=False):
    """
    Builds a stylized low-poly apple mesh with stem and leaf.
    Returns the root object.
    """
    radius = 0.045 if is_golden else 0.0425
    body_mat_name = "M_Apple_Gold" if is_golden else "M_Apple_Red"
    body_color = (1.0, 0.82, 0.18, 1.0) if is_golden else (0.85, 0.08, 0.10, 1.0)
    metallic = 0.85 if is_golden else 0.05
    roughness = 0.20 if is_golden else 0.35
    
    mat_body = create_material(body_mat_name, body_color, metallic=metallic, roughness=roughness,
                               is_emissive=is_golden, emission_color=(1.0, 0.80, 0.15, 1.0), emission_strength=1.5 if is_golden else 0.0)
    mat_stem = create_material("M_Apple_Stem", (0.35, 0.20, 0.10, 1.0), metallic=0.1, roughness=0.8)
    mat_leaf = create_material("M_Apple_Leaf", (0.20, 0.68, 0.15, 1.0), metallic=0.05, roughness=0.4)

    # 1. Apple Body: deformed sphere with top indentation and bottom lobes
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=12, radius=radius)
    
    # Deform vertices to make apple shape
    for v in bm.verts:
        x, y, z = v.co.x, v.co.y, v.co.z
        norm_z = z / radius  # -1 to 1
        
        # Apple profile: wider at top-mid, tapers down, indentations at top & bottom
        # Width factor based on height
        width_scale = 1.0 + 0.15 * math.sin((norm_z + 0.3) * math.pi * 0.7)
        v.co.x *= width_scale
        v.co.y *= width_scale
        
        # Top dimple (stem depression)
        if norm_z > 0.65:
            dep = (norm_z - 0.65) / 0.35
            v.co.z -= dep * dep * (radius * 0.45)
            
        # Bottom dimple & 5-fold lobes
        if norm_z < -0.5:
            dep_b = (-norm_z - 0.5) / 0.5
            angle = math.atan2(y, x)
            lobe = 1.0 + 0.08 * math.sin(5.0 * angle)
            v.co.x *= lobe
            v.co.y *= lobe
            v.co.z += dep_b * dep_b * (radius * 0.30)
            
    mesh_body = bpy.data.meshes.new("Apple_Body_Mesh")
    bm.to_mesh(mesh_body)
    bm.free()
    
    body_obj = bpy.data.objects.new("Apple_Body", mesh_body)
    bpy.context.collection.objects.link(body_obj)
    body_obj.data.materials.append(mat_body)
    
    # 2. Apple Stem: curved cylinder emerging from top dimple
    stem_bm = bmesh.new()
    stem_len = 0.024
    stem_rad = 0.0024
    bmesh.ops.create_cone(stem_bm, cap_ends=True, cap_tris=False, segments=8,
                          radius1=stem_rad, radius2=stem_rad * 0.85, depth=stem_len)
    
    # Curve the stem slightly
    for v in stem_bm.verts:
        # v.co.z goes from -stem_len/2 to +stem_len/2
        nz = (v.co.z + stem_len * 0.5) / stem_len
        v.co.x += nz * nz * 0.006
        v.co.z += radius * 0.70 + stem_len * 0.5
        
    mesh_stem = bpy.data.meshes.new("Apple_Stem_Mesh")
    stem_bm.to_mesh(mesh_stem)
    stem_bm.free()
    
    stem_obj = bpy.data.objects.new("Apple_Stem", mesh_stem)
    bpy.context.collection.objects.link(stem_obj)
    stem_obj.data.materials.append(mat_stem)
    
    # 3. Apple Leaf: angled low-poly leaf attached to upper stem
    leaf_bm = bmesh.new()
    leaf_len = 0.034
    leaf_width = 0.016
    
    # Construct stylized curved leaf
    v0 = leaf_bm.verts.new((0.0, 0.0, 0.0))
    v1 = leaf_bm.verts.new((leaf_len * 0.35, -leaf_width * 0.5, 0.003))
    v2 = leaf_bm.verts.new((leaf_len * 0.35, leaf_width * 0.5, 0.003))
    v3 = leaf_bm.verts.new((leaf_len * 0.70, -leaf_width * 0.45, 0.004))
    v4 = leaf_bm.verts.new((leaf_len * 0.70, leaf_width * 0.45, 0.004))
    v5 = leaf_bm.verts.new((leaf_len, 0.0, 0.002))
    
    leaf_bm.faces.new((v0, v1, v2))
    leaf_bm.faces.new((v1, v3, v4, v2))
    leaf_bm.faces.new((v3, v5, v4))
    
    # Position leaf at upper stem
    for v in leaf_bm.verts:
        # Tilt and elevate
        x, y, z = v.co.x, v.co.y, v.co.z
        v.co.x = x * 0.85 + radius * 0.15
        v.co.y = y + radius * 0.05
        v.co.z = z + radius * 0.85 + x * 0.25
        
    mesh_leaf = bpy.data.meshes.new("Apple_Leaf_Mesh")
    leaf_bm.to_mesh(mesh_leaf)
    leaf_bm.free()
    
    leaf_obj = bpy.data.objects.new("Apple_Leaf", mesh_leaf)
    bpy.context.collection.objects.link(leaf_obj)
    leaf_obj.data.materials.append(mat_leaf)
    
    # Join into single unified mesh with origin at (0,0,0)
    bpy.ops.object.select_all(action='DESELECT')
    body_obj.select_set(True)
    stem_obj.select_set(True)
    leaf_obj.select_set(True)
    bpy.context.view_layer.objects.active = body_obj
    bpy.ops.object.join()
    
    apple_obj = bpy.context.active_object
    apple_obj.name = "GoldenBonusApple" if is_golden else "InteractiveRedApple"
    
    # Ensure origin is exactly at (0, 0, 0)
    apple_obj.location = (0, 0, 0)
    
    return apple_obj

def build_wicker_basket(export_path):
    """
    Builds the WickerCollectionBasket 3D model.
    Basket with open rim (0.52m), tapered base (0.40m), height 0.38m,
    hollow interior, side wicker handles, and a rustic score signpost on the back.
    Base is perfectly flat at Z = 0.
    """
    reset_scene()
    print("Building WickerCollectionBasket 3D model...")
    
    mat_wicker = create_material("M_Wicker_Basket", (0.68, 0.50, 0.28, 1.0), metallic=0.05, roughness=0.85)
    mat_wood = create_material("M_Basket_SignPost", (0.48, 0.32, 0.18, 1.0), metallic=0.10, roughness=0.75)
    mat_score_plate = create_material("M_Basket_ScorePlate", (0.24, 0.16, 0.10, 1.0), metallic=0.20, roughness=0.60)
    mat_gold_trim = create_material("M_Basket_GoldTrim", (0.95, 0.78, 0.15, 1.0), metallic=0.85, roughness=0.25,
                                    is_emissive=True, emission_color=(0.95, 0.78, 0.15, 1.0), emission_strength=1.2)
    
    root = bpy.data.objects.new("WickerCollectionBasket", None)
    bpy.context.collection.objects.link(root)
    
    # 1. Basket Body: outer cone + hollow inner cavity
    # Outer bottom radius = 0.20m, top radius = 0.26m, height = 0.38m
    # Base sits at Z = 0, top at Z = 0.38m
    bm = bmesh.new()
    segments = 24
    bot_r_out = 0.20
    top_r_out = 0.26
    height = 0.38
    wall_thick = 0.024
    bot_r_in = bot_r_out - wall_thick
    top_r_in = top_r_out - wall_thick
    floor_z = 0.025  # thickness of bottom
    
    verts_bot_out = []
    verts_top_out = []
    verts_top_in = []
    verts_bot_in = []
    
    for i in range(segments):
        th = 2.0 * math.pi * i / segments
        cos_th, sin_th = math.cos(th), math.sin(th)
        # Texture ribbing / weave bump
        rib = 1.0 + 0.012 * math.sin(segments * 2.0 * th)
        
        v_bo = bm.verts.new((bot_r_out * rib * cos_th, bot_r_out * rib * sin_th, 0.0))
        v_to = bm.verts.new((top_r_out * rib * cos_th, top_r_out * rib * sin_th, height))
        v_ti = bm.verts.new((top_r_in * cos_th, top_r_in * sin_th, height))
        v_bi = bm.verts.new((bot_r_in * cos_th, bot_r_in * sin_th, floor_z))
        
        verts_bot_out.append(v_bo)
        verts_top_out.append(v_to)
        verts_top_in.append(v_ti)
        verts_bot_in.append(v_bi)
        
    # Faces for walls & rim
    for i in range(segments):
        i_next = (i + 1) % segments
        # Outer wall
        bm.faces.new((verts_bot_out[i], verts_bot_out[i_next], verts_top_out[i_next], verts_top_out[i]))
        # Top rim lip
        bm.faces.new((verts_top_out[i], verts_top_out[i_next], verts_top_in[i_next], verts_top_in[i]))
        # Inner wall
        bm.faces.new((verts_top_in[i], verts_top_in[i_next], verts_bot_in[i_next], verts_bot_in[i]))
        
    # Outer bottom cap (reversed normal for downwards)
    bm.faces.new(reversed(verts_bot_out))
    # Inner floor cap (pointing upwards)
    bm.faces.new(verts_bot_in)
    
    mesh_basket = bpy.data.meshes.new("Basket_Body_Mesh")
    bm.to_mesh(mesh_basket)
    bm.free()
    
    basket_obj = bpy.data.objects.new("Basket_Body", mesh_basket)
    bpy.context.collection.objects.link(basket_obj)
    basket_obj.data.materials.append(mat_wicker)
    basket_obj.parent = root
    
    # 2. Top Rim Rolled Lip Ring (Torus) for rich organic look
    bpy.ops.mesh.primitive_torus_add(
        major_radius=top_r_out - wall_thick * 0.5,
        minor_radius=0.016,
        major_segments=24,
        minor_segments=10,
        location=(0, 0, height)
    )
    rim_obj = bpy.context.active_object
    rim_obj.name = "Basket_Rim_Roll"
    rim_obj.data.materials.append(mat_wicker)
    rim_obj.parent = root
    
    # 3. Wicker Side Handles on -X and +X
    for sign, name in [(-1, "L"), (1, "R")]:
        bpy.ops.mesh.primitive_torus_add(
            major_radius=0.065,
            minor_radius=0.012,
            major_segments=18,
            minor_segments=8,
            location=(sign * (top_r_out + 0.035), 0, height - 0.04)
        )
        handle = bpy.context.active_object
        handle.name = f"Basket_Handle_{name}"
        handle.rotation_euler = (0, math.radians(90.0), 0)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        handle.data.materials.append(mat_wicker)
        handle.parent = root
        
    # 4. Rustic Wooden Signpost on the back (+Y side)
    # Sturdy vertical wood post going up to 0.72m
    post_y = top_r_out + 0.03
    bpy.ops.mesh.primitive_cylinder_add(
        radius=0.024,
        depth=0.72,
        vertices=12,
        location=(0, post_y, 0.36)
    )
    post_obj = bpy.context.active_object
    post_obj.name = "Basket_Signpost_Post"
    post_obj.data.materials.append(mat_wood)
    post_obj.parent = root
    
    # Horizontal wooden signboard
    # Width 0.42m (X), Height 0.18m (Z), Thickness 0.024m (Y)
    # Angled slightly backwards (~15 deg) so player standing/sitting sees it clearly
    board_z = 0.64
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=(0, post_y - 0.015, board_z)
    )
    board_obj = bpy.context.active_object
    board_obj.name = "Basket_Signboard_Wood"
    board_obj.scale = (0.42, 0.024, 0.18)
    board_obj.rotation_euler = (math.radians(-15.0), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    board_obj.data.materials.append(mat_wood)
    board_obj.parent = root
    
    # Score plate on front of board (for 3D text and clean display)
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=(0, post_y - 0.030, board_z)
    )
    plate_obj = bpy.context.active_object
    plate_obj.name = "Basket_Score_Plate"
    plate_obj.scale = (0.36, 0.008, 0.13)
    plate_obj.rotation_euler = (math.radians(-15.0), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    plate_obj.data.materials.append(mat_score_plate)
    plate_obj.parent = root
    
    # Golden border trim around score plate
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=(0, post_y - 0.032, board_z)
    )
    trim_obj = bpy.context.active_object
    trim_obj.name = "Basket_Score_Trim"
    trim_obj.scale = (0.38, 0.004, 0.15)
    trim_obj.rotation_euler = (math.radians(-15.0), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    trim_obj.data.materials.append(mat_gold_trim)
    trim_obj.parent = root
    
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
    print(f"Exported WickerCollectionBasket FBX to {export_path}")

def build_apple_fbx(is_golden, export_path):
    reset_scene()
    apple_obj = build_apple_mesh(is_golden=is_golden)
    
    bpy.ops.object.select_all(action='DESELECT')
    apple_obj.select_set(True)
    bpy.context.view_layer.objects.active = apple_obj
    
    bpy.ops.export_scene.fbx(
        filepath=export_path,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True
    )
    print(f"Exported {'GoldenBonusApple' if is_golden else 'InteractiveRedApple'} FBX to {export_path}")

if __name__ == '__main__':
    out_dir = r"D:\Alunos\Alexandre\Projeto IC\Assets\Models\Generated\Task11"
    os.makedirs(out_dir, exist_ok=True)
    
    build_apple_fbx(is_golden=False, export_path=os.path.join(out_dir, "InteractiveRedApple.fbx"))
    build_apple_fbx(is_golden=True, export_path=os.path.join(out_dir, "GoldenBonusApple.fbx"))
    build_wicker_basket(export_path=os.path.join(out_dir, "WickerCollectionBasket.fbx"))
    print("All Task 11 3D FBX models generated successfully with Blender 5.1 CLI.")
