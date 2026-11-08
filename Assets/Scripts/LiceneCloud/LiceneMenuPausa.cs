using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Ajustes compartidos por todos los juegos LICENE de PC (se guardan en el equipo).
// Pensados para todos los alumnos y en especial para alumnos con autismo.
public static class LiceneAjustes
{
    public static event Action AlCambiar;

    public static float Volumen { get => PlayerPrefs.GetFloat("licene_volumen", 0.8f); set { PlayerPrefs.SetFloat("licene_volumen", Mathf.Clamp01(value)); Aplicar(); } }
    // Modo calma: menos brillo, efectos y destellos, colores más suaves, sonidos más bajos.
    public static bool ModoCalma { get => PlayerPrefs.GetInt("licene_calma", 0) == 1; set { PlayerPrefs.SetInt("licene_calma", value ? 1 : 0); Aplicar(); } }
    // Sin presión de tiempo: se ocultan los cronómetros y no hay cuentas regresivas.
    public static bool SinTiempo { get => PlayerPrefs.GetInt("licene_sintiempo", 0) == 1; set { PlayerPrefs.SetInt("licene_sintiempo", value ? 1 : 0); Aplicar(); } }
    public static int TamanoTexto { get => PlayerPrefs.GetInt("licene_texto", 0); set { PlayerPrefs.SetInt("licene_texto", Mathf.Clamp(value, 0, 2)); Aplicar(); } }
    public static float Sensibilidad { get => PlayerPrefs.GetFloat("licene_sensibilidad", 1f); set { PlayerPrefs.SetFloat("licene_sensibilidad", Mathf.Clamp(value, 0.2f, 2f)); Aplicar(); } }
    public static bool CamaraSuave { get => PlayerPrefs.GetInt("licene_camsuave", 1) == 1; set { PlayerPrefs.SetInt("licene_camsuave", value ? 1 : 0); Aplicar(); } }
    // Ayudas visuales: rayos, flechas y pictogramas que guían sin texto.
    public static bool Ayudas { get => PlayerPrefs.GetInt("licene_ayudas", 1) == 1; set { PlayerPrefs.SetInt("licene_ayudas", value ? 1 : 0); Aplicar(); } }

    public static float EscalaTexto => TamanoTexto == 0 ? 1f : TamanoTexto == 1 ? 1.2f : 1.4f;

    public static void Aplicar()
    {
        AudioListener.volume = Volumen * (ModoCalma ? 0.6f : 1f);
        foreach (CanvasScaler s in UnityEngine.Object.FindObjectsByType<CanvasScaler>(FindObjectsSortMode.None))
            if (s.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) s.referenceResolution = new Vector2(1920f, 1080f) / EscalaTexto;
        foreach (UnityEngine.Rendering.Volume v in UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
            v.weight = ModoCalma ? 0.35f : 1f;
        AlCambiar?.Invoke();
    }
}

// Menú de pausa con opciones (Esc). Pausa el juego, libera el cursor y ofrece:
// continuar, cómo jugar, opciones de accesibilidad, reiniciar, cuenta LICENE y salir.
public class LiceneMenuPausa : MonoBehaviour
{
    public static LiceneMenuPausa Instance { get; private set; }
    public static bool Pausado => Instance != null && Instance.raiz != null && Instance.raiz.activeSelf;
    public static event Action<bool> AlPausar;

    string juego, ayuda;
    Action reiniciar;
    Action salir; // Si se da, el botón dice "Salir al menú" y hace esto en lugar de cerrar el juego.
    bool detenerTiempo = true; // En juegos en red el mundo sigue: solo se abre el menú.
    GameObject raiz, principal, opciones, comoJugar;
    CursorLockMode cursorAntes;
    bool cursorVisibleAntes;
    float escalaAntes = 1f;
    readonly List<Action> refrescar = new List<Action>();

    public static LiceneMenuPausa Crear(Transform lienzo, string juego, string ayuda, Action reiniciar = null, bool detenerTiempo = true, Action salir = null)
    {
        var go = new GameObject("Menu de pausa");
        go.transform.SetParent(lienzo, false);
        var m = go.AddComponent<LiceneMenuPausa>();
        m.juego = juego;
        m.ayuda = ayuda;
        m.reiniciar = reiniciar;
        m.detenerTiempo = detenerTiempo;
        m.salir = salir;
        m.Construir(lienzo);
        return m;
    }

    void Awake() => Instance = this;

    void Start() => LiceneAjustes.Aplicar();

