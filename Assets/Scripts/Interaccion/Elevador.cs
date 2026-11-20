using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Elevador del subterraneo (la jaula con la reja doble).
/// - Mientras no esten activadas las 3 palancas de la Aldea: la reja esta cerrada y no se puede abrir.
/// - Con las 3 palancas activadas, al bajar al subterraneo la reja ya esta abierta.
/// - Al meterte a la jaula: te acomodas al centro mirando hacia la reja, la reja se cierra,
///   arranca el motor (giran los engranes y la polea), la jaula sube, los cables se recogen
///   y a los pocos segundos la pantalla se va a negro.
/// Lo configura Goldy Pond > Interaccion > Elevador.
/// </summary>
public class Elevador : MonoBehaviour
{
    [Header("Partes")]
    [Tooltip("La jaula (Jaula_Base): sube con todo lo que tiene adentro (barrotes, cupula, reja...).")]
    public Transform jaula;
    [Tooltip("Las dos hojas de la reja.")]
    public Puerta[] reja = new Puerta[0];
    [Tooltip("Los cables (se van recogiendo al subir).")]
    public Renderer cables;
    [Tooltip("Lo que gira mientras sube (polea, engranes del motor).")]
    public Transform[] engranes = new Transform[0];
    [Tooltip("La viga de arriba (Soporte_Viga): la jaula frena antes de tocarla.")]
    public Renderer techo;

    [Header("Cuando se abre")]
    public int palancasNecesarias = 3;

    [Header("Subida")]
    [Tooltip("Metros por segundo ya a toda velocidad.")]
    public float velocidad = 1f;
    [Tooltip("Segundos subiendo antes de que empiece la pantalla negra.")]
    public float segundosAntesDeNegro = 5f;
    [Tooltip("Segundos que tarda la pantalla en ponerse negra.")]
    public float duracionNegro = 1.5f;
    [Tooltip("Grados por segundo de la polea y los engranes.")]
    public float giroEngranes = 120f;

    public bool Abierto { get; private set; }
    public bool Subiendo { get; private set; }

    Bounds piso;            // la base de la jaula, al inicio
    Vector3 haciaReja;      // del centro de la jaula hacia la reja (horizontal)
    float largoCables, topeCables;
    Vector3 escalaCables;
    int ejeCables = -1;
    Vector3[] ejesEngranes;
    Vector3[] centrosEngranes;
    float subidaMaxima = float.MaxValue;

    void Start()
    {
        if (jaula == null) { Debug.LogWarning("[Goldy Pond] El elevador no tiene jaula asignada."); enabled = false; return; }
        var r = jaula.GetComponent<Renderer>();
        piso = r != null ? r.bounds : new Bounds(jaula.position, new Vector3(1.8f, 0.1f, 1.8f));

        var centroReja = Vector3.zero;
        int n = 0;
        foreach (var p in reja)
            if (p != null) { centroReja += p.GetComponent<Renderer>() != null ? p.GetComponent<Renderer>().bounds.center : p.transform.position; n++; }
        haciaReja = n > 0 ? Vector3.ProjectOnPlane(centroReja / n - piso.center, Vector3.up).normalized : Vector3.forward;

        // Hasta donde puede subir sin meterse en la viga de arriba.
        if (techo != null)
        {
            float tope = float.MinValue;
            foreach (var rr in jaula.GetComponentsInChildren<Renderer>()) tope = Mathf.Max(tope, rr.bounds.max.y);
            subidaMaxima = Mathf.Max(0f, techo.bounds.min.y - tope - 0.1f);
        }

        PrepararCables();
        PrepararEngranes();

        Abierto = EstadoJuego.PalancasActivadas >= palancasNecesarias;
        foreach (var p in reja)
        {
            if (p == null) continue;
            p.bloqueada = true; // el elevador decide cuando se abre y cierra, no la E
            if (Abierto) p.FijarAlInstante(true, piso.center); // abre hacia afuera de la jaula
        }
    }

    void Update()
    {
        if (!Abierto || Subiendo) return;
        var j = JugadorPrimeraPersona.Instancia;
        if (j == null || j.Escalando) return;
        if (Adentro(j.transform.position)) StartCoroutine(Subir(j));
    }

    /// <summary>Ya paso la reja: dentro de la base de la jaula, sin contar una orilla de 35 cm.</summary>
    bool Adentro(Vector3 pies)
    {
        var interior = piso;
        interior.Expand(new Vector3(-0.7f, 0f, -0.7f));
        bool dentro = pies.x > interior.min.x && pies.x < interior.max.x && pies.z > interior.min.z && pies.z < interior.max.z;
        return dentro && pies.y > piso.min.y - 0.3f && pies.y < piso.max.y + 0.8f;
    }

