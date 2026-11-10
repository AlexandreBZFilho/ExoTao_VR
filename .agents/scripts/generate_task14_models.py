import bpy
import mathutils
import math
import os

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)
    for mat in list(bpy.data.materials):
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

def create_star_mesh_xy(name, outer_r=0.062, inner_r=0.026, base_z=0.012, tip_z=0.022, is_front=True):
    """
    Cria uma estrela prismática 3D escultural no plano X-Y com saliência no eixo Z.
    Frente: ponta em +Z. Trás: ponta em -Z.
    """
    verts = []
    faces = []
    
    num_pts = 10
    step = 2.0 * math.pi / num_pts
    sign = 1.0 if is_front else -1.0
    
    # 10 vértices de contorno alternando ponta externa e vale interno no plano X-Y
    for i in range(num_pts):
        angle = i * step + (math.pi / 2.0) # Ponta superior no eixo +Y
        r = outer_r if (i % 2 == 0) else inner_r
        x = r * math.cos(angle)
        y = r * math.sin(angle)
        z = base_z * sign
        verts.append((x, y, z))
        
    # Vértice central em ponta saliente
    center_idx = len(verts)
    verts.append((0.0, 0.0, tip_z * sign))
    
    # Vértice traseiro da base da estrela
    back_center_idx = len(verts)
    verts.append((0.0, 0.0, (base_z - 0.003) * sign))
    
    # Faces frontais (facetas piramidais da estrela)
    for i in range(num_pts):
        next_i = (i + 1) % num_pts
        if is_front:
            faces.append((center_idx, i, next_i))
            faces.append((back_center_idx, next_i, i))
        else:
            faces.append((center_idx, next_i, i))
            faces.append((back_center_idx, i, next_i))
            
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj

def build_energy_star_coin(output_path):
    clear_scene()
    
    # Materiais vibrantes e reluzentes
    mat_gold = create_material("M_Coin_Gold", (1.0, 0.82, 0.18, 1.0), metallic=0.88, roughness=0.20)
    mat_rim = create_material("M_Coin_Rim", (0.95, 0.72, 0.10, 1.0), metallic=0.92, roughness=0.16)
    mat_star = create_material("M_Coin_Star_Glow", (1.0, 0.95, 0.45, 1.0), metallic=0.25, roughness=0.10, emission_color=(1.0, 0.90, 0.30, 1.0), emission_strength=3.5)
    mat_cyan_ring = create_material("M_Coin_Cyan_Glow", (0.10, 0.85, 1.0, 1.0), metallic=0.20, roughness=0.18, emission_color=(0.10, 0.85, 1.0, 1.0), emission_strength=2.2)
    
    parts = []
    
    # 1. Corpo Octogonal da Moeda (Coin_Body)
    # Raio: 0.12m (diâmetro 0.24m), profundidade no eixo Z: 0.024m (de -0.012m a +0.012m)
    # Cilindro nativo fica com o disco no plano X-Y e profundidade em Z!
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=8,
        radius=0.120,
        depth=0.024,
        location=(0, 0, 0)
    )
    coin_body = bpy.context.active_object
    coin_body.name = "Coin_Body"
    coin_body.data.materials.append(mat_gold)
    parts.append(coin_body)
    
    # 2. Moldura Octogonal Saliente (Coin_Rim)
    # Raio: 0.123m, profundidade Z: 0.030m (de -0.015m a +0.015m)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=8,
        radius=0.123,
        depth=0.030,
        location=(0, 0, 0)
    )
    rim_outer = bpy.context.active_object
    rim_outer.name = "Coin_Rim"
    rim_outer.data.materials.append(mat_rim)
    parts.append(rim_outer)
    
    # 3. Anel de Energia Ciano Rúnico (Coin_Rune_Ring)
    # Raio: 0.086m, profundidade Z: 0.025m (de -0.0125m a +0.0125m)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=16,
        radius=0.086,
        depth=0.025,
        location=(0, 0, 0)
    )
    rune_ring = bpy.context.active_object
    rune_ring.name = "Coin_Rune_Ring"
    rune_ring.data.materials.append(mat_cyan_ring)
    parts.append(rune_ring)
    
    # 4. Estrela Central Saliente Lado Frente (+Z)
    # Base em Z = +0.012m, ponta central piramidal saliente em Z = +0.022m
    star_front = create_star_mesh_xy("Coin_Star_Front", outer_r=0.062, inner_r=0.026, base_z=0.012, tip_z=0.022, is_front=True)
    star_front.data.materials.append(mat_star)
    parts.append(star_front)
    
    # 5. Estrela Central Saliente Lado Trás (-Z)
    # Base em Z = -0.012m, ponta central piramidal saliente em Z = -0.022m
    star_back = create_star_mesh_xy("Coin_Star_Back", outer_r=0.062, inner_r=0.026, base_z=0.012, tip_z=0.022, is_front=False)
    star_back.data.materials.append(mat_star)
    parts.append(star_back)
    
    # Hierarquia: Anexa todas as partes ao Coin_Body
    for p in parts:
        if p != coin_body:
            p.parent = coin_body
            
    # Aplica rotação de 90 graus no eixo X ao Coin_Body para ficar em pé na vertical no espaço 3D (Y para cima)
    coin_body.rotation_euler = (math.pi / 2.0, 0, 0)
    
    # Seleciona todos
    bpy.ops.object.select_all(action='SELECT')
    
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=output_path,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_unit_scale=True,
        bake_space_transform=True
    )
    print(f"[Blender] Exportado EnergyStarCoin (Estrela 3D Co-Planar Perfeita) para {output_path}")