    void OnDestroy()
    {
        if (Instance != this) return;
        if (Pausado) { Time.timeScale = escalaAntes; AudioListener.pause = false; }
        Instance = null;
    }

    void Construir(Transform c)
    {
        Image fondo = LiceneUI.Panel("Pausa", c, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.02f, 0.04f, 0.1f, 0.7f));
        fondo.rectTransform.anchorMin = Vector2.zero;
        fondo.rectTransform.anchorMax = Vector2.one;
        fondo.rectTransform.sizeDelta = Vector2.zero;
        raiz = fondo.gameObject;

        // Principal.
        Image p = LiceneUI.Tarjeta("Principal", raiz.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, reiniciar != null ? 720f : 640f));
        LiceneUI.Texto(p.transform, "Pausa", 56, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(560f, 70f));
        LiceneUI.Texto(p.transform, juego, 26, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(560f, 34f), TextAnchor.MiddleCenter, FontStyle.Normal, new Color(1f, 1f, 1f, 0.7f));
        float y = -185f;
        Color gris = new Color(1f, 1f, 1f, 0.14f);
        LiceneUI.Boton(p.transform, "Continuar", new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(500f, 70f), new Color(0.15f, 0.6f, 0.95f), () => Mostrar(false));
        LiceneUI.Boton(p.transform, "Cómo jugar", new Vector2(0.5f, 1f), new Vector2(0f, y -= 85f), new Vector2(500f, 64f), gris, () => Ver(comoJugar));
        LiceneUI.Boton(p.transform, "Opciones", new Vector2(0.5f, 1f), new Vector2(0f, y -= 80f), new Vector2(500f, 64f), gris, () => Ver(opciones));
        if (reiniciar != null)
            LiceneUI.Boton(p.transform, "Reiniciar", new Vector2(0.5f, 1f), new Vector2(0f, y -= 80f), new Vector2(500f, 64f), gris, () => { Mostrar(false); reiniciar(); });
        LiceneUI.Boton(p.transform, "Cuenta LICENE", new Vector2(0.5f, 1f), new Vector2(0f, y -= 80f), new Vector2(500f, 64f), new Color(1f, 0.55f, 0.15f), () => { Mostrar(false); if (LiceneCuentaPanel.Instance != null) LiceneCuentaPanel.Instance.Mostrar(true); });
        LiceneUI.Boton(p.transform, salir != null ? "Salir al menú" : "Salir del juego", new Vector2(0.5f, 1f), new Vector2(0f, y -= 80f), new Vector2(500f, 64f), new Color(0.85f, 0.3f, 0.3f), Salir);
        LiceneUI.Texto(p.transform, detenerTiempo ? "Tu progreso se guarda solo" : "El juego sigue en línea mientras estás en el menú", 22, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(560f, 30f), TextAnchor.MiddleCenter, FontStyle.Normal, new Color(0.5f, 1f, 0.65f));
        principal = p.gameObject;

