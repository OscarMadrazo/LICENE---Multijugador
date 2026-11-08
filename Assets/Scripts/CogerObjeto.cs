using Photon.Pun;
using UnityEngine;

// Tomar, cargar, soltar y lanzar piezas en red.
// E: tomar / soltar · Clic izquierdo: lanzar. Mira la pieza (a menos de 3.5 m) para tomarla.
public class CogerObjeto : MonoBehaviourPun
{
    [Header("Referencia de la mano")]
    public Transform handPoint;

    public const float Alcance = 3.5f;

    Camera cam;
    CharacterController cuerpo;
    PiezaLICENE cargando, pendiente, mirando;
    float pendienteHasta;

    public PiezaLICENE Cargando => cargando;
    public PiezaLICENE Mirando => mirando;

    void Start()
    {
        cuerpo = GetComponentInParent<CharacterController>();
        var pc = GetComponentInParent<PlayerController>();
        if (pc != null && pc.camara != null) cam = pc.camara.GetComponent<Camera>();
        // La "mano" ya no usa física: se apagan su collider y Rigidbody para que no empujen nada.
        foreach (Collider c in GetComponents<Collider>()) c.enabled = false;
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
    }

    void Update()
    {
        if (!photonView.IsMine || cam == null) return;

        mirando = cargando == null ? BuscarPieza() : null;
        if (mirando != null) mirando.Resaltar();

        // Espera a que la red confirme que la pieza ya es nuestra.
        if (pendiente != null)
        {
            if (pendiente.photonView.IsMine) Agarrar(pendiente);
            else if (Time.time > pendienteHasta) pendiente = null;
        }

        if (cargando != null)
        {
            // Si otro jugador se la llevó, se suelta.
            if (!cargando.photonView.IsMine) { Liberar(); return; }

            Vector3 objetivo = cam.transform.position + cam.transform.forward * 2.1f - cam.transform.up * 0.7f + cam.transform.right * 0.7f;
            cargando.transform.position = Vector3.Lerp(cargando.transform.position, objetivo, Time.deltaTime * 18f);
            cargando.transform.rotation = Quaternion.Slerp(cargando.transform.rotation, cam.transform.rotation, Time.deltaTime * 10f);

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.R)) Soltar(Vector3.zero);
            else if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked) Soltar(cam.transform.forward * 9f + Vector3.up * 2f);
        }
        else if (mirando != null && Input.GetKeyDown(KeyCode.E))
        {
            IntentarTomar(mirando);
        }
    }

    PiezaLICENE BuscarPieza()
    {
        var ray = new Ray(cam.transform.position, cam.transform.forward);
        foreach (RaycastHit hit in Physics.SphereCastAll(ray, 0.3f, Alcance, ~0, QueryTriggerInteraction.Ignore))
        {
            var p = hit.collider.GetComponentInParent<PiezaLICENE>();
            if (p != null && !p.cargada) return p;
        }
        return null;
    }

    public void IntentarTomar(PiezaLICENE pieza)
    {
        if (pieza == null || pieza.cargada || cargando != null) return;
        if (pieza.photonView.IsMine) { Agarrar(pieza); return; }
        pieza.photonView.RequestOwnership();
        pendiente = pieza;
        pendienteHasta = Time.time + 1.5f;
    }

    void Agarrar(PiezaLICENE pieza)
    {
        pendiente = null;
        if (pieza.cargada) return;
        cargando = pieza;
        pieza.cargada = true;
        pieza.ultimoJugador = PhotonNetwork.LocalPlayer.ActorNumber;
        IgnorarChoque(pieza, true);
        if (RetoCooperativo.Instance != null) RetoCooperativo.Instance.Sonar(RetoCooperativo.Sonido.Tomar);
    }

    public void Soltar(Vector3 velocidad)
    {
        if (cargando == null) return;
        PiezaLICENE p = cargando;
        Liberar();
        Vector3 inercia = cuerpo != null ? cuerpo.velocity : Vector3.zero;
        p.Lanzar(velocidad + inercia * 0.8f);
    }

    void Liberar()
    {
        if (cargando != null)
        {
            cargando.cargada = false;
            IgnorarChoque(cargando, false);
        }
        cargando = null;
    }

    void IgnorarChoque(PiezaLICENE pieza, bool ignorar)
    {
        if (cuerpo == null || pieza == null) return;
        foreach (Collider c in pieza.GetComponents<Collider>()) Physics.IgnoreCollision(cuerpo, c, ignorar);
    }
}
