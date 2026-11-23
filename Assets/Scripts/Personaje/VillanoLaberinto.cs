using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// La villana del laberinto (Villana3). La prepara sola la Caceria si la villana esta dentro del laberinto.
///  - Espera afuera de la puerta con llave de la zona 5 hasta que lees el libro del laberinto (y lo cierras).
///  - Luego hace su ronda: recorre toda la espiral hasta la entrada (donde esta el portal), se da la vuelta
///    y regresa a la puerta de la zona 5. Si esa puerta ya no tiene llave, entra al cuarto, se da la vuelta y sigue.
///    Las otras puertas las abre al pasar.
///  - Mientras se acerca, las orillas de la pantalla se ponen rojas: leve, moderado y fuerte.
///  - Si te alcanza (y no estas escondida) te atrapa, igual que los villanos de la aldea.
/// </summary>
public class VillanoLaberinto : MonoBehaviour
{
    public static VillanoLaberinto Instancia { get; private set; }
    /// <summary>Ya se leyo el libro del laberinto (se acuerda aunque salgas y regreses).</summary>
    public static bool Despierta { get; private set; }

    [Tooltip("Velocidad base (se arrastra a jalones: en promedio ~0.88 de esto). El jugador camina a 3 en el laberinto.")]
    public float velocidad = 3.1f;
    [Tooltip("A que distancia te atrapa (metros, en horizontal).")]
    public float distanciaAtrapar = 1.4f;
    [Header("Orillas rojas: crecen poco a poco mientras mas cerca esta")]
    [Tooltip("Distancia CAMINANDO por los pasillos (no en linea recta). A esta distancia o mas el rojo es super bajito. " +
             "Medido en el .blend: puerta zona 5 -> entrada = 64 m caminando (10 m en linea recta).")]
    public float distanciaLejos = 40f;
    [Tooltip("A esta distancia (o menos) el rojo esta al maximo y late (esta a tu lado).")]
    public float distanciaPegada = 1.5f;
    [Range(0f, 1f)] public float rojoMinimo = 0.04f;
    [Range(0f, 1f)] public float rojoMaximo = 0.95f;
    [Tooltip("A menos de esto empieza a latir.")]
    public float distanciaLatido = 3.5f;

    enum Tramo { Esperando, Ronda }
    [Header("Ronda por todo el laberinto")]
    [Tooltip("Separacion entre los puntos que recorre (metros). Mas chico = pasa mas pegado a todos lados.")]
    public float separacionPuntos = 2.5f;
    Tramo tramo = Tramo.Esperando;

    Villano villano;
    Transform laberinto;
    Vector3 centro;
    Puerta puertaZona5;
    readonly List<Puerta> puertas = new List<Puerta>();
    Vector3 puntoPuerta, puntoPortal, puntoCuarto;
    float pausaHasta;
    readonly List<Vector3> puntosRonda = new List<Vector3>();   // pasillos y cuartos
    readonly List<Vector3> puntosCuarto = new List<Vector3>();  // adentro de la zona 5 (solo si no tiene seguro)
    readonly List<Vector3> pendientes = new List<Vector3>();
    Vector3 destino;
    float ultimoAvance, mejorDistancia;
    int vueltas;
    Image rojo;
    float alfaRojo;
    float distanciaCaminando = 999f, proximaMedida;
    NavMeshPath ruta;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void Reiniciar()
    {
        Despierta = false;
        Instancia = null;
    }

    /// <summary>Lo llama la Caceria al cerrar el libro del laberinto.</summary>
    public static void AlLeerLibro()
    {
        if (Despierta) return;
        Despierta = true;
        Debug.Log("[Goldy Pond] Laberinto: se leyo el libro, la villana empieza su ronda.");
        if (Instancia != null) Instancia.Empezar();
    }

    /// <summary>Prepara a la villana que esta en el laberinto "lab". Devuelve false si algo falta.</summary>
    public static bool Preparar(Transform raizVillana, Transform lab, Bounds limites)
    {
        var v = raizVillana.gameObject.AddComponent<VillanoLaberinto>();
        return v.Configurar(lab, limites);
    }

