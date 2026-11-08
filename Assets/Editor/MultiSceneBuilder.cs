using System.IO;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Construye el Laboratorio Cooperativo LICENE: materiales, prefab de pieza en red, escenario de la
// plaza con las 4 plataformas de color, ajustes del jugador y limpieza del menú.
// Menú: Herramientas > Construir Laboratorio Cooperativo (o batch: -executeMethod MultiSceneBuilder.Build).
public static class MultiSceneBuilder
{
    const string GameScene = "Assets/Scenes/SceneMultiplayer.unity";
    const string MenuScene = "Assets/Scenes/MainMenu_Multiplayer.unity";
    const string MatFolder = "Assets/Materiales/LICENE";
    const string PiezasMat = "Assets/Resources/PiezasMat";
    static readonly Vector3 Centro = new Vector3(35f, 0f, 31f);

    [MenuItem("Herramientas/Construir Laboratorio Cooperativo")]
    public static void Build()
    {
        Directory.CreateDirectory(MatFolder);
        Directory.CreateDirectory(PiezasMat);
        CrearMaterialesPiezas();
        CrearPrefabPieza();
        AjustarJugador();
        ConstruirEscenaJuego();
        LimpiarMenu();
        AssetDatabase.SaveAssets();
        Debug.Log("MultiSceneBuilder: listo");
    }

    // ---------- Materiales ----------

    static Material Mat(string carpeta, string nombre, Color color, float brillo = 0f, float suavidad = 0.3f)
    {
        string path = $"{carpeta}/{nombre}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", suavidad);
        mat.enableInstancing = true;
        if (brillo > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * brillo);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else mat.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void CrearMaterialesPiezas()
    {
        for (int i = 0; i < 4; i++) Mat(PiezasMat, PiezaLICENE.Colores[i], PiezaLICENE.Tintes[i], 0.35f, 0.55f);
        Mat(PiezasMat, "Visor", new Color(0.08f, 0.1f, 0.15f), 0f, 0.9f);

        string path = PiezasMat + "/Particula.mat";
        var p = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (p == null)
        {
            p = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            p.SetFloat("_Surface", 1f); // Transparente
            p.SetFloat("_Blend", 2f);   // Aditivo
            p.SetOverrideTag("RenderType", "Transparent");
            p.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            p.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            p.SetInt("_ZWrite", 0);
            p.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            p.renderQueue = 3000;
            AssetDatabase.CreateAsset(p, path);
        }
    }

    // ---------- Prefabs ----------

    static void CrearPrefabPieza()
    {
        var go = new GameObject("PiezaLICENE");
        go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        go.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(PiezasMat + "/Rojo.mat");
        go.AddComponent<BoxCollider>();
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.6f;
        rb.linearDamping = 0.2f;
        rb.angularDamping = 0.5f;
        var pv = go.AddComponent<PhotonView>();
        var pieza = go.AddComponent<PiezaLICENE>();
        pv.OwnershipTransfer = OwnershipOption.Takeover;
        pv.Synchronization = ViewSynchronization.UnreliableOnChange;
        pv.observableSearch = PhotonView.ObservableSearch.Manual;
        pv.ObservedComponents = new System.Collections.Generic.List<Component> { pieza };
        PrefabUtility.SaveAsPrefabAsset(go, "Assets/Resources/PiezaLICENE.prefab");
        Object.DestroyImmediate(go);
    }

