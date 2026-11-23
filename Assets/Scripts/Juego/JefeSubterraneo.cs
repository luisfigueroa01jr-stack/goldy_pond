using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// El jefe en el subterraneo (solo en la caceria infinita, ya con el elevador abierto). Lo crea el Elevador.
// 1. Bajas la escalera y caminas un poco por el puente: todo se detiene, retumba, la camara voltea hacia atras
// y el jefe cae junto a la escalera. Se queda viendote, la camara regresa y puedes seguir.
// 2. El jefe te persigue (mas lento que en la aldea: corriendo llegas al elevador; caminando te alcanza).
// 3. Ya en el elevador no te puede agarrar: llega hasta enfrente de la reja y se queda viendo hacia arriba
// (lo demas del final lo hace el Elevador).
public class JefeSubterraneo : MonoBehaviour
{
    public static JefeSubterraneo Instancia { get; private set; }

    private static bool yaCayo; // si vuelves a bajar, ya no se repite la caida

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void reiniciar()
    {
        yaCayo = false;
    }

    [Tooltip("Metros que caminas sobre el puente antes de que caiga el jefe.")]
    public float metrosSobreElPuente = 2f;
    [Tooltip("Velocidad base del jefe aqui abajo (en promedio ~0.88 de esto).")]
    public float velocidadPersiguiendo = 3.4f;
    [Tooltip("Desde que altura cae.")]
    public float alturaCaida = 7f;

    public Villano Jefe => jefe;
    // Ya llego enfrente del elevador.
    public bool Llego { get; private set; }
    public Vector3 Cabeza => jefe != null ? jefe.transform.position + Vector3.up * altoJefe * 0.85f : transform.position;

    private Elevador elevador;
    private GameObject prefab;
    private Villano jefe;
    private float altoJefe = 3f;
    private Bounds puente;
    private Vector3 inicioPuente, ejePuente, aterrizaje;
    private NavMeshDataInstance nav;
    private RelojCaceria reloj;

    public static JefeSubterraneo crear(Elevador elevador, GameObject prefab)
    {
        GameObject go = new GameObject("Jefe del subterraneo (caceria infinita)");
        JefeSubterraneo js = go.AddComponent<JefeSubterraneo>();
        js.elevador = elevador;
        js.prefab = prefab;
        return js;
    }

    private void Awake()
    {
        Instancia = this;
    }

    private void OnDestroy()
    {
        if (Instancia == this)
        {
            Instancia = null;
        }

        if (nav.valid)
        {
            NavMesh.RemoveNavMeshData(nav);
        }
    }

