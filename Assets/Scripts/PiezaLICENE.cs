using Photon.Pun;
using UnityEngine;

// Pieza del reto cooperativo: una figura (cubo, esfera, cápsula o cilindro) de un color.
// El dueño de la red la simula con física; los demás solo la ven moverse suavemente.
// Quien la carga se vuelve su dueño (PhotonView con OwnershipTransfer = Takeover).
[RequireComponent(typeof(Rigidbody))]
public class PiezaLICENE : MonoBehaviourPun, IPunObservable, IPunInstantiateMagicCallback
{
    public static readonly string[] Formas = { "Cubo", "Esfera", "Cápsula", "Cilindro" };
    public static readonly string[] Colores = { "Rojo", "Azul", "Verde", "Amarillo" };
    public static readonly Color[] Tintes =
    {
        new Color(0.92f, 0.22f, 0.22f), new Color(0.22f, 0.48f, 0.98f),
        new Color(0.22f, 0.82f, 0.32f), new Color(1f, 0.84f, 0.15f),
    };
    static readonly string[] Mallas = { "Cube.fbx", "Sphere.fbx", "Capsule.fbx", "Cylinder.fbx" };

    public int forma, color;
    public bool cargada;          // Alguien la lleva en la mano (sincronizado).
    public int ultimoJugador = -1; // Último ActorNumber que la tocó: recibe el punto al entregarla.

    Rigidbody rb;
    Vector3 posRed;
    Quaternion rotRed = Quaternion.identity;
    float resaltado;

    public string Nombre => $"{FormaConGenero} {ColorConGenero}";
    // Tamaño de cada figura (la cápsula y el cilindro miden el doble de alto).
    float EscalaBase => forma == 0 ? 0.55f : forma == 1 ? 0.62f : 0.42f;
    string FormaConGenero => Formas[forma];
    // "Esfera roja", "Cubo rojo": el color concuerda con el género de la figura.
    string ColorConGenero
    {
        get
        {
            bool femenino = forma == 1 || forma == 2;
            string c = Colores[color].ToLowerInvariant();
            if (color == 3) return c; // amarillo: igual
            return femenino && c.EndsWith("o") ? c.Substring(0, c.Length - 1) + "a" : c;
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.maxDepenetrationVelocity = 1.5f;
        rb.angularDamping = 1.5f; // Que las esferas no rueden por toda la plaza.
        posRed = transform.position;
        rotRed = transform.rotation;
    }

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = info.photonView.InstantiationData;
        if (data != null && data.Length >= 2) Configurar((int)data[0], (int)data[1]);
    }

    public void Configurar(int nuevaForma, int nuevoColor)
    {
        forma = Mathf.Clamp(nuevaForma, 0, 3);
        color = Mathf.Clamp(nuevoColor, 0, 3);
        name = "Pieza " + Nombre;

        GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(Mallas[forma]);
        var mat = Resources.Load<Material>("PiezasMat/" + Colores[color]);
        if (mat != null) GetComponent<MeshRenderer>().sharedMaterial = mat;

        foreach (Collider c in GetComponents<Collider>()) Destroy(c);
        switch (forma)
        {
            case 0: gameObject.AddComponent<BoxCollider>(); break;
            case 1: gameObject.AddComponent<SphereCollider>(); break;
            default: gameObject.AddComponent<CapsuleCollider>(); break; // cápsula y cilindro
        }
        transform.localScale = Vector3.one * EscalaBase;
    }

    void Update()
    {
        bool mia = photonView.IsMine;
        // Solo el dueño simula física; si alguien la carga, la mueve él a mano.
        rb.isKinematic = !mia || cargada;
        // La interpolación del Rigidbody pisaría la posición que ponemos a mano (al cargarla o
        // al seguir la red), así que solo se usa mientras la física la mueve.
        rb.interpolation = rb.isKinematic ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;

        if (!mia)
        {
            transform.position = Vector3.Lerp(transform.position, posRed, Time.deltaTime * 15f);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotRed, Time.deltaTime * 15f);
        }

        // Pulso suave cuando el jugador local la está mirando.
        float escalaBase = EscalaBase * (cargada && photonView.IsMine ? 0.7f : 1f); // En la mano se ve más chica para no tapar la vista.
        resaltado = Mathf.MoveTowards(resaltado, 0f, Time.deltaTime * 4f);
        transform.localScale = Vector3.one * escalaBase * (1f + resaltado * 0.12f * (1f + Mathf.Sin(Time.time * 10f)) * 0.5f);

        // Si cae fuera del mundo, el dueño la regresa al centro.
        // Si cae fuera del mundo o la lanzan por encima del seto, el dueño la regresa a la mesa.
        Vector3 d = transform.position - RetoCooperativo.CentroMesa;
        if (mia && !cargada && (transform.position.y < -20f || d.x * d.x + d.z * d.z > 18f * 18f))
            Reaparecer(RetoCooperativo.CentroMesa + Vector3.up * 3f);
    }

    public void Resaltar() => resaltado = 1f;

    public void Reaparecer(Vector3 pos)
    {
        cargada = false;
        transform.position = pos;
        rb.position = pos;
        if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
    }

    public void Lanzar(Vector3 velocidad)
    {
        cargada = false;
        rb.isKinematic = false;
        rb.linearVelocity = velocidad;
        rb.angularVelocity = Random.insideUnitSphere * 4f;
    }

    // Rebote cuando la dejan en la zona equivocada (lo ejecuta el dueño).
    public void Rebotar(Vector3 desde)
    {
        if (!photonView.IsMine || cargada) return;
        Vector3 dir = (transform.position - desde).normalized;
        dir.y = 0f;
        rb.isKinematic = false;
        rb.linearVelocity = dir * 5f + Vector3.up * 5f;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(cargada);
            stream.SendNext(ultimoJugador);
        }
        else
        {
            posRed = (Vector3)stream.ReceiveNext();
            rotRed = (Quaternion)stream.ReceiveNext();
            cargada = (bool)stream.ReceiveNext();
            ultimoJugador = (int)stream.ReceiveNext();
            if ((posRed - transform.position).sqrMagnitude > 25f) transform.position = posRed; // Salto grande: teletransporte.
        }
    }
}
