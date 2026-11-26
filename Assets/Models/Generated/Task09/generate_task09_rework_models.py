"""
TASK-09 (rework) - modelos do Botao de Fogos e do Mascote Bobblehead.

NAO toca na alavanca de clima (EnvironmentControlLever.fbx), que esta aprovada.
Uso (headless):
  "C:\\Program Files\\Blender Foundation\\Blender 5.1\\blender.exe" -b --factory-startup -P generate_task09_rework_models.py -- [button] [bobble]
Sem argumentos gera os dois.

Orcamentos (CLAUDE.md / plano TASK-09): botao <= 800 tris, bobblehead <= 1800 tris.
Convencao: Blender Z = cima, frente do objeto = -Y (vira +Z na Unity com axis_forward='-Z', axis_up='Y').

Bobblehead - hierarquia exportada (cada parte movel e um objeto proprio, com o pivo onde ela gira):
  ExoMascotBobblehead (empty)
    Bobble_Pedestal, Bobble_Pedestal_Ring, Bobble_Torso, Bobble_Chest_Reactor
    Bobble_Arm_L / Bobble_Arm_R          pivo no ombro (balanco secundario)
    Bobble_Neck_Spring                    mola helicoidal, pivo na BASE (estica em Y e dobra)
    Bobble_Head_Pivot                     cabeca ~1,25x maior, pivo no topo da mola
      Bobble_Antenna_L / Bobble_Antenna_R pivo na base; exportadas como filhas da raiz e
                                          reparentadas para a cabeca no editor da Unity
"""
import bpy
import bmesh
import math
import os
import sys

OUT_DIR = os.environ.get("TASK09_OUT_DIR", r"D:\Alunos\Alexandre\Projeto IC\Assets\Models\Generated\Task09")


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, color, metallic=0.0, roughness=0.5, emission=None, strength=1.5):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = color
        bsdf.inputs['Metallic'].default_value = metallic
        bsdf.inputs['Roughness'].default_value = roughness
        if emission is not None and 'Emission Color' in bsdf.inputs:
            bsdf.inputs['Emission Color'].default_value = emission
            bsdf.inputs['Emission Strength'].default_value = strength
    return mat


def active():
    return bpy.context.view_layer.objects.active


def finish(obj, mat, parent=None, smooth=True, bevel=None):
    """Aplica material, bevel (1-2 segmentos), shade smooth por angulo e parent mantendo a posicao no mundo."""
    if mat is not None:
        obj.data.materials.append(mat)
    if bevel is not None:
        width, segs = bevel
        mod = obj.modifiers.new("Bevel", 'BEVEL')
        mod.width = width
        mod.segments = segs
        mod.limit_method = 'ANGLE'
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if smooth:
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        try:
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40.0))
        except Exception:
            bpy.ops.object.shade_smooth()
        obj.select_set(False)
    if parent is not None:
        # Parent SEM matriz inversa (o exportador FBX com bake_space_transform erra filhos com inversa):
        # guarda a pose no mundo, zera a inversa e reaplica a pose -> transform local limpo.
        bpy.context.view_layer.update()
        world = obj.matrix_world.copy()
        obj.parent = parent
        obj.matrix_parent_inverse.identity()
        obj.matrix_world = world
        bpy.context.view_layer.update()
    return obj


def set_origin(obj, point):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.context.scene.cursor.location = point
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    obj.select_set(False)


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    obj = active()
    obj.name = name
    obj.select_set(False)
    return obj


