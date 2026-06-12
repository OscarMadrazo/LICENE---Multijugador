// EnemyAI.cs
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("Referencias y rangos")]
    public Transform player;            // Referencia al jugador
    public float detectionRange = 10f;  // Distancia para detectar
    public float attackRange = 2f;      // Distancia para atacar
    public float attackCooldown = 1.5f; // Tiempo entre ataques
    public int damage = 10;
    public float rotacionSuave = 5f;    // Velocidad de rotación hacia el jugador

    private NavMeshAgent agent;
    private float lastAttackTime;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (player == null)
        {
            Debug.LogError("EnemyAI: Asigna un jugador en el inspector");
        }
    }

    void Update()
    {
        if (player == null) return;

        float distancia = Vector3.Distance(player.position, transform.position);

        if (distancia <= detectionRange)
        {
            // Perseguir al jugador
            agent.SetDestination(player.position);

            // Rotar hacia el jugador suavemente
            Vector3 direccion = (player.position - transform.position).normalized;
            direccion.y = 0f; // Solo girar en Y
            if (direccion != Vector3.zero)
            {
                Quaternion rotacionObjetivo = Quaternion.LookRotation(direccion);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotacionObjetivo, Time.deltaTime * rotacionSuave);
            }

            // Atacar si está en rango
            if (distancia <= attackRange)
            {
                Attack();
            }
        }
        else
        {
            // Detener movimiento si el jugador está fuera de rango
            agent.SetDestination(transform.position);
        }
    }

    void Attack()
    {
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            lastAttackTime = Time.time;
            Debug.Log("Enemigo ataca al jugador");

            // Llamar al PlayerController para quitar vida
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.TakeDamage(damage);
            }
        }
    }
}
