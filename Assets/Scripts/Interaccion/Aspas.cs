using UnityEngine;

// Aspas del molino. Estan quietas hasta que se ponen los dos engranajes en el mecanismo;
// entonces empiezan a girar despacio y van agarrando velocidad.
// Si regresas del subterraneo con el mecanismo ya completo, ya estan girando.
// Va en el cubo de las aspas (Object.028); el eje y el centro los calcula Goldy Pond > Interaccion.
public class Aspas : MonoBehaviour
{
    [Tooltip("El mecanismo que las hace girar.")]
    public Mecanismo mecanismo;
    [Tooltip("Grados por segundo ya a toda velocidad.")]
    public float velocidad = 35f;
    [Tooltip("Segundos que tardan en llegar a toda velocidad.")]
    public float tiempoArranque = 4f;
    [Tooltip("Girar al reves.")]
    public bool invertir;
    [Tooltip("Eje de giro, en coordenadas de este objeto.")]
    public Vector3 ejeLocal = Vector3.forward;
    [Tooltip("Centro de giro, en coordenadas de este objeto.")]
    public Vector3 centroLocal;

    private bool girando;
    private float velocidadActual;

    private void OnEnable()
    {
        if (mecanismo != null)
        {
            mecanismo.AlCompletar += arrancar;
        }
    }

    private void OnDisable()
    {
        if (mecanismo != null)
        {
            mecanismo.AlCompletar -= arrancar;
        }
    }

    private void Start()
    {
        // Ya estaba completo (al volver del subterraneo): giran desde el principio.
        if (mecanismo != null && mecanismo.Completo)
        {
            girando = true;
            velocidadActual = velocidad;
        }
    }

    private void arrancar()
    {
        girando = true;
    }

    private void Update()
    {
        if (!girando)
        {
            return;
        }

        float aceleracion = tiempoArranque > 0f ? velocidad / tiempoArranque : float.MaxValue;
        velocidadActual = Mathf.MoveTowards(velocidadActual, velocidad, aceleracion * Time.deltaTime);
        Vector3 eje = transform.TransformDirection(ejeLocal);
        Vector3 centro = transform.TransformPoint(centroLocal);
        transform.RotateAround(centro, invertir ? -eje : eje, velocidadActual * Time.deltaTime);
    }
}
