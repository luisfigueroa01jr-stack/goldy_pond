using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Un villano de la cacería (Boss, Villana1, Villana2, Villana3). Lo agrega solo la Caceria al darle Play:
/// no hay que ponerlo a mano.
///  - Esperando: quieto detrás de la reja hasta que empieza la cacería.
///  - Persiguiendo: camina (lento) hacia el jugador.
///  - Deambulando: el jugador está a salvo (en una casa, el molino o el laberinto): camina por Goldy Pond.
/// Camina con NavMeshAgent sobre el NavMesh que arma la Caceria, que no deja pasar a las casas ni al molino.
/// Si llega junto al jugador, lo atrapa (la Caceria hace el final).
/// </summary>
public class Villano : MonoBehaviour
{
    public enum Estado { Esperando, Persiguiendo, Deambulando, Regresando, Patrullando, Quieto }

    /// <summary>
    /// Como persigue (para que no vayan todos en fila y te acorralen):
    /// Empuja = directo detras de ti. Rodea = se abre por un costado. Corta = se adelanta a donde huyes.
    /// Intercepta = va a donde vas a estar en unos segundos.
    /// </summary>
    public enum Rol { Empuja, Rodea, Corta, Intercepta }
    [Header("Persecucion")]
    public Rol rol = Rol.Empuja;
    [Tooltip("Para 'Rodea': 1 = por la derecha, -1 = por la izquierda.")]
    public int lado = 1;

    [Tooltip("Velocidad persiguiendo (el jugador camina a 3 y corre a 6: corriendo te escapas facil).")]
    public float velocidadPersiguiendo = 3.75f;
    [Tooltip("Velocidad paseando por la aldea cuando el jugador está escondido.")]
    public float velocidadDeambulando = 1.5f;

    [Header("Arrastrarse")]
    [Tooltip("Segundos de cada jalon (avanza rapido, frena, y otra vez).")]
    public float duracionJalon = 0.85f;
    [Tooltip("Que tanto se inclina hacia adelante al arrastrarse (grados).")]
    public float inclinacion = 14f;
    [Tooltip("A qué distancia (metros, en horizontal) atrapa al jugador.")]
    public float distanciaAtrapar = 1.1f;

    [Tooltip("Grados que levanta la cabeza (el jefe viendo subir el elevador).")]
    public float mirarArriba;

    /// <summary>Si esta puesto y regresa false, aunque te alcance no te atrapa (ej. dentro del molino, ya en el elevador).</summary>
    public System.Func<bool> puedeAtrapar;

    public Estado EstadoActual { get; private set; } = Estado.Esperando;
    public float Altura { get; private set; } = 1.9f;

    NavMeshAgent agente;
    Transform modelo;          // lo que se balancea al caminar
    Vector3 posModelo;
    Quaternion rotModelo;
    float siguienteRuta;
    float fase;

    /// <summary>La Caceria lo prepara cuando ya existe el NavMesh.</summary>
    public void Preparar(float altura, Vector3 centroAldea, float radioAldea)
    {
        Altura = altura;
        this.centroAldea = centroAldea;
        this.radioAldea = radioAldea;
        fase = Random.value * 10f;

        modelo = transform.childCount > 0 ? transform.GetChild(0) : null;
        if (modelo != null) { posModelo = modelo.localPosition; rotModelo = modelo.localRotation; }

        // Pegarlo al NavMesh (si quedó un poco arriba o abajo del suelo).
        if (NavMesh.SamplePosition(transform.position, out var hit, 6f, NavMesh.AllAreas))
            transform.position = hit.position;

        agente = GetComponent<NavMeshAgent>();
        if (agente == null) agente = gameObject.AddComponent<NavMeshAgent>();
        agente.radius = 0.35f;
        agente.height = altura;
        agente.baseOffset = 0f;
        agente.speed = velocidadPersiguiendo;
        agente.angularSpeed = 240f;
        agente.acceleration = 14f;
        agente.updateRotation = false; // se voltea suave en Girar()
        agente.stoppingDistance = 0.5f;
        agente.autoBraking = true;
        agente.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        agente.avoidancePriority = 40 + Random.Range(0, 20);
        agente.isStopped = true;
        lugar = transform.position;
        rotLugar = transform.rotation;
    }

