using Photon.Pun;
using TMPro;
using UnityEngine;

// Identidad visible del jugador: nombre flotante que mira a la cámara, color propio
// (según su número en la sala) y un visor para ver hacia dónde mira.
public class PlayerName : MonoBehaviourPunCallbacks
{
    public TMP_Text playerName;

    public static readonly Color[] ColoresJugador =
    {
        new Color(1f, 0.45f, 0.2f), new Color(0.3f, 0.7f, 1f), new Color(0.65f, 0.4f, 1f), new Color(0.2f, 0.85f, 0.65f),
        new Color(1f, 0.4f, 0.7f), new Color(0.95f, 0.8f, 0.2f), new Color(0.5f, 0.9f, 0.3f), new Color(0.9f, 0.9f, 0.95f),
    };

    public static Color ColorDe(Photon.Realtime.Player p) =>
        p == null ? Color.white : ColoresJugador[(Mathf.Max(1, p.ActorNumber) - 1) % ColoresJugador.Length];

    void Start()
    {
        Color color = ColorDe(photonView.Owner);
        var rend = GetComponent<MeshRenderer>();
        if (rend != null)
        {
            var bloque = new MaterialPropertyBlock();
            bloque.SetColor("_BaseColor", color);
            bloque.SetColor("_Color", color);
            rend.SetPropertyBlock(bloque);
        }
        CrearVisor();

        if (playerName != null)
        {
            playerName.text = photonView.Owner != null && !string.IsNullOrEmpty(photonView.Owner.NickName) ? photonView.Owner.NickName : "Jugador";
            playerName.color = color;
            playerName.alignment = TextAlignmentOptions.Center;
            playerName.fontStyle = FontStyles.Bold;
            // Tu propio nombre no estorba la vista.
            if (photonView.IsMine) playerName.gameObject.SetActive(false);
        }
    }

    void CrearVisor()
    {
        var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = "Visor";
        Destroy(visor.GetComponent<Collider>());
        visor.transform.SetParent(transform, false);
        visor.transform.localPosition = new Vector3(0f, 0.45f, 0.42f);
        visor.transform.localScale = new Vector3(0.75f, 0.28f, 0.2f);
        var mat = Resources.Load<Material>("PiezasMat/Visor");
        var r = visor.GetComponent<Renderer>();
        if (mat != null) r.sharedMaterial = mat;
        // El jugador local no ve su propio visor (está dentro de la cabeza), solo su sombra.
        if (photonView.IsMine) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
    }

    void LateUpdate()
    {
        if (playerName == null || !playerName.gameObject.activeSelf) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        playerName.transform.rotation = Quaternion.LookRotation(playerName.transform.position - cam.transform.position);
    }

    // Se mantiene por compatibilidad (el nombre ahora sale de NickName).
    [PunRPC]
    public void SetNameText(string name)
    {
        if (playerName != null) playerName.text = name;
    }
}
