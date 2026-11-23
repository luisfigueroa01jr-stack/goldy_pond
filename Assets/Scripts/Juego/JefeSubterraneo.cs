using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// El jefe en el subterráneo (solo en la cacería infinita, ya con el elevador abierto). Lo crea el Elevador.
///  1. Bajas la escalera y caminas un poco por el puente: todo se detiene, retumba, la cámara voltea hacia atrás
///     y el jefe cae junto a la escalera. Se queda viéndote, la cámara regresa y puedes seguir.
///  2. El jefe te persigue (más lento que en la aldea: corriendo llegas al elevador; caminando te alcanza).
///  3. Ya en el elevador no te puede agarrar: llega hasta enfrente de la reja y se queda viendo hacia arriba
///     (lo demás del final lo hace el Elevador).
/// </summary>
public class JefeSubterraneo : MonoBehaviour
{
    public static JefeSubterraneo Instancia { get; private set; }

    static bool yaCayo; // si vuelves a bajar, ya no se repite la caida

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void Reiniciar() => yaCayo = false;

    [Tooltip("Metros que caminas sobre el puente antes de que caiga el jefe.")]
    public float metrosSobreElPuente = 2f;
    [Tooltip("Velocidad base del jefe aqui abajo (en promedio ~0.88 de esto).")]
    public float velocidadPersiguiendo = 3.4f;
    [Tooltip("Desde que altura cae.")]
    public float alturaCaida = 7f;

    public Villano Jefe => jefe;
    /// <summary>Ya llego enfrente del elevador.</summary>
    public bool Llego { get; private set; }
    public Vector3 Cabeza => jefe != null ? jefe.transform.position + Vector3.up * altoJefe * 0.85f : transform.position;

    Elevador elevador;
    GameObject prefab;
    Villano jefe;
    float altoJefe = 3f;
    Bounds puente;
    Vector3 inicioPuente, ejePuente, aterrizaje;
    NavMeshDataInstance nav;
    RelojCaceria reloj;

    public static JefeSubterraneo Crear(Elevador elevador, GameObject prefab)
    {
        var go = new GameObject("Jefe del subterraneo (caceria infinita)");
        var js = go.AddComponent<JefeSubterraneo>();
        js.elevador = elevador;
        js.prefab = prefab;
        return js;
    }