        // Cómo jugar.
        Image a = LiceneUI.Tarjeta("Como jugar", raiz.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 660f), LiceneUI.Acento2);
        LiceneUI.Texto(a.transform, "Cómo jugar", 48, new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(880f, 64f));
        var t = LiceneUI.Texto(a.transform, ayuda, 28, new Vector2(0.5f, 0.5f), new Vector2(0f, -5f), new Vector2(860f, 450f), TextAnchor.UpperLeft, FontStyle.Normal);
        t.lineSpacing = 1.2f;
        LiceneUI.Boton(a.transform, "Volver", new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(300f, 60f), gris, () => Ver(principal));
        comoJugar = a.gameObject;

        // Opciones.
        Image o = LiceneUI.Tarjeta("Opciones", raiz.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 780f));
        LiceneUI.Texto(o.transform, "Opciones", 48, new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(840f, 64f));
        float fy = -130f;
        Fila(o.transform, ref fy, "Volumen", "", () => $"{Mathf.RoundToInt(LiceneAjustes.Volumen * 100)} %", d => LiceneAjustes.Volumen += d * 0.1f);
        Fila(o.transform, ref fy, "Modo calma", "Menos brillo, destellos y movimiento", () => SiNo(LiceneAjustes.ModoCalma), d => LiceneAjustes.ModoCalma = !LiceneAjustes.ModoCalma);
        Fila(o.transform, ref fy, "Sin límite de tiempo", "Oculta los cronómetros", () => SiNo(LiceneAjustes.SinTiempo), d => LiceneAjustes.SinTiempo = !LiceneAjustes.SinTiempo);
        Fila(o.transform, ref fy, "Tamaño del texto", "", () => new[] { "Normal", "Grande", "Muy grande" }[LiceneAjustes.TamanoTexto], d => LiceneAjustes.TamanoTexto = (LiceneAjustes.TamanoTexto + (d > 0 ? 1 : 2)) % 3);
        Fila(o.transform, ref fy, "Sensibilidad de cámara", "", () => $"{LiceneAjustes.Sensibilidad:0.0}x", d => LiceneAjustes.Sensibilidad += d * 0.1f);
        Fila(o.transform, ref fy, "Cámara suave", "Movimientos de cámara más lentos", () => SiNo(LiceneAjustes.CamaraSuave), d => LiceneAjustes.CamaraSuave = !LiceneAjustes.CamaraSuave);
        Fila(o.transform, ref fy, "Ayudas visuales", "Flechas, rayos y pictogramas guía", () => SiNo(LiceneAjustes.Ayudas), d => LiceneAjustes.Ayudas = !LiceneAjustes.Ayudas);
        LiceneUI.Boton(o.transform, "Volver", new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(300f, 60f), gris, () => Ver(principal));
        opciones = o.gameObject;

        raiz.SetActive(false);
    }

    static string SiNo(bool b) => b ? "<color=#7CFF9A>Sí</color>" : "No";

    void Fila(Transform padre, ref float y, string nombre, string detalle, Func<string> valor, Action<int> cambiar)
    {
        LiceneUI.Panel("Fila " + nombre, padre, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(820f, 70f), new Color(1f, 1f, 1f, 0.05f));
        LiceneUI.Texto(padre, nombre, 28, new Vector2(0.5f, 1f), new Vector2(-190f, y + (detalle.Length > 0 ? 10f : 0f)), new Vector2(420f, 36f), TextAnchor.MiddleLeft);
        if (detalle.Length > 0) LiceneUI.Texto(padre, detalle, 18, new Vector2(0.5f, 1f), new Vector2(-190f, y - 18f), new Vector2(420f, 24f), TextAnchor.MiddleLeft, FontStyle.Normal, new Color(1f, 1f, 1f, 0.6f));
        Text v = LiceneUI.Texto(padre, "", 28, new Vector2(0.5f, 1f), new Vector2(225f, y), new Vector2(200f, 40f));
        LiceneUI.Boton(padre, "<", new Vector2(0.5f, 1f), new Vector2(95f, y), new Vector2(56f, 52f), new Color(1f, 1f, 1f, 0.14f), () => { cambiar(-1); Refrescar(); });
        LiceneUI.Boton(padre, ">", new Vector2(0.5f, 1f), new Vector2(355f, y), new Vector2(56f, 52f), new Color(1f, 1f, 1f, 0.14f), () => { cambiar(1); Refrescar(); });
        refrescar.Add(() => v.text = valor());
        y -= 82f;
    }

    void Refrescar() { foreach (Action a in refrescar) a(); }

    void Ver(GameObject panel)
    {
        principal.SetActive(panel == principal);
        opciones.SetActive(panel == opciones);
        comoJugar.SetActive(panel == comoJugar);
        Refrescar();
    }

    void Update()
    {
        if (!LiceneUI.TeclaPresionada(KeyCode.Escape)) return;
        if (LiceneCuentaPanel.Abierto) { LiceneCuentaPanel.Instance.Mostrar(false); return; }
        if (Pausado && !principal.activeSelf) Ver(principal);
        else Mostrar(!Pausado);
    }

    public void Mostrar(bool pausar)
    {
        if (pausar == Pausado) return;
        if (pausar)
        {
            LiceneUI.AsegurarEventSystem();
            cursorAntes = Cursor.lockState;
            cursorVisibleAntes = Cursor.visible;
            escalaAntes = Time.timeScale > 0f ? Time.timeScale : 1f;
            if (detenerTiempo) { Time.timeScale = 0f; AudioListener.pause = true; }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            raiz.SetActive(true);
            Ver(principal);
        }
        else
        {
            raiz.SetActive(false);
            if (detenerTiempo) { Time.timeScale = escalaAntes; AudioListener.pause = false; }
            Cursor.lockState = cursorAntes;
            Cursor.visible = cursorVisibleAntes;
        }
        AlPausar?.Invoke(pausar);
    }

    void Salir()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (salir != null) { raiz.SetActive(false); salir(); return; }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