    IEnumerator Subir(JugadorPrimeraPersona j)
    {
        Subiendo = true;
        j.Inmovil = true;

        // 1. Al centro de la jaula, mirando hacia la reja (se ve como se cierra).
        var desde = j.transform.position;
        var hasta = new Vector3(piso.center.x, piso.max.y, piso.center.z) - haciaReja * 0.15f;
        var rotDesde = j.transform.rotation;
        var rotHasta = Quaternion.LookRotation(haciaReja, Vector3.up);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            j.transform.SetPositionAndRotation(Vector3.Lerp(desde, hasta, k), Quaternion.Slerp(rotDesde, rotHasta, k));
            yield return null;
        }
        j.transform.SetPositionAndRotation(hasta, rotHasta);

        // 2. Se cierra la reja.
        foreach (var p in reja) if (p != null) p.Fijar(false, hasta);
        float espera = 0.3f;
        foreach (var p in reja) if (p != null) espera = Mathf.Max(espera, p.duracion);
        yield return new WaitForSeconds(espera + 0.4f);

        // 3. Sube: arranca despacio, el motor gira, los cables se recogen. A los pocos segundos, negro.
        float tiempo = 0f, subido = 0f;
        Image negro = null;
        while (true)
        {
            tiempo += Time.deltaTime;
            float v = velocidad * Mathf.SmoothStep(0f, 1f, tiempo / 1.2f);
            v *= Mathf.Clamp01((subidaMaxima - subido) / 1.2f); // frena suave antes de la viga
            var paso = Vector3.up * (v * Time.deltaTime);
            subido += paso.y;
            jaula.position += paso;
            j.transform.position += paso;
            AjustarCables(subido);
            GirarEngranes(Mathf.Clamp01(tiempo / 1.2f));

            if (tiempo >= segundosAntesDeNegro)
            {
                if (negro == null) negro = CrearNegro();
                float a = Mathf.Clamp01((tiempo - segundosAntesDeNegro) / Mathf.Max(0.05f, duracionNegro));
                negro.color = new Color(0f, 0f, 0f, a);
                if (a >= 1f) break;
            }
            yield return null;
        }
        // Se queda en negro (aqui despues puede seguir la historia).
    }

    // ---------- Cables: la punta de arriba se queda fija y se acortan ----------

    void PrepararCables()
    {
        if (cables == null) return;
        var t = cables.transform;
        float mejor = 0f;
        for (int i = 0; i < 3; i++)
        {
            var e = Vector3.zero; e[i] = 1f;
            float vertical = Mathf.Abs(t.TransformVector(e).normalized.y);
            if (vertical > mejor) { mejor = vertical; ejeCables = i; }
        }
        escalaCables = t.localScale;
        largoCables = cables.bounds.size.y;
        topeCables = cables.bounds.max.y;
    }

    void AjustarCables(float subido)
    {
        if (cables == null || ejeCables < 0 || largoCables <= 0.01f) return;
        float factor = Mathf.Max(0.02f, (largoCables - subido) / largoCables);
        var t = cables.transform;
        var s = escalaCables; s[ejeCables] *= factor;
        t.localScale = s;
        t.position += Vector3.up * (topeCables - cables.bounds.max.y);
    }

    // ---------- Polea y engranes del motor ----------

    void PrepararEngranes()
    {
        ejesEngranes = new Vector3[engranes.Length];
        centrosEngranes = new Vector3[engranes.Length];
        for (int i = 0; i < engranes.Length; i++)
        {
            var g = engranes[i];
            if (g == null) continue;
            var mf = g.GetComponent<MeshFilter>();
            var b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            int k = b.size.x <= b.size.y && b.size.x <= b.size.z ? 0 : b.size.y <= b.size.z ? 1 : 2; // lo mas delgado = el eje
            var e = Vector3.zero; e[k] = 1f;
            ejesEngranes[i] = e;
            centrosEngranes[i] = b.center;
        }
    }

    void GirarEngranes(float fuerza)
    {
        for (int i = 0; i < engranes.Length; i++)
        {
            var g = engranes[i];
            if (g == null) continue;
            float sentido = i % 2 == 0 ? 1f : -1f; // los que estan juntos giran al reves
            g.RotateAround(g.TransformPoint(centrosEngranes[i]), g.TransformDirection(ejesEngranes[i]), giroEngranes * sentido * fuerza * Time.deltaTime);
        }
    }

    static Image CrearNegro()
    {
        var go = new GameObject("Pantalla Negra", typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // encima de todo, tambien de la barra
        var img = new GameObject("Negro", typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = false;
        return img;
    }
}