    void Awake() => Instancia = this;

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
        if (nav.valid) NavMesh.RemoveNavMeshData(nav);
    }

    IEnumerator Start()
    {
        yield return null; // que el jugador ya este en su punto de llegada
        yield return null;
        if (prefab == null || !BuscarPuente()) { Debug.LogWarning("[Goldy Pond] Jefe del subterraneo: falta el prefab del jefe o el puente."); yield break; }
        int triangulos = ConstruirNavMesh();
        reloj = RelojCaceria.Crear(transform);

        // Cae junto al pie de la escalera, un poquito hacia el puente (detras de ti).
        var esc = FindFirstObjectByType<Escalera>();
        var pie = esc != null ? esc.Punto(0f) : inicioPuente - ejePuente;
        var haciaPuente = Plano(inicioPuente - pie);
        aterrizaje = pie + (haciaPuente.sqrMagnitude > 0.01f ? haciaPuente.normalized * Mathf.Min(1f, haciaPuente.magnitude * 0.5f) : Vector3.zero);
        if (NavMesh.SamplePosition(aterrizaje, out var hit, 2.5f, NavMesh.AllAreas)) aterrizaje = hit.position;
        else if (NavMesh.SamplePosition(inicioPuente, out hit, 2.5f, NavMesh.AllAreas)) aterrizaje = hit.position;
        Debug.Log($"[Goldy Pond] Jefe del subterraneo listo: NavMesh {triangulos} triangulos, cae en {aterrizaje} {(yaCayo ? "(ya habia caido)" : "")}.");

        if (yaCayo)
        {
            Aparecer(aterrizaje);
            yield return new WaitForSeconds(1.5f);
            Soltar();
            yield break;
        }

        // Espera a que bajes y camines un poco por el puente.
        while (true)
        {
            var j = JugadorPrimeraPersona.Instancia;
            if (j != null && !j.Escalando && !j.Inmovil && SobreElPuente(j.transform.position)) break;
            yield return null;
        }
        yield return Caida();
        Soltar();
    }

    // ---------- La caida ----------

    IEnumerator Caida()
    {
        yaCayo = true;
        var j = JugadorPrimeraPersona.Instancia;
        var cam = j.camara.transform;
        Debug.Log("[Goldy Pond] Subterraneo: se detiene todo, retumba y cae el jefe.");

        var rotCuerpo = j.transform.rotation;
        var rotCamara = cam.localRotation;
        Time.timeScale = 0f;
        j.Inmovil = true;
        j.BloquearMirar = true;
        StartCoroutine(Retumbe.Temblar(5.6f, 0.8f));
        try
        {
            yield return Esperar(0.7f);

            // La camara voltea hacia donde va a caer.
            yield return Voltear(j, cam, aterrizaje + Vector3.up * 1.4f, 1.1f);
            yield return Esperar(0.25f);

            // Cae.
            Aparecer(aterrizaje + Vector3.up * alturaCaida);
            jefe.transform.rotation = Quaternion.LookRotation(Plano(j.transform.position - aterrizaje).normalized + Vector3.forward * 0.0001f);
            float t = 0f, dur = Mathf.Sqrt(2f * alturaCaida / 22f);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                jefe.transform.position = aterrizaje + Vector3.up * alturaCaida * (1f - k * k);
                MirarA(j, cam, Cabeza, 10f);
                yield return null;
            }
            jefe.transform.position = aterrizaje;
            Retumbe.Golpe(1.8f);

            // Se queda viendote.
            t = 0f;
            while (t < 2f)
            {
                t += Time.unscaledDeltaTime;
                // Del golpe queda agachado y se va enderezando.
                float agachado = Mathf.Lerp(0.25f, 0f, Mathf.SmoothStep(0f, 1f, t / 1.2f));
                jefe.transform.position = aterrizaje - Vector3.up * agachado * 0.4f;
                MirarA(j, cam, Cabeza, 6f);
                yield return null;
            }
            jefe.transform.position = aterrizaje;

            // La camara regresa a como estaba.
            yield return Regresar(j, cam, rotCuerpo, rotCamara, 1.0f);
            yield return Esperar(0.3f);
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

    static IEnumerator Esperar(float segundos)
    {
        float t = 0f;
        while (t < segundos) { t += Time.unscaledDeltaTime; yield return null; }
    }

    /// <summary>Gira el cuerpo (horizontal) y la camara (vertical) hasta ver el punto.</summary>
    static IEnumerator Voltear(JugadorPrimeraPersona j, Transform cam, Vector3 punto, float duracion)
    {
        var cuerpo0 = j.transform.rotation; var cam0 = cam.localRotation;
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duracion);
            float k = Mathf.SmoothStep(0f, 1f, t);
            var (cuerpo, camara) = Mirada(j, cam, punto);
            j.transform.rotation = Quaternion.Slerp(cuerpo0, cuerpo, k);
            cam.localRotation = Quaternion.Slerp(cam0, camara, k);
            yield return null;
        }
    }

    static IEnumerator Regresar(JugadorPrimeraPersona j, Transform cam, Quaternion cuerpo, Quaternion camara, float duracion)
    {
        var cuerpo0 = j.transform.rotation; var cam0 = cam.localRotation;
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

    /// <summary>Sigue un punto con la mirada (suave).</summary>
    public static void MirarA(JugadorPrimeraPersona j, Transform cam, Vector3 punto, float rapidez)
    {
        var (cuerpo, camara) = Mirada(j, cam, punto);
        float k = 1f - Mathf.Exp(-rapidez * Time.unscaledDeltaTime);
        j.transform.rotation = Quaternion.Slerp(j.transform.rotation, cuerpo, k);
        cam.localRotation = Quaternion.Slerp(cam.localRotation, camara, k);
    }

    static (Quaternion cuerpo, Quaternion camara) Mirada(JugadorPrimeraPersona j, Transform cam, Vector3 punto)
    {
        var d = punto - cam.position;
        var plano = Plano(d);
        var cuerpo = plano.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(plano, Vector3.up) : j.transform.rotation;
        float pitch = -Mathf.Atan2(d.y, Mathf.Max(0.01f, plano.magnitude)) * Mathf.Rad2Deg;
        return (cuerpo, Quaternion.Euler(Mathf.Clamp(pitch, -80f, 80f), 0f, 0f));
    }

    // ---------- El jefe ----------

    void Aparecer(Vector3 posicion)
    {
        if (jefe != null) { jefe.transform.position = posicion; return; }
        var go = Instantiate(prefab, posicion, Quaternion.identity);
        go.name = "Jefe (subterraneo)";
        jefe = go.GetComponent<Villano>();
        if (jefe == null) jefe = go.AddComponent<Villano>();
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            altoJefe = Mathf.Max(1f, b.size.y);
        }
    }

    void Soltar()
    {
        if (jefe == null) return;
        jefe.velocidadPersiguiendo = velocidadPersiguiendo;
        jefe.velocidadDeambulando = 1.5f;
        jefe.duracionJalon = 0.95f;
        jefe.distanciaAtrapar = 1.3f;
        jefe.rol = Villano.Rol.Empuja;
        jefe.puedeAtrapar = PuedeAtrapar;
        jefe.Preparar(altoJefe, aterrizaje, 6f);
        jefe.Cambiar(Villano.Estado.Persiguiendo);
    }

    /// <summary>Ya en el elevador (o subiendo) no te puede agarrar.</summary>
    bool PuedeAtrapar()
    {
        if (Time.timeScale <= 0f) return false;
        var j = JugadorPrimeraPersona.Instancia;
        if (elevador != null && (elevador.Subiendo || (j != null && elevador.JugadorAdentro(j.transform.position)))) return false;
        return true;
    }

    /// <summary>El elevador empezo a subir: el jefe llega hasta enfrente de la reja y se queda viendo hacia arriba.</summary>
    public void IrAlElevador(Vector3 punto, Vector3 centroJaula)
    {
        if (reloj != null) Destroy(reloj.gameObject); // ya escapaste: se quita "CACERIA INFINITA"
        if (jefe == null) return;
        StopAllCoroutines();
        StartCoroutine(Acercarse(punto, centroJaula));
    }

    IEnumerator Acercarse(Vector3 punto, Vector3 centroJaula)
    {
        if (NavMesh.SamplePosition(punto, out var hit, 3f, NavMesh.AllAreas)) punto = hit.position;
        Debug.Log($"[Goldy Pond] El jefe va al elevador ({punto}).");
        jefe.IrA(punto);
        float t = 0f;
        while (t < 12f && Plano(jefe.transform.position - punto).magnitude > 0.5f)
        {
            t += Time.deltaTime;
            yield return null;
        }
        jefe.Cambiar(Villano.Estado.Quieto);
        Llego = true;

        // Mira hacia la jaula y va levantando la cabeza conforme subes.
        while (true)
        {
            var hacia = Plano(centroJaula - jefe.transform.position);
            if (hacia.sqrMagnitude > 0.01f)
                jefe.transform.rotation = Quaternion.Slerp(jefe.transform.rotation, Quaternion.LookRotation(hacia), 1f - Mathf.Exp(-4f * Time.deltaTime));
            var j = JugadorPrimeraPersona.Instancia;
            float objetivo = 0f;
            if (j != null)
            {
                var d = j.transform.position + Vector3.up * 1.2f - Cabeza;
                objetivo = Mathf.Clamp(Mathf.Atan2(d.y, Mathf.Max(0.3f, Plano(d).magnitude)) * Mathf.Rad2Deg, 0f, 55f);
            }
            jefe.mirarArriba = Mathf.MoveTowards(jefe.mirarArriba, objetivo, 35f * Time.deltaTime);
            yield return null;
        }
    }

    // ---------- El puente y el NavMesh ----------

    bool BuscarPuente()
    {
        bool hay = false;
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (r.gameObject.scene != gameObject.scene || !r.name.StartsWith("Puente_Tablas")) continue;
            if (!hay) { puente = r.bounds; hay = true; } else puente.Encapsulate(r.bounds);
        }
        if (!hay) return false;
        // El puente es largo en un eje; el inicio es la punta mas cerca de la escalera.
        ejePuente = puente.size.x > puente.size.z ? Vector3.right : Vector3.forward;
        float medio = Mathf.Max(puente.extents.x, puente.extents.z);
        var a = puente.center - ejePuente * medio;
        var b = puente.center + ejePuente * medio;
        var esc = FindFirstObjectByType<Escalera>();
        var pie = esc != null ? esc.Punto(0f) : a;
        bool aMasCerca = Plano(a - pie).sqrMagnitude < Plano(b - pie).sqrMagnitude;
        inicioPuente = aMasCerca ? a : b;
        if (!aMasCerca) ejePuente = -ejePuente;
        return true;
    }

    bool SobreElPuente(Vector3 p)
    {
        var b = puente;
        b.Expand(new Vector3(0.4f, 0f, 0.4f));
        if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z) return false;
        if (Mathf.Abs(p.y - puente.max.y) > 1.5f) return false;
        return Vector3.Dot(Plano(p - inicioPuente), ejePuente) >= metrosSobreElPuente;
    }

    int ConstruirNavMesh()
    {
        var jugador = JugadorPrimeraPersona.Instancia;
        var zona = puente;
        var esc = FindFirstObjectByType<Escalera>();
        if (esc != null) zona.Encapsulate(esc.Punto(0f));
        Bounds jaula = default;
        bool hayJaula = false;
        if (elevador != null && elevador.jaula != null)
        {
            var r = elevador.jaula.GetComponent<Renderer>();
            jaula = r != null ? r.bounds : new Bounds(elevador.jaula.position, new Vector3(1.8f, 0.1f, 1.8f));
            hayJaula = true;
            zona.Encapsulate(jaula);
        }
        zona.Expand(new Vector3(14f, 8f, 14f));

        var fuentes = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(zona, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), fuentes);
        fuentes.RemoveAll(f =>
        {
            var c = f.component as Collider;
            if (c == null) return false;
            if (c.isTrigger) return true;
            if (c.gameObject.scene != gameObject.scene) return true;
            var tr = c.transform;
            if (jugador != null && tr.IsChildOf(jugador.transform)) return true;
            if (elevador != null && elevador.jaula != null && tr.IsChildOf(elevador.jaula)) return true;
            // El agua no se pisa.
            for (var x = tr; x != null; x = x.parent)
                if (x.name.StartsWith("Agua") || x.name.StartsWith("Cascada") || x.name == "AGUA") return true;
            return false;
        });
        // No se mete a la jaula del elevador.
        if (hayJaula)
            fuentes.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.ModifierBox,
                transform = Matrix4x4.TRS(jaula.center, Quaternion.identity, Vector3.one),
                size = new Vector3(jaula.size.x + 0.6f, 6f, jaula.size.z + 0.6f),
                area = 1,
            });

        var ajustes = NavMesh.GetSettingsByID(0);
        ajustes.agentRadius = 0.35f;
        ajustes.agentHeight = 1.8f;
        ajustes.agentClimb = 0.45f;
        ajustes.agentSlope = 45f;
        ajustes.overrideVoxelSize = true;
        ajustes.voxelSize = 0.12f;
        var datos = NavMeshBuilder.BuildNavMeshData(ajustes, fuentes, zona, Vector3.zero, Quaternion.identity);
        if (datos == null) return 0;
        nav = NavMesh.AddNavMeshData(datos);
        return NavMesh.CalculateTriangulation().indices.Length / 3;
    }

    static Vector3 Plano(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
