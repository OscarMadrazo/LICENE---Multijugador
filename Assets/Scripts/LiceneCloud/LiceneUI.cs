using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Interfaz con el estilo de tarjetas LICENE (paneles azul marino, franja de acento, texto en negritas)
// hecha con uGUI clásico para que funcione en cualquier proyecto. Incluye el panel de cuenta LICENE (tecla L).
public static class LiceneUI
{
    public static readonly Color Fondo = new Color(0.06f, 0.09f, 0.18f, 0.86f);
    public static readonly Color Acento = new Color(0.2f, 0.75f, 1f);
    public static readonly Color Acento2 = new Color(1f, 0.62f, 0.2f);
    public static readonly Color Exito = new Color(0.45f, 1f, 0.6f);
    public static readonly Color Aviso = new Color(1f, 0.9f, 0.45f);

    static Font fuente;
    public static Font Fuente => fuente != null ? fuente : (fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

    public static Transform Lienzo(string nombre, Transform padre, int orden = 10)
    {
        var go = new GameObject(nombre, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (padre != null) go.transform.SetParent(padre, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = orden;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return go.transform;
    }

    public static RectTransform Rect(string nombre, Transform padre, Vector2 ancla, Vector2 pos, Vector2 tam)
    {
        var rt = new GameObject(nombre, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(padre, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = ancla;
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        return rt;
    }

    public static Image Panel(string nombre, Transform padre, Vector2 ancla, Vector2 pos, Vector2 tam, Color color)
    {
        var img = Rect(nombre, padre, ancla, pos, tam).gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    // Tarjeta: panel de fondo con franja de acento arriba.
    public static Image Tarjeta(string nombre, Transform padre, Vector2 ancla, Vector2 pos, Vector2 tam, Color? acento = null)
    {
        Image p = Panel(nombre, padre, ancla, pos, tam, Fondo);
        Panel("Acento", p.transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(tam.x, 5f), acento ?? Acento);
        return p;
    }

    public static Text Texto(Transform padre, string texto, int tam, Vector2 ancla, Vector2 pos, Vector2 caja,
        TextAnchor alineacion = TextAnchor.MiddleCenter, FontStyle estilo = FontStyle.Bold, Color? color = null)
    {
        var t = Rect("Texto", padre, ancla, pos, caja).gameObject.AddComponent<Text>();
        t.font = Fuente;
        t.text = texto;
        t.fontSize = tam;
        t.fontStyle = estilo;
        t.alignment = alineacion;
        t.color = color ?? Color.white;
        t.supportRichText = true;
        t.raycastTarget = false;
        var sombra = t.gameObject.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.5f);
        sombra.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    public static Button Boton(Transform padre, string texto, Vector2 ancla, Vector2 pos, Vector2 tam, Color color, Action accion)
    {
        Image img = Panel("Boton " + texto, padre, ancla, pos, tam, color);
        var b = img.gameObject.AddComponent<Button>();
        b.onClick.AddListener(() => accion());
        Texto(img.transform, texto, 30, new Vector2(0.5f, 0.5f), Vector2.zero, tam).name = "Etiqueta";
        return b;
    }

    public static InputField Campo(Transform padre, string sugerencia, Vector2 ancla, Vector2 pos, Vector2 tam, bool clave = false)
    {
        Image fondo = Panel("Campo " + sugerencia, padre, ancla, pos, tam, new Color(1f, 1f, 1f, 0.12f));
        var texto = Texto(fondo.transform, "", 28, new Vector2(0.5f, 0.5f), Vector2.zero, tam - new Vector2(40f, 10f), TextAnchor.MiddleLeft, FontStyle.Normal);
        var ph = Texto(fondo.transform, sugerencia, 28, new Vector2(0.5f, 0.5f), Vector2.zero, tam - new Vector2(40f, 10f), TextAnchor.MiddleLeft, FontStyle.Italic, new Color(1f, 1f, 1f, 0.4f));
        var campo = fondo.gameObject.AddComponent<InputField>();
        campo.textComponent = texto;
        campo.placeholder = ph;
        if (clave) { campo.contentType = InputField.ContentType.Password; campo.asteriskChar = '•'; }
        return campo;
    }

    public static bool TeclaPresionada(KeyCode tecla)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;
        switch (tecla)
        {
            case KeyCode.L: return kb.lKey.wasPressedThisFrame;
            case KeyCode.Tab: return kb.tabKey.wasPressedThisFrame;
            case KeyCode.R: return kb.rKey.wasPressedThisFrame;
            case KeyCode.Escape: return kb.escapeKey.wasPressedThisFrame;
            default: return false;
        }
#else
        return Input.GetKeyDown(tecla);
#endif
    }

    public static void AsegurarEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
#if ENABLE_INPUT_SYSTEM
        new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
#else
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
    }
}

// Panel de cuenta LICENE: se abre con L. Mientras está abierto avisa al juego (Abierto) para pausar controles.
public class LiceneCuentaPanel : MonoBehaviour
{
    public static LiceneCuentaPanel Instance { get; private set; }
    public static bool Abierto => Instance != null && Instance.panel != null && Instance.panel.activeSelf;
    public static event Action<bool> AlCambiar;

    GameObject panel;
    InputField correo, clave;
    Text estado, esquina;
    Button boton;

    public static LiceneCuentaPanel Crear(Transform lienzo)
    {
        var go = new GameObject("Cuenta LICENE");
        go.transform.SetParent(lienzo, false);
        return go.AddComponent<LiceneCuentaPanel>();
    }

    void Awake()
    {
        Instance = this;
        Transform c = transform.parent;
        esquina = LiceneUI.Texto(c, "", 22, new Vector2(1f, 1f), new Vector2(-30f, -62f), new Vector2(560f, 30f), TextAnchor.MiddleRight, FontStyle.Normal);

        Image p = LiceneUI.Tarjeta("Panel cuenta", c, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 420f), LiceneUI.Acento2);
        LiceneUI.Texto(p.transform, "Cuenta LICENE", 40, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(600f, 60f));
        LiceneUI.Texto(p.transform, "Tu progreso se guarda en tu app LICENE", 22, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(600f, 30f), TextAnchor.MiddleCenter, FontStyle.Normal, new Color(1f, 1f, 1f, 0.7f));
        correo = LiceneUI.Campo(p.transform, "Correo", new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(540f, 60f));
        clave = LiceneUI.Campo(p.transform, "Contraseña", new Vector2(0.5f, 1f), new Vector2(0f, -222f), new Vector2(540f, 60f), true);
        boton = LiceneUI.Boton(p.transform, "Iniciar sesión", new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(540f, 64f), new Color(1f, 0.55f, 0.15f), Entrar);
        estado = LiceneUI.Texto(p.transform, "", 22, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(600f, 30f), TextAnchor.MiddleCenter, FontStyle.Normal);
        LiceneUI.Texto(p.transform, "L para cerrar", 20, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(600f, 26f), TextAnchor.MiddleCenter, FontStyle.Normal, new Color(1f, 1f, 1f, 0.55f));
        panel = p.gameObject;
        panel.SetActive(false);
        LiceneCloud.Flush();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        bool escribiendo = Abierto && (correo.isFocused || clave.isFocused);
        if (LiceneUI.TeclaPresionada(KeyCode.L) && !escribiendo) Mostrar(!Abierto);
        if (Abierto) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        esquina.text = LiceneCloud.SignedIn ? "<color=#7CFF9A>●</color> " + LiceneCloud.Email : "<color=#FFFFFF88>L · cuenta LICENE</color>";
        boton.GetComponentInChildren<Text>().text = LiceneCloud.SignedIn ? "Cerrar sesión" : "Iniciar sesión";
    }

    public void Mostrar(bool abrir)
    {
        if (abrir) { LiceneUI.AsegurarEventSystem(); correo.text = LiceneCloud.Email; estado.text = LiceneCloud.StatusText; }
        panel.SetActive(abrir);
        AlCambiar?.Invoke(abrir);
    }

    void Entrar()
    {
        if (LiceneCloud.SignedIn) { LiceneCloud.SignOut(); estado.text = "Sesión cerrada"; return; }
        if (string.IsNullOrWhiteSpace(correo.text) || string.IsNullOrEmpty(clave.text)) { estado.text = "Escribe tu correo y contraseña"; return; }
        boton.interactable = false;
        estado.text = "Conectando...";
        LiceneCloud.SignIn(correo.text.Trim(), clave.text, (ok, mensaje) =>
        {
            boton.interactable = true;
            clave.text = "";
            estado.text = ok ? "<color=#7CFF9A>¡Listo! Sesión iniciada</color>" : "<color=#FF8A80>" + mensaje + "</color>";
            if (ok) Mostrar(false);
        });
    }
}
