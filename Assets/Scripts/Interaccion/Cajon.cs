using UnityEngine;

/// <summary>
/// Cajon que se abre y se cierra con E deslizandose hacia afuera del mueble.
/// El origen del cajon esta en su frente (asi viene de Blender) y la caja del cajon queda atras,
/// por eso "afuera" es del centro de la malla hacia el origen.
/// Lo que pongas adentro (pines, llave, nota...) se mueve con el cajon:
///  - Cerrado: no se puede agarrar (solo sale "(E) Abrir cajón").
///  - Abierto con algo adentro: sale "(E) Recoger ..." en vez de cerrar.
///  - Ya vacio: se puede cerrar.
/// </summary>
public class Cajon : Interactuable
{
    [Tooltip("Como aparece en el aviso: (E) Abrir <nombre>.")]
    public string nombre = "cajón";
    [Tooltip("Cuanto sale al abrirse, en metros.")]
    public float distancia = 0.22f;
    [Tooltip("Segundos que tarda en abrirse o cerrarse.")]
    [Min(0.05f)] public float duracion = 0.35f;

    public bool Abierto { get; private set; }

    readonly System.Collections.Generic.List<ObjetoRecogible> adentro = new System.Collections.Generic.List<ObjetoRecogible>();
    Vector3 cerrado, afuera;
    float progreso = 1f, desde, hasta, actual;

    /// <summary>Abierto y con algo que agarrar: el jugador ve primero el objeto, no "Cerrar cajón".</summary>
    public override bool Disponible => !(Abierto && progreso >= 1f && QuedaAlgo());

    bool QuedaAlgo() => adentro.Exists(o => o != null && o.gameObject.activeInHierarchy);

    public override string TextoAccion => $"{(Abierto ? "Cerrar" : "Abrir")} <color=#FFDFA0>{nombre}</color>";

    void Awake()
    {
        cerrado = transform.localPosition;
        var mf = GetComponent<MeshFilter>();
        var dir = Vector3.zero;
        if (mf != null && mf.sharedMesh != null)
            dir = transform.TransformDirection(-mf.sharedMesh.bounds.center);
        dir = Vector3.ProjectOnPlane(dir, Vector3.up);
        if (dir.sqrMagnitude < 1e-6f) dir = -transform.forward;
        // en el espacio del padre, porque movemos localPosition
        afuera = transform.parent != null ? transform.parent.InverseTransformDirection(dir.normalized) : dir.normalized;
    }

    void Start()
    {
        // Lo que se puso adentro del cajon se hace su hijo, para que salga con el.
        var r = GetComponent<Renderer>();
        if (r == null) return;
        var caja = r.bounds;
        foreach (var o in FindObjectsByType<ObjetoRecogible>(FindObjectsInactive.Include))
        {
            if (o.GetComponentInParent<Cajon>() != null && !o.transform.IsChildOf(transform)) continue; // ya es de otro cajon
            var centro = o.transform.position;
            var ro = o.GetComponentInChildren<Renderer>();
            if (ro != null) centro = ro.bounds.center;
            // Dentro de la huella del cajon (con 3 cm de margen) y a su altura (puede sobresalir un poco arriba).
            bool dentro = centro.x > caja.min.x - 0.03f && centro.x < caja.max.x + 0.03f
                       && centro.z > caja.min.z - 0.03f && centro.z < caja.max.z + 0.03f
                       && centro.y > caja.min.y - 0.05f && centro.y < caja.max.y + 0.2f;
            if (!dentro) continue;
            o.transform.SetParent(transform, true);
            adentro.Add(o);
        }
        if (adentro.Count > 0)
            Debug.Log($"[Goldy Pond] {name}: adentro tiene {string.Join(", ", adentro.ConvertAll(o => o.name))}");
        Esconder(true);
    }

    /// <summary>Con el cajon cerrado lo de adentro no se puede agarrar.</summary>
    void Esconder(bool esconder)
    {
        foreach (var o in adentro)
            if (o != null)
                foreach (var c in o.GetComponentsInChildren<Collider>(true)) c.enabled = !esconder;
    }

    public override bool Interactuar(Transform jugador)
    {
        Abierto = !Abierto;
        if (Abierto) Esconder(false);
        desde = actual;
        hasta = Abierto ? distancia : 0f;
        progreso = 0f;
        return true;
    }

    void Update()
    {
        if (progreso >= 1f) return;
        progreso = Mathf.Min(1f, progreso + Time.deltaTime / duracion);
        float t = progreso * progreso * (3f - 2f * progreso);
        actual = Mathf.Lerp(desde, hasta, t);
        transform.localPosition = cerrado + afuera * actual;
        if (progreso >= 1f && !Abierto) Esconder(true);
    }
}
