using System;
using UnityEngine;

/// <summary>
/// Los dos canales de sonido que se pueden ajustar en Opciones del menu principal.
///  - Juego: musica y ambiente.
///  - Animacion: sonidos de las animaciones (puertas, elevador, palancas, etc.).
/// </summary>
public enum CanalSonido { Juego, Animacion }

/// <summary>
/// Guarda el volumen de cada canal (de 0 a 1) con PlayerPrefs, asi se acuerda aunque cierres el juego.
/// Cualquier script puede leer ConfiguracionSonido.VolumenJuego o ConfiguracionSonido.VolumenAnimacion.
/// </summary>
public static class ConfiguracionSonido
{
    const string ClaveJuego = "GoldyPond_VolumenJuego";
    const string ClaveAnimacion = "GoldyPond_VolumenAnimacion";

    /// <summary>Avisa cuando cambia algun volumen (lo usa SonidoConVolumen para actualizarse al momento).</summary>
    public static event Action Cambio;

    public static float VolumenJuego
    {
        get => PlayerPrefs.GetFloat(ClaveJuego, 1f);
        set => Guardar(ClaveJuego, value);
    }

    public static float VolumenAnimacion
    {
        get => PlayerPrefs.GetFloat(ClaveAnimacion, 1f);
        set => Guardar(ClaveAnimacion, value);
    }

    /// <summary>Volumen del canal que le pidas.</summary>
    public static float Volumen(CanalSonido canal) =>
        canal == CanalSonido.Juego ? VolumenJuego : VolumenAnimacion;

    static void Guardar(string clave, float valor)
    {
        PlayerPrefs.SetFloat(clave, Mathf.Clamp01(valor));
        PlayerPrefs.Save();
        Cambio?.Invoke();
    }
}
