import bpy
import bmesh
import math
import os

# Reset
bpy.ops.wm.read_factory_settings(use_empty=True)

mesh = bpy.data.meshes.new("ReticleTargetRingMesh")
obj = bpy.data.objects.new("ReticleTargetRing", mesh)
bpy.context.collection.objects.link(obj)
bpy.context.view_layer.objects.active = obj
obj.select_set(True)

bm = bmesh.new()

# Metric Dimensions:
# Outer diameter = 0.25m -> radius = 0.125m
# Inner diameter = ~0.20m -> radius = 0.102m
# Height / thickness = 0.005m
r_out = 0.125
r_in = 0.102
h = 0.004

# 4 Main curved quadrant segments with gaps at cardinals
quadrants = [(9, 81), (99, 171), (189, 261), (279, 351)]
steps_per_quad = 16

for q_start, q_end in quadrants:
    rad_start = math.radians(q_start)
    rad_end = math.radians(q_end)
    step_rad = (rad_end - rad_start) / steps_per_quad
    
    verts_bottom_outer = []
    verts_bottom_inner = []
    verts_top_outer = []
    verts_top_inner = []
    
    for i in range(steps_per_quad + 1):
        angle = rad_start + i * step_rad
        cos_a = math.cos(angle)
        sin_a = math.sin(angle)
        
        # Bottom (Z=0)
        v_bo = bm.verts.new((r_out * cos_a, r_out * sin_a, 0.0))
        v_bi = bm.verts.new((r_in * cos_a, r_in * sin_a, 0.0))
        # Top (Z=h)
        v_to = bm.verts.new((r_out * cos_a, r_out * sin_a, h))
        v_ti = bm.verts.new((r_in * cos_a, r_in * sin_a, h))
        
        verts_bottom_outer.append(v_bo)
        verts_bottom_inner.append(v_bi)
        verts_top_outer.append(v_to)
        verts_top_inner.append(v_ti)
        
    for i in range(steps_per_quad):
        # Top
        bm.faces.new((verts_top_inner[i], verts_top_outer[i], verts_top_outer[i+1], verts_top_inner[i+1]))
        # Bottom
        bm.faces.new((verts_bottom_outer[i], verts_bottom_inner[i], verts_bottom_inner[i+1], verts_bottom_outer[i+1]))
        # Outer wall
        bm.faces.new((verts_bottom_outer[i], verts_bottom_outer[i+1], verts_top_outer[i+1], verts_top_outer[i]))
        # Inner wall
        bm.faces.new((verts_bottom_inner[i+1], verts_bottom_inner[i], verts_top_inner[i], verts_top_inner[i+1]))
        
    # Cap start
    bm.faces.new((verts_bottom_outer[0], verts_top_outer[0], verts_top_inner[0], verts_bottom_inner[0]))
    # Cap end
    bm.faces.new((verts_bottom_inner[-1], verts_top_inner[-1], verts_top_outer[-1], verts_bottom_outer[-1]))

# 4 Cardinal Chevron Pointers at 0, 90, 180, 270 deg
cardinals = [0, 90, 180, 270]
arrow_h = h * 1.25

