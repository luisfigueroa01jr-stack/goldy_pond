using UnityEngine;

// Puerta que se abre y se cierra con la tecla E girando sobre su bisagra.
// La bisagra es el origen (pivote) del objeto: en los modelos de Blender el origen
// de Puerta_Principal y Puerta_Reja ya esta en el borde de la bisagra.
// Si el origen esta al centro (como la puerta del molino) activa "Calcular Bisagra"
// y la bisagra se pone sola en un borde de la puerta.
// Gira con un movimiento suave (acelera al empezar y frena al terminar) y,
// por defecto, se abre hacia el lado contrario a donde esta el jugador, como si la empujaras.
// Cerradura (opcional): si "Objeto Necesario" tiene algo (ej. Llave), la puerta esta cerrada.
// Al acercarte sin la llave en la mano sale un aviso abajo a la izquierda; con la llave
// seleccionada en la barra se abre sola al llegar (o con E) y ya se queda sin seguro.
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
    [Tooltip("Si pones algo aqui, siempre abre alejandose de este objeto (ej. el piso del molino = abre hacia afuera, sin chocar con la mesa).")]
    public Transform abrirLejosDe;

    [Header("Bisagra")]
    [Tooltip("Activalo si el origen de la puerta NO esta en la bisagra: la pone en el borde de la puerta.")]
    public bool calcularBisagra;
    [Tooltip("Solo con 'Calcular Bisagra': usa el otro borde.")]
    public bool bisagraDelOtroLado;

    [Header("Cerradura (vacio = sin seguro)")]
    [Tooltip("Nombre del objeto de la barra que la abre, ej. Llave.")]
    public string objetoNecesario = "";
    [Tooltip("Tiene que estar seleccionado en la barra (en la mano). Si lo apagas basta con tenerlo en la barra.")]
    public bool enLaMano = true;
    public string mensajeCerrada = "La puerta está cerrada. Necesitas una llave";
    [Tooltip("Con la llave en la mano, la puerta se abre sola al acercarte (sin presionar E).")]
    public bool abrirAlAcercarse = true;
    [Tooltip("A que distancia (metros) sale el aviso o se abre sola.")]
    public float distanciaCerradura = 2.2f;
    [Tooltip("Quita la llave de la barra al usarla.")]
    public bool gastarObjeto;

    [Header("Extras")]
    [Tooltip("Otras hojas que se abren y cierran junto con esta (ej. la otra mitad de una reja doble).")]
    public Puerta[] companeras;
    public AudioClip sonidoAbrir;
    public AudioClip sonidoCerrar;

    [Tooltip("La controla otro script (ej. el elevador): no se puede abrir ni cerrar con E y no sale aviso.")]
    public bool bloqueada;

    public bool Abierta { get; private set; }
    public override bool Disponible => !bloqueada;
    public bool ConSeguro => !string.IsNullOrEmpty(objetoNecesario) && !desbloqueada;

    private bool desbloqueada;
    private bool jugadorCerca;
    private Quaternion rotacionCerrada;
    private Vector3 posicionCerrada;
    private Vector3 bisagra;          // punto (mundo) sobre el que gira
    private Vector3 brazo;            // de la bisagra al centro de la puerta, en horizontal (mundo)
    private float anguloActual, anguloDesde, anguloHasta;
    private float progreso = 1f;

    public override string TextoAccion
    {
        get
        {
            if (ConSeguro && tieneLlave())
            {
                return $"Abrir con <color=#FFDFA0>{objetoNecesario}</color>";
            }

            return $"{(Abierta ? "Cerrar" : "Abrir")} <color=#FFDFA0>{nombre}</color>";
        }
    }

    private void Awake()
    {
        rotacionCerrada = transform.rotation;
        posicionCerrada = transform.position;
        bisagra = obtenerBisagraCalculada();

        // El centro de la puerta (sumando la manija) dice hacia donde queda el borde libre.
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                b.Encapsulate(r.bounds);
            }

            brazo = Vector3.ProjectOnPlane(b.center - bisagra, Vector3.up);
        }

        if (brazo.sqrMagnitude < 0.0001f)
        {
            brazo = transform.right * 0.5f;
        }
    }

    // El origen, o (con calcularBisagra) el borde de la puerta a lo ancho.
    private Vector3 obtenerBisagraCalculada()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (!calcularBisagra || mf == null || mf.sharedMesh == null)
        {
            return transform.position;
        }

        Bounds mb = mf.sharedMesh.bounds;
        int ancho = -1;
        float mejor = 0f;
        for (int i = 0; i < 3; i++)
        {
            Vector3 eje = Vector3.zero;
            eje[i] = mb.size[i];
            Vector3 enMundo = transform.TransformVector(eje);
            float horizontal = Vector3.ProjectOnPlane(enMundo, Vector3.up).magnitude;
            float vertical = Mathf.Abs(enMundo.y);
            if (vertical < horizontal * 0.5f && horizontal > mejor)
            {
                mejor = horizontal;
                ancho = i;
            }
        }

        if (ancho < 0)
        {
            return transform.position;
        }

        Vector3 local = mb.center;
        local[ancho] = bisagraDelOtroLado ? mb.max[ancho] : mb.min[ancho];
        return transform.TransformPoint(local);
    }

    public override bool interactuar(Transform jugador)
    {
        Vector3 desde = jugador != null ? jugador.position : transform.position;

        if (ConSeguro)
        {
            if (!tieneLlave())
            {
                Notificacion.mostrar(mensajeCerrada);
                return true;
            }

            desbloquear();
        }

        bool abrir = !Abierta;
        fijar(abrir, desde);
        if (companeras != null)
        {
            foreach (Puerta c in companeras)
            {
                if (c != null && c != this)
                {
                    c.fijar(abrir, desde);
                }
            }
        }

        return true;
    }

    private bool tieneLlave()
    {
        BarraHerramientas barra = BarraHerramientas.Instancia;
        if (barra == null || string.IsNullOrEmpty(objetoNecesario))
        {
            return false;
        }

        return enLaMano ? barra.ObjetoSeleccionado == objetoNecesario : barra.tieneObjeto(objetoNecesario);
    }

    // true si esta puerta tiene cerradura (aunque ya se haya abierto con la llave).
    public bool ConCerradura => !string.IsNullOrEmpty(objetoNecesario);

    // La deja sin seguro sin gastar la llave (al volver a la escena, si ya la habias abierto).
    public void quitarSeguro()
    {
        desbloqueada = true;
        foreach (Puerta c in companeras ?? new Puerta[0])
        {
            if (c != null)
            {
                c.desbloqueada = true;
            }
        }
    }

    private void desbloquear()
    {
        desbloqueada = true;
        if (gastarObjeto)
        {
            BarraHerramientas.Instancia?.quitar(objetoNecesario);
        }

        foreach (Puerta c in companeras ?? new Puerta[0])
        {
            if (c != null)
            {
                c.desbloqueada = true;
            }
        }
    }

    // Abre o cierra la puerta. "desde" es donde esta el jugador (para saber hacia donde empujar).
    public void fijar(bool abrir, Vector3 desde)
    {
        if (abrir == Abierta)
        {
            return;
        }

        Abierta = abrir;

        float destino = 0f;
        if (abrir)
        {
            float sentido = invertirSentido ? -1f : 1f;
            if (abrirLejosDe != null)
            {
                desde = abrirLejosDe.position;
            }

            if (alejarseDelJugador || abrirLejosDe != null)
            {
                // Hacia donde se mueve el borde libre al girar en positivo alrededor de Y.
                Vector3 movimiento = Vector3.Cross(Vector3.up, brazo);
                Vector3 haciaJugador = Vector3.ProjectOnPlane(desde - (bisagra + brazo), Vector3.up);
                sentido = Vector3.Dot(movimiento, haciaJugador) > 0f ? -1f : 1f;
            }

            destino = sentido * anguloApertura;
        }

        anguloDesde = anguloActual;
        anguloHasta = destino;
        progreso = 0f;

        AudioClip clip = abrir ? sonidoAbrir : sonidoCerrar;
        // El volumen sale del slider "Sonido de animacion" del menu de Opciones.
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, ConfiguracionSonido.VolumenAnimacion);
        }
    }

    // Abre o cierra sin animacion (ej. el elevador que ya esta abierto al llegar). Tambien las companeras.
    public void fijarAlInstante(bool abrir, Vector3 desde)
    {
        fijar(abrir, desde);
        progreso = 1f;
        anguloActual = anguloHasta;
        Quaternion giro = Quaternion.AngleAxis(anguloActual, Vector3.up);
        transform.SetPositionAndRotation(bisagra + giro * (posicionCerrada - bisagra), giro * rotacionCerrada);
    }

    // Esta girando (abriendose o cerrandose).
    public bool Moviendose => progreso < 1f;

    private void Update()
    {
        if (ConSeguro && !bloqueada)
        {
            revisarCerradura();
        }

        if (progreso >= 1f)
        {
            return;
        }

        progreso = Mathf.Min(1f, progreso + Time.deltaTime / duracion);
        float t = progreso * progreso * (3f - 2f * progreso); // suave al empezar y al terminar
        anguloActual = Mathf.Lerp(anguloDesde, anguloHasta, t);
        Quaternion giro = Quaternion.AngleAxis(anguloActual, Vector3.up);
        transform.SetPositionAndRotation(bisagra + giro * (posicionCerrada - bisagra), giro * rotacionCerrada);
    }

    // Al llegar a la puerta: con la llave en la mano se abre sola; sin ella sale el aviso.
    private void revisarCerradura()
    {
        JugadorPrimeraPersona jugador = JugadorPrimeraPersona.Instancia;
        if (jugador == null)
        {
            return;
        }

        Vector3 centro = bisagra + brazo;
        Vector3 diferencia = jugador.transform.position - centro;
        bool cerca = Vector3.ProjectOnPlane(diferencia, Vector3.up).magnitude < distanciaCerradura
                     && Mathf.Abs(diferencia.y) < 3f;

        if (cerca && tieneLlave() && abrirAlAcercarse)
        {
            interactuar(jugador.transform);
        }
        else if (cerca && !jugadorCerca && !tieneLlave())
        {
            Notificacion.mostrar(mensajeCerrada);
        }

        jugadorCerca = cerca;
    }

    // En la escena (objeto seleccionado) dibuja la bisagra en amarillo para ubicarla facil.
    private void OnDrawGizmosSelected()
    {
        Vector3 p = Application.isPlaying ? bisagra : obtenerBisagraCalculada();
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(p, p + Vector3.up * 2f);
        Gizmos.DrawWireSphere(p, 0.05f);
        if (!string.IsNullOrEmpty(objetoNecesario))
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, distanciaCerradura);
        }
    }
}
