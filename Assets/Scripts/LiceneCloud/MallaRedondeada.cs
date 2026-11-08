using System.Collections.Generic;
using UnityEngine;

// Cajas con bordes redondeados (biselados) generadas por código, a su tamaño real: se ven como
// muebles y paredes de verdad en lugar de cubos duros. Las mallas se reutilizan por tamaño.
public static class MallaRedondeada
{
    static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

    public static Mesh Caja(Vector3 tam, float radio = 0.06f, int segmentos = 4)
    {
        radio = Mathf.Clamp(radio, 0.001f, Mathf.Min(tam.x, Mathf.Min(tam.y, tam.z)) * 0.5f - 0.0005f);
        string clave = $"{tam.x:0.###}_{tam.y:0.###}_{tam.z:0.###}_{radio:0.###}_{segmentos}";
        if (cache.TryGetValue(clave, out Mesh m) && m != null) return m;

        Vector3 medio = tam * 0.5f;
        Vector3 interior = medio - Vector3.one * radio;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        Vector3[] caras = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 cara in caras)
        {
            Vector3 u = Mathf.Abs(cara.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, cara).normalized;
            Vector3 v = Vector3.Cross(cara, u);
            float lu = Mathf.Abs(Vector3.Dot(u, tam)), lv = Mathf.Abs(Vector3.Dot(v, tam)), lc = Mathf.Abs(Vector3.Dot(cara, tam));
            List<float> cu = Coordenadas(lu, radio, segmentos), cv = Coordenadas(lv, radio, segmentos);
            int inicio = verts.Count;
            foreach (float b in cv)
                foreach (float a in cu)
                {
                    Vector3 p = cara * (lc * 0.5f) + u * a + v * b;
                    Vector3 dentro = new Vector3(Mathf.Clamp(p.x, -interior.x, interior.x), Mathf.Clamp(p.y, -interior.y, interior.y), Mathf.Clamp(p.z, -interior.z, interior.z));
                    Vector3 normal = (p - dentro).sqrMagnitude > 1e-10f ? (p - dentro).normalized : cara;
                    Vector3 final = dentro + normal * radio;
                    verts.Add(final);
                    norms.Add(normal);
                    uvs.Add(new Vector2(Vector3.Dot(final, u), Vector3.Dot(final, v))); // 1 unidad = 1 m
                }
            int nu = cu.Count, nv = cv.Count;
            for (int j = 0; j < nv - 1; j++)
                for (int i = 0; i < nu - 1; i++)
                {
                    int k = inicio + j * nu + i;
                    tris.Add(k); tris.Add(k + 1); tris.Add(k + nu);
                    tris.Add(k + 1); tris.Add(k + nu + 1); tris.Add(k + nu);
                }
        }

        m = new Mesh { name = "Caja redondeada " + clave };
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        m.RecalculateTangents();
        cache[clave] = m;
        return m;
    }

    // Coordenadas a lo largo de un lado: varias en cada borde curvo y la parte plana en medio.
    static List<float> Coordenadas(float largo, float radio, int segmentos)
    {
        var c = new List<float>();
        float h = largo * 0.5f;
        for (int k = 0; k <= segmentos; k++) c.Add(-h + radio * k / segmentos);
        for (int k = 0; k <= segmentos; k++) c.Add(h - radio + radio * k / segmentos);
        return c;
    }

    // Crea un objeto con caja redondeada y su colisionador.
    public static GameObject Crear(string nombre, Transform padre, Vector3 posLocal, Vector3 tam, Material mat, float radio = 0.06f, bool collider = true)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = posLocal;
        go.AddComponent<MeshFilter>().sharedMesh = Caja(tam, radio);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<BoxCollider>().size = tam;
        return go;
    }
}
