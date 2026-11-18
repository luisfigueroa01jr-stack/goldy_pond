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

    public override string TextoAccion => $"Recoger <color=#FFDFA0>{nombre}</color>";
    public override string MensajeFallo => "La barra esta llena";
    public override bool Interactuar(Transform jugador) => Recoger();

    /// <summary>Lo guarda en la barra. Devuelve false si la barra esta llena.</summary>
    public bool Recoger()
    {
        var barra = BarraHerramientas.Instancia;
        if (barra == null || !barra.Agregar(nombre, icono, cantidad))
            return false;
        ObjetoEnMano.Registrar(nombre, gameObject); // para verlo en la mano y poder ponerlo en otros lados
        gameObject.SetActive(false);
        return true;
    }
}