def delete_below(obj, z_local):
    """Apaga vertices abaixo de z (para fazer hemisferios)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    doomed = [v for v in bm.verts if v.co.z < z_local - 1e-6]
    bmesh.ops.delete(bm, geom=doomed, context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()


def tri_count(objs):
    total = 0
    for o in objs:
        if o.type != 'MESH':
            continue
        o.data.calc_loop_triangles()
        total += len(o.data.loop_triangles)
    return total


def export(root, path):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in bpy.context.scene.objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True,
        use_mesh_modifiers=True,
    )
    tris = tri_count(bpy.context.scene.objects)
    print(f"[TASK-09] {os.path.basename(path)} exportado: {tris} tris")
    for o in bpy.context.scene.objects:
        if o.type == 'MESH':
            o.data.calc_loop_triangles()
            print(f"    {o.name}: {len(o.data.loop_triangles)} tris")
    return tris


# ------------------------------------------------------------------ BOTAO DE FOGOS
def build_button(path):
    reset_scene()
    m_chassis = material("M_FwBtn_Chassis", (0.12, 0.13, 0.16, 1), 0.60, 0.40)
    m_chrome = material("M_FwBtn_Chrome", (0.88, 0.90, 0.94, 1), 0.95, 0.10)
    m_dome = material("M_FwBtn_Dome", (0.95, 0.12, 0.10, 1), 0.15, 0.20, (0.95, 0.12, 0.10, 1), 1.8)
    m_gold = material("M_FwBtn_Gold", (1.0, 0.78, 0.15, 1), 0.88, 0.22)
    m_ring = material("M_FwBtn_Ring", (1.0, 0.60, 0.0, 1), 0.10, 0.25, (1.0, 0.60, 0.0, 1), 2.2)

    root = bpy.data.objects.new("FireworksLaunchButton", None)
    bpy.context.collection.objects.link(root)

    bpy.ops.mesh.primitive_cylinder_add(radius=0.065, depth=0.042, vertices=12, location=(0, 0, 0.021))
    chassis = active(); chassis.name = "Button_Chassis"
    finish(chassis, m_chassis, root, bevel=(0.004, 1))

    bpy.ops.mesh.primitive_cylinder_add(radius=0.050, depth=0.012, vertices=24, location=(0, 0, 0.044))
    bezel = active(); bezel.name = "Button_Bezel_Chrome"
    finish(bezel, m_chrome, root)

    bpy.ops.mesh.primitive_torus_add(major_radius=0.052, minor_radius=0.0025, major_segments=24, minor_segments=4, location=(0, 0, 0.042))
    ring = active(); ring.name = "Button_Glow_Ring"
    finish(ring, m_ring, root)

    # Domo = hemisferio (a metade de baixo ficava escondida dentro do aro e so gastava triangulos)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.038, segments=20, ring_count=10, location=(0, 0, 0.044))
    dome = active(); dome.name = "Button_Dome"
    delete_below(dome, -0.004)
    dome.scale = (1.0, 1.0, 0.55)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    finish(dome, m_dome, root)

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, -0.0645, 0.022))
    plate = active(); plate.name = "Button_Label_Plate"
    plate.scale = (0.055, 0.006, 0.016)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    finish(plate, m_gold, root, smooth=False)

    return export(root, path)


# ------------------------------------------------------------------ BOBBLEHEAD
def helix_spring(name, base_z, height, coil_r, wire_r, turns, steps_per_turn, sides):
    """Mola helicoidal low-poly com o pivo (origem) na base."""
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bm = bmesh.new()
    steps = turns * steps_per_turn
    rings = []
    for s in range(steps + 1):
        a = 2.0 * math.pi * s / steps_per_turn
        z = height * s / steps
        cx, cy = coil_r * math.cos(a), coil_r * math.sin(a)
        # Frame do tubo: normal radial + eixo Z
        nx, ny = math.cos(a), math.sin(a)
        ring = []
        for k in range(sides):
            b = 2.0 * math.pi * k / sides
            ox = nx * math.cos(b) * wire_r
            oy = ny * math.cos(b) * wire_r
            oz = math.sin(b) * wire_r
            ring.append(bm.verts.new((cx + ox, cy + oy, z + oz)))
        rings.append(ring)
    for s in range(steps):
        r0, r1 = rings[s], rings[s + 1]
        for k in range(sides):
            k2 = (k + 1) % sides
            bm.faces.new((r0[k], r0[k2], r1[k2], r1[k]))
    # Tampas planas nas pontas
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj.location = (0, 0, base_z)
    return obj


def build_bobble(path):
    reset_scene()
    m_ped = material("M_Bobble_Pedestal", (0.13, 0.14, 0.17, 1), 0.70, 0.35)
    m_armor = material("M_Bobble_Armor", (0.05, 0.42, 0.65, 1), 0.65, 0.25)
    m_trim = material("M_Bobble_Trim", (0.85, 0.88, 0.92, 1), 0.85, 0.20)
    m_spring = material("M_Bobble_Spring", (0.75, 0.77, 0.80, 1), 0.95, 0.15)
    m_visor = material("M_Bobble_Visor", (0.0, 0.95, 1.0, 1), 0.10, 0.15, (0.0, 0.95, 1.0, 1), 2.6)
    m_ears = material("M_Bobble_Ears", (1.0, 0.55, 0.05, 1), 0.50, 0.30, (1.0, 0.55, 0.05, 1), 1.8)

    root = bpy.data.objects.new("ExoMascotBobblehead", None)
    bpy.context.collection.objects.link(root)

    # Base
    bpy.ops.mesh.primitive_cylinder_add(radius=0.045, depth=0.018, vertices=20, location=(0, 0, 0.009))
    ped = active(); ped.name = "Bobble_Pedestal"
    finish(ped, m_ped, root, bevel=(0.003, 1))

    bpy.ops.mesh.primitive_torus_add(major_radius=0.046, minor_radius=0.002, major_segments=20, minor_segments=3, location=(0, 0, 0.012))
    ped_ring = active(); ped_ring.name = "Bobble_Pedestal_Ring"
    finish(ped_ring, m_visor, root)

    # Corpo pequeno (o contraste com a cabeca grande e o que faz o 'bobblehead')
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.047))
    torso = active(); torso.name = "Bobble_Torso"
    torso.scale = (0.052, 0.040, 0.054)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    finish(torso, m_armor, root, bevel=(0.008, 2))

    bpy.ops.mesh.primitive_cylinder_add(radius=0.010, depth=0.006, vertices=12, location=(0, -0.021, 0.050))
    chest = active(); chest.name = "Bobble_Chest_Reactor"
    chest.rotation_euler = (math.radians(90.0), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    finish(chest, m_visor, root)

    # Bracos com pivo no ombro
    for side, sign in (("L", -1), ("R", 1)):
        bpy.ops.mesh.primitive_cylinder_add(radius=0.0075, depth=0.034, vertices=8, location=(sign * 0.031, 0, 0.044))
        arm = active(); arm.name = f"Bobble_Arm_{side}"
        arm.rotation_euler = (0, sign * math.radians(18.0), 0)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.0095, segments=8, ring_count=4, location=(sign * 0.036, 0, 0.028))
        hand = active()
        arm = join([arm, hand], f"Bobble_Arm_{side}")
        set_origin(arm, (sign * 0.027, 0, 0.062))
        finish(arm, m_trim, root)

    # Mola do pescoco (visivel, 3 voltas) - pivo na base, no topo do tronco
    spring_base = 0.072
    spring_h = 0.026
    spring = helix_spring("Bobble_Neck_Spring", spring_base, spring_h, 0.0095, 0.0019, 3, 10, 4)
    finish(spring, m_spring, root)

    # Cabeca ~1,25x maior que a anterior, pivo no topo da mola
    pivot_z = spring_base + spring_h      # 0.098
    hw, hd, hh = 0.0925, 0.0875, 0.085    # largura, profundidade, altura
    hc = pivot_z + 0.0035 + hh / 2.0      # centro do capacete

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, hc))
    helmet = active(); helmet.name = "Head_Helmet"
    helmet.scale = (hw, hd, hh)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    helmet.data.materials.append(m_armor)
    mod = helmet.modifiers.new("Bevel", 'BEVEL'); mod.width = 0.020; mod.segments = 2
    bpy.context.view_layer.objects.active = helmet
    bpy.ops.object.modifier_apply(modifier=mod.name)

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, -hd / 2.0 - 0.0005, hc + 0.002))
    visor = active(); visor.name = "Head_Visor"
    visor.scale = (0.070, 0.006, 0.036)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    visor.data.materials.append(m_visor)
    mod = visor.modifiers.new("Bevel", 'BEVEL'); mod.width = 0.004; mod.segments = 1
    bpy.context.view_layer.objects.active = visor
    bpy.ops.object.modifier_apply(modifier=mod.name)

    parts = [helmet, visor]
    for side, sign in (("L", -1), ("R", 1)):
        bpy.ops.mesh.primitive_cylinder_add(radius=0.015, depth=0.012, vertices=12, location=(sign * (hw / 2.0 + 0.004), 0, hc))
        ear = active(); ear.name = f"Head_EarPod_{side}"
        ear.rotation_euler = (0, math.radians(90.0), 0)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        ear.data.materials.append(m_trim)
        parts.append(ear)

    head = join(parts, "Bobble_Head_Pivot")
    set_origin(head, (0, 0, pivot_z))
    finish(head, None, root)  # o join ja trouxe os slots [armor, visor, trim]

    # Antenas: objetos proprios, filhos da cabeca, pivo na base (fazem o movimento secundario)
    top_z = hc + hh / 2.0
    for side, sign in (("L", -1), ("R", 1)):
        base = (sign * 0.028, 0.004, top_z - 0.004)
        bpy.ops.mesh.primitive_cone_add(radius1=0.0045, radius2=0.0016, depth=0.032, vertices=8,
                                        location=(base[0], base[1], base[2] + 0.016))
        stem = active()
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.0065, segments=8, ring_count=5,
                                             location=(base[0], base[1], base[2] + 0.034))
        tip = active()
        ant = join([stem, tip], f"Bobble_Antenna_{side}")
        set_origin(ant, base)
        ant.rotation_euler = (0, sign * math.radians(18.0), 0)
        bpy.ops.object.select_all(action='DESELECT')
        ant.select_set(True)
        bpy.context.view_layer.objects.active = ant
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        # Exporta como filho da RAIZ: o exportador FBX (bake_space_transform) gira filhos de filhos.
        # O editor da Unity (Task09_FidgetRework) reparenta as antenas para Bobble_Head_Pivot mantendo a pose.
        finish(ant, m_ears, root)

    return export(root, path)


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    which = set(argv) or {'button', 'bobble'}
    os.makedirs(OUT_DIR, exist_ok=True)
    if 'button' in which:
        build_button(os.path.join(OUT_DIR, "FireworksLaunchButton.fbx"))
    if 'bobble' in which:
        build_bobble(os.path.join(OUT_DIR, "ExoMascotBobblehead.fbx"))
