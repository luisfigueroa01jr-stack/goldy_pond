using UnityEngine;

/// <summary>
/// Base de todo lo que el jugador puede usar con la tecla E (objetos recogibles, puertas...).
/// JugadorPrimeraPersona busca el Interactuable que tienes enfrente, muestra "(E) TextoAccion"
/// y al presionar E llama a Interactuar.
/// </summary>
public abstract class Interactuable : MonoBehaviour
{
    /// <summary>Texto que va despues de "(E)" en el aviso, ej. "Abrir puerta".</summary>
    public abstract string TextoAccion { get; }

    /// <summary>false = el jugador lo ignora (ej. un mecanismo que ya se uso).</summary>
    public virtual bool Disponible => true;

    /// <summary>Iconito que aparece a la izquierda del aviso (ej. un ojo para leer). null = sin icono.</summary>
    public virtual Sprite IconoAccion => null;

    /// <summary>Mensaje que se muestra un momento si Interactuar devuelve false.</summary>
    public virtual string MensajeFallo => "";

    /// <summary>Hace la accion. Devuelve false si no se pudo.</summary>
    public abstract bool Interactuar(Transform jugador);
}
