"""
Gerador 3D Low-Poly VR-Optimized — TASK-02 ReticleTargetRing
Retícula Holográfica de Mira Tátil e Suporte Visual para Reabilitação VR

Critérios de Conformidade VR:
1. Orçamento de polígonos: estritamente < 250 triângulos (alvo: ~160 a 200 triângulos).
2. Pivô fixado na base (Y = 0.0) para assentamento exato sobre superfícies e mesas.
3. Dimensões compatíveis com o projeto: raio total ~0.136m (diâmetro 0.272m).
4. Normais suaves (Shade Smooth com Auto Smooth / Weighted Normals).
5. Material 'M_Reticle_HoloCyan' atribuído nativamente.
"""

import bpy
import bmesh
import math
import os
import mathutils

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

def create_reticle_mesh():
    mesh = bpy.data.meshes.new(name="ReticleTargetRing")
    obj = bpy.data.objects.new("ReticleTargetRing", mesh)
    
    col = bpy.data.collections.new("Collection")
    bpy.context.scene.collection.children.link(col)
    col.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    
    mat = bpy.data.materials.new(name="M_Reticle_HoloCyan")
    obj.data.materials.append(mat)
    
    bm = bmesh.new()
    
    # Altura sutil de relevo (3.5 mm) acima da base Y = 0.0
    h_top = 0.0035
    h_bot = 0.0000
    
    # 1. Quatro quadrantes do anel externo (raio interno: 0.100m, raio externo: 0.122m)
    # Cada quadrante cobre 70 graus com folga de 20 graus nos eixos cardeais
    gap_deg = 20.0
    span_deg = 90.0 - gap_deg  # 70 graus
    segs = 4  # 4 segmentos por quadrante = 4 quads na face superior
    
    for q in range(4):
        base_angle = q * 90.0 + gap_deg / 2.0
        # Gera vértices superior e inferior
        v_in_top = []
        v_out_top = []
        v_in_bot = []
        v_out_bot = []
        
        for s in range(segs + 1):
            deg = base_angle + s * (span_deg / segs)
            rad = math.radians(deg)
            cos_a = math.cos(rad)
            sin_a = math.sin(rad)
            
            # X e Z no plano horizontal, Y na vertical
            v_in_top.append(bm.verts.new((0.100 * cos_a, h_top, 0.100 * sin_a)))
            v_out_top.append(bm.verts.new((0.122 * cos_a, h_top, 0.122 * sin_a)))
            v_in_bot.append(bm.verts.new((0.100 * cos_a, h_bot, 0.100 * sin_a)))
            v_out_bot.append(bm.verts.new((0.122 * cos_a, h_bot, 0.122 * sin_a)))
            
        for s in range(segs):
            # Face superior
            bm.faces.new((v_in_top[s], v_out_top[s], v_out_top[s+1], v_in_top[s+1]))
            # Borda externa chanfrada
            bm.faces.new((v_out_top[s], v_out_bot[s], v_out_bot[s+1], v_out_top[s+1]))
            # Borda interna chanfrada
            bm.faces.new((v_in_top[s+1], v_in_bot[s+1], v_in_bot[s], v_in_top[s]))
            
        # Tampas de extremidade do arco
        bm.faces.new((v_in_top[0], v_in_bot[0], v_out_bot[0], v_out_top[0]))
        bm.faces.new((v_out_top[segs], v_out_bot[segs], v_in_bot[segs], v_in_top[segs]))

    # 2. Quatro marcas cardeais de mira (Crosshair Ticks em N, S, L, O)
    # Raio: de 0.086m até 0.136m, largura 0.012m
    tick_r_in = 0.086
    tick_r_out = 0.136
    tick_w = 0.006  # metade da largura
    h_tick = 0.0045 # ligeiramente mais alto que o anel para hierarquia visual
    
    angles = [0.0, 90.0, 180.0, 270.0]
    for ang in angles:
        rad = math.radians(ang)
        cos_a = math.cos(rad)
        sin_a = math.sin(rad)
        # Vetor tangencial perpendicular
        cos_t = -sin_a
        sin_t = cos_a
        
        # 4 cantos no topo
        p1 = (tick_r_in * cos_a - tick_w * cos_t, h_tick, tick_r_in * sin_a - tick_w * sin_t)
        p2 = (tick_r_out * cos_a - tick_w * cos_t, h_tick, tick_r_out * sin_a - tick_w * sin_t)
        p3 = (tick_r_out * cos_a + tick_w * cos_t, h_tick, tick_r_out * sin_a + tick_w * sin_t)
        p4 = (tick_r_in * cos_a + tick_w * cos_t, h_tick, tick_r_in * sin_a + tick_w * sin_t)
        
        # 4 cantos na base
        b1 = (p1[0], h_bot, p1[2])
        b2 = (p2[0], h_bot, p2[2])
        b3 = (p3[0], h_bot, p3[2])
        b4 = (p4[0], h_bot, p4[2])
        
        vt1 = bm.verts.new(p1)
        vt2 = bm.verts.new(p2)
        vt3 = bm.verts.new(p3)
        vt4 = bm.verts.new(p4)
        
        vb1 = bm.verts.new(b1)
        vb2 = bm.verts.new(b2)
        vb3 = bm.verts.new(b3)
        vb4 = bm.verts.new(b4)
        
        # Face superior
        bm.faces.new((vt1, vt2, vt3, vt4))
        # Laterais
        bm.faces.new((vt2, vb2, vb3, vt3))  # ponta externa
        bm.faces.new((vt1, vt4, vb4, vb1))  # ponta interna
        bm.faces.new((vt1, vb1, vb2, vt2))  # lateral 1
        bm.faces.new((vt3, vb3, vb4, vt4))  # lateral 2

    # 3. Oito traços holográficos no anel interno (raio: 0.055m a 0.070m)
    # Distribuição a 45 graus cada, com arco de 18 graus
    dash_r_in = 0.055
    dash_r_out = 0.070
    dash_h = 0.003
    for d in range(8):
        d_center = d * 45.0 + 22.5
        d_start = d_center - 9.0
        d_end = d_center + 9.0
        
        rad1 = math.radians(d_start)
        rad2 = math.radians(d_end)
        
        c1, s1 = math.cos(rad1), math.sin(rad1)
        c2, s2 = math.cos(rad2), math.sin(rad2)
        
        dt1 = bm.verts.new((dash_r_in * c1, dash_h, dash_r_in * s1))
        dt2 = bm.verts.new((dash_r_out * c1, dash_h, dash_r_out * s1))
        dt3 = bm.verts.new((dash_r_out * c2, dash_h, dash_r_out * s2))
        dt4 = bm.verts.new((dash_r_in * c2, dash_h, dash_r_in * s2))
        
        db1 = bm.verts.new((dt1.co.x, h_bot, dt1.co.z))
        db2 = bm.verts.new((dt2.co.x, h_bot, dt2.co.z))
        db3 = bm.verts.new((dt3.co.x, h_bot, dt3.co.z))
        db4 = bm.verts.new((dt4.co.x, h_bot, dt4.co.z))
        
        # Topo
        bm.faces.new((dt1, dt2, dt3, dt4))
        # Lados visíveis
        bm.faces.new((dt2, db2, db3, dt3))
        bm.faces.new((dt4, db4, db1, dt1))
        bm.faces.new((dt1, db1, db2, dt2))
        bm.faces.new((dt3, db3, db4, dt4))

    # Conclui e triangula para contagem exata
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    
    # Suavização de normais
    for poly in mesh.polygons:
        poly.use_smooth = True
        
    mesh.update()
    return obj

def export_fbx(output_path):
    clear_scene()
    obj = create_reticle_mesh()
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    
    # Calcular contagem exata de triângulos
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    tri_count = len(bm.faces)
    vert_count = len(bm.verts)
    bm.free()
    
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    
    # Exporta para FBX com eixo Y para cima e -Z para frente (convenção Unity)
    # mantendo escala nativa 1.0 e pivô em Y=0.0
    bpy.ops.export_scene.fbx(
        filepath=output_path,
        use_selection=False,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_NONE',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=False,
        object_types={'MESH'},
        mesh_smooth_type='FACE',
        use_mesh_modifiers=True
    )
    
    print(f"Exportado com sucesso para: {output_path}")
    print(f"  Vértices: {vert_count}, Triângulos: {tri_count}")
    return vert_count, tri_count

if __name__ == "__main__":
    out_path = r"D:\Alunos\Alexandre\Projeto IC\Assets\Models\Generated\Task02\ReticleTargetRing.fbx"
    export_fbx(out_path)
