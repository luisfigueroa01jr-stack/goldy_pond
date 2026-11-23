using System;
using UnityEngine;

// Los dos canales de sonido que se pueden ajustar en Opciones del menu principal.
// - Juego: musica y ambiente.
// - Animacion: sonidos de las animaciones (puertas, elevador, palancas, etc.).
public enum CanalSonido { Juego, Animacion }

// Guarda el volumen de cada canal (de 0 a 1) con PlayerPrefs, asi se acuerda aunque cierres el juego.
// Cualquier script puede leer ConfiguracionSonido.VolumenJuego o ConfiguracionSonido.VolumenAnimacion.
public static class ConfiguracionSonido
{
    private const string CLAVE_JUEGO = "GoldyPond_VolumenJuego";
    private const string CLAVE_ANIMACION = "GoldyPond_VolumenAnimacion";

    // Avisa cuando cambia algun volumen (lo usa SonidoConVolumen para actualizarse al momento).
    public static event Action Cambio;

    public static float VolumenJuego
    {
        get => PlayerPrefs.GetFloat(CLAVE_JUEGO, 1f);
        set => guardar(CLAVE_JUEGO, value);
    }

    public static float VolumenAnimacion
    {
        get => PlayerPrefs.GetFloat(CLAVE_ANIMACION, 1f);
        set => guardar(CLAVE_ANIMACION, value);
    }

    // Volumen del canal que le pidas.
    public static float obtenerVolumen(CanalSonido canal)
    {
        return canal == CanalSonido.Juego ? VolumenJuego : VolumenAnimacion;
    }

    private static void guardar(string clave, float valor)
    {
        PlayerPrefs.SetFloat(clave, Mathf.Clamp01(valor));
        PlayerPrefs.Save();
        Cambio?.Invoke();
    }
}
