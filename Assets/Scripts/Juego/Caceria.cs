using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// La caceria de la Aldea. Se arma sola al darle Play si en la escena hay villanos
// (objetos llamados Boss, Villana1, Villana2, Villana3, o "Villana1 (1)"...; sirven el prefab o el .fbx):
// 1. Los villanos esperan detras de la reja (Reja_Entrada).
// 2. El jugador lee el libro de la plaza (Libro_de_las_Reglas) y lo cierra: la reja sube
// y los villanos empiezan a perseguirlo, caminando lento.
// 3. Si el jugador entra a una casa, al molino o al laberinto, lo dejan de perseguir y se pasean por Goldy Pond.
// Cuando sale, lo sienten y lo vuelven a seguir. Los villanos NO entran a las casas ni al molino.
// 4. Si uno lo alcanza: la camara voltea a ver a ese villano, las orillas de la pantalla se ponen rojas,
// se funde a negro y se regresa al menu principal (partida nueva).
// CACERIA INFINITA: al regresar del subterraneo (ya se pueden mover las palancas) se acaba el ciclo caceria/descanso.
// - Solo persigue el jefe (Boss), sin descanso, lo mas rapido que se puede escapar (ver velocidadJefeInfinita).
// Las casas siguen siendo refugio, pero el jefe se queda rondando cerca de donde te metiste.
// - Al activar la 3a palanca todo se congela y retumba (se abrio el elevador) y sale "VE AL SUBTERRANEO".
// - Desde ahi el jefe puede entrar al molino (abre la puerta), pero dentro del molino no te puede agarrar.
public class Caceria : MonoBehaviour
{
    public static Caceria Instancia { get; private set; }

    // true desde que se cerro el libro de la plaza (se acuerda aunque bajes al subterraneo y regreses).
    public static bool Iniciada => FaseActual != Fase.Antes;

    // Antes del libro / caceria (te persiguen) / retirada (regresan a la reja) / descanso (paseas tranquila).
    public enum Fase { Antes, Caceria, Retirada, Descanso }
    public static Fase FaseActual { get; private set; }
    // Segundos que le quedan a la caceria o al descanso.
    public static float Restante { get; private set; }
    // Que caceria va (1 = la primera).
    public static int NumeroCaceria { get; private set; }

    // La caceria infinita (empieza al regresar del subterraneo): solo el jefe, sin descanso.
    public static bool Infinita { get; private set; }
    // Ya retumbo con la 3a palanca: el elevador del subterraneo esta abierto y el jefe puede entrar al molino.
    public static bool ElevadorAbierto { get; private set; }

    [Header("Caceria infinita (al regresar del subterraneo)")]
    [Tooltip("Velocidad base del jefe. Se arrastra a jalones y en la practica avanza a ~0.76 de esto (4.4 -> ~3.35 m/s, medido). " +
             "El jugador camina a 3 y corre a 6 con estamina (sostenido ~3.5-4 m/s): caminando te alcanza, " +
             "corriendo sin cuidar la estamina tambien; corriendo a ratos (sin vaciarla) te escapas despacio.")]
    public float velocidadJefeInfinita = 4.4f;
    [Tooltip("Velocidad del jefe rondando mientras estas escondida.")]
    public float paseoJefeInfinita = 2.2f;
    [Tooltip("Mientras estas escondida, el jefe ronda a esta distancia de donde te metiste.")]
    public float radioAcecho = 14f;
    [Tooltip("Segundos que dura el retumbe al activar la ultima palanca.")]
    public float duracionRetumbe = 3.5f;

    [Header("Tiempos (segundos)")]
    [Tooltip("Duracion de la primera caceria. Corre siempre (tambien en casas, molino y subterraneo); entrar al laberinto la termina.")]
    public float primeraCaceria = 90f;
    [Tooltip("Cuanto mas dura cada caceria siguiente.")]
    public float aumentoPorCaceria = 30f;
    [Tooltip("La caceria mas larga.")]
    public float caceriaMaxima = 180f;
    [Tooltip("Duracion del descanso (sin villanos).")]
    public float descanso = 60f;

    [Header("Musica de la caceria")]
    [Tooltip("Si esta vacio usa Assets/Resources/Musica/Caceria (mp3, ogg o wav).")]
    public AudioClip musicaCaceria;

    // La reja ya subio lo suficiente y los villanos estan saliendo.
    public bool Liberados => liberados;

    private AudioSource musica;
    private RelojCaceria reloj;
    private Coroutine fundidoMusica;

    private const string ESCENA_INICIO = "MenuPrincipal";
    private static readonly Regex PatronVillano = new Regex(@"^(Boss|Villana\d)( \(\d+\))?$");
    private static readonly Regex PatronCasa = new Regex(@"^Casa_\d+$");

    private class Refugio { public string nombre; public Bounds limites; public bool bloquearPaso; }

    private readonly List<Villano> villanos = new List<Villano>();
    private readonly List<Refugio> refugios = new List<Refugio>();
    private Transform reja;
    private Vector3 rejaCerrada;
    private float altoReja;
    private Vector3 centroAldea;
    private bool liberados;     // ya salieron (la reja ya subio lo suficiente)
    private bool atrapando;
    private bool aSalvoAntes;
    // Entraste al laberinto en plena caceria: se termina y al salir empieza el descanso completo (60 s).
    private bool descansoCompletoAlSalir;
    private bool estabaEnLaberinto;
    // Portal de la aldea al laberinto: solo aparece en caceria y despues de poner el primer engranaje.
    private GameObject portalAldea;
    // Para que el tiempo siga corriendo mientras estas en el subterraneo (otra escena).
    private static float marcaSalida = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void reiniciar()
    {
        FaseActual = Fase.Antes;
        Restante = 0f;
        NumeroCaceria = 0;
        marcaSalida = -1f;
        Instancia = null;
        Infinita = false;
        ElevadorAbierto = false;
        atrapandoFuera = false;
        JefeSubterraneo.reiniciar();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void alIniciarJuego()
    {
        SceneManager.sceneLoaded -= alCargarEscena;
        SceneManager.sceneLoaded += alCargarEscena;
        PanelLectura.Cerrado -= alCerrarLectura;
        PanelLectura.Cerrado += alCerrarLectura;
        Palanca.AlActivar -= alActivarPalanca;
        Palanca.AlActivar += alActivarPalanca;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            revisar(SceneManager.GetSceneAt(i));
        }
    }

