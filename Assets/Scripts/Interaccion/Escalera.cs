using UnityEngine;

// Escalera de mano (la del subterraneo que sube a la trampilla del molino).
// - Abajo: camina hacia ella mirandola y te agarras sola. W sube, S baja.
// - Al llegar hasta abajo con S te sueltas; con Espacio te sueltas donde vayas.
// - Hasta arriba no se puede seguir subiendo (ahi aparece "(E) Subir a la Aldea", ver CambioEscena).
// Los puntos de abajo/arriba y el frente los calcula Goldy Pond > Interaccion con la forma del modelo.
public class Escalera : MonoBehaviour
{
    [Tooltip("Pie de la escalera, al centro entre los rieles (coordenadas del modelo).")]
    public Vector3 abajoLocal;
    [Tooltip("Punta de la escalera, al centro entre los rieles (coordenadas del modelo).")]
    public Vector3 arribaLocal;
    [Tooltip("Hacia donde se para el jugador para subirla (coordenadas del modelo).")]
    public Vector3 frenteLocal = Vector3.forward;
    [Tooltip("Que tan separado de los peldanos va el jugador, en metros.")]
    public float separacion = 0.4f;
    [Tooltip("Metros por segundo al subir o bajar.")]
    public float velocidad = 2f;
    [Tooltip("Que tan cerca del pie de la escalera hay que estar para agarrarse, en metros.")]
    public float distanciaAgarrar = 0.8f;

    public Vector3 Abajo => transform.TransformPoint(abajoLocal);
    public Vector3 Arriba => transform.TransformPoint(arribaLocal);
    public float Largo => Vector3.Distance(Abajo, Arriba);

    public Vector3 Frente
    {
        get
        {
            Vector3 f = Vector3.ProjectOnPlane(transform.TransformDirection(frenteLocal), Vector3.up);
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        }
    }

    // Punto sobre la escalera (0 = pie, 1 = punta), ya separado hacia el frente.
    public Vector3 punto(float t)
    {
        return Vector3.Lerp(Abajo, Arriba, Mathf.Clamp01(t)) + Frente * separacion;
    }

    private void Update()
    {
        JugadorPrimeraPersona j = JugadorPrimeraPersona.Instancia;
        if (j == null || j.Escalando || PanelLectura.Bloqueando)
        {
            return;
        }

        if (j.Entrada.y < 0.5f)
        {
            return; // tiene que ir caminando hacia adelante
        }

        // Cerca del pie, del lado del frente, a la misma altura y mirando hacia la escalera.
        Vector3 pie = punto(0f);
        Vector3 diferencia = j.transform.position - pie;
        if (Mathf.Abs(diferencia.y) > 0.6f)
        {
            return;
        }

        if (Vector3.ProjectOnPlane(diferencia, Vector3.up).magnitude > distanciaAgarrar)
        {
            return;
        }

        if (Vector3.Dot(j.transform.forward, -Frente) < 0.5f)
        {
            return;
        }

        j.empezarEscalar(this, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(punto(0f), punto(1f));
        Gizmos.DrawWireSphere(punto(0f), 0.15f);
        Gizmos.DrawWireSphere(punto(1f), 0.15f);
    }
}
