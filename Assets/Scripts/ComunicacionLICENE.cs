using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

// Comunicación sin escribir (ideal para cooperar y para alumnos que prefieren no hablar):
// - Teclas 1 a 4: reacciones rápidas que aparecen sobre tu personaje para todos.
// - Tecla G: marca el lugar que miras con una columna de luz de tu color (dura 5 s).
public class ComunicacionLICENE : MonoBehaviourPunCallbacks, IOnEventCallback
{
    const byte EvReaccion = 10, EvMarca = 11;
    public static readonly string[] Reacciones = { "¡Bien!", "¡Ayuda!", "¡Gracias!", "¡Por aquí!" };
    static readonly Color[] ColoresReaccion = { new Color(0.45f, 1f, 0.6f), new Color(1f, 0.55f, 0.45f), new Color(1f, 0.8f, 0.35f), new Color(0.45f, 0.8f, 1f) };
    float listoEn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        Crear();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => Crear();
    }

    static void Crear()
    {
        if (FindFirstObjectByType<RetoCooperativo>() != null && FindFirstObjectByType<ComunicacionLICENE>() == null)
            new GameObject("Comunicacion LICENE").AddComponent<ComunicacionLICENE>();
    }

    void Update()
    {
        if (!PhotonNetwork.InRoom || PlayerController.Local == null) return;
        if (LiceneMenuPausa.Pausado || LiceneCuentaPanel.Abierto || Time.time < listoEn) return;

        for (int i = 0; i < Reacciones.Length; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) { Enviar(EvReaccion, i, Vector3.zero); return; }

        if (Input.GetKeyDown(KeyCode.G))
        {
            Transform cam = PlayerController.Local.camara;
            if (cam != null && Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore))
                Enviar(EvMarca, 0, hit.point);
        }
    }

    void Enviar(byte codigo, int indice, Vector3 punto)
    {
        listoEn = Time.time + 0.8f; // evita llenar la pantalla de mensajes
        PhotonNetwork.RaiseEvent(codigo, new object[] { PhotonNetwork.LocalPlayer.ActorNumber, indice, punto },
            new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
    }

    public void OnEvent(EventData ev)
    {
        if (ev.Code != EvReaccion && ev.Code != EvMarca) return;
        var d = (object[])ev.CustomData;
        int actor = (int)d[0], indice = Mathf.Clamp((int)d[1], 0, Reacciones.Length - 1);
        Vector3 punto = (Vector3)d[2];
        Player quien = PhotonNetwork.CurrentRoom?.GetPlayer(actor);
        Color colorJugador = PlayerName.ColorDe(quien);

        if (ev.Code == EvReaccion)
        {
            Transform cuerpo = BuscarCuerpo(actor);
            if (cuerpo != null) Burbuja(cuerpo, Reacciones[indice], ColoresReaccion[indice]);
            if (RetoCooperativo.Instance != null)
            {
                RetoCooperativo.Instance.Sonar(RetoCooperativo.Sonido.Tomar);
                RetoCooperativo.Instance.Noticia($"<color=#{ColorUtility.ToHtmlStringRGB(colorJugador)}>{quien?.NickName ?? "Alguien"}</color>: {Reacciones[indice]}");
            }
        }
        else Marca(punto, colorJugador, quien?.NickName ?? "");
    }

    static Transform BuscarCuerpo(int actor)
    {
        foreach (PlayerController pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (pc.photonView.Owner != null && pc.photonView.Owner.ActorNumber == actor) return pc.transform;
        return null;
    }

    static void Burbuja(Transform cuerpo, string texto, Color color)
    {
        var go = new GameObject("Reaccion");
        go.transform.SetParent(cuerpo, false);
        go.transform.localPosition = Vector3.up * 2.1f;
        var t = go.AddComponent<TextMeshPro>();
        t.text = texto;
        t.fontSize = 4f;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.color = color;
        t.outlineWidth = 0.25f;
        t.outlineColor = new Color32(10, 20, 40, 255);
        go.AddComponent<Efimero>().Iniciar(2.5f, 0.6f);
    }

    static void Marca(Vector3 punto, Color color, string nombre)
    {
        var raiz = new GameObject("Marca de " + nombre);
        raiz.transform.position = punto;
        var columna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(columna.GetComponent<Collider>());
        columna.transform.SetParent(raiz.transform, false);
        columna.transform.localPosition = Vector3.up * 3f;
        columna.transform.localScale = new Vector3(0.25f, 3f, 0.25f);
        var mat = Resources.Load<Material>("PiezasMat/Particula");
        var r = columna.GetComponent<Renderer>();
        if (mat != null) { r.material = mat; r.material.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.6f)); }
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var etiqueta = new GameObject("Etiqueta").AddComponent<TextMeshPro>();
        etiqueta.transform.SetParent(raiz.transform, false);
        etiqueta.transform.localPosition = Vector3.up * 6.6f;
        etiqueta.text = "¡Aquí!";
        etiqueta.fontSize = 5f;
        etiqueta.alignment = TextAlignmentOptions.Center;
        etiqueta.fontStyle = FontStyles.Bold;
        etiqueta.color = color;
        etiqueta.gameObject.AddComponent<MiraCamara>();
        raiz.AddComponent<Efimero>().Iniciar(5f, 1f);
        if (RetoCooperativo.Instance != null) RetoCooperativo.Instance.Sonar(RetoCooperativo.Sonido.Tomar);
    }
}

// Aparece, flota un poco, mira a la cámara y desaparece sola.
public class Efimero : MonoBehaviour
{
    float vida, desvanecer, inicio;
    Vector3 escala;

    public void Iniciar(float segundos, float salida)
    {
        vida = segundos;
        desvanecer = salida;
        inicio = Time.time;
        escala = transform.localScale;
    }

    void LateUpdate()
    {
        float t = Time.time - inicio;
        if (t > vida) { Destroy(gameObject); return; }
        float k = Mathf.Min(1f, t * 6f) * Mathf.Clamp01((vida - t) / desvanecer);
        transform.localScale = escala * k;
        if (GetComponent<TextMeshPro>() != null)
        {
            transform.localPosition += Vector3.up * Time.deltaTime * 0.3f;
            Camera cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
