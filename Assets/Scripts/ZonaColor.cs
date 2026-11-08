using UnityEngine;

// Plataforma de entrega de un color. El anillo brilla y late cuando recibe una pieza.
public class ZonaColor : MonoBehaviour
{
    public int color;
    public float radio = 2.6f;
    public Transform anillo;

    float pulso;
    Vector3 escalaAnillo = Vector3.one;

    void Start()
    {
        if (anillo != null) escalaAnillo = anillo.localScale;
    }

    public bool Contiene(Vector3 p)
    {
        Vector3 d = p - transform.position;
        return d.y > -0.5f && d.y < 2.5f && new Vector2(d.x, d.z).sqrMagnitude < radio * radio;
    }

    public void Celebrar() => pulso = 1f;

    void Update()
    {
        if (anillo == null) return;
        pulso = Mathf.MoveTowards(pulso, 0f, Time.deltaTime * 1.5f);
        float s = 1f + Mathf.Sin(Time.time * 2f + color) * 0.03f + pulso * 0.25f;
        anillo.localScale = new Vector3(escalaAnillo.x * s, escalaAnillo.y, escalaAnillo.z * s);
        anillo.Rotate(0f, 25f * Time.deltaTime * (1f + pulso * 6f), 0f, Space.World);
    }
}
