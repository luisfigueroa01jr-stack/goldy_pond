using UnityEngine;

// Donde aparece el jugador al llegar desde otra escena (lo pide CambioEscena con el mismo "id").
// - Con "Escalera": apareces agarrado hasta arriba de la escalera, mirandola; luego bajas con S.
// - Sin escalera: apareces parado aqui, mirando hacia el eje Z (azul) de este objeto.
public class PuntoLlegada : MonoBehaviour
{
    public string id = "Escalera";
    [Tooltip("Si tiene escalera, apareces agarrado hasta arriba de ella.")]
    public Escalera escalera;

    private void Start()
    {
        if (EstadoJuego.Llegada != id)
        {
            return;
        }

        EstadoJuego.Llegada = null;

        JugadorPrimeraPersona j = JugadorPrimeraPersona.Instancia;
        if (j == null)
        {
            return;
        }

        if (escalera != null)
        {
            j.empezarEscalar(escalera, 1f);
        }
        else
        {
            j.teletransportar(transform.position, Vector3.ProjectOnPlane(transform.forward, Vector3.up));
        }
    }

    private void OnDrawGizmos()
    {
        if (escalera != null)
        {
            return;
        }

        Gizmos.color = new Color(0.3f, 0.9f, 1f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.6f, 0.3f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.6f, transform.position + Vector3.up * 0.6f + transform.forward * 0.8f);
    }
}