    Vector3 lugar;          // donde espera detras de la reja
    Quaternion rotLugar;

    /// <summary>Ya llego a su lugar detras de la reja (al retirarse).</summary>
    public bool EnSuLugar => agente == null || !agente.isOnNavMesh
        || Vector3.ProjectOnPlane(transform.position - lugar, Vector3.up).magnitude < 0.6f;

    /// <summary>Lo pone en su lugar detras de la reja, quieto y mirando a la aldea.</summary>
    public void VolverASuLugar()
    {
        if (agente != null && agente.isOnNavMesh) agente.Warp(lugar);
        else transform.position = lugar;
        transform.rotation = rotLugar;
        Cambiar(Estado.Esperando);
        if (modelo != null) { modelo.localPosition = posModelo; modelo.localRotation = rotModelo; }
    }

    Vector3 centroAldea;
    float radioAldea;

    public NavMeshAgent Agente => agente;

    /// <summary>Donde se pasea cuando estas escondida (el jefe ronda cerca de donde te metiste).</summary>
    public void CentrarPaseo(Vector3 centro, float radio)
    {
        centroAldea = centro;
        radioAldea = radio;
        siguienteRuta = 0f;
        if (EstadoActual == Estado.Deambulando && agente != null && agente.isOnNavMesh) agente.ResetPath();
    }

    /// <summary>Patrulla (la villana del laberinto): camina hacia "destino" a velocidadPersiguiendo.</summary>
    public void IrA(Vector3 destino)
    {
        Cambiar(Estado.Patrullando);
        if (agente != null && agente.isOnNavMesh) { agente.isStopped = false; agente.SetDestination(destino); }
    }

    public void Cambiar(Estado nuevo)
    {
        if (agente == null || !agente.isOnNavMesh || EstadoActual == nuevo) { EstadoActual = nuevo; return; }
        EstadoActual = nuevo;
        siguienteRuta = 0f;
        switch (nuevo)
        {
            case Estado.Persiguiendo:
                agente.isStopped = false;
                break;
            case Estado.Deambulando:
                agente.isStopped = false;
                agente.ResetPath();
                break;
            case Estado.Regresando:
                agente.isStopped = false;
                agente.SetDestination(lugar);
                break;
            default:
                agente.isStopped = true;
                agente.ResetPath();
                break;
        }
    }

    void Update()
    {
        if (agente == null || !agente.isOnNavMesh) return;
        var jugador = JugadorPrimeraPersona.Instancia;

        if (EstadoActual == Estado.Persiguiendo && jugador != null)
        {
            if (Time.time >= siguienteRuta)
            {
                siguienteRuta = Time.time + Random.Range(0.25f, 0.45f);
                var c = Caceria.Instancia;
                var destino = c != null ? c.ObjetivoPara(this) : jugador.transform.position;
                if (c == null && NavMesh.SamplePosition(destino, out var cerca, 3f, NavMesh.AllAreas)) destino = cerca.position;
                agente.SetDestination(destino);
            }
            var diferencia = jugador.transform.position - transform.position;
            if (new Vector2(diferencia.x, diferencia.z).magnitude < distanciaAtrapar && Mathf.Abs(diferencia.y) < 2.5f
                && (puedeAtrapar == null || puedeAtrapar()))
            {
                if (Caceria.Instancia != null) Caceria.Instancia.Atrapar(this);
                else Caceria.AtraparFuera(this); // en el subterraneo no hay Caceria
            }
        }
        else if (EstadoActual == Estado.Deambulando)
        {
            if (Time.time >= siguienteRuta && (!agente.hasPath || agente.remainingDistance < 1f))
            {
                siguienteRuta = Time.time + Random.Range(1.5f, 4f); // se queda un ratito y luego a otro lado
                var punto = centroAldea + Random.insideUnitSphere * radioAldea;
                if (NavMesh.SamplePosition(punto, out var hit, 5f, NavMesh.AllAreas))
                    agente.SetDestination(hit.position);
            }
        }

        Arrastrarse();
        Girar();
        Postura();
    }

