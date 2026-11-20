using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// "(E) Entrar al subterraneo" / "(E) Subir a la Aldea": guarda lo que llevas (EstadoJuego),
/// carga la otra escena y te pone en su PuntoLlegada.
/// - Con "Trampilla": solo aparece cuando la trampilla ya esta abierta.
/// - Con "Escalera": solo aparece cuando vas agarrado a esa escalera, hasta arriba.
/// </summary>
public class CambioEscena : Interactuable
{
    [Tooltip("Lo que sale despues de (E).")]
    public string texto = "Entrar al subterráneo";
    [Tooltip("Nombre de la escena a la que te manda (tiene que estar en File > Build Profiles).")]
    public string escenaDestino = "Subterraneo";
    [Tooltip("Id del PuntoLlegada de la otra escena donde apareces.")]
    public string llegada = "Escalera";
    [Tooltip("Si esta puesto NO cambia de escena: te lleva a este punto de la misma escena (ej. el laberinto a un lado de la Aldea).")]
    public Transform destinoEnEscena;
    [Tooltip("Barra que dejas y barra que recuperas (ej. Aldea -> Laberinto). Vacio = la barra viaja completa.")]
    public string inventarioDesde, inventarioHacia;

    [Header("Cuando aparece")]
    [Tooltip("Solo si esta trampilla ya esta abierta.")]
    public Trampilla trampilla;
    [Tooltip("Solo si vas agarrado a esta escalera, hasta arriba.")]
    public Escalera escalera;
    [Range(0.5f, 1f)] public float alturaMinima = 0.92f;

    bool cargando;

    public override string TextoAccion => texto;

    public override bool Disponible
    {
        get
        {
            if (cargando) return false;
            if (trampilla != null && !trampilla.Abierta) return false;
            if (escalera != null)
            {
                var j = JugadorPrimeraPersona.Instancia;
                if (j == null || j.EscaleraActual != escalera || j.AlturaEnEscalera < alturaMinima) return false;
            }
            return true;
        }
    }

    public override bool Interactuar(Transform jugador)
    {
        if (!Disponible) return false;
        if (!string.IsNullOrEmpty(inventarioDesde) && !string.IsNullOrEmpty(inventarioHacia))
            Inventarios.Cambiar(inventarioDesde, inventarioHacia);
        if (destinoEnEscena != null)
        {
            var j = JugadorPrimeraPersona.Instancia;
            if (j == null) return false;
            j.Teletransportar(destinoEnEscena.position, Vector3.ProjectOnPlane(destinoEnEscena.forward, Vector3.up));
            return true;
        }
        cargando = true;
        EstadoJuego.Guardar();
        EstadoJuego.Llegada = llegada;
        SceneManager.LoadScene(escenaDestino);
        return true;
    }
}
