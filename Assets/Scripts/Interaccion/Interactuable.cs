using UnityEngine;

// Base de todo lo que el jugador puede usar con la tecla E (objetos recogibles, puertas...).
// JugadorPrimeraPersona busca el Interactuable que tienes enfrente, muestra "(E) TextoAccion"
// y al presionar E llama a Interactuar.
public abstract class Interactuable : MonoBehaviour
{
    // Texto que va despues de "(E)" en el aviso, ej. "Abrir puerta".
    public abstract string TextoAccion { get; }

    // false = el jugador lo ignora (ej. un mecanismo que ya se uso).
    public virtual bool Disponible => true;

    // Iconito que aparece a la izquierda del aviso (ej. un ojo para leer). null = sin icono.
    public virtual Sprite IconoAccion => null;

    // Mensaje que se muestra un momento si Interactuar devuelve false.
    public virtual string MensajeFallo => "";

    // Hace la accion. Devuelve false si no se pudo.
    public abstract bool interactuar(Transform jugador);
}