    static void AjustarJugador()
    {
        const string path = "Assets/Resources/Player 1.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        var pc = root.GetComponent<PlayerController>();
        pc.gravedad = -18f;
        pc.fuerzaSalto = 1.3f;
        pc.aceleracion = 10f;
        pc.desaceleracion = 8f;
        pc.suavizadoCamara = 25f;
        pc.sensibilidadMouse = 100f;
        pc.velocidad = 5f;
        var cc = root.GetComponent<CharacterController>();
        cc.center = new Vector3(0f, 0f, 0f);
        cc.height = 2f;
        cc.radius = 0.45f;
        cc.stepOffset = 0.4f;
        // Una sola cámara principal: la del jugador local.
        var cam = root.GetComponentInChildren<Camera>(true);
        if (cam != null)
        {
            cam.tag = "MainCamera";
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 400f;
            cam.fieldOfView = 70f;
            var datos = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (datos != null)
            {
                datos.renderPostProcessing = true;
                datos.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
        }
        var name = root.GetComponent<PlayerName>();
        if (name != null && name.playerName != null)
        {
            name.playerName.fontSize = 5f;
            name.playerName.transform.localPosition = new Vector3(0f, 1.45f, 0f);
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ---------- Escena del juego ----------

    static GameObject Prim(PrimitiveType tipo, string nombre, Transform padre, Vector3 pos, Vector3 escala, Material mat, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.position = pos;
        go.transform.localScale = escala;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return go;
    }

    static TextMeshPro Letrero(Transform padre, string texto, Vector3 pos, Quaternion rot, float tam, Color color)
    {
        var go = new GameObject("Letrero " + texto);
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(pos, rot);
        var t = go.AddComponent<TextMeshPro>();
        t.text = texto;
        t.fontSize = tam;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.color = color;
        t.rectTransform.sizeDelta = new Vector2(20f, 5f);
        var contorno = AssetDatabase.LoadAssetAtPath<Material>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat");
        if (contorno != null) t.fontSharedMaterial = contorno;
        return t;
    }

    static GameObject Malla(string nombre, Transform padre, Mesh malla, Vector3 pos, Vector3 escala, Material mat, bool collider = true)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.position = pos;
        go.transform.localScale = escala;
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = malla;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static GameObject Parte(Transform padre, PrimitiveType tipo, string nombre, Vector3 local, Vector3 escala, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = local;
        go.transform.localScale = escala;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static GameObject ParteMalla(Transform padre, Mesh malla, string nombre, Vector3 local, Vector3 escala, Material mat)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = local;
        go.transform.localScale = escala;
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static Mesh GuardarMalla(Mesh m, string nombre)
    {
        Directory.CreateDirectory(MatFolder + "/Mallas");
        string path = $"{MatFolder}/Mallas/{nombre}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // Esfera deformada con ruido: copas de árbol, arbustos, rocas y nubes con forma orgánica.
    static Mesh MallaIrregular(string nombre, int semilla, float cantidad)
    {
        Mesh baseMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
        var v = baseMesh.vertices;
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 d = v[i].normalized;
            float n = Mathf.PerlinNoise(d.x * 2.1f + semilla, d.y * 2.1f + semilla * 0.37f) * 0.6f
                    + Mathf.PerlinNoise(d.z * 4.3f + semilla * 1.7f, d.x * 4.3f) * 0.4f;
            v[i] = d * 0.5f * (1f + (n - 0.5f) * 2f * cantidad);
        }
        var m = new Mesh { name = nombre, vertices = v, triangles = baseMesh.triangles, uv = baseMesh.uv };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return GuardarMalla(m, nombre);
    }

    static Mesh MallaCono()
    {
        const int lados = 14;
        var v = new System.Collections.Generic.List<Vector3>();
        var t = new System.Collections.Generic.List<int>();
        for (int i = 0; i < lados; i++)
        {
            float a0 = i / (float)lados * Mathf.PI * 2f, a1 = (i + 1) / (float)lados * Mathf.PI * 2f;
            Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.5f, p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.5f;
            int b = v.Count;
            v.Add(p0); v.Add(Vector3.up * 0.5f); v.Add(p1);
            t.Add(b); t.Add(b + 1); t.Add(b + 2);
            b = v.Count;
            v.Add(p0); v.Add(p1); v.Add(Vector3.zero);
            t.Add(b); t.Add(b + 1); t.Add(b + 2);
        }
        var m = new Mesh { name = "Cono", vertices = v.ToArray(), triangles = t.ToArray() };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return GuardarMalla(m, "Cono");
    }

    static Material MatTextura(string nombre, Texture2D tex, Color color, Vector2 repetir)
    {
        Material m = Mat(MatFolder, nombre, color, 0f, 0.25f);
        m.SetTexture("_BaseMap", tex);
        m.SetTextureScale("_BaseMap", repetir);
        return m;
    }

    static Texture2D GuardarTextura(string nombre, Texture2D tex)
    {
        string path = $"{MatFolder}/{nombre}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Baldosas de piedra con juntas y variación de tono.
    static Texture2D TexturaBaldosas()
    {
        const int n = 512, celdas = 8;
        var tex = new Texture2D(n, n);
        var rnd = new System.Random(5);
        var tonos = new float[celdas * celdas];
        for (int i = 0; i < tonos.Length; i++) tonos[i] = 0.86f + (float)rnd.NextDouble() * 0.14f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int cx = x * celdas / n, cy = y * celdas / n;
                float fx = (x * celdas % n) / (float)n, fy = (y * celdas % n) / (float)n;
                bool junta = fx < 0.035f || fy < 0.035f;
                float ruido = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.08f + Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 0.05f;
                float t = junta ? 0.55f : tonos[cy * celdas + cx] - ruido;
                tex.SetPixel(x, y, new Color(t, t * 0.97f, t * 0.92f));
            }
        tex.Apply();
        return GuardarTextura("Baldosas", tex);
    }

    static Texture2D TexturaMadera()
    {
        const int n = 256;
        var tex = new Texture2D(n, n);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float vetas = Mathf.Sin((y + Mathf.PerlinNoise(x * 0.02f, y * 0.05f) * 40f) * 0.25f) * 0.5f + 0.5f;
                float t = 0.75f + vetas * 0.2f + Mathf.PerlinNoise(x * 0.3f, y * 0.05f) * 0.08f;
                tex.SetPixel(x, y, new Color(t, t * 0.8f, t * 0.6f));
            }
        tex.Apply();
        return GuardarTextura("MaderaVetas", tex);
    }

    static Texture2D TexturaCorteza()
    {
        const int n = 256;
        var tex = new Texture2D(n, n);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float surcos = Mathf.PerlinNoise(x * 0.12f, y * 0.015f);
                float t = 0.55f + surcos * 0.45f - (surcos < 0.35f ? 0.25f : 0f);
                tex.SetPixel(x, y, new Color(t, t * 0.85f, t * 0.72f));
            }
        tex.Apply();
        return GuardarTextura("Corteza", tex);
    }

    static void ConfigurarPostProceso()
    {
        const string path = "Assets/Settings/LICENE_PostProceso.asset";
        AssetDatabase.DeleteAsset(path);
        var perfil = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        AssetDatabase.CreateAsset(perfil, path);
        var bloom = perfil.Add<UnityEngine.Rendering.Universal.Bloom>(true);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.7f);
        bloom.scatter.Override(0.6f);
        var tono = perfil.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);
        tono.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.Neutral);
        var color = perfil.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
        color.contrast.Override(10f);
        color.saturation.Override(14f);
        color.postExposure.Override(0.15f);
        var vineta = perfil.Add<UnityEngine.Rendering.Universal.Vignette>(true);
        vineta.intensity.Override(0.22f);
        vineta.smoothness.Override(0.5f);
        foreach (var comp in perfil.components) AssetDatabase.AddObjectToAsset(comp, perfil);
        EditorUtility.SetDirty(perfil);