def build_bonus_diamond_coin(output_path):
    clear_scene()
    
    # Materiais
    mat_crystal = create_material("M_Diamond_Crystal", (0.15, 0.80, 1.0, 0.88), metallic=0.15, roughness=0.12, emission_color=(0.10, 0.75, 1.0, 1.0), emission_strength=1.8)
    mat_core = create_material("M_Diamond_Core_Glow", (0.95, 0.40, 1.0, 1.0), metallic=0.10, roughness=0.10, emission_color=(0.95, 0.35, 1.0, 1.0), emission_strength=4.5)
    mat_gold_ring = create_material("M_Diamond_Gold_Ring", (1.0, 0.85, 0.25, 1.0), metallic=0.90, roughness=0.20)
    mat_orbit_nodes = create_material("M_Diamond_Orbit_Nodes", (0.20, 1.0, 0.90, 1.0), metallic=0.20, roughness=0.15, emission_color=(0.20, 1.0, 0.90, 1.0), emission_strength=3.0)
    
    parts = []
    
    # 1. Diamante Prismático Central (Diamond_Core)
    # Altura total: 0.22m (-0.11m a +0.11m no eixo Y)
    # Cintura octogonal: raio 0.075m em Y=0
    verts = []
    faces = []
    
    verts.append((0.0, 0.110, 0.0))  # Cume superior
    verts.append((0.0, -0.110, 0.0)) # Cume inferior
    
    num_pts = 8
    for i in range(num_pts):
        ang = i * (2.0 * math.pi / num_pts)
        r = 0.075
        x = r * math.cos(ang)
        z = r * math.sin(ang)
        verts.append((x, 0.0, z))
        
    for i in range(num_pts):
        next_i = (i + 1) % num_pts
        faces.append((0, 2 + i, 2 + next_i))
        
    for i in range(num_pts):
        next_i = (i + 1) % num_pts
        faces.append((1, 2 + next_i, 2 + i))
        
    d_mesh = bpy.data.meshes.new("Diamond_Mesh")
    d_mesh.from_pydata(verts, [], faces)
    d_mesh.update()
    
    diamond_obj = bpy.data.objects.new("Diamond_Body", d_mesh)
    bpy.context.collection.objects.link(diamond_obj)
    diamond_obj.data.materials.append(mat_crystal)
    parts.append(diamond_obj)
    
    # 2. Núcleo Interno de Energia (Diamond_Inner_Core)
    oct_verts = [
        (0.0, 0.032, 0.0),
        (0.0, -0.032, 0.0),
        (0.026, 0.0, 0.0),
        (-0.026, 0.0, 0.0),
        (0.0, 0.0, 0.026),
        (0.0, 0.0, -0.026)
    ]
    oct_faces = [
        (0, 2, 4), (0, 4, 3), (0, 3, 5), (0, 5, 2),
        (1, 4, 2), (1, 3, 4), (1, 5, 3), (1, 2, 5)
    ]
    oct_mesh = bpy.data.meshes.new("Diamond_Inner_Mesh")
    oct_mesh.from_pydata(oct_verts, [], oct_faces)
    oct_mesh.update()
    inner_core = bpy.data.objects.new("Diamond_Inner_Core", oct_mesh)
    bpy.context.collection.objects.link(inner_core)
    inner_core.data.materials.append(mat_core)
    parts.append(inner_core)
    
    # 3. Anel Orbital Tecnológico (Diamond_Orbit_Ring)
    bpy.ops.mesh.primitive_torus_add(
        major_radius=0.105,
        minor_radius=0.006,
        major_segments=32,
        minor_segments=8,
        location=(0, 0, 0)
    )
    ring = bpy.context.active_object
    ring.name = "Diamond_Orbit_Ring"
    ring.rotation_euler = (math.radians(25), 0, math.radians(15))
    ring.data.materials.append(mat_gold_ring)
    parts.append(ring)
    
    # 4. Três Esferas/Nós Orbitais acoplados no anel
    for k in range(3):
        theta = k * (2.0 * math.pi / 3.0)
        px = 0.105 * math.cos(theta)
        pz = 0.105 * math.sin(theta)
        py = 0.0
        rot_mat = mathutils.Euler((math.radians(25), 0, math.radians(15))).to_matrix()
        pos = rot_mat @ mathutils.Vector((px, py, pz))
        
        bpy.ops.mesh.primitive_uv_sphere_add(
            radius=0.012,
            segments=16,
            ring_count=8,
            location=pos
        )
        node = bpy.context.active_object
        node.name = f"Diamond_Orbit_Node_{k+1}"
        node.data.materials.append(mat_orbit_nodes)
        parts.append(node)
        node.parent = ring
        
    inner_core.parent = diamond_obj
    ring.parent = diamond_obj
    
    bpy.ops.object.select_all(action='SELECT')
    
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=output_path,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_unit_scale=True,
        bake_space_transform=True
    )
    print(f"[Blender] Exportado BonusDiamondCoin (Escala VR Calibrada) para {output_path}")

if __name__ == "__main__":
    base_dir = os.path.abspath("Assets/Models/Generated/Task14")
    coin_path = os.path.join(base_dir, "EnergyStarCoin.fbx")
    diamond_path = os.path.join(base_dir, "BonusDiamondCoin.fbx")
    
    build_energy_star_coin(coin_path)
    build_bonus_diamond_coin(diamond_path)
    print("ALL TASK-14 3D MODELS REGENERATED WITH FLAWLESS CO-PLANAR STARS!")
