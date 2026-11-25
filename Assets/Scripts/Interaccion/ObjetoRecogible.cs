using UnityEngine;

// Marca un objeto como recogible. El jugador (JugadorPrimeraPersona) muestra "(E) Recoger"
// al estar cerca y, al presionar E, lo guarda en la BarraHerramientas y lo oculta de la escena.
// Necesita un Collider en el mismo objeto o en sus hijos.
public class ObjetoRecogible : Interactuable
{
    [Tooltip("Nombre que se muestra y con el que se guarda en la barra (ej. Llave).")]
    public string nombre = "Objeto";
    [Tooltip("Imagen que aparece en la casilla de la barra.")]
    public Sprite icono;
    [Min(1)] public int cantidad = 1;
    [Tooltip("Si ya traes uno igual en la barra, no deja recoger otro hasta que lo uses (ej. el engranaje).")]
    public bool soloUnoALaVez;
    [Tooltip("Lo que sale si intentas recoger otro teniendo ya uno. Vacio = mensaje automatico.")]
    public string mensajeYaTienes = "";

    [Header("Sonido")]
    public AudioClip sonidoAlRecoger;
    [Range(0f, 1f)]
    public float volumenSonido = 0.8f;
    

    private string fallo = "La barra esta llena";

    public override string TextoAccion => $"Recoger <color=#FFDFA0>{nombre}</color>";
    public override string MensajeFallo => fallo;
    public override bool interactuar(Transform jugador)
    {
        return recogerObjeto();
    }

    // Lo guarda en la barra. Devuelve false si la barra esta llena o ya traes uno (soloUnoALaVez).
    public bool recogerObjeto()
    {
        BarraHerramientas barra = BarraHerramientas.Instancia;
        if (barra == null)
        {
            return false;
        }

        if (soloUnoALaVez && barra.tieneObjeto(nombre))
        {
            fallo = string.IsNullOrEmpty(mensajeYaTienes)
                ? $"Ya traes un {nombre.ToLower()}. Úsalo primero"
                : mensajeYaTienes;
            return false;
        }

        if (!barra.agregar(nombre, icono, cantidad))
        {
            fallo = "La barra esta llena";
            return false;
        }

        if (sonidoAlRecoger != null)
        {
            AudioSource.PlayClipAtPoint(sonidoAlRecoger, transform.position, volumenSonido * ConfiguracionSonido.VolumenJuego);
        }

        ObjetoEnMano.registrar(nombre, gameObject); // para verlo en la mano y poder ponerlo en otros lados
        EstadoJuego.marcarRecogido(gameObject);     // para que no vuelva a aparecer al regresar a la escena
        gameObject.SetActive(false);
        return true;
    }
}