    float jalon;          // 0..1 dentro de cada jalon
    float empuje;         // que tan fuerte esta jalando ahora (0 = deslizandose, 1 = jalon)
    float inclinacionActual, ladeoActual;
    float yawAnterior;

    /// <summary>
    /// No caminan: se arrastran a jalones. Cada jalon empieza rapido (se impulsan) y se va frenando
    /// (se deslizan), como si se jalaran del suelo. La velocidad promedio queda cerca de la velocidad elegida.
    /// </summary>
    void Arrastrarse()
    {
        bool persigue = EstadoActual == Estado.Persiguiendo || EstadoActual == Estado.Patrullando;
        float baseVel = persigue ? velocidadPersiguiendo
                      : EstadoActual == Estado.Regresando ? velocidadDeambulando * 1.4f
                      : velocidadDeambulando;
        float duracion = persigue ? duracionJalon : duracionJalon * 1.5f;
        jalon += Time.deltaTime / duracion;
        if (jalon >= 1f) jalon -= 1f;
        // Impulso fuerte al inicio del jalon y luego se apaga.
        empuje = Mathf.Pow(1f - jalon, 2.2f);
        agente.speed = baseVel * Mathf.Lerp(0.55f, 1.6f, empuje);
    }

    /// <summary>Se voltea suave hacia donde va (no de golpe).</summary>
    void Girar()
    {
        var v = Vector3.ProjectOnPlane(agente.velocity, Vector3.up);
        if (v.sqrMagnitude < 0.04f) return;
        var objetivo = Quaternion.LookRotation(v, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, objetivo, 1f - Mathf.Exp(-6f * Time.deltaTime));
    }

    /// <summary>
    /// Cuerpo agachado hacia adelante (mas en cada jalon), ladeado hacia dentro de las curvas
    /// y meciendose despacio. Sin saltitos.
    /// </summary>
    void Postura()
    {
        if (modelo == null) return;
        float rapidez = Mathf.Clamp01(agente.velocity.magnitude / Mathf.Max(0.1f, velocidadPersiguiendo));
        float yaw = transform.eulerAngles.y;
        float giro = Mathf.DeltaAngle(yawAnterior, yaw) / Mathf.Max(Time.deltaTime, 0.0001f); // grados/s
        yawAnterior = yaw;

        float k = 1f - Mathf.Exp(-8f * Time.deltaTime);
        inclinacionActual = Mathf.Lerp(inclinacionActual, rapidez * (inclinacion * 0.6f + inclinacion * 0.6f * empuje), k);
        ladeoActual = Mathf.Lerp(ladeoActual, Mathf.Clamp(-giro * 0.06f, -12f, 12f) * rapidez, k);

        fase += Time.deltaTime;
        float mecer = Mathf.Sin(fase * 1.7f) * 2.5f;                 // va y viene despacio
        float respirar = Mathf.Sin(fase * 1.1f) * 0.012f * Altura;    // sube y baja muy poquito
        float hundir = -0.03f * Altura * empuje * rapidez;            // se pega al suelo al jalar

        modelo.localPosition = posModelo + Vector3.up * (respirar + hundir);
        // inclinarse hacia adelante (+X), ladearse (Z) y mecerse (Y), sobre la rotacion original del modelo
        modelo.localRotation = Quaternion.Euler(inclinacionActual - mirarArriba, mecer, ladeoActual + mecer * 0.4f) * rotModelo;
    }

    /// <summary>Voltea a ver al jugador (cuando lo atrapa).</summary>
    public void MirarHacia(Vector3 punto)
    {
        var d = Vector3.ProjectOnPlane(punto - transform.position, Vector3.up);
        if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        if (modelo != null) { modelo.localPosition = posModelo; modelo.localRotation = rotModelo; }
    }
}
