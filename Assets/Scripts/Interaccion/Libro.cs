using UnityEngine;

// Libro (o cartel) que se lee con E. Al acercarte sale el aviso con un ojito: "(E) Leer libro".
// Al presionar E se abre la imagen en grande (PanelLectura) y se cierra con la X, E o Esc.
// Necesita un Collider en el mismo objeto o en sus hijos (los del modelo de Blender sirven).
public class Libro : Interactuable
{
    [Tooltip("Como aparece en el aviso: (E) Leer <nombre>.")]
    public string nombre = "libro";
    [Tooltip("La imagen que se ve al leerlo (ej. Assets/Imagenes/Reglas del juego en rojo).")]
    public Sprite imagen;
    [Tooltip("Mostrar solo la imagen: sin marco dorado, sin fondo oscuro y sin boton X (se cierra con E o Esc).")]
    public bool soloImagen;

    [Header("Sonido")]
    public AudioClip sonidoAlAbrir;
    [Range(0f, 1f)]
    public float volumenSonido = 0.8f;

    private static Sprite ojo;

    // El ultimo libro que se abrio (lo usa la Caceria: al cerrar el libro de la plaza empieza).
    public static Libro UltimoLeido { get; set; }

    public override string TextoAccion => string.IsNullOrEmpty(nombre) ? "Leer" : $"Leer <color=#FFDFA0>{nombre}</color>";
    public override Sprite IconoAccion => ojo != null ? ojo : (ojo = Resources.Load<Sprite>("UI/Ojo"));

    public override bool interactuar(Transform jugador)
    {
        if (sonidoAlAbrir != null)
        {
            Vector3 posicion = jugador != null ? jugador.position : transform.position;
            AudioSource.PlayClipAtPoint(sonidoAlAbrir, posicion, volumenSonido * ConfiguracionSonido.VolumenJuego);
        }

        UltimoLeido = this;
        PanelLectura.abrirPanel(imagen, false, soloImagen);
        return true;
    }
}
