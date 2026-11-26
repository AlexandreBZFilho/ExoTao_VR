"""
TASK-16 (retoque) - nova base da estante de trofeus (Trophy_DisplayStand_v2.fbx).

Uso (headless):
  "C:\\Program Files\\Blender Foundation\\Blender 5.1\\blender.exe" -b --factory-startup -P generate_task16_stand_v2.py -- [largura] [espacamento]
  padrao: largura 0.86 m, espacamento 0.20 m entre centros (alternativa do plano: 0.72 / 0.17)

Blender Z = cima, frente = -Y (vira +Z na Unity com axis_forward='-Z', axis_up='Y'); pivo na base central (z = 0).
Objetos (cada um com nome proprio para o builder da Unity achar):
  Stand_Plinth            plinto chanfrado (metal escuro)
  Stand_Trim_Front        friso de latao na quina da frente
  Stand_BackPanel         painel baixo de fundo (nao cobre os trofeus)
  Stand_BackCap           friso de latao no topo do painel
  Stand_Pedestal_0..3     pedestais redondos chanfrados
  Stand_Ring_0..3         aneis emissivos embutidos no topo de cada pedestal (renderer proprio -> cor por MPB)
  Stand_Plaque_0..3       placas inclinadas 30 graus na frente de cada pedestal (latao)
Orcamento: <= 1.800 tris no conjunto.
"""
import bpy
import bmesh
import math
import os
import sys

OUT = os.environ.get("TASK16_OUT_DIR", os.path.dirname(os.path.abspath(__file__)))

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
W = float(argv[0]) if len(argv) > 0 else 0.86
S = float(argv[1]) if len(argv) > 1 else 0.20
D = 0.22            # profundidade
PLINTH_H = 0.045
PED_R = 0.058
PED_H = 0.022
PED_Y = 0.010       # centro do pedestal um pouco para tras (a placa fica na frente; a coroa de 0,16 m nao encosta no painel)


def mat(name, color, metallic, rough, emit=None, strength=1.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = color
    b.inputs["Metallic"].default_value = metallic
    b.inputs["Roughness"].default_value = rough
    if emit is not None:
        b.inputs["Emission Color"].default_value = emit
        b.inputs["Emission Strength"].default_value = strength
    return m


def active():
    return bpy.context.view_layer.objects.active


def finish(o, m, root, bevel=None, smooth=True):
    o.data.materials.append(m)
    if bevel:
        md = o.modifiers.new("Bevel", "BEVEL")
        md.width, md.segments = bevel
        md.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=md.name)
    if smooth:
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        try:
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
        except Exception:
            bpy.ops.object.shade_smooth()
        o.select_set(False)
    o.parent = root          # raiz na origem com identidade: sem matriz inversa
    return o


def box(name, size, loc):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    o = active()
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return o


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    m_dark = mat("M_Stand_DarkMetal", (0.07, 0.075, 0.09, 1), 0.75, 0.38)
    m_brass = mat("M_Stand_Brass", (0.86, 0.66, 0.30, 1), 0.9, 0.28)
    m_ring = mat("M_Stand_Ring", (1, 1, 1, 1), 0.0, 0.3, (1, 1, 1, 1), 1.5)

    root = bpy.data.objects.new("Trophy_DisplayStand_v2", None)
    bpy.context.collection.objects.link(root)

    finish(box("Stand_Plinth", (W, D, PLINTH_H), (0, 0, PLINTH_H / 2)), m_dark, root, bevel=(0.008, 1))
    finish(box("Stand_Trim_Front", (W - 0.012, 0.010, 0.008), (0, -D / 2 + 0.002, PLINTH_H - 0.003)), m_brass, root, smooth=False)
    panel_h = 0.12
    finish(box("Stand_BackPanel", (W, 0.016, panel_h), (0, D / 2 - 0.008, PLINTH_H + panel_h / 2)), m_dark, root, bevel=(0.004, 1))
    finish(box("Stand_BackCap", (W + 0.004, 0.022, 0.008), (0, D / 2 - 0.008, PLINTH_H + panel_h + 0.003)), m_brass, root, smooth=False)

    for i in range(4):
        x = (i - 1.5) * S
        bpy.ops.mesh.primitive_cylinder_add(radius=PED_R, depth=PED_H, vertices=16, location=(x, PED_Y, PLINTH_H + PED_H / 2))
        finish(active(), m_dark, root, bevel=(0.004, 1)).name = f"Stand_Pedestal_{i}"
        bpy.ops.mesh.primitive_torus_add(major_radius=PED_R - 0.010, minor_radius=0.0035, major_segments=16, minor_segments=3,
                                         location=(x, PED_Y, PLINTH_H + PED_H + 0.0005))
        finish(active(), m_ring, root).name = f"Stand_Ring_{i}"
        # Placa inclinada 30 graus (topo para tras), apoiada no plinto na frente do pedestal
        p = box(f"Stand_Plaque_{i}", (min(0.15, S - 0.03), 0.006, 0.046), (0, 0, 0))
        p.rotation_euler = (math.radians(-30.0), 0, 0)   # placa reclinada 30 graus: a face frontal olha para a frente e 30 graus para cima
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        p.location = (x, -D / 2 + 0.030, PLINTH_H + 0.021)
        bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
        finish(p, m_brass, root, bevel=(0.002, 1), smooth=False)

    path = os.path.join(OUT, "Trophy_DisplayStand_v2.fbx")
    bpy.ops.object.select_all(action="DESELECT")
    for o in bpy.context.scene.objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, axis_forward="-Z", axis_up="Y",
                             apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, use_mesh_modifiers=True)
    total = 0
    for o in bpy.context.scene.objects:
        if o.type == "MESH":
            o.data.calc_loop_triangles()
            total += len(o.data.loop_triangles)
    print(f"[TASK-16] {path}: largura {W} m, espacamento {S} m, {total} tris")


main()
