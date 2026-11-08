using UnityEngine;

// Hace que un letrero siempre mire a la cámara activa y flote suavemente.
public class MiraCamara : MonoBehaviour
{
    Vector3 inicio;

    void Start() => inicio = transform.position;

    void LateUpdate()
    {
        Camera cam = Camera.main;
        transform.position = inicio + Vector3.up * Mathf.Sin(Time.time * 1.5f) * 0.15f;
        if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}
