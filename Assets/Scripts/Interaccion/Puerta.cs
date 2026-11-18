using UnityEngine;

/// <summary>
/// Puerta que se abre y se cierra con la tecla E girando sobre su bisagra.
/// La bisagra es el origen (pivote) del objeto: en los modelos de Blender el origen
/// de Puerta_Principal y Puerta_Reja ya esta en el borde de la bisagra.
/// Gira con un movimiento suave (acelera al empezar y frena al terminar) y,
/// por defecto, se abre hacia el lado contrario a donde esta el jugador, como si la empujaras.
/// </summary>
public class Puerta : Interactuable
{
    [Tooltip("Como aparece en el aviso: (E) Abrir <nombre>.")]
    public string nombre = "puerta";
    [Tooltip("Cuantos grados gira al abrirse.")]
    [Range(10f, 170f)] public float anguloApertura = 100f;
    [Tooltip("Segundos que tarda en abrirse o cerrarse.")]
    [Min(0.05f)] public float duracion = 0.9f;

    [Header("Sentido")]
    [Tooltip("Se abre alejandose del jugador (como empujandola). Si lo apagas, siempre abre hacia el mismo lado.")]
    public bool alejarseDelJugador = true;
    [Tooltip("Solo si 'Alejarse Del Jugador' esta apagado: cambia el lado hacia el que abre.")]
    public bool invertirSentido;

    [Header("Extras")]
    [Tooltip("Otras hojas que se abren y cierran junto con esta (ej. la otra mitad de una reja doble).")]
    public Puerta[] companeras;
    public AudioClip sonidoAbrir;
    public AudioClip sonidoCerrar;

    public bool Abierta { get; private set; }

    Quaternion rotacionCerrada;
    Vector3 brazo;            // de la bisagra al centro de la puerta, en horizontal (mundo)
    float anguloActual, anguloDesde, anguloHasta;
    float progreso = 1f;

    public override string TextoAccion =>
        $"{(Abierta ? "Cerrar" : "Abrir")} <color=#FFDFA0>{nombre}</color>";

    void Awake()
    {
        rotacionCerrada = transform.rotation;

        // El centro de la puerta (sumando la manija) dice hacia donde queda el borde libre.
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            brazo = Vector3.ProjectOnPlane(b.center - transform.position, Vector3.up);
        }
        if (brazo.sqrMagnitude < 0.0001f) brazo = transform.right * 0.5f;
    }

    public override bool Interactuar(Transform jugador)
    {
        bool abrir = !Abierta;
        Vector3 desde = jugador != null ? jugador.position : transform.position;
        Fijar(abrir, desde);
        if (companeras != null)
            foreach (var c in companeras)
                if (c != null && c != this) c.Fijar(abrir, desde);
        return true;
    }

    /// <summary>Abre o cierra la puerta. "desde" es donde esta el jugador (para saber hacia donde empujar).</summary>
    public void Fijar(bool abrir, Vector3 desde)
    {
        if (abrir == Abierta) return;
        Abierta = abrir;

        float destino = 0f;
        if (abrir)
        {
            float sentido = invertirSentido ? -1f : 1f;
            if (alejarseDelJugador)
            {
                // Hacia donde se mueve el borde libre al girar en positivo alrededor de Y.
                Vector3 movimiento = Vector3.Cross(Vector3.up, brazo);
                Vector3 haciaJugador = Vector3.ProjectOnPlane(desde - (transform.position + brazo), Vector3.up);
                sentido = Vector3.Dot(movimiento, haciaJugador) > 0f ? -1f : 1f;
            }
            destino = sentido * anguloApertura;
        }

        anguloDesde = anguloActual;
        anguloHasta = destino;
        progreso = 0f;

        var clip = abrir ? sonidoAbrir : sonidoCerrar;
        if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position);
    }

    void Update()
    {
        if (progreso >= 1f) return;

        progreso = Mathf.Min(1f, progreso + Time.deltaTime / duracion);
        float t = progreso * progreso * (3f - 2f * progreso); // suave al empezar y al terminar
        anguloActual = Mathf.Lerp(anguloDesde, anguloHasta, t);
        transform.rotation = Quaternion.AngleAxis(anguloActual, Vector3.up) * rotacionCerrada;
    }

    // En la escena (objeto seleccionado) dibuja la bisagra en amarillo para ubicarla facil.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
        Gizmos.DrawWireSphere(transform.position, 0.05f);
    }
}
