using System.Collections.Generic;
using UnityEngine;

// Muestra frente a la camara (abajo a la derecha) el objeto que tienes seleccionado en la barra,
// como si el personaje lo trajera en la mano (sin mano). Si la casilla esta vacia no se ve nada.
// Al cambiar de casilla el objeto baja, se cambia y vuelve a subir. Se mueve un poquito al caminar.
// Va en la camara del jugador (JugadorPrimeraPersona lo agrega solo).
// Los modelos salen de los objetos que recogiste (ObjetoRecogible los registra al recogerlos).
// Va muy cerca de la camara y chiquito, asi nunca se mete dentro de las paredes.
public class ObjetoEnMano : MonoBehaviour
{
    [Tooltip("Donde va respecto a la camara (x derecha, y arriba, z al frente), en metros.")]
    public Vector3 posicion = new Vector3(0.095f, -0.08f, 0.17f);
    [Tooltip("Tamano del objeto (su lado mas largo), en metros. Esta muy cerca de la camara, por eso es chiquito.")]
    public float tamano = 0.075f;
    [Tooltip("Que tanto se mueve al caminar.")]
    public float balanceo = 0.004f;

    // Giro de cada objeto para que se vea bien "agarrado".
    private static readonly Dictionary<string, Vector3> Giros = new Dictionary<string, Vector3>
    {
        { "Llave", new Vector3(10f, -30f, -40f) },
        { "Engranaje", new Vector3(10f, -60f, 0f) },
        { "Linterna", new Vector3(5f, -20f, 0f) },
        { "Pergamino", new Vector3(10f, -30f, 70f) },
    };

    private static readonly Dictionary<string, GameObject> originales = new Dictionary<string, GameObject>();

    // Guarda el objeto de la escena para poder hacer copias (en la mano, en el mecanismo...).
    public static void registrar(string nombre, GameObject original)
    {
        if (!string.IsNullOrEmpty(nombre) && original != null)
        {
            originales[nombre] = original;
        }
    }

    // Antes de cambiar de escena: guarda una copia escondida de cada modelo registrado que no se borre
    // al cargar la otra escena, para seguir viendolo en la mano y poder ponerlo (ej. el engranaje).
    public static void conservarModelos()
    {
        foreach (string nombre in new List<string>(originales.Keys))
        {
            GameObject original = originales[nombre];
            if (original == null || original.scene.name == "DontDestroyOnLoad")
            {
                continue;
            }

            GameObject copia = crearCopia(nombre); // ya sale apagada y sin scripts
            if (copia == null)
            {
                continue;
            }

            copia.transform.SetParent(null, true);
            copia.name = $"{nombre} (guardado)";
            DontDestroyOnLoad(copia);
            originales[nombre] = copia;
        }
    }

    // Copia del modelo, sin colliders ni scripts, desactivada. null si nunca se recogio.
    public static GameObject crearCopia(string nombre)
    {
        if (string.IsNullOrEmpty(nombre) || !originales.TryGetValue(nombre, out GameObject original) || original == null)
        {
            return null;
        }

        bool estabaActivo = original.activeSelf;
        original.SetActive(false); // asi la copia nace apagada y no corre nada al crearse
        GameObject copia = Instantiate(original);
        original.SetActive(estabaActivo);
        copia.name = $"{nombre} (copia)";
        foreach (Collider c in copia.GetComponentsInChildren<Collider>(true))
        {
            DestroyImmediate(c);
        }

        foreach (Rigidbody c in copia.GetComponentsInChildren<Rigidbody>(true))
        {
            DestroyImmediate(c);
        }

        foreach (MonoBehaviour c in copia.GetComponentsInChildren<MonoBehaviour>(true))
        {
            DestroyImmediate(c);
        }

        return copia;
    }

    private Transform soporte;
    private GameObject modelo;
    private string nombreActual, nombreDeseado;
    private float subida;                 // 0 = escondido abajo, 1 = en su lugar
    private CharacterController cuerpo;
    private float fase;

    private void Awake()
    {
        soporte = new GameObject("Objeto en la mano").transform;
        soporte.SetParent(transform, false);
        cuerpo = GetComponentInParent<CharacterController>();
    }

    private void LateUpdate()
    {
        BarraHerramientas barra = BarraHerramientas.Instancia;
        nombreDeseado = barra != null ? barra.ObjetoSeleccionado : null;
        if (string.IsNullOrEmpty(nombreDeseado))
        {
            nombreDeseado = null;
        }

        // Si cambio la casilla: primero baja, luego cambia el modelo y sube.
        if (nombreDeseado != nombreActual)
        {
            subida = Mathf.MoveTowards(subida, 0f, Time.deltaTime * 7f);
            if (subida <= 0f || modelo == null)
            {
                cambiarObjeto(nombreDeseado);
            }
        }
        else if (modelo != null)
        {
            subida = Mathf.MoveTowards(subida, 1f, Time.deltaTime * 5f);
        }

        if (modelo == null)
        {
            return;
        }

        // Balanceo al caminar.
        float velocidad = cuerpo != null ? new Vector2(cuerpo.velocity.x, cuerpo.velocity.z).magnitude : 0f;
        fase += Time.deltaTime * (6f + velocidad * 1.5f);
        float fuerza = Mathf.Clamp01(velocidad / 3f);
        Vector3 bamboleo = new Vector3(Mathf.Cos(fase) * balanceo, Mathf.Abs(Mathf.Sin(fase)) * balanceo, 0f) * fuerza;

        float suave = 1f - Mathf.Pow(1f - subida, 3f);
        soporte.localPosition = posicion + bamboleo + Vector3.down * (0.09f * (1f - suave));
    }

    private void cambiarObjeto(string nombre)
    {
        if (modelo != null)
        {
            Destroy(modelo);
        }

        modelo = null;
        nombreActual = nombre;
        subida = 0f;
        if (nombre == null)
        {
            return;
        }

        modelo = crearCopia(nombre);
        if (modelo == null)
        {
            return; // nunca se recogio en esta partida
        }

        Transform t = modelo.transform;
        t.SetParent(soporte, false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.Euler(Giros.TryGetValue(nombre, out Vector3 giro) ? giro : new Vector3(10f, -30f, 0f));
        t.localScale = Vector3.one;

        foreach (Renderer r in modelo.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.gameObject.layer = gameObject.layer;
        }

        modelo.SetActive(true);

        // Mismo tamano para todos y centrado en el soporte.
        Bounds b = limites(modelo);
        float mayor = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (mayor < 0.0001f)
        {
            return;
        }

        float escala = tamano / mayor;
        Vector3 centro = soporte.InverseTransformPoint(b.center);
        t.localScale = Vector3.one * escala;
        t.localPosition = -centro * escala;
    }

    public static Bounds limites(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0)
        {
            return new Bounds(go.transform.position, Vector3.zero);
        }

        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs)
        {
            b.Encapsulate(r.bounds);
        }

        return b;
    }
}
