
import bpy, os
def count_tris(fbx_path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx_path)
    total_tris = 0
    total_verts = 0
    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            mesh = obj.data
            mesh.calc_loop_triangles()
            tris = len(mesh.loop_triangles)
            verts = len(mesh.vertices)
            total_tris += tris
            total_verts += verts
            print(f'  {obj.name}: {tris} tris, {verts} verts')
    print(f'==> {os.path.basename(fbx_path)}: Total Tris = {total_tris}, Total Verts = {total_verts}')

models = [
    r'D:/Alunos/Alexandre/Projeto IC/Assets/Models/Generated/Task06/RetroCassetteTape.fbx',
    r'D:/Alunos/Alexandre/Projeto IC/Assets/Models/Generated/Task06/CassetteDeckSlot.fbx',
    r'D:/Alunos/Alexandre/Projeto IC/Assets/Models/Generated/Task06/CassetteOrganizerRack.fbx'
]
for m in models:
    count_tris(m)