    bool Configurar(Transform lab, Bounds limites)
    {
        Instancia = this;
        laberinto = lab;
        centro = lab.position;

        Transform Hijo(string n) { foreach (var t in lab.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
        var marco5 = Hijo("marco zona 5");
        var puerta5 = Hijo("puerta zona 5");
        var entrada = Hijo("marco entrada");
        foreach (var p in lab.GetComponentsInChildren<Puerta>(true)) puertas.Add(p);
        puertaZona5 = puerta5 != null ? puerta5.GetComponent<Puerta>() : null;

        int triangulos = ConstruirNavMesh(limites);

        // Los tres puntos de la ronda.
        var centroPuerta = Centro(marco5 != null ? marco5 : puerta5);
        var afuera = Plano(centroPuerta - centro);                       // del cuarto hacia el pasillo
        puntoPuerta = Pegar(centroPuerta + afuera * 1.0f);
        puntoCuarto = Pegar(centroPuerta - afuera * 1.6f);
        var centroEntrada = entrada != null ? Centro(entrada) : centro + Vector3.back * 10f;
        puntoPortal = Pegar(centroEntrada + Plano(centroEntrada - centro) * 1.0f);
        GenerarRonda(limites, Vector3.Distance(Plano0(centroPuerta), Plano0(centro)));

        // Villano: mismo andar arrastrado que los de la aldea.
        villano = GetComponent<Villano>();
        if (villano == null) villano = gameObject.AddComponent<Villano>();
        villano.velocidadPersiguiendo = velocidad;
        transform.position = puntoPuerta;
        transform.rotation = Quaternion.LookRotation(afuera);
        float alto = 1.9f;
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); alto = b.size.y; }
        villano.Preparar(alto, centro, 8f);
        if (villano.Agente != null) { villano.Agente.radius = 0.25f; villano.Agente.Warp(puntoPuerta); }

        CrearOrillas();
        Debug.Log($"[Goldy Pond] Laberinto: villana '{name}' lista afuera de la zona 5 (NavMesh {triangulos} triangulos, " +
                  $"puerta zona 5 {(puertaZona5 != null ? "encontrada" : "NO encontrada")}, entrada {(entrada != null ? "encontrada" : "NO encontrada")})" +
                  (Despierta ? " — ya estaba despierta" : " — espera a que leas el libro"));
        if (Despierta) Empezar();
        return marco5 != null || puerta5 != null;
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    void Empezar()
    {
        if (tramo != Tramo.Esperando) return;
        tramo = Tramo.Ronda;
        Debug.Log($"[Goldy Pond] Laberinto: la villana recorre TODO el laberinto ({puntosRonda.Count} puntos + {puntosCuarto.Count} en la zona 5).");
        SiguientePunto();
    }

    /// <summary>Reparte puntos por todo el piso del laberinto a los que la villana puede llegar caminando.</summary>
    void GenerarRonda(Bounds limites, float radioCuarto)
    {
        puntosRonda.Clear(); puntosCuarto.Clear();
        float piso = puntoPuerta.y;
        var ruta = new NavMeshPath();
        var todos = new List<Vector3>();
        float paso = Mathf.Max(1.2f, separacionPuntos);
        for (float x = limites.min.x + paso * 0.5f; x <= limites.max.x; x += paso)
            for (float z = limites.min.z + paso * 0.5f; z <= limites.max.z; z += paso)
            {
                var p = new Vector3(x, piso + 0.2f, z);
                if (!NavMesh.SamplePosition(p, out var hit, paso * 0.6f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(hit.position.y - piso) > 0.6f) continue;          // nada de techos
                bool repetido = false;
                foreach (var t in todos) if ((t - hit.position).sqrMagnitude < paso * paso * 0.45f) { repetido = true; break; }
                if (repetido) continue;
                if (!NavMesh.CalculatePath(puntoPuerta, hit.position, NavMesh.AllAreas, ruta) || ruta.status != NavMeshPathStatus.PathComplete) continue;
                todos.Add(hit.position);
                if (Vector3.Distance(Plano0(hit.position), Plano0(centro)) < radioCuarto * 0.92f) puntosCuarto.Add(hit.position);
                else puntosRonda.Add(hit.position);
            }
        if (puntosRonda.Count == 0) { puntosRonda.Add(puntoPortal); puntosRonda.Add(puntoPuerta); }
    }

    static Vector3 Plano0(Vector3 v) { v.y = 0f; return v; }

    /// <summary>Va al punto pendiente mas cercano caminando (a veces al segundo, para no ser igual cada vuelta).</summary>
    void SiguientePunto()
    {
        if (pendientes.Count == 0)
        {
            pendientes.AddRange(puntosRonda);
            if (PuertaAbierta()) pendientes.AddRange(puntosCuarto);
            if (vueltas++ > 0) Debug.Log($"[Goldy Pond] Laberinto: la villana empieza la vuelta {vueltas}.");
        }
        var ruta = new NavMeshPath();
        int mejor = -1, segundo = -1; float dMejor = float.MaxValue, dSegundo = float.MaxValue;
        for (int i = 0; i < pendientes.Count; i++)
        {
            float d = NavMesh.CalculatePath(transform.position, pendientes[i], NavMesh.AllAreas, ruta) && ruta.status == NavMeshPathStatus.PathComplete
                ? Largo(ruta) : Vector3.Distance(transform.position, pendientes[i]) * 4f;
            if (d < dMejor) { segundo = mejor; dSegundo = dMejor; mejor = i; dMejor = d; }
            else if (d < dSegundo) { segundo = i; dSegundo = d; }
        }
        int elegido = segundo >= 0 && dSegundo < dMejor + 3f && Random.value < 0.3f ? segundo : mejor;
        destino = pendientes[elegido];
        pendientes.RemoveAt(elegido);
        villano.IrA(destino);
        ultimoAvance = Time.time;
        mejorDistancia = float.MaxValue;
    }

    static float Largo(NavMeshPath ruta)
    {
        float l = 0f; var c = ruta.corners;
        for (int i = 1; i < c.Length; i++) l += Vector3.Distance(c[i - 1], c[i]);
        return l;
    }

    void Update()
    {
        var jugador = JugadorPrimeraPersona.Instancia;
        ActualizarOrillas(jugador);

        // ¿Te alcanzo? (aunque todavia este esperando: si estas pegada a ella y no escondida, te agarra)
        if (jugador != null && !jugador.Oculto && villano != null && villano.EstadoActual != Villano.Estado.Quieto)
        {
            var d = jugador.transform.position - transform.position;
            if (new Vector2(d.x, d.z).magnitude < distanciaAtrapar && Mathf.Abs(d.y) < 2f)
            {
                alfaRojo = 0f;
                if (rojo != null) rojo.enabled = false;
                Caceria.Instancia?.Atrapar(villano);
            }
        }

        if (tramo == Tramo.Esperando || villano == null || villano.Agente == null || !villano.Agente.isOnNavMesh) return;
        if (villano.EstadoActual == Villano.Estado.Quieto) return; // ya atrapo a alguien

        AbrirPuertasCercanas();

        // ¿Llego? Una pausita y al siguiente punto. Si se atora 5 s, se lo salta.
        var a = villano.Agente;
        if (!a.pathPending)
        {
            if (a.remainingDistance < mejorDistancia - 0.3f) { mejorDistancia = a.remainingDistance; ultimoAvance = Time.time; }
            bool llego = a.remainingDistance < 0.6f;
            if (llego)
            {
                if (pausaHasta <= 0f) pausaHasta = Time.time + 0.3f;
                if (Time.time >= pausaHasta) { pausaHasta = 0f; SiguientePunto(); }
            }
            else if (Time.time - ultimoAvance > 5f) SiguientePunto();
        }

    }

    bool PuertaAbierta() => puertaZona5 == null || !puertaZona5.ConSeguro;

    /// <summary>Las puertas del laberinto (sin llave) se abren solas cuando la villana llega a ellas.</summary>
    void AbrirPuertasCercanas()
    {
        foreach (var p in puertas)
        {
            if (p == null || p.Abierta || p.ConSeguro || p.Moviendose) continue;
            var c = Centro(p.transform);
            var d = c - transform.position; d.y = 0f;
            if (d.magnitude < 1.7f) p.Fijar(true, transform.position);
        }
    }

    // ---------- NavMesh del laberinto ----------

    int ConstruirNavMesh(Bounds limites)
    {
        var zona = limites;
        zona.Expand(new Vector3(6f, 2f, 6f));
        var fuentes = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(zona, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), fuentes);
        var jugador = JugadorPrimeraPersona.Instancia;
        // Fuera: triggers, el jugador, la villana y las puertas (la villana las abre al pasar).
        fuentes.RemoveAll(f =>
        {
            var c = f.component as Collider;
            if (c == null) return false;
            if (c.isTrigger) return true;
            if (jugador != null && c.transform.IsChildOf(jugador.transform)) return true;
            if (c.transform.IsChildOf(transform)) return true;
            if (c.GetComponentInParent<Puerta>() != null) return true;
            return false;
        });
        var ajustes = NavMesh.GetSettingsByID(0);
        ajustes.agentRadius = 0.25f;
        ajustes.agentHeight = 1.8f;
        ajustes.agentClimb = 0.3f;
        ajustes.agentSlope = 30f;
        ajustes.overrideVoxelSize = true;
        ajustes.voxelSize = 0.08f;
        var datos = NavMeshBuilder.BuildNavMeshData(ajustes, fuentes, zona, Vector3.zero, Quaternion.identity);
        if (datos == null) { Debug.LogWarning("[Goldy Pond] Laberinto: no se pudo armar el NavMesh."); return 0; }
        NavMesh.AddNavMeshData(datos);
        return NavMesh.CalculateTriangulation().indices.Length / 3;
    }

    static Vector3 Pegar(Vector3 p) => NavMesh.SamplePosition(p, out var hit, 2.5f, NavMesh.AllAreas) ? hit.position : p;
    static Vector3 Plano(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }

    static Vector3 Centro(Transform t)
    {
        if (t == null) return Vector3.zero;
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return t.position;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return new Vector3(b.center.x, b.min.y, b.center.z);
    }

    // ---------- Orillas rojas (crecen poco a poco con la cercania) ----------

    void CrearOrillas()
    {
        var go = new GameObject("Orillas rojas (laberinto)", typeof(Canvas));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 650;
        var img = new GameObject("Rojo", typeof(RectTransform), typeof(Image));
        img.transform.SetParent(go.transform, false);
        var rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rojo = img.GetComponent<Image>();
        rojo.raycastTarget = false;
        rojo.sprite = Caceria.SpriteOrillasRojas();
        rojo.color = new Color(1f, 1f, 1f, 0f);
        rojo.enabled = false;
    }

    /// <summary>
    /// Distancia caminando por los pasillos (ruta del NavMesh) de la villana al jugador.
    /// Si estas escondida (armario, cama) usa el piso mas cercano a ti.
    /// </summary>
    float MedirCaminando(Vector3 jugadorPos)
    {
        float recta = Vector3.Distance(jugadorPos, transform.position);
        if (recta < 2f) return recta;
        if (ruta == null) ruta = new NavMeshPath();
        if (!NavMesh.SamplePosition(jugadorPos, out var hitJ, 2.5f, NavMesh.AllAreas)) return recta * 3f;
        if (!NavMesh.SamplePosition(transform.position, out var hitV, 2.5f, NavMesh.AllAreas)) return recta * 3f;
        if (!NavMesh.CalculatePath(hitV.position, hitJ.position, NavMesh.AllAreas, ruta) || ruta.corners.Length < 2)
            return recta * 3f;
        float largo = 0f;
        var c = ruta.corners;
        for (int i = 1; i < c.Length; i++) largo += Vector3.Distance(c[i - 1], c[i]);
        if (ruta.status == NavMeshPathStatus.PathPartial) largo += Vector3.Distance(c[c.Length - 1], hitJ.position) * 3f;
        return Mathf.Max(largo, recta);
    }

    void ActualizarOrillas(JugadorPrimeraPersona jugador)
    {
        if (rojo == null) return;
        float objetivo = 0f;
        // Se ve en todo el laberinto (aunque estes escondida); solo se quita si ya te atrapo (Quieto).
        if (jugador != null && villano != null && villano.EstadoActual != Villano.Estado.Quieto
            && LaberintoAutomatico.Contiene(jugador.transform.position))
        {
            if (Time.time >= proximaMedida) { proximaMedida = Time.time + 0.2f; distanciaCaminando = MedirCaminando(jugador.transform.position); }
            float d = distanciaCaminando;
            // 0 = lejos, 1 = pegada a ti. La curva (al cubo) lo deja bajito casi todo el camino y crece fuerte al final.
            float cerca = 1f - Mathf.InverseLerp(distanciaPegada, distanciaLejos, d);
            objetivo = Mathf.Lerp(rojoMinimo, rojoMaximo, cerca * cerca * cerca);
            if (d < distanciaLatido)
            {
                float fuerzaLatido = 1f - Mathf.InverseLerp(distanciaPegada, distanciaLatido, d);
                float rapidez = Mathf.Lerp(5f, 10f, fuerzaLatido);
                objetivo = Mathf.Clamp01(objetivo - 0.12f * fuerzaLatido * (0.5f + 0.5f * Mathf.Sin(Time.time * rapidez)));
            }
        }
        // Sube y baja suave (no de golpe).
        alfaRojo = Mathf.Lerp(alfaRojo, objetivo, 1f - Mathf.Exp(-4f * Time.deltaTime));
        rojo.enabled = alfaRojo > 0.005f;
        rojo.color = new Color(1f, 1f, 1f, alfaRojo);
    }
}
