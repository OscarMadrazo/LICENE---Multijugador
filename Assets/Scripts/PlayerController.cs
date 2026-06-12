using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PhotonView))]
public class PlayerController : MonoBehaviourPun
{
    [Header("Movimiento")]
    public float velocidad = 5f;
    public float aceleracion = 8f;
    public float desaceleracion = 6f;
    public float gravedad = -9.81f;
    public float fuerzaSalto = 3f;
    public float controlAereo = 0.5f;

    [Header("Mouse")]
    public Transform camara;
    public float sensibilidadMouse = 100f;
    public float suavizadoCamara = 10f;

    [Header("Salud del jugador")]
    public int maxHealth = 100;
    private int currentHealth;

    private CharacterController controlador;
    private Vector3 velocidadJugador;
    private Vector3 velocidadDeseada;
    private Vector3 velocidadActual;
    private Vector2 rotacionActual;
    private Vector2 rotacionObjetivo;

    void Start()
    {
        controlador = GetComponent<CharacterController>();
        currentHealth = maxHealth;

        if (photonView.IsMine)
        {
            Cursor.lockState = CursorLockMode.Locked; // Bloquear cursor solo para jugador local
        }
        else
        {
            // Desactivar cámara y control para jugadores remotos
            camara.gameObject.SetActive(false);
            this.enabled = false; // Desactiva script de movimiento en jugadores remotos
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return; // Solo controla el jugador local

        // --- Cámara suavizada ---
        float mouseX = Input.GetAxis("Mouse X") * sensibilidadMouse * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * sensibilidadMouse * Time.deltaTime;

        rotacionObjetivo.x -= mouseY;
        rotacionObjetivo.x = Mathf.Clamp(rotacionObjetivo.x, -90f, 90f);
        rotacionObjetivo.y += mouseX;

        rotacionActual = Vector2.Lerp(rotacionActual, rotacionObjetivo, Time.deltaTime * suavizadoCamara);
        camara.localRotation = Quaternion.Euler(rotacionActual.x, 0f, 0f);
        transform.rotation = Quaternion.Euler(0f, rotacionActual.y, 0f);

        // --- Movimiento ---
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 direccion = transform.right * x + transform.forward * z;
        direccion.Normalize();

        float factorMovimiento = controlador.isGrounded ? 1f : controlAereo;
        velocidadDeseada = direccion * velocidad * factorMovimiento;

        velocidadActual = Vector3.Lerp(
            velocidadActual,
            velocidadDeseada,
            Time.deltaTime * (direccion.magnitude > 0 ? aceleracion : desaceleracion)
        );

        controlador.Move(velocidadActual * Time.deltaTime);

        // --- Salto y gravedad ---
        if (controlador.isGrounded && velocidadJugador.y < 0)
        {
            velocidadJugador.y = -2f;
        }

        if (Input.GetKeyDown(KeyCode.Space) && controlador.isGrounded)
        {
            velocidadJugador.y = Mathf.Sqrt(fuerzaSalto * -2f * gravedad);
        }

        velocidadJugador.y += gravedad * Time.deltaTime;
        controlador.Move(velocidadJugador * Time.deltaTime);
    }

    // --- Vida ---
    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        Debug.Log("Jugador recibe daño. Vida actual: " + currentHealth);

        if (currentHealth <= 0) Die();
    }

    void Die()
    {
        Debug.Log("Jugador ha muerto");
        // Respawn o game over aquí si deseas
    }
}