    private IEnumerator Start()
    {
        yield return null; // que el jugador ya este en su punto de llegada
        yield return null;
        if (prefab == null || !buscarPuente())
        {
            Debug.LogWarning("[Goldy Pond] Jefe del subterraneo: falta el prefab del jefe o el puente.");
            yield break;
        }

        int triangulos = construirNavMesh();
        reloj = RelojCaceria.crear(transform);

        // Cae junto al pie de la escalera, un poquito hacia el puente (detras de ti).
        Escalera esc = FindFirstObjectByType<Escalera>();
        Vector3 pie = esc != null ? esc.punto(0f) : inicioPuente - ejePuente;
        Vector3 haciaPuente = enPlano(inicioPuente - pie);
        aterrizaje = pie + (haciaPuente.sqrMagnitude > 0.01f ? haciaPuente.normalized * Mathf.Min(1f, haciaPuente.magnitude * 0.5f) : Vector3.zero);
        if (NavMesh.SamplePosition(aterrizaje, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
        {
            aterrizaje = hit.position;
        }
        else if (NavMesh.SamplePosition(inicioPuente, out hit, 2.5f, NavMesh.AllAreas))
        {
            aterrizaje = hit.position;
        }

        Debug.Log($"[Goldy Pond] Jefe del subterraneo listo: NavMesh {triangulos} triangulos, cae en {aterrizaje} {(yaCayo ? "(ya habia caido)" : "")}.");

        if (yaCayo)
        {
            aparecer(aterrizaje);
            yield return new WaitForSeconds(1.5f);
            soltar();
            yield break;
        }

        // Espera a que bajes y camines un poco por el puente.
        while (true)
        {
            JugadorPrimeraPersona j = JugadorPrimeraPersona.Instancia;
            if (j != null && !j.Escalando && !j.Inmovil && sobreElPuente(j.transform.position))
            {
                break;
            }

            yield return null;
        }

        yield return caidaDelJefe();
        soltar();
    }

    // ------------------- LA CAIDA ------------------------------

    private IEnumerator caidaDelJefe()
    {
        yaCayo = true;
        JugadorPrimeraPersona j = JugadorPrimeraPersona.Instancia;
        Transform cam = j.camara.transform;
        Debug.Log("[Goldy Pond] Subterraneo: se detiene todo, retumba y cae el jefe.");

        Quaternion rotCuerpo = j.transform.rotation;
        Quaternion rotCamara = cam.localRotation;
        Time.timeScale = 0f;
        j.Inmovil = true;
        j.BloquearMirar = true;
        StartCoroutine(Retumbe.temblar(5.6f, 0.8f));
        try
        {
            yield return esperar(0.7f);

            // La camara voltea hacia donde va a caer.
            yield return voltear(j, cam, aterrizaje + Vector3.up * 1.4f, 1.1f);
            yield return esperar(0.25f);

            // Cae.
            aparecer(aterrizaje + Vector3.up * alturaCaida);
            jefe.transform.rotation = Quaternion.LookRotation(enPlano(j.transform.position - aterrizaje).normalized + Vector3.forward * 0.0001f);
            float t = 0f, dur = Mathf.Sqrt(2f * alturaCaida / 22f);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                jefe.transform.position = aterrizaje + Vector3.up * alturaCaida * (1f - k * k);
                mirarA(j, cam, Cabeza, 10f);
                yield return null;
            }

            jefe.transform.position = aterrizaje;
            Retumbe.darGolpe(1.8f);

            // Se queda viendote.
            t = 0f;
            while (t < 2f)
            {
                t += Time.unscaledDeltaTime;
                // Del golpe queda agachado y se va enderezando.
                float agachado = Mathf.Lerp(0.25f, 0f, Mathf.SmoothStep(0f, 1f, t / 1.2f));
                jefe.transform.position = aterrizaje - Vector3.up * agachado * 0.4f;
                mirarA(j, cam, Cabeza, 6f);
                yield return null;
            }

            jefe.transform.position = aterrizaje;

            // La camara regresa a como estaba.
            yield return regresar(j, cam, rotCuerpo, rotCamara, 1.0f);
            yield return esperar(0.3f);
        }
        finally
        {
            j.transform.rotation = rotCuerpo;
            cam.localRotation = rotCamara;
            Time.timeScale = 1f;
            j.Inmovil = false;
            j.BloquearMirar = false;
        }

        Debug.Log("[Goldy Pond] Subterraneo: el jefe te persigue.");
    }

    private static IEnumerator esperar(float segundos)
    {
        float t = 0f;
        while (t < segundos)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // Gira el cuerpo (horizontal) y la camara (vertical) hasta ver el punto.
    private static IEnumerator voltear(
        JugadorPrimeraPersona j,
        Transform cam,
        Vector3 punto,
        float duracion
    )
    {
        Quaternion cuerpo0 = j.transform.rotation; Quaternion cam0 = cam.localRotation;
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duracion);
            float k = Mathf.SmoothStep(0f, 1f, t);
            var (cuerpo, camara) = calcularMirada(j, cam, punto);
            j.transform.rotation = Quaternion.Slerp(cuerpo0, cuerpo, k);
            cam.localRotation = Quaternion.Slerp(cam0, camara, k);
            yield return null;
        }
    }

