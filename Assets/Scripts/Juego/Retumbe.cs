using System.Collections;
using UnityEngine;

// Temblores de la caceria infinita (se crea solo, no hay que ponerlo en la escena). Solo mueve la camara, sin sonido:
// - Temblar: la camara del jugador se sacude (al abrirse el elevador, al caer el jefe).
// - Golpe: un sacudon fuerte de un instante (el jefe cae al suelo).
// - FuerzaContinua: temblor que se queda (durante el grito del jefe).
// Funciona aunque el tiempo este congelado (Time.timeScale = 0).
public class Retumbe : MonoBehaviour
{
    private static Retumbe instancia;

    private float fuerza;    // temblor continuo
    private float golpe;     // sacudones que se apagan solos
    private Transform camAplicada;
    private Vector3 offsetPuesto;
    private Quaternion rotPuesta = Quaternion.identity;

    private static Retumbe obtener()
    {
        if (instancia != null)
        {
            return instancia;
        }

        GameObject go = new GameObject("Retumbe");
        instancia = go.AddComponent<Retumbe>();
        return instancia;
    }

    private void OnDestroy()
    {
        if (instancia == this)
        {
            instancia = null;
        }
    }

    // Temblor completo: sube, se sostiene y se apaga. Usar con yield return / StartCoroutine.
    public static IEnumerator temblar(float duracion, float fuerzaMaxima)
    {
        Retumbe r = obtener();
        float t = 0f;
        while (t < duracion && r != null)
        {
            t += Time.unscaledDeltaTime;
            float sube = Mathf.Clamp01(t / 0.5f);
            float baja = Mathf.Clamp01((duracion - t) / 1.2f);
            r.fuerza = fuerzaMaxima * Mathf.Min(sube, baja) * (0.75f + 0.25f * Mathf.PerlinNoise(t * 2.5f, 4.2f));
            yield return null;
        }

        if (r != null)
        {
            r.fuerza = 0f;
        }
    }

    // Sacudon de un instante.
    public static void darGolpe(float f)
    {
        Retumbe r = obtener();
        r.golpe = Mathf.Max(r.golpe, f);
    }

    // Temblor que se queda hasta volverlo a 0 (lo usa el grito).
    public static void fuerzaContinua(float f)
    {
        obtener().fuerza = f;
    }

    private void LateUpdate()
    {
        JugadorPrimeraPersona j = JugadorPrimeraPersona.Instancia;
        Transform cam = j != null && j.camara != null ? j.camara.transform : null;

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
        if (cam == null || f <= 0.001f)
        {
            return;
        }

        float t = Time.unscaledTime * 21f;
        offsetPuesto = new Vector3(ruido(t, 0), ruido(t, 1), 0f) * (0.045f * f);
        rotPuesta = Quaternion.Euler(ruido(t, 2) * 1.5f * f, ruido(t, 3) * 0.9f * f, ruido(t, 4) * 2.2f * f);
        cam.localPosition += offsetPuesto;
        cam.localRotation = cam.localRotation * rotPuesta;
        camAplicada = cam;
    }

    private static float ruido(float t, int i)
    {
        return Mathf.PerlinNoise(t, i * 13.7f + 0.5f) * 2f - 1f;
    }
}
