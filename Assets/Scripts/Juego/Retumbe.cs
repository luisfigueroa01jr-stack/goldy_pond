using System.Collections;
using UnityEngine;

/// <summary>
/// Temblores de la caceria infinita (se crea solo, no hay que ponerlo en la escena). Solo mueve la camara, sin sonido:
///  - Temblar: la camara del jugador se sacude (al abrirse el elevador, al caer el jefe).
///  - Golpe: un sacudon fuerte de un instante (el jefe cae al suelo).
///  - FuerzaContinua: temblor que se queda (durante el grito del jefe).
/// Funciona aunque el tiempo este congelado (Time.timeScale = 0).
/// </summary>
public class Retumbe : MonoBehaviour
{
    static Retumbe instancia;

    float fuerza;    // temblor continuo
    float golpe;     // sacudones que se apagan solos
    Transform camAplicada;
    Vector3 offsetPuesto;
    Quaternion rotPuesta = Quaternion.identity;

    static Retumbe Obtener()
    {
        if (instancia != null) return instancia;
        var go = new GameObject("Retumbe");
        instancia = go.AddComponent<Retumbe>();
        return instancia;
    }

    void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    /// <summary>Temblor completo: sube, se sostiene y se apaga. Usar con yield return / StartCoroutine.</summary>
    public static IEnumerator Temblar(float duracion, float fuerzaMaxima)
    {
        var r = Obtener();
        float t = 0f;
        while (t < duracion && r != null)
        {
            t += Time.unscaledDeltaTime;
            float sube = Mathf.Clamp01(t / 0.5f);
            float baja = Mathf.Clamp01((duracion - t) / 1.2f);
            r.fuerza = fuerzaMaxima * Mathf.Min(sube, baja) * (0.75f + 0.25f * Mathf.PerlinNoise(t * 2.5f, 4.2f));
            yield return null;
        }
        if (r != null) r.fuerza = 0f;
    }

    /// <summary>Sacudon de un instante.</summary>
    public static void Golpe(float f)
    {
        var r = Obtener();
        r.golpe = Mathf.Max(r.golpe, f);
    }

    /// <summary>Temblor que se queda hasta volverlo a 0 (lo usa el grito).</summary>
    public static void FuerzaContinua(float f) => Obtener().fuerza = f;

    void LateUpdate()
    {
        var j = JugadorPrimeraPersona.Instancia;
        var cam = j != null && j.camara != null ? j.camara.transform : null;

        // Quitar la sacudida del cuadro anterior (si otro script ya puso la camara, el error es de un cuadro y no se acumula).
        if (camAplicada != null && camAplicada == cam)
        {
            cam.localPosition -= offsetPuesto;
            cam.localRotation = cam.localRotation * Quaternion.Inverse(rotPuesta);
        }
        camAplicada = null;
        offsetPuesto = Vector3.zero;
        rotPuesta = Quaternion.identity;

        golpe = Mathf.MoveTowards(golpe, 0f, Time.unscaledDeltaTime * 1.8f);
        float f = fuerza + golpe;
        if (cam == null || f <= 0.001f) return;

        float t = Time.unscaledTime * 21f;
        offsetPuesto = new Vector3(Ruido(t, 0), Ruido(t, 1), 0f) * (0.045f * f);
        rotPuesta = Quaternion.Euler(Ruido(t, 2) * 1.5f * f, Ruido(t, 3) * 0.9f * f, Ruido(t, 4) * 2.2f * f);
        cam.localPosition += offsetPuesto;
        cam.localRotation = cam.localRotation * rotPuesta;
        camAplicada = cam;
    }

    static float Ruido(float t, int i) => Mathf.PerlinNoise(t, i * 13.7f + 0.5f) * 2f - 1f;
}