for deg in cardinals:
    rad = math.radians(deg)
    cos_c = math.cos(rad)
    sin_c = math.sin(rad)
    perp_x = -sin_c
    perp_y = cos_c
    
    # Arrow tip (pointing inward)
    p_tip = (0.086 * cos_c, 0.086 * sin_c)
    w = 0.014
    l_base = 0.136
    p_bl = (l_base * cos_c + w * perp_x, l_base * sin_c + w * perp_y)
    p_br = (l_base * cos_c - w * perp_x, l_base * sin_c - w * perp_y)
    p_notch = (0.120 * cos_c, 0.120 * sin_c)
    
    v_b_tip = bm.verts.new((p_tip[0], p_tip[1], 0.0))
    v_t_tip = bm.verts.new((p_tip[0], p_tip[1], arrow_h))
    
    v_b_bl = bm.verts.new((p_bl[0], p_bl[1], 0.0))
    v_t_bl = bm.verts.new((p_bl[0], p_bl[1], arrow_h))
    
    v_b_br = bm.verts.new((p_br[0], p_br[1], 0.0))
    v_t_br = bm.verts.new((p_br[0], p_br[1], arrow_h))
    
    v_b_n = bm.verts.new((p_notch[0], p_notch[1], 0.0))
    v_t_n = bm.verts.new((p_notch[0], p_notch[1], arrow_h))
    
    # Left wing
    bm.faces.new((v_t_tip, v_t_bl, v_t_n))
    bm.faces.new((v_b_tip, v_b_n, v_b_bl))
    bm.faces.new((v_b_tip, v_t_tip, v_t_bl, v_b_bl))
    bm.faces.new((v_b_bl, v_t_bl, v_t_n, v_b_n))
    
    # Right wing
    bm.faces.new((v_t_tip, v_t_n, v_t_br))
    bm.faces.new((v_b_tip, v_b_br, v_b_n))
    bm.faces.new((v_b_tip, v_b_br, v_t_br, v_t_tip))
    bm.faces.new((v_b_br, v_b_n, v_t_n, v_t_br))

# Inner concentric segmented circle (8 segments)
for i in range(8):
    a_mid = i * (360.0 / 8.0) + 22.5
    a1 = math.radians(a_mid - 7.5)
    a2 = math.radians(a_mid + 7.5)
    r_in_dot = 0.064
    r_out_dot = 0.072
    
    c1, s1 = math.cos(a1), math.sin(a1)
    c2, s2 = math.cos(a2), math.sin(a2)
    
    vb_o1 = bm.verts.new((r_out_dot * c1, r_out_dot * s1, 0.0))
    vb_o2 = bm.verts.new((r_out_dot * c2, r_out_dot * s2, 0.0))
    vb_i1 = bm.verts.new((r_in_dot * c1, r_in_dot * s1, 0.0))
    vb_i2 = bm.verts.new((r_in_dot * c2, r_in_dot * s2, 0.0))
    
    vt_o1 = bm.verts.new((r_out_dot * c1, r_out_dot * s1, h * 0.75))
    vt_o2 = bm.verts.new((r_out_dot * c2, r_out_dot * s2, h * 0.75))
    vt_i1 = bm.verts.new((r_in_dot * c1, r_in_dot * s1, h * 0.75))
    vt_i2 = bm.verts.new((r_in_dot * c2, r_in_dot * s2, h * 0.75))
    
    bm.faces.new((vt_i1, vt_o1, vt_o2, vt_i2))
    bm.faces.new((vb_o1, vb_i1, vb_i2, vb_o2))
    bm.faces.new((vb_o1, vb_o2, vt_o2, vt_o1))
    bm.faces.new((vb_i2, vb_i1, vt_i1, vt_i2))
    bm.faces.new((vb_i1, vt_i1, vt_o1, vb_o1))
    bm.faces.new((vb_o2, vt_o2, vt_i2, vb_i2))

bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(mesh)
bm.free()

# Material
mat = bpy.data.materials.new(name="M_Reticle_HoloCyan")
mat.use_nodes = True
nodes = mat.node_tree.nodes
bsdf = nodes.get("Principled BSDF")
if bsdf:
    bsdf.inputs["Base Color"].default_value = (0.0, 0.94, 1.0, 1.0)
    if "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (0.0, 0.94, 1.0, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 2.0
    elif "Emission" in bsdf.inputs:
        bsdf.inputs["Emission"].default_value = (0.0, 0.94, 1.0, 1.0)
obj.data.materials.append(mat)

out_dir = r"D:\Alunos\Alexandre\Projeto IC\Assets\Models\Generated\Task02"
os.makedirs(out_dir, exist_ok=True)
out_path = os.path.join(out_dir, "ReticleTargetRing.fbx")

bpy.ops.export_scene.fbx(
    filepath=out_path,
    check_existing=False,
    axis_forward='-Z',
    axis_up='Y',
    use_selection=True,
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',
    bake_space_transform=True,
    mesh_smooth_type='FACE'
)
print("FBX exported successfully to:", out_path)