        var volumen = Object.FindFirstObjectByType<UnityEngine.Rendering.Volume>();
        if (volumen == null) volumen = new GameObject("Global Volume").AddComponent<UnityEngine.Rendering.Volume>();
        volumen.isGlobal = true;
        volumen.sharedProfile = perfil;

        // Calidad en PC: antialiasing y sombras a mayor distancia.
        var urp = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        if (urp != null) { urp.msaaSampleCount = 4; urp.shadowDistance = 80f; EditorUtility.SetDirty(urp); }
    }

    static void ConstruirEscenaJuego()
    {
        var scene = EditorSceneManager.OpenScene(GameScene);

        // Fuera lo que causaba errores: un segundo gestor de red (duplicaba jugadores) y el enemigo sin red.
        foreach (string nombre in new[] { "GameManager", "GameController", "Enemy", "SpawnPoint", "Escenario LICENE", "Reto Cooperativo", "Puntos de salida" })
            foreach (GameObject go in scene.GetRootGameObjects().Where(g => g.name == nombre).ToArray()) Object.DestroyImmediate(go);

        Material piedra = MatTextura("Baldosas", TexturaBaldosas(), new Color(1f, 0.98f, 0.95f), new Vector2(9f, 9f));
        Material metal = Mat(MatFolder, "Metal", new Color(0.22f, 0.24f, 0.28f), 0f, 0.7f);
        metal.SetFloat("_Metallic", 0.6f);
        Material piedraOscura = Mat(MatFolder, "PiedraOscura", new Color(0.55f, 0.58f, 0.66f), 0f, 0.25f);
        Material madera = MatTextura("Madera", TexturaMadera(), new Color(0.75f, 0.55f, 0.38f), new Vector2(2f, 1f));
        Material blanco = Mat(MatFolder, "BlancoLICENE", new Color(0.95f, 0.96f, 0.98f), 0f, 0.5f);
        Material arbusto = Mat(MatFolder, "Arbusto", new Color(0.2f, 0.5f, 0.25f), 0f, 0.1f);
        Material roca = Mat(MatFolder, "Roca", new Color(0.5f, 0.5f, 0.52f), 0f, 0.1f);
        Material luz = Mat(MatFolder, "Farol", new Color(1f, 0.85f, 0.55f), 2.5f);
        Material acento = Mat(MatFolder, "AcentoLICENE", new Color(0.2f, 0.75f, 1f), 1.5f);
        Material[] hojas =
        {
            Mat(MatFolder, "Hoja1", new Color(0.3f, 0.65f, 0.3f), 0f, 0.1f),
            Mat(MatFolder, "Hoja2", new Color(0.45f, 0.75f, 0.3f), 0f, 0.1f),
            Mat(MatFolder, "Hoja3", new Color(0.95f, 0.6f, 0.25f), 0f, 0.1f),
        };
        Material[] brillos = Enumerable.Range(0, 4).Select(i => Mat(MatFolder, "Brillo" + PiezaLICENE.Colores[i], PiezaLICENE.Tintes[i], 2f)).ToArray();
        Material[] bases = Enumerable.Range(0, 4).Select(i => Mat(MatFolder, "Base" + PiezaLICENE.Colores[i], Color.Lerp(PiezaLICENE.Tintes[i], Color.white, 0.15f), 0.4f, 0.4f)).ToArray();

        Transform raiz = new GameObject("Escenario LICENE").transform;

        // Plaza.
        Prim(PrimitiveType.Cylinder, "Plaza", raiz, Centro + Vector3.up * 0.05f, new Vector3(36f, 0.1f, 36f), piedra);
        Prim(PrimitiveType.Cylinder, "Circulo central", raiz, Centro + Vector3.up * 0.07f, new Vector3(9f, 0.1f, 9f), piedraOscura);

        // Mesa central donde aparecen las figuras.
        GameObject mesa = Prim(PrimitiveType.Cylinder, "Mesa de figuras", raiz, Centro + Vector3.up * 0.5f, new Vector3(4.6f, 0.5f, 4.6f), blanco);
        Prim(PrimitiveType.Cylinder, "Banda luminosa", raiz, Centro + Vector3.up * 0.86f, new Vector3(4.75f, 0.05f, 4.75f), acento, false);
        Prim(PrimitiveType.Cylinder, "Pedestal", raiz, Centro + Vector3.up * 0.12f, new Vector3(5.4f, 0.12f, 5.4f), piedraOscura);
        Letrero(raiz, "Toma una figura", Centro + new Vector3(0f, 4.6f, 0f), Quaternion.identity, 6f, Color.white).gameObject.AddComponent<MiraCamara>();

        // Plataformas de color.
        var zonas = new ZonaColor[4];
        for (int i = 0; i < 4; i++)
        {
            float ang = (45f + 90f * i) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
            Vector3 pos = Centro + dir * 11f;
            Transform zona = new GameObject("Plataforma " + PiezaLICENE.Colores[i]).transform;
            zona.SetParent(raiz, false);
            zona.position = pos + Vector3.up * 0.1f;

            Prim(PrimitiveType.Cylinder, "Base", zona, pos + Vector3.up * 0.12f, new Vector3(5.2f, 0.06f, 5.2f), bases[i]);
            Transform anillo = new GameObject("Anillo").transform;
            anillo.SetParent(zona, false);
            anillo.position = pos + Vector3.up * 0.25f;
            for (int k = 0; k < 28; k++)
            {
                float a = k / 28f * Mathf.PI * 2f;
                var cubo = Prim(PrimitiveType.Cube, "Luz", anillo, pos + Vector3.up * 0.25f + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.75f, new Vector3(0.35f, 0.12f, 0.18f), brillos[i], false);
                cubo.transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                GameObjectUtility.SetStaticEditorFlags(cubo, 0); // El anillo gira.
            }

            // Poste con el nombre del color, mirando al centro.
            Vector3 poste = pos + dir * 3.4f;
            Prim(PrimitiveType.Cube, "Poste", zona, poste + Vector3.up * 1.6f, new Vector3(0.35f, 3.2f, 0.35f), blanco);
            Prim(PrimitiveType.Sphere, "Gema", zona, poste + Vector3.up * 3.5f, Vector3.one * 0.8f, brillos[i], false);
            Quaternion haciaCentro = Quaternion.LookRotation(dir);
            Letrero(zona, PiezaLICENE.Colores[i].ToUpperInvariant(), poste + Vector3.up * 2.6f - dir * 0.25f, haciaCentro, 9f, PiezaLICENE.Tintes[i]);

            var z = zona.gameObject.AddComponent<ZonaColor>();
            z.color = i;
            z.radio = 2.6f;
            z.anillo = anillo;
            zonas[i] = z;
        }

        // Seto de arbustos redondeados (con pared invisible alta para no salirse) y flores.
        var rnd = new System.Random(11);
        Mesh blob = MallaIrregular("Blob", 3, 0.22f);
        Mesh blob2 = MallaIrregular("Blob2", 9, 0.3f);
        Mesh cono = MallaCono();
        Material[] flores =
        {
            Mat(MatFolder, "FlorRosa", new Color(1f, 0.55f, 0.75f), 0.15f, 0.3f),
            Mat(MatFolder, "FlorBlanca", new Color(1f, 0.98f, 0.92f), 0.1f, 0.3f),
            Mat(MatFolder, "FlorAmarilla", new Color(1f, 0.85f, 0.25f), 0.15f, 0.3f),
            Mat(MatFolder, "FlorMorada", new Color(0.7f, 0.5f, 1f), 0.15f, 0.3f),
        };
        Material arbusto2 = Mat(MatFolder, "Arbusto2", new Color(0.26f, 0.58f, 0.28f), 0f, 0.1f);
        const int setos = 56;
        for (int i = 0; i < setos; i++)
        {
            float a = i / (float)setos * Mathf.PI * 2f;
            Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 p = Centro + radial * 19.2f;
            var seto = Malla("Seto", raiz, i % 2 == 0 ? blob : blob2, p + Vector3.up * 0.75f, new Vector3(2.7f, 1.6f, 1.5f), i % 3 == 0 ? arbusto2 : arbusto, false);
            seto.transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            var muro = new GameObject("Muro invisible");
            muro.transform.SetParent(raiz, false);
            muro.transform.SetPositionAndRotation(p + Vector3.up * 2f, seto.transform.rotation);
            muro.transform.localScale = new Vector3(2.3f, 4f, 1f);
            muro.AddComponent<BoxCollider>();
            // Florecitas sobre el seto.
            for (int f = 0; f < 3; f++)
            {
                Vector3 q = p + seto.transform.right * ((float)rnd.NextDouble() * 2f - 1f) - radial * 0.55f + Vector3.up * (1.0f + (float)rnd.NextDouble() * 0.45f);
                Malla("Flor", raiz, blob, q, Vector3.one * 0.16f, flores[rnd.Next(flores.Length)], false);
            }
        }

        // Faroles con base y capuchón.
        for (int i = 0; i < 8; i++)
        {
            float a = (i / 8f + 1f / 16f) * Mathf.PI * 2f;
            Vector3 p = Centro + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 16.5f;
            Prim(PrimitiveType.Cylinder, "Farol base", raiz, p + Vector3.up * 0.15f, new Vector3(0.45f, 0.15f, 0.45f), metal);
            Prim(PrimitiveType.Cylinder, "Farol poste", raiz, p + Vector3.up * 1.6f, new Vector3(0.14f, 1.5f, 0.14f), metal);
            Prim(PrimitiveType.Sphere, "Farol luz", raiz, p + Vector3.up * 3.2f, Vector3.one * 0.55f, luz, false);
            Prim(PrimitiveType.Cylinder, "Farol capuchon", raiz, p + Vector3.up * 3.52f, new Vector3(0.75f, 0.06f, 0.75f), metal, false);
        }

        // Bancas de madera mirando al centro.
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            Vector3 p = Centro + dir * 16.3f;
            Transform banca = new GameObject("Banca").transform;
            banca.SetParent(raiz, false);
            banca.SetPositionAndRotation(p, Quaternion.LookRotation(-dir));
            Parte(banca, PrimitiveType.Cube, "Asiento", new Vector3(0f, 0.48f, 0f), new Vector3(2.2f, 0.08f, 0.55f), madera);
            Parte(banca, PrimitiveType.Cube, "Respaldo", new Vector3(0f, 0.85f, -0.27f), new Vector3(2.2f, 0.45f, 0.06f), madera);
            foreach (float x in new[] { -0.95f, 0.95f })
            {
                Parte(banca, PrimitiveType.Cube, "Pata", new Vector3(x, 0.24f, 0f), new Vector3(0.08f, 0.48f, 0.5f), metal);
                Parte(banca, PrimitiveType.Cube, "Soporte", new Vector3(x, 0.75f, -0.27f), new Vector3(0.08f, 0.6f, 0.06f), metal);
            }
        }

        // Bosque: árboles frondosos, pinos y árboles de otoño, con inclinación y tamaño variados.
        Material corteza = MatTextura("Corteza", TexturaCorteza(), new Color(0.55f, 0.4f, 0.3f), new Vector2(1f, 2f));
        Material pino = Mat(MatFolder, "Pino", new Color(0.13f, 0.38f, 0.22f), 0f, 0.1f);
        for (int i = 0; i < 85; i++)
        {
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float d = 23f + (float)rnd.NextDouble() * 35f;
            Vector3 p = Centro + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d;
            float s = 0.8f + (float)rnd.NextDouble() * 0.9f;
            Transform arbol = new GameObject("Arbol").transform;
            arbol.SetParent(raiz, false);
            arbol.position = p;
            arbol.rotation = Quaternion.Euler((float)rnd.NextDouble() * 6f - 3f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 6f - 3f);
            int tipo = rnd.Next(10);
            if (tipo < 3)
            {
                Parte(arbol, PrimitiveType.Cylinder, "Tronco", new Vector3(0f, 1f, 0f) * s, new Vector3(0.35f, 1f, 0.35f) * s, corteza);
                for (int k = 0; k < 4; k++)
                    ParteMalla(arbol, cono, "Ramas", new Vector3(0f, (1.6f + k * 1.25f) * s, 0f), new Vector3(3.2f - k * 0.65f, 2.2f, 3.2f - k * 0.65f) * s, pino);
            }
            else
            {
                bool otono = tipo >= 8;
                Material hoja = otono ? hojas[2] : hojas[rnd.Next(2)];
                Material hojaClara = otono ? hojas[2] : hojas[1];
                Parte(arbol, PrimitiveType.Cylinder, "Tronco", new Vector3(0f, 1.5f, 0f) * s, new Vector3(0.4f, 1.5f, 0.4f) * s, corteza);
                Parte(arbol, PrimitiveType.Cylinder, "Rama", new Vector3(0.45f, 2.6f, 0f) * s, new Vector3(0.16f, 0.6f, 0.16f) * s, corteza).transform.localRotation = Quaternion.Euler(0f, 0f, -40f);
                int copas = 3 + rnd.Next(3);
                for (int k = 0; k < copas; k++)
                {
                    Vector3 off = new Vector3((float)rnd.NextDouble() * 1.6f - 0.8f, 3.4f + (float)rnd.NextDouble() * 1.4f, (float)rnd.NextDouble() * 1.6f - 0.8f) * s;
                    float t = (1.6f + (float)rnd.NextDouble() * 1.3f) * s;
                    ParteMalla(arbol, k % 2 == 0 ? blob : blob2, "Copa", off, new Vector3(t, t * 0.85f, t), k == copas - 1 ? hojaClara : hoja);
                }
            }
        }

        // Rocas irregulares con musgo y flores silvestres en el pasto.
        Material[] rocas = { roca, Mat(MatFolder, "Roca2", new Color(0.6f, 0.58f, 0.55f), 0f, 0.15f) };
        for (int i = 0; i < 30; i++)
        {
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
            Vector3 p = Centro + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (21f + (float)rnd.NextDouble() * 28f);
            float t = 0.5f + (float)rnd.NextDouble() * 1.1f;
            var r = Malla("Roca", raiz, blob2, p + Vector3.up * 0.15f * t, new Vector3(1.5f, 0.85f, 1.2f) * t, rocas[rnd.Next(2)]);
            r.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            if (rnd.Next(3) == 0) Malla("Musgo", raiz, blob, p + Vector3.up * 0.55f * t, new Vector3(1f, 0.25f, 0.8f) * t, arbusto2, false);
        }
        for (int i = 0; i < 45; i++)
        {
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
            Vector3 c = Centro + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (20.8f + (float)rnd.NextDouble() * 22f);
            Material m = flores[rnd.Next(flores.Length)];
            for (int k = 0; k < 6; k++)
            {
                Vector3 q = c + new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f) * 1.6f;
                float h = 0.25f + (float)rnd.NextDouble() * 0.2f;
                Prim(PrimitiveType.Cylinder, "Tallo", raiz, q + Vector3.up * h * 0.5f, new Vector3(0.025f, h * 0.5f, 0.025f), arbusto2, false);
                Malla("Flor", raiz, blob, q + Vector3.up * h, Vector3.one * 0.14f, m, false);
            }
        }

        // Nubes esponjosas.
        Material nube = Mat(MatFolder, "Nube", new Color(1f, 1f, 1f), 0.35f, 0f);
        for (int i = 0; i < 12; i++)
        {
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
            Vector3 c = Centro + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (70f + (float)rnd.NextDouble() * 70f) + Vector3.up * (45f + (float)rnd.NextDouble() * 25f);
            Transform n = new GameObject("Nube").transform;
            n.SetParent(raiz, false);
            n.position = c;
            int partes = 4 + rnd.Next(4);
            for (int k = 0; k < partes; k++)
            {
                float t = 6f + (float)rnd.NextDouble() * 6f;
                var go = ParteMalla(n, k % 2 == 0 ? blob : blob2, "Bola", new Vector3((k - partes * 0.5f) * 4.5f, (float)rnd.NextDouble() * 2f, (float)rnd.NextDouble() * 4f - 2f), new Vector3(t, t * 0.6f, t * 0.8f), nube);
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // Gran letrero de bienvenida.
        Vector3 norte = Centro + new Vector3(0f, 0f, 21f);
        Prim(PrimitiveType.Cube, "Arco izquierdo", raiz, norte + new Vector3(-7f, 3f, 0f), new Vector3(0.6f, 6f, 0.6f), blanco);
        Prim(PrimitiveType.Cube, "Arco derecho", raiz, norte + new Vector3(7f, 3f, 0f), new Vector3(0.6f, 6f, 0.6f), blanco);
        Prim(PrimitiveType.Cube, "Arco viga", raiz, norte + new Vector3(0f, 6.2f, 0f), new Vector3(15f, 1.8f, 0.5f), Mat(MatFolder, "Panel", new Color(0.08f, 0.12f, 0.25f), 0f, 0.6f));
        Letrero(raiz, "<color=#33BFFF>LICENE</color>  Laboratorio Cooperativo", norte + new Vector3(0f, 6.2f, -0.3f), Quaternion.identity, 9f, Color.white)
            .rectTransform.sizeDelta = new Vector2(15f, 2f);

        // Reto y puntos de salida.
        var reto = new GameObject("Reto Cooperativo").AddComponent<RetoCooperativo>();
        reto.mesa = mesa.transform;
        reto.zonas = zonas;

        Transform salidas = new GameObject("Puntos de salida").transform;
        var puntos = new Transform[8];
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            Vector3 p = Centro + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 6.5f + Vector3.up * 1.2f;
            var t = new GameObject("Salida " + (i + 1)).transform;
            t.SetParent(salidas, false);
            t.position = p;
            t.rotation = Quaternion.LookRotation(Centro + Vector3.up * 1.2f - p);
            puntos[i] = t;
        }
        var launcher = Object.FindFirstObjectByType<Launcher>();
        launcher.spawnPoints = puntos;
        launcher.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Player 1.prefab");
        EditorUtility.SetDirty(launcher);

        // Ambiente: cielo, luz cálida, niebla suave y césped.
        var cieloPath = MatFolder + "/Cielo.mat";
        var cielo = AssetDatabase.LoadAssetAtPath<Material>(cieloPath);
        if (cielo == null) { cielo = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(cielo, cieloPath); }
        cielo.SetColor("_SkyTint", new Color(0.5f, 0.55f, 0.65f));
        cielo.SetColor("_GroundColor", new Color(0.72f, 0.82f, 0.95f));
        cielo.SetFloat("_AtmosphereThickness", 0.8f);
        cielo.SetFloat("_Exposure", 1.3f);
        cielo.SetFloat("_SunSize", 0.05f);
        RenderSettings.skybox = cielo;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.9f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.6f, 0.58f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.32f, 0.28f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.95f);
        RenderSettings.fogStartDistance = 45f;
        RenderSettings.fogEndDistance = 160f;

        var sol = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        if (sol != null)
        {
            sol.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sol.color = new Color(1f, 0.93f, 0.82f);
            sol.intensity = 1.35f;
            sol.shadows = LightShadows.Soft;
            sol.shadowStrength = 0.75f;
            RenderSettings.sun = sol;
        }

        PintarTerreno();
        ConfigurarPostProceso();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void PintarTerreno()
    {
        Terrain t = Object.FindFirstObjectByType<Terrain>();
        if (t == null) return;
        // La plaza queda en medio del terreno (antes estaba a 35 m de la orilla y se veía el borde).
        Vector3 tam = t.terrainData.size;
        t.transform.position = new Vector3(Centro.x - tam.x * 0.5f, 0f, Centro.z - tam.z * 0.5f);
        var col = t.GetComponent<TerrainCollider>();
        if (col != null) col.terrainData = t.terrainData;
        const string texPath = MatFolder + "/Pasto.png";
        if (!File.Exists(texPath))
        {
            var tex = new Texture2D(256, 256);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.6f + Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.4f;
                    tex.SetPixel(x, y, Color.Lerp(new Color(0.28f, 0.55f, 0.22f), new Color(0.45f, 0.7f, 0.3f), n));
                }
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
        }
        const string layerPath = MatFolder + "/Pasto.terrainlayer";
        var capa = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (capa == null) { capa = new TerrainLayer(); AssetDatabase.CreateAsset(capa, layerPath); }
        capa.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        capa.tileSize = new Vector2(8f, 8f);
        t.terrainData.terrainLayers = new[] { capa };
        EditorUtility.SetDirty(t.terrainData);
    }

    static void LimpiarMenu()
    {
        var scene = EditorSceneManager.OpenScene(MenuScene);
        foreach (GameObject go in scene.GetRootGameObjects().Where(g => g.name == "Canvas").ToArray()) Object.DestroyImmediate(go);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
