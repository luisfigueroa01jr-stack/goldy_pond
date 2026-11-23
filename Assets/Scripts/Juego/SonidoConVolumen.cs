using UnityEngine;

// Ponlo junto a cualquier AudioSource y elige su canal (Juego o Animacion).
// Su volumen se ajusta solo con los sliders de Opciones del menu principal.
// Ejemplo: musica de la Aldea -> canal Juego; sonido del elevador -> canal Animacion.
[RequireComponent(typeof(AudioSource))]
public class SonidoConVolumen : MonoBehaviour
{
    [Tooltip("Que slider de Opciones controla este sonido.")]
    public CanalSonido canal = CanalSonido.Juego;

    private AudioSource fuente;
    private float volumenOriginal;   // el volumen que le pusiste al AudioSource en el Inspector

    private void Awake()
    {
        fuente = GetComponent<AudioSource>();
        volumenOriginal = fuente.volume;
    }

    private void OnEnable()
    {
        ConfiguracionSonido.Cambio += aplicar;
        aplicar();
    }

    private void OnDisable()
    {
        ConfiguracionSonido.Cambio -= aplicar;
    }

    private void aplicar()
    {
        fuente.volume = volumenOriginal * ConfiguracionSonido.obtenerVolumen(canal);
    }
}
