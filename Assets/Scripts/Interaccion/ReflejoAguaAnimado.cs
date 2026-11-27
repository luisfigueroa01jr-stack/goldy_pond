using UnityEngine;

public class ReflejoAguaAnimado : MonoBehaviour
{
    [Header("Movimiento del Reflejo")]
    public float velocidadOnda = 1.5f;
    public float variacionIntensidad = 15f;
    public float desplazamiento = 0.4f;

    private Light[] luces;
    private float[] intensidadesBase;
    private Vector3[] posicionesBase;

    private void Start()
    {
        // Detecta automaticamente todas las luces hijas de este objeto
        luces = GetComponentsInChildren<Light>();
        intensidadesBase = new float[luces.Length];
        posicionesBase = new Vector3[luces.Length];

        for (int i = 0; i < luces.Length; i++)
        {
            intensidadesBase[i] = luces[i].intensity;
            posicionesBase[i] = luces[i].transform.localPosition;
        }
    }

    private void Update()
    {
        for (int i = 0; i < luces.Length; i++)
        {
            if (luces[i] == null) continue;
            float fase = Time.time * velocidadOnda + (i * 1.7f);

            // Sube y baja la intensidad suavemente como ondas de agua
            luces[i].intensity = intensidadesBase[i] + Mathf.Sin(fase) * variacionIntensidad;

            // Mueve ligeramente la luz de lado a lado para simular el vaiven del agua
            Vector3 offset = new Vector3(
                Mathf.Cos(fase * 0.8f) * desplazamiento,
                Mathf.Sin(fase * 1.2f) * (desplazamiento * 0.3f),
                Mathf.Sin(fase * 0.7f) * desplazamiento
            );
            luces[i].transform.localPosition = posicionesBase[i] + offset;
        }
    }
}
