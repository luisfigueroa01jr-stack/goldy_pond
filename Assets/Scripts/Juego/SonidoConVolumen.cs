using UnityEngine;

/// <summary>
/// Ponlo junto a cualquier AudioSource y elige su canal (Juego o Animacion).
/// Su volumen se ajusta solo con los sliders de Opciones del menu principal.
/// Ejemplo: musica de la Aldea -> canal Juego; sonido del elevador -> canal Animacion.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SonidoConVolumen : MonoBehaviour
{
    [Tooltip("Que slider de Opciones controla este sonido.")]
    public CanalSonido canal = CanalSonido.Juego;

    AudioSource fuente;
    float volumenOriginal;   // el volumen que le pusiste al AudioSource en el Inspector

    void Awake()
    {
        fuente = GetComponent<AudioSource>();
        volumenOriginal = fuente.volume;
    }

    void OnEnable()
    {
        ConfiguracionSonido.Cambio += Aplicar;
        Aplicar();
    }

    void OnDisable() => ConfiguracionSonido.Cambio -= Aplicar;

    void Aplicar() => fuente.volume = volumenOriginal * ConfiguracionSonido.Volumen(canal);
}
