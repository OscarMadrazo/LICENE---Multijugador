using Photon.Pun;
using UnityEngine;

// Movimiento en primera persona del jugador local (a los remotos los mueve la red).
// WASD/flechas: mover · Mouse: mirar · Espacio: saltar · Shift: correr · Esc: liberar el cursor.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PhotonView))]
public class PlayerController : MonoBehaviourPun
{
    [Header("Movimiento")]
    public float velocidad = 5f;
    public float aceleracion = 10f;
    public float desaceleracion = 8f;
    public float gravedad = -18f;
    public float fuerzaSalto = 1.3f;
    public float controlAereo = 0.5f;

    [Header("Mouse")]
    public Transform camara;
    public float sensibilidadMouse = 100f;
    public float suavizadoCamara = 25f;

    [Header("Salud del jugador")]
    public int maxHealth = 100;
    private int currentHealth;

    public static PlayerController Local { get; private set; }

    private CharacterController controlador;
    private Vector3 velocidadJugador;
    private Vector3 velocidadActual;
    private Vector2 rotacionActual;
    private Vector2 rotacionObjetivo;
    private Vector3 puntoInicio;

    void Start()
    {
        controlador = GetComponent<CharacterController>();
        currentHealth = maxHealth;
        puntoInicio = transform.position;
        rotacionObjetivo.y = rotacionActual.y = transform.eulerAngles.y;

        if (photonView.IsMine)
        {
            Local = this;
            BloquearCursor(true);
        }
        else
        {
            // Jugadores remotos: sin cámara, sin control y sin colisionador propio (los mueve la red).
            if (camara != null) camara.gameObject.SetActive(false);
            controlador.enabled = false;
            enabled = false;
        }
    }

    void OnDestroy()
    {
        if (Local == this) Local = null;
    }

    public static void BloquearCursor(bool bloquear)
    {
        if (Application.isBatchMode) return;
        Cursor.lockState = bloquear ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !bloquear;
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        // Esc libera el cursor; un clic lo vuelve a capturar.
        // Con el menú o la cuenta abiertos no se mueve ni se mira (la red sigue funcionando).
        if (LiceneMenuPausa.Pausado || LiceneCuentaPanel.Abierto) return;
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) BloquearCursor(true);

        // --- Cámara (el eje del mouse ya es un desplazamiento: no se multiplica por deltaTime) ---
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            rotacionObjetivo.x -= Input.GetAxis("Mouse Y") * sensibilidadMouse * 0.02f * LiceneAjustes.Sensibilidad;
            rotacionObjetivo.y += Input.GetAxis("Mouse X") * sensibilidadMouse * 0.02f * LiceneAjustes.Sensibilidad;
            rotacionObjetivo.x = Mathf.Clamp(rotacionObjetivo.x, -85f, 85f);
        }
        float t = 1f - Mathf.Exp(-(LiceneAjustes.CamaraSuave ? suavizadoCamara * 0.45f : suavizadoCamara) * Time.deltaTime);
        rotacionActual = Vector2.Lerp(rotacionActual, rotacionObjetivo, t);
        if (camara != null) camara.localRotation = Quaternion.Euler(rotacionActual.x, 0f, 0f);
        transform.rotation = Quaternion.Euler(0f, rotacionActual.y, 0f);

        // --- Movimiento ---
        Vector3 direccion = transform.right * Input.GetAxisRaw("Horizontal") + transform.forward * Input.GetAxisRaw("Vertical");
        if (direccion.sqrMagnitude > 1f) direccion.Normalize();
        float rapidez = velocidad * (Input.GetKey(KeyCode.LeftShift) ? 1.6f : 1f);
        float factor = controlador.isGrounded ? 1f : controlAereo;
        float k = 1f - Mathf.Exp(-(direccion.sqrMagnitude > 0f ? aceleracion : desaceleracion) * factor * Time.deltaTime);
        velocidadActual = Vector3.Lerp(velocidadActual, direccion * rapidez, k);

        // --- Salto y gravedad ---
        if (controlador.isGrounded && velocidadJugador.y < 0f) velocidadJugador.y = -2f;
        if (Input.GetKeyDown(KeyCode.Space) && controlador.isGrounded) velocidadJugador.y = Mathf.Sqrt(fuerzaSalto * -2f * gravedad);
        velocidadJugador.y += gravedad * Time.deltaTime;

        controlador.Move((velocidadActual + velocidadJugador) * Time.deltaTime);

        // Si cae fuera del mapa, vuelve a su punto de inicio.
        if (transform.position.y < -15f) Reaparecer();
    }

    public void Reaparecer()
    {
        controlador.enabled = false;
        transform.position = puntoInicio;
        velocidadJugador = velocidadActual = Vector3.zero;
        controlador.enabled = true;
    }

    // --- Vida (la usa EnemyAI) ---
    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0) Die();
    }

    void Die()
    {
        currentHealth = maxHealth;
        Reaparecer();
    }
}