    private static void alCargarEscena(Scene escena, LoadSceneMode modo)
    {
        revisar(escena);
    }

    private static void revisar(Scene escena)
    {
        if (!escena.isLoaded || Instancia != null && Instancia.gameObject.scene == escena)
        {
            return;
        }

        List<Transform> encontrados = buscarVillanos(escena);
        if (encontrados.Count == 0)
        {
            return;
        }

        GameObject go = new GameObject("Caceria (automatica)");
        SceneManager.MoveGameObjectToScene(go, escena);
        go.AddComponent<Caceria>().pendientes = encontrados;
    }

    private static List<Transform> buscarVillanos(Scene escena)
    {
        List<Transform> lista = new List<Transform>();
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(false))
            {
                if (PatronVillano.IsMatch(t.name) && !lista.Exists(v => t.IsChildOf(v)))
                {
                    lista.Add(t);
                }
            }
        }

        return lista;
    }

    private static void alCerrarLectura()
    {
        Libro libro = Libro.UltimoLeido;
        Libro.UltimoLeido = null;
        if (libro == null)
        {
            return;
        }

        // El libro del laberinto despierta a la villana del laberinto.
        if (!esLibroDeLaPlaza(libro.transform) && LaberintoAutomatico.contiene(libro.transform.position))
        {
            VillanoLaberinto.alLeerLibro();
            return;
        }

        if (Iniciada || Instancia == null)
        {
            return;
        }

        if (!esLibroDeLaPlaza(libro.transform))
        {
            return;
        }

        Instancia.empezar();
    }

    // true en la Aldea mientras no se ha leido el libro de la plaza: solo se puede usar ese libro.
    public static bool BloqueaInteracciones => Instancia != null && !Iniciada;

    public static bool esLibroDeLaPlaza(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.name.StartsWith("Libro_de_las_Reglas"))
            {
                return true;
            }
        }

        return false;
    }

    private List<Transform> pendientes;

    private void Awake()
    {
        Instancia = this;
        descontarTiempoFuera();
    }

    private void OnDestroy()
    {
        if (Instancia == this)
        {
            Instancia = null;
        }

        // El NavMesh no se borra solo al cambiar de escena: se quitaria encima del subterraneo.
        if (navInstancia.valid)
        {
            NavMesh.RemoveNavMeshData(navInstancia);
        }

        // Sales de la Aldea (ej. al subterraneo): se guarda cuando, para seguir contando.
        if (!atrapando && Iniciada)
        {
            marcaSalida = Time.time;
        }
    }

    private IEnumerator Start()
    {
        yield return null; // que primero se preparen puertas, recogibles, etc.

        buscarReja();
        buscarRefugios();
        List<Transform> raices = new List<Transform>();
        // La villana que esta dentro del laberinto no es de la caceria de la aldea: hace su ronda alla.
        List<Transform> delLaberinto = new List<Transform>();
        foreach (Transform t in pendientes)
        {
            if (LaberintoAutomatico.raizQueContiene(t.position, out _) != null)
            {
                delLaberinto.Add(t);
            }
            else
            {
                raices.Add(prepararRaiz(t));
            }
        }

        int triangulos = construirNavMesh(raices);
        foreach (Transform t in delLaberinto)
        {
            Transform raiz = prepararRaiz(t);
            Transform lab = LaberintoAutomatico.raizQueContiene(raiz.position, out Bounds limites);
            VillanoLaberinto.preparar(raiz, lab, limites);
        }

        centroAldea = Vector3.zero;
        Libro libro = FindFirstObjectByType<Libro>();
        foreach (Libro l in FindObjectsByType<Libro>(FindObjectsInactive.Exclude))
        {
            if (esLibroDeLaPlaza(l.transform))
            {
                libro = l;
            }
        }

        if (libro != null)
        {
            centroAldea = libro.transform.position;
        }

        // Papeles para acorralar: el Boss empuja; los demas rodean, cortan e interceptan.
        Villano.Rol[] roles = new[] { Villano.Rol.Rodea, Villano.Rol.Corta, Villano.Rol.Intercepta, Villano.Rol.Rodea };
        int siguienteRol = 0, siguienteLado = 1;
        raices.Sort((a, b) => (b.name.StartsWith("Boss") ? 1 : 0) - (a.name.StartsWith("Boss") ? 1 : 0));
        foreach (Transform r in raices)
        {
            Villano v = r.GetComponent<Villano>();
            if (v == null)
            {
                v = r.gameObject.AddComponent<Villano>();
            }

            if (r.name.StartsWith("Boss") && villanos.Count == 0)
            {
                v.rol = Villano.Rol.Empuja;
            }
            else
            {
                v.rol = roles[siguienteRol++ % roles.Length];
                if (v.rol == Villano.Rol.Rodea)
                {
                    v.lado = siguienteLado;
                    siguienteLado = -siguienteLado;
                }
            }

            float alto = altoDe(r);
            if (r.name.StartsWith("Boss"))
            {
                v.velocidadPersiguiendo = 3.4f;
                v.duracionJalon = 1.0f;
                v.distanciaAtrapar = 1.4f;
            }

            v.preparar(alto, centroAldea, 32f);
            villanos.Add(v);
            Debug.Log($"[Goldy Pond] Caceria: {r.name} persigue como '{v.rol}'" + (v.rol == Villano.Rol.Rodea ? (v.lado > 0 ? " (derecha)" : " (izquierda)") : ""));
        }

        Debug.Log($"[Goldy Pond] Caceria lista: {villanos.Count} villanos ({string.Join(", ", raices.ConvertAll(r => r.name))}), " +
                  $"{refugios.Count} refugios, reja {(reja != null ? "encontrada" : "NO encontrada")}, NavMesh con {triangulos} triangulos" +
                  (Iniciada ? " (la caceria ya habia empezado)" : " (empieza al cerrar el libro de la plaza)"));

        prepararMusicaYReloj();
        if (FaseActual == Fase.Caceria)
        {
            tocarMusica(true);
            // Regresaste (ej. del subterraneo) en plena caceria: la reja ya esta arriba y te siguen buscando.
            if (reja != null)
            {
                reja.position = rejaCerrada + Vector3.up * (altoReja + 0.3f);
            }

            liberados = true;
        }

        buscarPortalAldea();

        // Regresaste del subterraneo (ya se pueden mover las palancas): caceria infinita.
        if (Iniciada && EstadoJuego.VisitoSubterraneo)
        {
            empezarInfinita(!Infinita);
        }
    }

    // El tiempo sigue corriendo mientras estas fuera de la Aldea (subterraneo): al regresar se descuenta
    // lo que estuviste fuera, pasando de caceria a descanso (y a la siguiente caceria) si se acabo.
    private void descontarTiempoFuera()
    {
        if (marcaSalida < 0f || !Iniciada || Infinita)
        {
            marcaSalida = -1f;
            return;
        }

        float fuera = Time.time - marcaSalida;
        marcaSalida = -1f;
        if (FaseActual == Fase.Retirada)
        {
            FaseActual = Fase.Descanso;
            Restante = descanso;
        }

        for (int vueltas = 0; fuera > 0f && vueltas < 30; vueltas++)
        {
            if (Restante > fuera)
            {
                Restante -= fuera;
                break;
            }

            fuera -= Restante;
            if (FaseActual == Fase.Caceria)
            {
                FaseActual = Fase.Descanso;
                Restante = descanso;
            }
            else
            {
                NumeroCaceria++;
                FaseActual = Fase.Caceria;
                Restante = duracionCaceria(NumeroCaceria);
            }
        }

        Debug.Log($"[Goldy Pond] Caceria: regresaste a la Aldea; el tiempo siguio corriendo ({FaseActual}, quedan {Restante:0} s).");
    }

    private void buscarPortalAldea()
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t.gameObject.scene != gameObject.scene || t.Find("energia") == null || t.Find("portal") == null)
            {
                continue;
            }

            if (LaberintoAutomatico.contiene(t.position))
            {
                continue; // ese es el de regreso, dentro del laberinto
            }

            portalAldea = t.gameObject;
            break;
        }

        if (portalAldea != null)
        {
            actualizarPortal(true);
        }
        else
        {
            Debug.LogWarning("[Goldy Pond] Caceria: no encontre el portal de la aldea.");
        }
    }

    private static bool hayEngranajePuesto()
    {
        foreach (Mecanismo m in FindObjectsByType<Mecanismo>(FindObjectsInactive.Include))
        {
            if (m.Puestas > 0)
            {
                return true;
            }
        }

        return false;
    }

    private float proximaRevisionPortal;
    private void actualizarPortal(bool forzar = false)
    {
        if (portalAldea == null || (!forzar && Time.time < proximaRevisionPortal))
        {
            return;
        }

        proximaRevisionPortal = Time.time + 0.25f;
        bool ver = FaseActual == Fase.Caceria && hayEngranajePuesto();
        if (portalAldea.activeSelf != ver)
        {
            portalAldea.SetActive(ver);
            Debug.Log(ver ? "[Goldy Pond] Caceria: aparece el portal al laberinto." : "[Goldy Pond] Caceria: el portal al laberinto desaparece.");
        }
    }

    // ------------------- PREPARAR LA ESCENA ------------------------------

    private void buscarReja()
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (t.name.StartsWith("Reja_Entrada") && t.gameObject.scene == gameObject.scene)
            {
                reja = t;
                rejaCerrada = t.position;
                altoReja = obtenerLimites(t).size.y;
                return;
            }
        }

        Debug.LogWarning("[Goldy Pond] Caceria: no encontre Reja_Entrada.");
    }

    private void buscarRefugios()
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (t.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            if (PatronCasa.IsMatch(t.name))
            {
                // La casa por dentro: lo que encierran sus paredes.
                if (limitesDeHijos(t, "Pared", out Bounds b))
                {
                    b.Expand(new Vector3(0.6f, 0f, 0.6f));
                    refugios.Add(new Refugio { nombre = t.name, limites = b, bloquearPaso = true });
                }
            }
            else if (t.name == "Molino")
            {
                if (limitesDeHijos(t, "Piso_Planta_Baja", out Bounds b))
                {
                    b.Expand(new Vector3(1.6f, 0f, 1.6f));
                    b.max = new Vector3(b.max.x, b.max.y + 10f, b.max.z);
                    refugios.Add(new Refugio { nombre = t.name, limites = b, bloquearPaso = true });
                    molino = t;
                    limitesMolino = b;
                    puertasMolino.AddRange(t.GetComponentsInChildren<Puerta>(true));
                }
            }
            else if (t.name.StartsWith("laberinto_colores") && (t.parent == null || !t.parent.name.StartsWith("laberinto_colores")))
            {
                refugios.Add(new Refugio { nombre = "Laberinto", limites = obtenerLimites(t), bloquearPaso = false });
            }
        }
    }

    // Si pusiste el .fbx directo (sin el prefab), lo mete en un objeto vacio que mira hacia donde mira el villano.
    private Transform prepararRaiz(Transform t)
    {
        if (t.Find("Modelo") != null)
        {
            return t;
        }

        Transform raiz = new GameObject(t.name).transform;
        SceneManager.MoveGameObjectToScene(raiz.gameObject, gameObject.scene);
        raiz.position = t.position;
        // Los modelos de Camila miran hacia -Z de su .fbx.
        Vector3 frente = Vector3.ProjectOnPlane(-t.forward, Vector3.up);
        raiz.rotation = frente.sqrMagnitude > 0.001f ? Quaternion.LookRotation(frente) : Quaternion.identity;
        raiz.SetParent(t.parent, true);
        t.SetParent(raiz, true);
        t.name = "Modelo";
        Bounds b = obtenerLimites(raiz);
        CapsuleCollider capsula = raiz.gameObject.AddComponent<CapsuleCollider>();
        capsula.height = b.size.y;
        capsula.radius = Mathf.Min(0.35f, b.size.y * 0.5f);
        capsula.center = raiz.InverseTransformPoint(new Vector3(raiz.position.x, b.center.y, raiz.position.z));
        return raiz;
    }

    private int construirNavMesh(List<Transform> raices)
    {
        raicesNav = raices;
        JugadorPrimeraPersona jugador = JugadorPrimeraPersona.Instancia;

        // Zona: la aldea entera con la muralla y lo que está detrás de la reja.
        Bounds zona = new Bounds(centroAldeaAproximado(raices), Vector3.one);
        foreach (Refugio r in refugios)
        {
            if (r.bloquearPaso)
            {
                zona.Encapsulate(r.limites);
            }
        }

        foreach (Transform r in raices)
        {
            zona.Encapsulate(r.position);
        }

        if (reja != null)
        {
            zona.Encapsulate(rejaCerrada);
        }

        if (jugador != null)
        {
            zona.Encapsulate(jugador.transform.position);
        }

        zona.Expand(new Vector3(40f, 30f, 40f));

        List<NavMeshBuildSource> fuentes = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(zona, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), fuentes);
        // Fuera: triggers, el jugador, los villanos y la reja (la reja se abre despues).
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

            Transform tr = c.transform;
            if (jugador != null && tr.IsChildOf(jugador.transform))
            {
                return true;
            }

            if (reja != null && tr.IsChildOf(reja))
            {
                return true;
            }

            foreach (Transform r in raices)
            {
                if (tr.IsChildOf(r))
                {
                    return true;
                }
            }

            // Ya abierto el elevador, el jefe pasa por la puerta del molino (la abre al llegar).
            if (ElevadorAbierto && molino != null && tr.IsChildOf(molino) && c.GetComponentInParent<Puerta>() != null)
            {
                return true;
            }

            return false;
        });
        // Las casas y el molino: "no caminable", asi los villanos nunca entran.
        foreach (Refugio r in refugios)
        {
            if (!r.bloquearPaso)
            {
                continue;
            }

            if (ElevadorAbierto && r.nombre == "Molino")
            {
                continue; // ya puede entrar al molino
            }

            fuentes.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.ModifierBox,
                transform = Matrix4x4.TRS(r.limites.center, Quaternion.identity, Vector3.one),
                size = r.limites.size + Vector3.up * 2f,
                area = 1, // Not Walkable
            });
        }

        NavMeshBuildSettings ajustes = NavMesh.GetSettingsByID(0);
        ajustes.agentRadius = 0.35f;
        ajustes.agentHeight = 1.8f;
        ajustes.agentClimb = 0.45f;
        ajustes.agentSlope = 45f;
        ajustes.overrideVoxelSize = true;
        ajustes.voxelSize = 0.15f;
        NavMeshData datos = NavMeshBuilder.BuildNavMeshData(ajustes, fuentes, zona, Vector3.zero, Quaternion.identity);
        if (datos == null)
        {
            Debug.LogWarning("[Goldy Pond] Caceria: no se pudo armar el NavMesh.");
            return 0;
        }

        if (navInstancia.valid)
        {
            NavMesh.RemoveNavMeshData(navInstancia);
        }

        navInstancia = NavMesh.AddNavMeshData(datos);
        return NavMesh.CalculateTriangulation().indices.Length / 3;
    }

    private Vector3 centroAldeaAproximado(List<Transform> raices)
    {
        if (refugios.Count > 0)
        {
            return refugios[0].limites.center;
        }

        return raices.Count > 0 ? raices[0].position : Vector3.zero;
    }

    // ------------------- LA CACERIA ------------------------------

    private float duracionCaceria(int numero)
    {
        return Mathf.Min(caceriaMaxima, primeraCaceria + aumentoPorCaceria * (numero - 1));
    }

    private void empezar()
    {
        if (Iniciada)
        {
            return;
        }

        Debug.Log("[Goldy Pond] Caceria: se cerro el libro de la plaza, empieza la primera caceria.");
        NumeroCaceria = 0;
        empezarCaceria();
    }

    private void empezarCaceria()
    {
        NumeroCaceria++;
        FaseActual = Fase.Caceria;
        Restante = duracionCaceria(NumeroCaceria);
        liberados = false;
        aSalvoAntes = false;
        reloj?.mostrarEmpieza(3f);
        tocarMusica(true);
        Debug.Log($"[Goldy Pond] Caceria {NumeroCaceria}: dura {Restante:0} s.");
        if (rejaMoviendose != null)
        {
            StopCoroutine(rejaMoviendose);
        }

        rejaMoviendose = StartCoroutine(moverReja(true));
    }

    private void empezarRetirada()
    {
        FaseActual = Fase.Retirada;
        tiempoRetirada = 0f;
        foreach (Villano v in villanos)
        {
            if (v != null)
            {
                v.cambiar(Villano.Estado.Regresando);
            }
        }

        Notificacion.mostrar("Los villanos se retiran... Tiempo de descanso");
        tocarMusica(false);
        Debug.Log($"[Goldy Pond] Caceria {NumeroCaceria}: termino, los villanos regresan a la reja.");
    }

    private void empezarDescanso()
    {
        foreach (Villano v in villanos)
        {
            if (v != null)
            {
                v.volverASuLugar();
            }
        }

        FaseActual = Fase.Descanso;
        Restante = descanso;
        if (rejaMoviendose != null)
        {
            StopCoroutine(rejaMoviendose);
        }

        rejaMoviendose = StartCoroutine(moverReja(false));
        Debug.Log($"[Goldy Pond] Descanso de {descanso:0} s.");
    }

    private float tiempoRetirada;

    private void prepararMusicaYReloj()
    {
        reloj = RelojCaceria.crear(transform);
        if (musicaCaceria == null)
        {
            musicaCaceria = Resources.Load<AudioClip>("Musica/Caceria");
        }

        musica = gameObject.AddComponent<AudioSource>();
        musica.clip = musicaCaceria;
        musica.loop = true;
        musica.playOnAwake = false;
        musica.spatialBlend = 0f;
        musica.volume = 0f;
        if (musicaCaceria == null)
        {
            Debug.Log("[Goldy Pond] Caceria: no hay musica (pon el archivo en Assets/Resources/Musica/Caceria).");
        }
    }

    private bool musicaActiva;

    // El jugador esta en el laberinto: el ciclo caceria/descanso se pausa y la musica se apaga.
    public static bool JugadorEnLaberinto
    {
        get { JugadorPrimeraPersona j = JugadorPrimeraPersona.Instancia; return j != null && LaberintoAutomatico.contiene(j.transform.position); }
    }

    private void tocarMusica(bool tocar)
    {
        musicaActiva = tocar;
        if (musica == null || musica.clip == null)
        {
            return;
        }

        if (fundidoMusica != null)
        {
            StopCoroutine(fundidoMusica);
        }

        fundidoMusica = StartCoroutine(fundirMusica(tocar));
    }

    private IEnumerator fundirMusica(bool tocar)
    {
        float volumen = ConfiguracionSonido.VolumenJuego;
        if (tocar)
        {
            musica.volume = volumen;
            if (!musica.isPlaying)
            {
                musica.time = 0f;
                musica.Play();
            }

            yield break;
        }

        float desde = musica.volume, t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / 2.5f);
            musica.volume = Mathf.Lerp(desde, 0f, t);
            yield return null;
        }

        musica.Stop();
    }

    private Coroutine rejaMoviendose;

    private IEnumerator moverReja(bool abrir)
    {
        if (reja == null)
        {
            liberados = abrir;
            yield break;
        }

        float arriba = altoReja + 0.3f;
        float desde = reja.position.y - rejaCerrada.y, hasta = abrir ? arriba : 0f;
        if (!abrir)
        {
            // No bajarla encima del jugador.
            JugadorPrimeraPersona j = JugadorPrimeraPersona.Instancia;
            while (j != null && Vector3.ProjectOnPlane(j.transform.position - rejaCerrada, Vector3.up).magnitude < 2.5f)
            {
                yield return null;
            }
        }

        float t = 0f, duracion = 3.5f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / duracion);
            float k = t * t * (3f - 2f * t);
            reja.position = rejaCerrada + Vector3.up * Mathf.Lerp(desde, hasta, k);
            if (abrir && !liberados && k > 0.55f)
            {
                liberados = true; // ya se puede pasar por debajo
            }

            yield return null;
        }
    }

    // Velocidad del jugador (suavizada) para adivinar hacia donde huye.
    private Vector3 velJugador, posJugadorAntes;
    private bool hayPosAntes;

    private void seguirVelocidadJugador(Transform j)
    {
        Vector3 p = j.position;
        if (hayPosAntes && Time.deltaTime > 0f)
        {
            Vector3 v = Vector3.ProjectOnPlane((p - posJugadorAntes) / Time.deltaTime, Vector3.up);
            if (v.magnitude > 12f)
            {
                v = Vector3.zero; // teletransporte (portal, escena)
            }

            velJugador = Vector3.Lerp(velJugador, v, 1f - Mathf.Exp(-4f * Time.deltaTime));
        }

        posJugadorAntes = p;
        hayPosAntes = true;
    }

    // A donde va cada villano segun su papel (cerca del jugador todos van directo).
    public Vector3 objetivoPara(Villano v)
    {
        JugadorPrimeraPersona jugador = JugadorPrimeraPersona.Instancia;
        if (jugador == null)
        {
            return v.transform.position;
        }

        Vector3 p = jugador.transform.position;
        Vector3 haciaJugador = Vector3.ProjectOnPlane(p - v.transform.position, Vector3.up);
        float d = haciaJugador.magnitude;
        if (d < 5f)
        {
            return p;
        }

        Vector3 dir = haciaJugador / d;
        Vector3 huida = velJugador.magnitude > 0.5f ? velJugador.normalized : huidaProbable(p, v);
        Vector3 objetivo;
        switch (v.rol)
        {
            case Villano.Rol.Rodea:
                // Se abre por un costado y va cerrando conforme se acerca.
                Vector3 costado = Vector3.Cross(Vector3.up, dir) * v.lado;
                objetivo = p + costado * Mathf.Min(7f, d * 0.55f) + velJugador * 0.8f;
                break;
            case Villano.Rol.Corta:
                // Se pone adelante, en el camino por donde huyes.
                objetivo = p + huida * Mathf.Clamp(d * 0.6f, 4f, 10f);
                break;
            case Villano.Rol.Intercepta:
                objetivo = p + velJugador * Mathf.Clamp(d / Mathf.Max(1f, v.velocidadPersiguiendo), 0.5f, 3f);
                break;
            default:
                objetivo = p;
                break;
        }

        // Un desvio propio que cambia despacio: nunca van exactamente por el mismo camino.
        float semilla = (v.name.GetHashCode() & 1023) * 0.137f;
        Vector3 ruido = new Vector3(Mathf.PerlinNoise(semilla, Time.time * 0.25f) - 0.5f, 0f,
                                Mathf.PerlinNoise(semilla + 7.3f, Time.time * 0.25f) - 0.5f) * Mathf.Min(4f, d * 0.25f);
        objetivo += ruido;

        return NavMesh.SamplePosition(objetivo, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : p;
    }

    // Si el jugador esta quieto: huiria hacia el lado contrario de los villanos.
    private Vector3 huidaProbable(Vector3 p, Villano yo)
    {
        Vector3 suma = Vector3.zero;
        foreach (Villano o in villanos)
        {
            if (o != null && o != yo)
            {
                suma += Vector3.ProjectOnPlane(p - o.transform.position, Vector3.up).normalized;
            }
        }

        return suma.sqrMagnitude > 0.01f ? suma.normalized : Vector3.ProjectOnPlane(p - centroAldea, Vector3.up).normalized;
    }

    private void Update()
    {
        if (atrapando)
        {
            return;
        }

        JugadorPrimeraPersona jugador = JugadorPrimeraPersona.Instancia;
        if (jugador == null)
        {
            return;
        }

        seguirVelocidadJugador(jugador.transform);
        bool enLaberinto = JugadorEnLaberinto;
        actualizarPortal();
        if (Infinita)
        {
            actualizarInfinita(jugador, enLaberinto);
            return;
        }

        // Entraste al laberinto en plena caceria: se acaba (los villanos regresan) y se quita la cuenta.
        if (enLaberinto && FaseActual == Fase.Caceria)
        {
            Debug.Log("[Goldy Pond] Caceria: entraste al laberinto, la caceria se termina.");
            empezarRetirada();
            descansoCompletoAlSalir = true;
        }

        // Saliste del laberinto: empieza el descanso completo (60 s) con su cuenta regresiva.
        if (!enLaberinto && estabaEnLaberinto && descansoCompletoAlSalir)
        {
            descansoCompletoAlSalir = false;
            if (FaseActual == Fase.Retirada)
            {
                foreach (Villano v in villanos)
                {
                    if (v != null)
                    {
                        v.volverASuLugar();
                    }
                }
            }

            if (FaseActual == Fase.Retirada || FaseActual == Fase.Descanso)
            {
                empezarDescanso();
            }

            Debug.Log("[Goldy Pond] Caceria: saliste del laberinto, descanso completo.");
        }

        estabaEnLaberinto = enLaberinto;

        if (musicaActiva && musica != null && musica.isPlaying && (fundidoMusica == null || musica.volume > 0.001f || enLaberinto))
        {
            musica.volume = Mathf.MoveTowards(musica.volume, enLaberinto ? 0f : ConfiguracionSonido.VolumenJuego, Time.deltaTime * 1.5f);
        }

        switch (FaseActual)
        {
            case Fase.Caceria:
                if (!liberados)
                {
                    return;
                }

                bool aSalvo = jugadorASalvo(jugador.transform.position);
                if (aSalvo != aSalvoAntes)
                {
                    Debug.Log(aSalvo ? "[Goldy Pond] Caceria: el jugador se escondio, los villanos deambulan."
                                     : "[Goldy Pond] Caceria: el jugador salio, los villanos lo persiguen.");
                    aSalvoAntes = aSalvo;
                }

                foreach (Villano v in villanos)
                {
                    if (v != null)
                    {
                        v.cambiar(aSalvo ? Villano.Estado.Deambulando : Villano.Estado.Persiguiendo);
                    }
                }

                Restante -= Time.deltaTime; // corre siempre, tambien dentro de casas y molino
                if (Restante <= 0f)
                {
                    empezarRetirada();
                }

                break;

            case Fase.Retirada:
                tiempoRetirada += Time.deltaTime;
                bool todos = true;
                foreach (Villano v in villanos)
                {
                    if (v != null && !v.EnSuLugar)
                    {
                        todos = false;
                    }
                }

                if (todos || tiempoRetirada > 30f)
                {
                    empezarDescanso();
                }

                break;

            case Fase.Descanso:
                if (!enLaberinto)
                {
                    Restante -= Time.deltaTime; // en el laberinto el descanso se pausa
                }

                if (Restante <= 0f)
                {
                    empezarCaceria();
                }

                break;
        }
    }

    private bool jugadorASalvo(Vector3 p)
    {
        foreach (Refugio r in refugios)
        {
            if (ElevadorAbierto && r.nombre == "Molino")
            {
                continue; // el jefe ya entra al molino
            }

            if (estaDentro(r.limites, p))
            {
                return true;
            }
        }

        return false;
    }

    private static bool estaDentro(Bounds b, Vector3 p)
    {
        return p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z && p.y > b.min.y - 1.5f && p.y < b.max.y + 2f;
    }

    // ------------------- CACERIA INFINITA ------------------------------

    private Villano jefe;
    private Transform molino;
    private Bounds limitesMolino;
    private readonly List<Puerta> puertasMolino = new List<Puerta>();
    private List<Transform> raicesNav = new List<Transform>();
    private NavMeshDataInstance navInstancia;

    private void empezarInfinita(bool primeraVez)
    {
        Infinita = true;
        FaseActual = Fase.Caceria;
        Restante = 0f;
        aSalvoAntes = false;

        jefe = villanos.Find(v => v != null && v.name.StartsWith("Boss"));
        if (jefe == null && villanos.Count > 0)
        {
            jefe = villanos[0];
        }

        // Solo persigue el jefe: las demas se quedan detras de la reja.
        foreach (Villano v in villanos)
        {
            if (v != null && v != jefe)
            {
                v.volverASuLugar();
            }
        }

        if (jefe != null)
        {
            jefe.rol = Villano.Rol.Intercepta; // va a donde vas a estar, no solo detras de ti
            jefe.velocidadPersiguiendo = velocidadJefeInfinita;
            jefe.velocidadDeambulando = paseoJefeInfinita;
            jefe.duracionJalon = 0.9f;
            jefe.distanciaAtrapar = 1.4f;
            jefe.puedeAtrapar = puedeAtrapar;
        }

        reloj?.mostrarEmpieza(3.5f);
        tocarMusica(true);
        if (!liberados)
        {
            if (rejaMoviendose != null)
            {
                StopCoroutine(rejaMoviendose);
            }

            rejaMoviendose = StartCoroutine(moverReja(true));
        }

        Debug.Log($"[Goldy Pond] CACERIA INFINITA {(primeraVez ? "empieza" : "sigue")}: solo el jefe ({(jefe != null ? jefe.name : "NO HAY JEFE")}), " +
                  $"velocidad base {velocidadJefeInfinita} (en la practica ~{velocidadJefeInfinita * 0.76f:0.0} m/s). Elevador abierto: {ElevadorAbierto}.");
    }

    private void actualizarInfinita(JugadorPrimeraPersona jugador, bool enLaberinto)
    {
        if (musicaActiva && musica != null && musica.isPlaying && (fundidoMusica == null || musica.volume > 0.001f || enLaberinto))
        {
            musica.volume = Mathf.MoveTowards(musica.volume, enLaberinto ? 0f : ConfiguracionSonido.VolumenJuego, Time.deltaTime * 1.5f);
        }

        if (!liberados || jefe == null)
        {
            return;
        }

        if (ElevadorAbierto)
        {
            abrirPuertasDelMolino();
        }

        Vector3 p = jugador.transform.position;
        bool aSalvo = enLaberinto || jugadorASalvo(p);
        if (aSalvo != aSalvoAntes)
        {
            aSalvoAntes = aSalvo;
            // Escondida: el jefe ronda cerca de donde te metiste (en el laberinto, por la aldea).
            if (aSalvo)
            {
                jefe.centrarPaseo(enLaberinto ? centroAldea : p, enLaberinto ? 32f : radioAcecho);
            }

            Debug.Log(aSalvo ? "[Goldy Pond] Caceria infinita: te escondiste, el jefe ronda cerca."
                             : "[Goldy Pond] Caceria infinita: saliste, el jefe te persigue.");
        }

        jefe.cambiar(aSalvo ? Villano.Estado.Deambulando : Villano.Estado.Persiguiendo);
    }

    // No te agarra con el tiempo congelado, ni dentro del molino una vez abierto el elevador.
    private bool puedeAtrapar()
    {
        if (Time.timeScale <= 0f)
        {
            return false;
        }

        JugadorPrimeraPersona j = JugadorPrimeraPersona.Instancia;
        if (ElevadorAbierto && molino != null && j != null && estaDentro(limitesMolino, j.transform.position))
        {
            return false;
        }

        return true;
    }

    private void abrirPuertasDelMolino()
    {
        foreach (Puerta puerta in puertasMolino)
        {
            if (puerta == null || puerta.Abierta || puerta.ConSeguro || puerta.Moviendose)
            {
                continue;
            }

            Renderer r = puerta.GetComponent<Renderer>();
            Vector3 d = (r != null ? r.bounds.center : puerta.transform.position) - jefe.transform.position;
            d.y = 0f;
            if (d.magnitude < 2f)
            {
                puerta.fijar(true, jefe.transform.position);
            }
        }
    }

    private static void alActivarPalanca(Palanca palanca)
    {
        if (ElevadorAbierto)
        {
            return;
        }

        int activas = 0;
        foreach (Palanca p in FindObjectsByType<Palanca>(FindObjectsInactive.Include))
        {
            if (p.Activada && p.gameObject.scene == palanca.gameObject.scene)
            {
                activas++;
            }
        }

        Debug.Log($"[Goldy Pond] Palancas activadas: {activas}/3.");
        if (activas < 3)
        {
            return;
        }

        ElevadorAbierto = true;
        MonoBehaviour quien = Instancia != null ? (MonoBehaviour)Instancia : palanca;
        quien.StartCoroutine(retumbeDelElevador());
    }

    // La 3a palanca: todo se congela, retumba la tierra (se abrio el elevador) y luego "VE AL SUBTERRANEO".
    private static IEnumerator retumbeDelElevador()
    {
        yield return new WaitForSecondsRealtime(0.8f); // que termine de bajar la palanca
        JugadorPrimeraPersona j = JugadorPrimeraPersona.Instancia;
        float duracion = Instancia != null ? Instancia.duracionRetumbe : 3.5f;
        Debug.Log("[Goldy Pond] Ultima palanca: retumbe, se abre el elevador del subterraneo.");
        Time.timeScale = 0f;
        if (j != null)
        {
            j.BloquearMirar = true;
        }

        try
        {
            // Ya puede entrar al molino: se vuelve a armar el NavMesh sin el bloqueo del molino.
            if (Instancia != null)
            {
                Instancia.abrirMolino();
            }

            yield return Retumbe.temblar(duracion, 1f);
            yield return new WaitForSecondsRealtime(0.3f);
        }
        finally
        {
            Time.timeScale = 1f;
            if (j != null)
            {
                j.BloquearMirar = false;
            }
        }

        Notificacion.mostrar("VE AL SUBTERRÁNEO", null, 6f);
    }

    private void abrirMolino()
    {
        int triangulos = construirNavMesh(raicesNav);
        foreach (Villano v in villanos)
        {
            if (v != null && v.Agente != null)
            {
                v.Agente.Warp(v.transform.position); // pegarlos al NavMesh nuevo
            }
        }

        Debug.Log($"[Goldy Pond] El jefe ya puede entrar al molino (NavMesh nuevo: {triangulos} triangulos, {puertasMolino.Count} puertas en el molino).");
    }

    // Un villano alcanzo al jugador.
    public void atrapar(Villano quien)
    {
        if (atrapando)
        {
            return;
        }

        atrapando = true;
        Debug.Log($"[Goldy Pond] Caceria: {quien.name} atrapo al jugador.");
        StartCoroutine(final(quien));
    }

    private static bool atrapandoFuera;

    // Te atrapo un villano en una escena sin Caceria (el jefe en el subterraneo).
    public static void atraparFuera(Villano quien)
    {
        if (atrapandoFuera || quien == null)
        {
            return;
        }

        atrapandoFuera = true;
        Debug.Log($"[Goldy Pond] {quien.name} atrapo al jugador.");
        quien.StartCoroutine(final(quien));
    }

    private static IEnumerator final(Villano quien)
    {
        foreach (Villano v in FindObjectsByType<Villano>(FindObjectsInactive.Exclude))
        {
            v.cambiar(Villano.Estado.Quieto);
        }

        PanelLectura.cerrarPanel();

        JugadorPrimeraPersona jugador = JugadorPrimeraPersona.Instancia;
        Camera cam = jugador != null ? jugador.camara : Camera.main;
        if (jugador != null)
        {
            jugador.enabled = false; // ya no se mueve ni mira con el mouse
        }

        quien.mirarHacia(jugador != null ? jugador.transform.position : cam.transform.position);

        var (vineta, negro) = crearPantallaFinal();

        // 1) La cámara voltea a ver la cara del villano mientras se ponen rojas las orillas.
        Quaternion desde = cam.transform.rotation;
        Vector3 cara = quien.transform.position + Vector3.up * quien.Altura * 0.85f;
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / 0.7f);
            float k = 1f - Mathf.Pow(1f - t, 3f);
            cam.transform.rotation = Quaternion.Slerp(desde, Quaternion.LookRotation(cara - cam.transform.position), k);
            vineta.color = new Color(1f, 1f, 1f, k);
            yield return null;
        }

        // 2) Rojo intenso que late.
        float inicio = Time.time;
        while (Time.time - inicio < 1.4f)
        {
            float latido = 0.8f + 0.2f * Mathf.Sin((Time.time - inicio) * 14f);
            vineta.color = new Color(1f, 1f, 1f, latido);
            cam.transform.rotation = Quaternion.LookRotation(cara - cam.transform.position);
            yield return null;
        }

        // 3) Negro.
        t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / 0.9f);
            negro.color = new Color(0f, 0f, 0f, t);
            yield return null;
        }

        yield return new WaitForSeconds(0.6f);

        // 4) Al inicio del juego, de cero.
        EstadoJuego.nuevaPartida();
        SceneManager.LoadScene(ESCENA_INICIO);
    }

    private static (Image vineta, Image negro) crearPantallaFinal()
    {
        GameObject lienzo = new GameObject("Pantalla Atrapado", typeof(Canvas));
        Canvas canvas = lienzo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        Image vineta = capa(lienzo.transform, "Orillas rojas");
        vineta.sprite = spriteVineta();
        vineta.color = new Color(1f, 1f, 1f, 0f);

        Image negro = capa(lienzo.transform, "Negro");
        negro.color = new Color(0f, 0f, 0f, 0f);
        return (vineta, negro);
    }

    private static Image capa(Transform padre, string nombre)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    // Centro transparente y orillas rojo intenso (se estira a toda la pantalla).
    private static Sprite spriteRojo;
    // Orillas rojas (centro transparente). La usa tambien la villana del laberinto.
    public static Sprite spriteOrillasRojas()
    {
        return spriteRojo != null ? spriteRojo : (spriteRojo = spriteVineta());
    }

    private static Sprite spriteVineta()
    {
        const int N = 256;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.2f, d));
                a = Mathf.Clamp01(a * 1.25f);
                px[y * N + x] = new Color32((byte)Mathf.Lerp(150, 235, a), 0, (byte)(a * 10), (byte)(a * 255));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
    }

    // ------------------- UTILIDADES ------------------------------

    private static Bounds obtenerLimites(Transform t)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0)
        {
            return new Bounds(t.position, Vector3.zero);
        }

        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs)
        {
            b.Encapsulate(r.bounds);
        }

        return b;
    }

    private static bool limitesDeHijos(Transform t, string empiezaCon, out Bounds b)
    {
        b = default;
        bool hay = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!r.name.StartsWith(empiezaCon))
            {
                continue;
            }

            if (!hay)
            {
                b = r.bounds;
                hay = true;
            }else
            {
                b.Encapsulate(r.bounds);
            }
        }

        return hay;
    }

    private static float altoDe(Transform t)
    {
        Bounds b = obtenerLimites(t);
        return Mathf.Max(1f, b.size.y);
    }
}
