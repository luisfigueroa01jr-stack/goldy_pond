using UnityEngine;

/// <summary>
/// Marca un objeto como recogible. El jugador (JugadorPrimeraPersona) muestra "(E) Recoger"
/// al estar cerca y, al presionar E, lo guarda en la BarraHerramientas y lo oculta de la escena.
/// Necesita un Collider en el mismo objeto o en sus hijos.
/// </summary>
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

    string fallo = "La barra esta llena";

    public override string TextoAccion => $"Recoger <color=#FFDFA0>{nombre}</color>";
    public override string MensajeFallo => fallo;
    public override bool Interactuar(Transform jugador) => Recoger();

    /// <summary>Lo guarda en la barra. Devuelve false si la barra esta llena o ya traes uno (soloUnoALaVez).</summary>
    public bool Recoger()
    {
        var barra = BarraHerramientas.Instancia;
        if (barra == null) return false;
        if (soloUnoALaVez && barra.Tiene(nombre))
        {
            fallo = string.IsNullOrEmpty(mensajeYaTienes)
                ? $"Ya traes un {nombre.ToLower()}. Úsalo primero"
                : mensajeYaTienes;
            return false;
        }
        if (!barra.Agregar(nombre, icono, cantidad))
        {
            fallo = "La barra esta llena";
            return false;
        }
        ObjetoEnMano.Registrar(nombre, gameObject); // para verlo en la mano y poder ponerlo en otros lados
        EstadoJuego.MarcarRecogido(gameObject);     // para que no vuelva a aparecer al regresar a la escena
        gameObject.SetActive(false);
        return true;
    }
}
