using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construtor procedural de malha 3D unificada (Low-Poly otimizado, 1 único MeshRenderer com 3 submalhas)
/// fiel ao Esquemático Holográfico CAD do Drone EXO-BOT:
/// - Corpo esférico central com linhas de cavidade (seams) luminosas, calota superior e base inferior escalonada;
/// - Lente óptica concêntrica frontal escalonada com núcleo emissivo ciano;
/// - Dois anéis toroidais (Torus) laterais contínuos acoplados por eixos cilíndricos escalonados (sem cilindros soltos).
/// </summary>
public static class GeradorMalhaDroneBlueprint
{
    private class BuilderSubmesh
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<Vector3> norms = new List<Vector3>();
        public readonly List<Vector2> uvs   = new List<Vector2>();
        public readonly List<int>[] tris    = new List<int>[3]
        {
            new List<int>(), // Submesh 0: Corpo / Blindagem Principal
            new List<int>(), // Submesh 1: Metal Escuro Técnico (Aros, Molduras, Eixos)
            new List<int>()  // Submesh 2: Emissivo Ciano Holográfico (Lente Central, Frisos CAD, Interior dos Anéis)
        };

        public void AddQuad(int submesh, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n0, Vector3 n1, Vector3 n2, Vector3 n3)
        {
            int baseIdx = verts.Count;
            verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
            norms.Add(n0); norms.Add(n1); norms.Add(n2); norms.Add(n3);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));

            var t = tris[submesh];
            t.Add(baseIdx + 0); t.Add(baseIdx + 1); t.Add(baseIdx + 2);
            t.Add(baseIdx + 0); t.Add(baseIdx + 2); t.Add(baseIdx + 3);
        }
    }

    public static Mesh ConstruirMalhaDroneCAD()
    {
        Mesh Existente = Resources.Load<Mesh>("Meshes/ExoBot_CAD_Mesh");
        if (Existente != null) return Existente;

        BuilderSubmesh b = new BuilderSubmesh();

        // =====================================================================
        // 1. ESFERA CENTRAL COM FRISOS LUMINOSOS CAD (Raio = 0.21m)
        // =====================================================================
        AdicionarEsferaComFrisosCAD(b, raio: 0.21f, segLat: 24, segLon: 36);

        // =====================================================================
        // 2. CALOTA SUPERIOR REBAIXADA (Topo +Y, igual ao blueprint)
        // =====================================================================
        AdicionarAnelRebaixadoEixoY(b, yCentro: 0.198f, raioExt: 0.078f, raioInt: 0.052f, altura: 0.022f, segs: 28, apontarParaCima: true);

        // =====================================================================
        // 3. ANEL BASE ESTABILIZADOR INFERIOR (Base -Y, igual ao detalhe inferior do blueprint)
        // =====================================================================
        AdicionarAnelRebaixadoEixoY(b, yCentro: -0.195f, raioExt: 0.115f, raioInt: 0.075f, altura: 0.032f, segs: 28, apontarParaCima: false);

        // =====================================================================
        // 4. LENTE ÓPTICA FRONTAL CONCÊNTRICA (+Z, igual à vista explodida superior direita)
        // =====================================================================
        AdicionarLenteFrontalConcentrica(b, zBase: 0.175f, segs: 32);

        // =====================================================================
        // 5. DOIS ANÉIS TOROIDAIS LATERAIS E EIXOS ESCALONADOS (-X e +X)
        // =====================================================================
        AdicionarConjuntoAnelLateral(b, ladoX: -1f);
        AdicionarConjuntoAnelLateral(b, ladoX:  1f);

        Mesh mesh = new Mesh();
        mesh.name = "ExoBot_CAD_Mesh";
        mesh.subMeshCount = 3;
        mesh.SetVertices(b.verts);
        mesh.SetNormals(b.norms);
        mesh.SetUVs(0, b.uvs);
        mesh.SetTriangles(b.tris[0], 0);
        mesh.SetTriangles(b.tris[1], 1);
        mesh.SetTriangles(b.tris[2], 2);
        mesh.RecalculateBounds();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources/Meshes"))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Resources", "Meshes");

            string path = "Assets/Resources/Meshes/ExoBot_CAD_Mesh.asset";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path) == null)
            {
                UnityEditor.AssetDatabase.CreateAsset(mesh, path);
            }
        }
