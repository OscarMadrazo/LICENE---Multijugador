using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Ayudantes para construir la interfaz con código (menú y HUD) con el estilo LICENE.
public static class UIKit
{
    public static readonly Color Fondo = new Color(0.06f, 0.09f, 0.18f, 0.88f);
    public static readonly Color Acento = new Color(0.2f, 0.75f, 1f);
    public static readonly Color Acento2 = new Color(1f, 0.62f, 0.2f);
    public static readonly Color ColorCampo = new Color(1f, 1f, 1f, 0.1f);

    public static Canvas Canvas(string nombre, int orden = 0)
    {
        var go = new GameObject(nombre, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = orden;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static RectTransform Rect(string nombre, Transform padre, Vector2 ancla, Vector2 pos, Vector2 tam)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
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

    public static Image Estirar(Image img)
    {
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return img;
    }

    public static TextMeshProUGUI Texto(Transform padre, string texto, float tam, Vector2 ancla, Vector2 pos, Vector2 caja,
        TextAlignmentOptions alineacion = TextAlignmentOptions.Center, Color? color = null, FontStyles estilo = FontStyles.Normal)
    {
        var t = Rect("Texto", padre, ancla, pos, caja).gameObject.AddComponent<TextMeshProUGUI>();
        t.text = texto;
        t.fontSize = tam;
        t.alignment = alineacion;
        t.color = color ?? Color.white;
        t.fontStyle = estilo;
        t.raycastTarget = false;
        t.richText = true;
        return t;
    }

    public static Button Boton(Transform padre, string texto, Vector2 ancla, Vector2 pos, Vector2 tam, Color color, UnityAction accion)
    {
        Image img = Panel("Boton " + texto, padre, ancla, pos, tam, color);
        var b = img.gameObject.AddComponent<Button>();
        var colores = b.colors;
        colores.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colores.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colores.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        b.colors = colores;
        b.onClick.AddListener(accion);
        var t = Texto(img.transform, texto, 34f, new Vector2(0.5f, 0.5f), Vector2.zero, tam, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        t.name = "Etiqueta";
        return b;
    }

    public static TMP_InputField Campo(Transform padre, string sugerencia, Vector2 ancla, Vector2 pos, Vector2 tam, bool clave = false)
    {
        Image fondo = Panel("Campo " + sugerencia, padre, ancla, pos, tam, ColorCampo);
        var area = Rect("Area", fondo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, tam - new Vector2(40f, 10f));
        area.gameObject.AddComponent<RectMask2D>();

        var placeholder = Texto(area, sugerencia, 30f, new Vector2(0.5f, 0.5f), Vector2.zero, area.sizeDelta, TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.45f), FontStyles.Italic);
        var texto = Texto(area, "", 30f, new Vector2(0.5f, 0.5f), Vector2.zero, area.sizeDelta, TextAlignmentOptions.MidlineLeft);

        var campo = fondo.gameObject.AddComponent<TMP_InputField>();
        campo.textViewport = area;
        campo.textComponent = texto;
        campo.placeholder = placeholder;
        campo.caretColor = Acento;
        campo.customCaretColor = true;
        campo.selectionColor = new Color(0.2f, 0.75f, 1f, 0.35f);
        if (clave) { campo.contentType = TMP_InputField.ContentType.Password; campo.asteriskChar = '•'; }
        return campo;
    }
}