    private static IEnumerator regresar(
        JugadorPrimeraPersona j,
        Transform cam,
        Quaternion cuerpo,
        Quaternion camara,
        float duracion
    )
    {
        Quaternion cuerpo0 = j.transform.rotation; Quaternion cam0 = cam.localRotation;
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duracion);
            float k = Mathf.SmoothStep(0f, 1f, t);
            j.transform.rotation = Quaternion.Slerp(cuerpo0, cuerpo, k);
            cam.localRotation = Quaternion.Slerp(cam0, camara, k);
            yield return null;
        }
    }

    // Sigue un punto con la mirada (suave).
    public static void mirarA(
        JugadorPrimeraPersona j,
        Transform cam,
        Vector3 punto,
        float rapidez
    )
    {
        var (cuerpo, camara) = calcularMirada(j, cam, punto);
        float k = 1f - Mathf.Exp(-rapidez * Time.unscaledDeltaTime);
        j.transform.rotation = Quaternion.Slerp(j.transform.rotation, cuerpo, k);
        cam.localRotation = Quaternion.Slerp(cam.localRotation, camara, k);
    }

    private static (Quaternion cuerpo, Quaternion camara) calcularMirada(
        JugadorPrimeraPersona j,
        Transform cam,
        Vector3 punto
    )
    {
        Vector3 d = punto - cam.position;
        Vector3 plano = enPlano(d);
        Quaternion cuerpo = plano.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(plano, Vector3.up) : j.transform.rotation;
        float pitch = -Mathf.Atan2(d.y, Mathf.Max(0.01f, plano.magnitude)) * Mathf.Rad2Deg;
        return (cuerpo, Quaternion.Euler(Mathf.Clamp(pitch, -80f, 80f), 0f, 0f));
    }

    // ------------------- EL JEFE ------------------------------

    private void aparecer(Vector3 posicion)
    {
        if (jefe != null)
        {
            jefe.transform.position = posicion;
            return;
        }

        GameObject go = Instantiate(prefab, posicion, Quaternion.identity);
        go.name = "Jefe (subterraneo)";
        jefe = go.GetComponent<Villano>();
        if (jefe == null)
        {
            jefe = go.AddComponent<Villano>();
        }

        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs)
            {
                b.Encapsulate(r.bounds);
            }

            altoJefe = Mathf.Max(1f, b.size.y);
        }
    }

    private void soltar()
    {
        if (jefe == null)
        {
            return;
        }

        jefe.velocidadPersiguiendo = velocidadPersiguiendo;
        jefe.velocidadDeambulando = 1.5f;
        jefe.duracionJalon = 0.95f;
        jefe.distanciaAtrapar = 1.3f;
        jefe.rol = Villano.Rol.Empuja;
        jefe.puedeAtrapar = puedeAtrapar;
        jefe.preparar(altoJefe, aterrizaje, 6f);
        jefe.cambiar(Villano.Estado.Persiguiendo);
    }

    // Ya en el elevador (o subiendo) no te puede agarrar.
    private bool puedeAtrapar()
    {
        if (Time.timeScale <= 0f)
        {
            return false;
        }

        JugadorPrimeraPersona j = JugadorPrimeraPersona.Instancia;
        if (elevador != null && (elevador.Subiendo || (j != null && elevador.jugadorAdentro(j.transform.position))))
        {
            return false;
        }

        return true;
    }

    // El elevador empezo a subir: el jefe llega hasta enfrente de la reja y se queda viendo hacia arriba.
    public void irAlElevador(Vector3 punto, Vector3 centroJaula)
    {
        if (reloj != null)
        {
            Destroy(reloj.gameObject); // ya escapaste: se quita "CACERIA INFINITA"
        }

        if (jefe == null)
        {
            return;
        }

        StopAllCoroutines();
        StartCoroutine(acercarse(punto, centroJaula));
    }

    private IEnumerator acercarse(Vector3 punto, Vector3 centroJaula)
    {
        if (NavMesh.SamplePosition(punto, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            punto = hit.position;
        }

        Debug.Log($"[Goldy Pond] El jefe va al elevador ({punto}).");
        jefe.irA(punto);
        float t = 0f;
        while (t < 12f && enPlano(jefe.transform.position - punto).magnitude > 0.5f)
        {
            t += Time.deltaTime;
            yield return null;
        }

        jefe.cambiar(Villano.Estado.Quieto);
        Llego = true;

        // Mira hacia la jaula y va levantando la cabeza conforme subes.
        while (true)
        {
            Vector3 hacia = enPlano(centroJaula - jefe.transform.position);
            if (hacia.sqrMagnitude > 0.01f)
            {
                jefe.transform.rotation = Quaternion.Slerp(jefe.transform.rotation, Quaternion.LookRotation(hacia), 1f - Mathf.Exp(-4f * Time.deltaTime));
            }

            JugadorPrimeraPersona j = JugadorPrimeraPersona.Instancia;
            float objetivo = 0f;
            if (j != null)
            {
                Vector3 d = j.transform.position + Vector3.up * 1.2f - Cabeza;
                objetivo = Mathf.Clamp(Mathf.Atan2(d.y, Mathf.Max(0.3f, enPlano(d).magnitude)) * Mathf.Rad2Deg, 0f, 55f);
            }

            jefe.mirarArriba = Mathf.MoveTowards(jefe.mirarArriba, objetivo, 35f * Time.deltaTime);
            yield return null;
        }
    }

    // ------------------- EL PUENTE Y EL NAVMESH ------------------------------

    private bool buscarPuente()
    {
        bool hay = false;
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (r.gameObject.scene != gameObject.scene || !r.name.StartsWith("Puente_Tablas"))
            {
                continue;
            }

            if (!hay)
            {
                puente = r.bounds;
                hay = true;
            }else
            {
                puente.Encapsulate(r.bounds);
            }
        }

        if (!hay)
        {
            return false;
        }

        // El puente es largo en un eje; el inicio es la punta mas cerca de la escalera.
        ejePuente = puente.size.x > puente.size.z ? Vector3.right : Vector3.forward;
        float medio = Mathf.Max(puente.extents.x, puente.extents.z);
        Vector3 a = puente.center - ejePuente * medio;
        Vector3 b = puente.center + ejePuente * medio;
        Escalera esc = FindFirstObjectByType<Escalera>();
        Vector3 pie = esc != null ? esc.punto(0f) : a;
        bool aMasCerca = enPlano(a - pie).sqrMagnitude < enPlano(b - pie).sqrMagnitude;
        inicioPuente = aMasCerca ? a : b;
        if (!aMasCerca)
        {
            ejePuente = -ejePuente;
        }

        return true;
    }

    private bool sobreElPuente(Vector3 p)
    {
        Bounds b = puente;
        b.Expand(new Vector3(0.4f, 0f, 0.4f));
        if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z)
        {
            return false;
        }

        if (Mathf.Abs(p.y - puente.max.y) > 1.5f)
        {
            return false;
        }

        return Vector3.Dot(enPlano(p - inicioPuente), ejePuente) >= metrosSobreElPuente;
    }

    private int construirNavMesh()
    {
        JugadorPrimeraPersona jugador = JugadorPrimeraPersona.Instancia;
        Bounds zona = puente;
        Escalera esc = FindFirstObjectByType<Escalera>();
        if (esc != null)
        {
            zona.Encapsulate(esc.punto(0f));
        }

        Bounds jaula = default;
        bool hayJaula = false;
        if (elevador != null && elevador.jaula != null)
        {
            Renderer r = elevador.jaula.GetComponent<Renderer>();
            jaula = r != null ? r.bounds : new Bounds(elevador.jaula.position, new Vector3(1.8f, 0.1f, 1.8f));
            hayJaula = true;
            zona.Encapsulate(jaula);
        }

        zona.Expand(new Vector3(14f, 8f, 14f));

        List<NavMeshBuildSource> fuentes = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(zona, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), fuentes);
        fuentes.RemoveAll(f =>
        {
            Collider c = f.component as Collider;
            if (c == null)
            {
                return false;
            }

            if (c.isTrigger)
            {
                return true;
            }

            if (c.gameObject.scene != gameObject.scene)
            {
                return true;
            }

            Transform tr = c.transform;
            if (jugador != null && tr.IsChildOf(jugador.transform))
            {
                return true;
            }

            if (elevador != null && elevador.jaula != null && tr.IsChildOf(elevador.jaula))
            {
                return true;
            }

            // El agua no se pisa.
            for (Transform x = tr; x != null; x = x.parent)
            {
                if (x.name.StartsWith("Agua") || x.name.StartsWith("Cascada") || x.name == "AGUA")
                {
                    return true;
                }
            }

            return false;
        });
        // No se mete a la jaula del elevador.
        if (hayJaula)
        {
            fuentes.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.ModifierBox,
                transform = Matrix4x4.TRS(jaula.center, Quaternion.identity, Vector3.one),
                size = new Vector3(jaula.size.x + 0.6f, 6f, jaula.size.z + 0.6f),
                area = 1,
            });
        }

        NavMeshBuildSettings ajustes = NavMesh.GetSettingsByID(0);
        ajustes.agentRadius = 0.35f;
        ajustes.agentHeight = 1.8f;
        ajustes.agentClimb = 0.45f;
        ajustes.agentSlope = 45f;
        ajustes.overrideVoxelSize = true;
        ajustes.voxelSize = 0.12f;
        NavMeshData datos = NavMeshBuilder.BuildNavMeshData(ajustes, fuentes, zona, Vector3.zero, Quaternion.identity);
        if (datos == null)
        {
            return 0;
        }

        nav = NavMesh.AddNavMeshData(datos);
        return NavMesh.CalculateTriangulation().indices.Length / 3;
    }

    private static Vector3 enPlano(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }
}