#endif
        return mesh;
    }

    private static void AdicionarEsferaComFrisosCAD(BuilderSubmesh b, float raio, int segLat, int segLon)
    {
        for (int lat = 0; lat < segLat; lat++)
        {
            float t0 = (float)lat / segLat;
            float t1 = (float)(lat + 1) / segLat;
            float phi0 = (t0 - 0.5f) * Mathf.PI;
            float phi1 = (t1 - 0.5f) * Mathf.PI;

            // Define se a faixa latitudinal é um friso luminoso ciano (equador e anéis de blindagem)
            bool faixaLuminosa = (lat == segLat / 2) || (lat == segLat - 5) || (lat == 4);

            for (int lon = 0; lon < segLon; lon++)
            {
                float u0 = (float)lon / segLon * Mathf.PI * 2f;
                float u1 = (float)(lon + 1) / segLon * Mathf.PI * 2f;

                bool meridianoLuminoso = (lon % 9 == 0) && (lat > 4 && lat < segLat - 4);
                int submesh = (faixaLuminosa || meridianoLuminoso) ? 2 : 0;

                float rEscalonado = (faixaLuminosa || meridianoLuminoso) ? raio * 0.992f : raio;

                Vector3 n00 = Esferico(phi0, u0);
                Vector3 n10 = Esferico(phi0, u1);
                Vector3 n11 = Esferico(phi1, u1);
                Vector3 n01 = Esferico(phi1, u0);

                b.AddQuad(submesh,
                    n00 * rEscalonado, n01 * rEscalonado, n11 * rEscalonado, n10 * rEscalonado,
                    n00, n01, n11, n10);
            }
        }
    }

    private static Vector3 Esferico(float phi, float theta)
    {
        float cp = Mathf.Cos(phi);
        return new Vector3(cp * Mathf.Sin(theta), Mathf.Sin(phi), cp * Mathf.Cos(theta));
    }

    private static void AdicionarAnelRebaixadoEixoY(BuilderSubmesh b, float yCentro, float raioExt, float raioInt, float altura, int segs, bool apontarParaCima)
    {
        float dir = apontarParaCima ? 1f : -1f;
        float y0 = yCentro;
        float y1 = yCentro + dir * altura;
        Vector3 nTopo = new Vector3(0f, dir, 0f);

        for (int i = 0; i < segs; i++)
        {
            float a0 = (float)i / segs * Mathf.PI * 2f;
            float a1 = (float)(i + 1) / segs * Mathf.PI * 2f;
            float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0);
            float c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);

            Vector3 nExt0 = new Vector3(c0, 0f, s0);
            Vector3 nExt1 = new Vector3(c1, 0f, s1);

            // Parede externa (Metal Escuro = submesh 1)
            Vector3 p00 = new Vector3(c0 * raioExt, y0, s0 * raioExt);
            Vector3 p10 = new Vector3(c1 * raioExt, y0, s1 * raioExt);
            Vector3 p11 = new Vector3(c1 * raioExt, y1, s1 * raioExt);
            Vector3 p01 = new Vector3(c0 * raioExt, y1, s0 * raioExt);

            if (apontarParaCima)
                b.AddQuad(1, p00, p01, p11, p10, nExt0, nExt0, nExt1, nExt1);
            else
                b.AddQuad(1, p00, p10, p11, p01, nExt0, nExt1, nExt1, nExt0);

            // Borda superior do aro (Metal Escuro = submesh 1)
            Vector3 q00 = new Vector3(c0 * raioInt, y1, s0 * raioInt);
            Vector3 q10 = new Vector3(c1 * raioInt, y1, s1 * raioInt);

            if (apontarParaCima)
                b.AddQuad(1, p01, q00, q10, p11, nTopo, nTopo, nTopo, nTopo);
            else
                b.AddQuad(1, p01, p11, q10, q00, nTopo, nTopo, nTopo, nTopo);

            // Disco central emissivo (Ciano = submesh 2)
            Vector3 centro = new Vector3(0f, y1 - dir * 0.004f, 0f);
            if (apontarParaCima)
                b.AddQuad(2, q00, centro, centro, q10, nTopo, nTopo, nTopo, nTopo);
            else
                b.AddQuad(2, q00, q10, centro, centro, nTopo, nTopo, nTopo, nTopo);
        }
    }

    private static void AdicionarLenteFrontalConcentrica(BuilderSubmesh b, float zBase, int segs)
    {
        // Anel Externo Escalonado 1 (Moldura blindada da lente)
        AdicionarTuboEixoZ(b, submeshParede: 1, submeshFrente: 2, z0: zBase - 0.02f, z1: zBase + 0.028f, rExt: 0.112f, rInt: 0.088f, segs);
        // Anel Intermediário Escalonado 2 (Tubo óptico)
        AdicionarTuboEixoZ(b, submeshParede: 0, submeshFrente: 1, z0: zBase + 0.015f, z1: zBase + 0.048f, rExt: 0.086f, rInt: 0.064f, segs);
        // Lente Central Convexa Emissiva Ciano (Submesh 2)
        float zLente = zBase + 0.044f;
        float rLente = 0.064f;
        Vector3 centroFrente = new Vector3(0f, 0f, zLente + 0.016f);

        for (int i = 0; i < segs; i++)
        {
            float a0 = (float)i / segs * Mathf.PI * 2f;
            float a1 = (float)(i + 1) / segs * Mathf.PI * 2f;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * rLente, Mathf.Sin(a0) * rLente, zLente);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * rLente, Mathf.Sin(a1) * rLente, zLente);
            Vector3 n0 = (p0 - new Vector3(0f, 0f, zLente - 0.05f)).normalized;
            Vector3 n1 = (p1 - new Vector3(0f, 0f, zLente - 0.05f)).normalized;

            b.AddQuad(2, p0, p1, centroFrente, centroFrente, n0, n1, Vector3.forward, Vector3.forward);
        }
    }

    private static void AdicionarTuboEixoZ(BuilderSubmesh b, int submeshParede, int submeshFrente, float z0, float z1, float rExt, float rInt, int segs)
    {
        for (int i = 0; i < segs; i++)
        {
            float a0 = (float)i / segs * Mathf.PI * 2f;
            float a1 = (float)(i + 1) / segs * Mathf.PI * 2f;
            float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0);
            float c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);

            Vector3 nExt0 = new Vector3(c0, s0, 0f);
            Vector3 nExt1 = new Vector3(c1, s1, 0f);

            // Parede Cilíndrica Externa
            Vector3 e00 = new Vector3(c0 * rExt, s0 * rExt, z0);
            Vector3 e10 = new Vector3(c1 * rExt, s1 * rExt, z0);
            Vector3 e11 = new Vector3(c1 * rExt, s1 * rExt, z1);
            Vector3 e01 = new Vector3(c0 * rExt, s0 * rExt, z1);
            b.AddQuad(submeshParede, e00, e10, e11, e01, nExt0, nExt1, nExt1, nExt0);

            // Anel Frontal (+Z)
            Vector3 i01 = new Vector3(c0 * rInt, s0 * rInt, z1);
            Vector3 i11 = new Vector3(c1 * rInt, s1 * rInt, z1);
            b.AddQuad(submeshFrente, e01, e11, i11, i01, Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward);

            // Parede Cilíndrica Interna
            Vector3 i00 = new Vector3(c0 * rInt, s0 * rInt, z0);
            Vector3 i10 = new Vector3(c1 * rInt, s1 * rInt, z0);
            b.AddQuad(1, i01, i11, i10, i00, -nExt0, -nExt1, -nExt1, -nExt0);
        }
    }

    private static void AdicionarConjuntoAnelLateral(BuilderSubmesh b, float ladoX)
    {
        int segs = 36;
        // 1. Base de acoplamento lateral na esfera (x = 0.16 .. 0.215, raio = 0.115)
        AdicionarCilindroEixoX(b, submesh: 1, x0: 0.155f * ladoX, x1: 0.205f * ladoX, r0: 0.115f, r1: 0.095f, segs: 24);
        // Anel luminoso no colarinho do eixo lateral
        AdicionarCilindroEixoX(b, submesh: 2, x0: 0.205f * ladoX, x1: 0.218f * ladoX, r0: 0.088f, r1: 0.088f, segs: 24);
        // Eixo central que vai até o cubo do anel toroidal (x = 0.218 .. 0.265, raio = 0.052)
        AdicionarCilindroEixoX(b, submesh: 1, x0: 0.218f * ladoX, x1: 0.268f * ladoX, r0: 0.052f, r1: 0.048f, segs: 20);

        // 2. Cubo central do anel lateral + 3 hastes radiais discretas internas
        AdicionarCilindroEixoX(b, submesh: 0, x0: 0.245f * ladoX, x1: 0.278f * ladoX, r0: 0.064f, r1: 0.056f, segs: 20);

        // 3. ANEL TOROIDAL LATERAL CONTÍNUO (idêntico às duas rodas/aros laterais do blueprint!)
        // Plano YZ centrado em x = 0.262 * ladoX, Raio Maior = 0.175m, Raio do Tubo = 0.028m
        float xCentroAnel = 0.262f * ladoX;
        float raioMaior = 0.175f;
        float raioTuboY = 0.027f; // espessura radial
        float raioTuboX = 0.032f; // largura axial X
        int segsTubo = 14;

        for (int i = 0; i < segs; i++)
        {
            float a0 = (float)i / segs * Mathf.PI * 2f;
            float a1 = (float)(i + 1) / segs * Mathf.PI * 2f;

            for (int j = 0; j < segsTubo; j++)
            {
                float v0 = (float)j / segsTubo * Mathf.PI * 2f;
                float v1 = (float)(j + 1) / segsTubo * Mathf.PI * 2f;

                // A face interna do aro toroidal (voltada para o centro do anel, cos(v) < -0.35) brilha em ciano (submesh 2),
                // exatamente como o brilho interno dos aros no blueprint!
                float cosMedio = Mathf.Cos((v0 + v1) * 0.5f);
                int sub = (cosMedio < -0.35f) ? 2 : (Mathf.Abs(cosMedio) < 0.25f ? 0 : 1);

                Vector3 p00 = PontoToroEixoX(xCentroAnel, raioMaior, raioTuboY, raioTuboX, a0, v0, out Vector3 n00);
                Vector3 p10 = PontoToroEixoX(xCentroAnel, raioMaior, raioTuboY, raioTuboX, a1, v0, out Vector3 n10);
                Vector3 p11 = PontoToroEixoX(xCentroAnel, raioMaior, raioTuboY, raioTuboX, a1, v1, out Vector3 n11);
                Vector3 p01 = PontoToroEixoX(xCentroAnel, raioMaior, raioTuboY, raioTuboX, a0, v1, out Vector3 n01);

                b.AddQuad(sub, p00, p10, p11, p01, n00, n10, n11, n01);
            }
        }
    }

    private static Vector3 PontoToroEixoX(float xCentro, float R, float rRadial, float rAxial, float theta, float phi, out Vector3 normal)
    {
        float ct = Mathf.Cos(theta), st = Mathf.Sin(theta);
        float cp = Mathf.Cos(phi),   sp = Mathf.Sin(phi);

        float rAtual = R + rRadial * cp;
        float x = xCentro + rAxial * sp;
        float y = rAtual * st;
        float z = rAtual * ct;

        normal = new Vector3(sp / rAxial, (cp * st) / rRadial, (cp * ct) / rRadial).normalized;
        return new Vector3(x, y, z);
    }

    private static void AdicionarCilindroEixoX(BuilderSubmesh b, int submesh, float x0, float x1, float r0, float r1, int segs)
    {
        bool invertido = x1 < x0;
        for (int i = 0; i < segs; i++)
        {
            float a0 = (float)i / segs * Mathf.PI * 2f;
            float a1 = (float)(i + 1) / segs * Mathf.PI * 2f;
            float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0);
            float c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);

            Vector3 n0 = new Vector3(0f, s0, c0);
            Vector3 n1 = new Vector3(0f, s1, c1);

            Vector3 p00 = new Vector3(x0, s0 * r0, c0 * r0);
            Vector3 p10 = new Vector3(x0, s1 * r0, c1 * r0);
            Vector3 p11 = new Vector3(x1, s1 * r1, c1 * r1);
            Vector3 p01 = new Vector3(x1, s0 * r1, c0 * r1);

            if (!invertido)
                b.AddQuad(submesh, p00, p10, p11, p01, n0, n1, n1, n0);
            else
                b.AddQuad(submesh, p00, p01, p11, p10, n0, n0, n1, n1);
        }
    }
}
