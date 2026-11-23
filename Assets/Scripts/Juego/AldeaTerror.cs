using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Iluminacion de terror en la escena "Aldea" (incluye el laberinto, que esta dentro de la misma escena).
// ALDEA: noche de luna, penumbra azulada con sombras y niebla azul oscura que se traga lo lejano.
// LABERINTO (callejon de noche): penumbra gris, lamparas blancas frias que hacen charcos de luz fuertes,
// niebla ligera para ver el pasillo hasta el fondo, colores casi en gris con mas contraste.
// - Una luz muy tenue va con el jugador para que alcance a ver el suelo.
// - Con la Linterna seleccionada en la barra sale un cono de luz hacia donde miras y la niebla se abre.
// - CASAS Y MOLINO por dentro: luz calida (como de velas/faroles) con sombras, para que no se salga por las paredes;
// al entrar sube un poco la luz ambiente. Solo se encienden las que estan cerca del jugador.
// No cambia Aldea.unity: todo se aplica al darle Play y Unity lo regresa solo al salir de Play.
public class AldeaTerror : MonoBehaviour
{
    private const string ESCENA = "Aldea";

    [Header("Aldea (noche de luna)")]
    public Color ambiente = new Color(0.045f, 0.052f, 0.085f);
    [Tooltip("Niebla sin linterna. 0.08 = claro hasta ~7 m, siluetas a ~15 m, a ~22 m ya no se ve.")]
    public float nieblaSinLinterna = 0.08f;
    public float nieblaConLinterna = 0.03f;
    public Color colorNiebla = new Color(0.035f, 0.045f, 0.075f);
    public Color colorLuna = new Color(0.6f, 0.7f, 1f);
    public float intensidadLuna = 0.2f;
    public Vector3 direccionLuna = new Vector3(35f, -40f, 0f);
    public Color colorTenue = new Color(0.55f, 0.62f, 0.85f);
    public float intensidadTenue = 0.55f;
    public float alcanceTenue = 5f;

    [Header("Laberinto (callejon de noche)")]
    [Tooltip("Luz ambiente del laberinto (gris: las paredes se ven en penumbra).")]
    public Color ambienteLaberinto = new Color(0.22f, 0.225f, 0.24f);
    [Tooltip("Niebla ligera: se ve el pasillo hasta el fondo, lo muy lejano se apaga.")]
    public float nieblaLaberinto = 0.018f;
    public float nieblaLaberintoConLinterna = 0.012f;
    public Color colorNieblaLaberinto = new Color(0.025f, 0.026f, 0.03f);
    public Color colorTenueLaberinto = new Color(0.8f, 0.82f, 0.88f);
    public float intensidadTenueLaberinto = 1f;
    public float alcanceTenueLaberinto = 6f;
    [Tooltip("Color de las lamparas (blanco frio como farol).")]
    public Color colorLamparas = new Color(0.86f, 0.91f, 1f);
    [Tooltip("Fuerza de las lamparas (1 = como estaban).")]
    public float fuerzaLamparas = 2.6f;
    [Tooltip("Alcance de las lamparas (1 = como estaban).")]
    public float alcanceLamparas = 1.4f;
    [Tooltip("1 de cada N lamparas parpadea un poquito (0 = ninguna).")]
    public int parpadeaUnaDeCada = 4;
    [Tooltip("Colores casi en gris (-100 = blanco y negro).")]
    public float saturacion = -60f;
    public float contraste = 12f;
    [Range(0f, 1f)] public float vineta = 0.35f;

    [Header("Linterna")]
    public Color colorLinterna = new Color(1f, 0.827f, 0.478f); // #FFD37A, igual que la del prefab
    [Tooltip("Fuerza del cono (URP baja la luz con la distancia: 55 = muy fuerte a 5 m, se nota hasta ~18 m).")]
    public float intensidadLinterna = 55f;
    public float alcanceLinterna = 28f;
    public float anguloLinterna = 50f;
    [Tooltip("Brillo de la linterna tirada en la aldea, para encontrarla en la oscuridad.")]
    public float intensidadBrilloSuelo = 2f;
    public float alcanceBrilloSuelo = 3f;

    [Header("Interiores (casas y molino)")]
    [Tooltip("Color de la luz de adentro (calida, como de vela).")]
    public Color colorInterior = new Color(1f, 0.75f, 0.47f);
    [Tooltip("Fuerza de la luz del techo de cada casa.")]
    public float intensidadCasa = 5f;
    public float alcanceCasa = 5f;
    [Tooltip("Metros sobre el suelo de la casa (las paredes de los lados miden ~2.2 m).")]
    public float alturaLuzCasa = 1.65f;
    [Tooltip("Fuerza de cada farol de pared del molino.")]
    public float intensidadMolino = 4.5f;
    public float alcanceMolino = 6f;
    [Tooltip("Luz ambiente estando adentro (la de afuera es casi negra).")]
    public Color ambienteInterior = new Color(0.14f, 0.115f, 0.09f);
    [Tooltip("Las luces de adentro solo se prenden si el jugador esta a menos de estos metros (con la niebla no se ven mas lejos).")]
    public float distanciaEncendido = 25f;

    private class Interior { public string nombre; public Bounds dentro; public readonly List<Light> luces = new List<Light>(); }
    private readonly List<Interior> interiores = new List<Interior>();
    private bool interioresListos;
    private float mezclaInterior, mezclaAplicada = -1f;
    private static readonly System.Text.RegularExpressions.Regex PatronCasa =
        new System.Text.RegularExpressions.Regex(@"^Casa_\d+$");

    private enum Zona { Ninguna, Aldea, Laberinto }
    private Zona zona = Zona.Ninguna;
    private float densidadActual;
    private bool primeraVez = true;
    private Camera camara;
    private Light tenue, linterna, luna, foco;

    // Lamparas del laberinto: luz, fuerza original, encendida o no, semilla del parpadeo.
    private readonly List<(Light luz, float fuerza, bool parpadea, float semilla)> lamparas =
        new List<(Light, float, bool, float)>();
    private Volume efectoGris;
    private Transform laberintoPreparado;

    // Linternas de la escena (sin recoger) y su brillo. El brillo NO va dentro de la linterna
    // para que no se copie al ponerla en la mano.
    private readonly List<(Transform objeto, Light luz)> brillos = new List<(Transform, Light)>();
    private static readonly System.Text.RegularExpressions.Regex PatronLinterna =
        new System.Text.RegularExpressions.Regex(@"^Linterna( \(\d+\))?$");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void iniciar()
    {
        SceneManager.sceneLoaded -= alCargar;
        SceneManager.sceneLoaded += alCargar;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            crear(SceneManager.GetSceneAt(i));
        }
    }

    private static void alCargar(Scene escena, LoadSceneMode modo)
    {
        crear(escena);
    }

    private static void crear(Scene escena)
    {
        if (!escena.isLoaded || escena.name != ESCENA)
        {
            return;
        }

        foreach (GameObject r in escena.GetRootGameObjects())
        {
            if (r.GetComponent<AldeaTerror>() != null)
            {
                return;
            }
        }

        GameObject go = new GameObject("Noche de terror");
        SceneManager.MoveGameObjectToScene(go, escena);
        go.AddComponent<AldeaTerror>();
        Debug.Log("[Goldy Pond] Noche de terror en la Aldea");
    }

    private void LateUpdate()
    {
        JugadorPrimeraPersona jugador = JugadorPrimeraPersona.Instancia;
        if (jugador == null || jugador.camara == null)
        {
            return;
        }

        if (camara != jugador.camara)
        {
            conectar(jugador.camara);
        }

        Transform raizLab = LaberintoAutomatico.raizQueContiene(jugador.transform.position, out _);
        Zona nueva = raizLab != null ? Zona.Laberinto : Zona.Aldea;
        if (nueva != zona)
        {
            if (nueva == Zona.Laberinto)
            {
                prepararLamparas(raizLab);
            }

            aplicarZona(nueva);
        }

        BarraHerramientas barra = BarraHerramientas.Instancia;
        bool conLinterna = barra != null && barra.ObjetoSeleccionado == "Linterna";
        linterna.enabled = conLinterna;
        foco.enabled = conLinterna;
        actualizarBrillos();
        if (zona == Zona.Laberinto)
        {
            parpadearLamparas();
        }

        actualizarInteriores(jugador.transform.position);

        // La niebla se abre/cierra suave al sacar o guardar la linterna.
        float meta = zona == Zona.Laberinto
            ? (conLinterna ? nieblaLaberintoConLinterna : nieblaLaberinto)
            : (conLinterna ? nieblaConLinterna : nieblaSinLinterna);
        densidadActual = primeraVez ? meta : Mathf.MoveTowards(densidadActual, meta, Time.deltaTime * 0.35f);
        primeraVez = false;
        RenderSettings.fogDensity = densidadActual;
    }

    private void aplicarZona(Zona z)
    {
        bool lab = z == Zona.Laberinto;
        bool deGolpe = zona == Zona.Ninguna || zona != z; // al cruzar el portal cambia al instante
        zona = z;

        Color amb = lab ? ambienteLaberinto : ambiente;
        Color niebla = lab ? colorNieblaLaberinto : colorNiebla;

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        ponerAmbiente(amb);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.reflectionIntensity = 0f;
        mezclaAplicada = -1f; // la mezcla de adentro se vuelve a poner

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = niebla;
        if (deGolpe)
        {
            primeraVez = true;
        }

        camara.clearFlags = CameraClearFlags.SolidColor;
        camara.backgroundColor = niebla;

        if (luna == null)
        {
            crearLuna();
        }

        luna.enabled = !lab;

        tenue.color = lab ? colorTenueLaberinto : colorTenue;
        tenue.intensity = lab ? intensidadTenueLaberinto : intensidadTenue;
        tenue.range = lab ? alcanceTenueLaberinto : alcanceTenue;

        if (efectoGris == null)
        {
            crearEfectoGris();
        }

        efectoGris.weight = lab ? 1f : 0f;
        UniversalAdditionalCameraData datos = camara.GetUniversalAdditionalCameraData();
        if (datos != null)
        {
            datos.renderPostProcessing = true;
        }
    }

    private void prepararLamparas(Transform raiz)
    {
        if (raiz == laberintoPreparado)
        {
            return;
        }

        laberintoPreparado = raiz;
        lamparas.Clear();
        Light[] luces = raiz.GetComponentsInChildren<Light>(true);
        // Orden fijo (por nombre) para que siempre queden encendidas las mismas.
        System.Array.Sort(luces, (a, b) => string.CompareOrdinal(ruta(a.transform), ruta(b.transform)));
        int i = 0;
        foreach (Light l in luces)
        {
            if (l.type == LightType.Directional)
            {
                continue;
            }

            bool parpadea = parpadeaUnaDeCada > 0 && i % parpadeaUnaDeCada == parpadeaUnaDeCada - 1;
            lamparas.Add((l, l.intensity, parpadea, Random.Range(0f, 100f)));
            l.enabled = true;
            l.color = colorLamparas;
            l.range *= alcanceLamparas;
            l.intensity *= fuerzaLamparas;
            i++;
        }

        Debug.Log($"[Goldy Pond] Laberinto de noche: {lamparas.Count} lamparas blancas.");
    }

    private static string ruta(Transform t)
    {
        return t.parent == null ? t.name : ruta(t.parent) + "/" + t.name;
    }

    private void parpadearLamparas()
    {
        float tiempo = Time.time;
        foreach (var (luz, fuerza, parpadea, semilla) in lamparas)
        {
            if (luz == null || !parpadea)
            {
                continue;
            }

            // Zumbido + de vez en cuando se apaga un instante, como farol viejo.
            float zumbido = 0.85f + 0.15f * Mathf.PerlinNoise(tiempo * 6f, semilla);
            float corte = Mathf.PerlinNoise(tiempo * 1.1f, semilla + 50f) > 0.75f
                          && Mathf.PerlinNoise(tiempo * 18f, semilla) > 0.5f ? 0.1f : 1f;
            luz.intensity = fuerza * fuerzaLamparas * zumbido * corte;
        }
    }

    private void crearEfectoGris()
    {
        GameObject go = new GameObject("Efecto gris laberinto (terror)");
        go.transform.SetParent(transform, false);
        efectoGris = go.AddComponent<Volume>();
        efectoGris.isGlobal = true;
        efectoGris.priority = 50f;
        VolumeProfile perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        ColorAdjustments color = perfil.Add<ColorAdjustments>(true);
        color.saturation.Override(saturacion);
        color.contrast.Override(contraste);
        Vignette v = perfil.Add<Vignette>(true);
        v.intensity.Override(vineta);
        v.smoothness.Override(0.5f);
        efectoGris.profile = perfil;
        efectoGris.weight = 0f;
    }

    private void conectar(Camera cam)
    {
        camara = cam;
        zona = Zona.Ninguna; // se vuelve a aplicar con la camara nueva
        if (tenue != null)
        {
            Destroy(tenue.gameObject);
        }

        if (linterna != null)
        {
            Destroy(linterna.gameObject);
        }

        if (foco != null)
        {
            Destroy(foco.gameObject);
        }

        if (brillos.Count == 0)
        {
            buscarLinternas();
        }

        // Luz tenue un poco arriba de los ojos: alumbra el suelo y lo que traes en la mano.
        GameObject goT = new GameObject("Luz tenue (terror)");
        goT.transform.SetParent(cam.transform, false);
        goT.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        tenue = goT.AddComponent<Light>();
        tenue.type = LightType.Point;
        tenue.shadows = LightShadows.None;

        // Cono de la linterna, sale de la mano derecha hacia donde miras.
        GameObject goL = new GameObject("Luz de la linterna (terror)");
        goL.transform.SetParent(cam.transform, false);
        goL.transform.localPosition = new Vector3(0.1f, -0.08f, 0.15f);
        linterna = goL.AddComponent<Light>();
        linterna.type = LightType.Spot;
        linterna.color = colorLinterna;
        linterna.intensity = intensidadLinterna;
        linterna.range = alcanceLinterna;
        linterna.spotAngle = anguloLinterna;
        linterna.innerSpotAngle = anguloLinterna * 0.6f;
        linterna.shadows = LightShadows.Soft;
        linterna.enabled = false;

        // Lucecita pegada a la linterna en la mano, para que se vea encendida.
        GameObject goF = new GameObject("Foco de la linterna (terror)");
        goF.transform.SetParent(cam.transform, false);
        goF.transform.localPosition = new Vector3(0.095f, -0.06f, 0.2f);
        foco = goF.AddComponent<Light>();
        foco.type = LightType.Point;
        foco.color = colorLinterna;
        foco.intensity = 3f;
        foco.range = 1f;
        foco.shadows = LightShadows.None;
        foco.enabled = false;
    }

    private void buscarLinternas()
    {
        foreach (GameObject raiz in gameObject.scene.GetRootGameObjects())
        {
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (!PatronLinterna.IsMatch(t.name))
                {
                    continue;
                }

                GameObject go = new GameObject($"Brillo de {t.name} (terror)");
                go.transform.SetParent(transform, false);
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = colorLinterna;
                l.intensity = intensidadBrilloSuelo;
                l.range = alcanceBrilloSuelo;
                l.shadows = LightShadows.None;
                brillos.Add((t, l));
            }
        }
    }

    private void actualizarBrillos()
    {
        foreach (var (objeto, luz) in brillos)
        {
            bool ver = objeto != null && objeto.gameObject.activeInHierarchy;
            luz.enabled = ver;
            if (!ver)
            {
                continue;
            }

            Renderer r = objeto.GetComponentInChildren<Renderer>();
            Vector3 centro = r != null ? r.bounds.center : objeto.position;
            luz.transform.position = centro + Vector3.up * 0.35f;
            // Parpadeo suave, como foco viejo.
            luz.intensity = intensidadBrilloSuelo * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 3f, 0.5f));
        }
    }

    private static void ponerAmbiente(Color amb)
    {
        RenderSettings.ambientLight = amb;
        SphericalHarmonicsL2 sh = new SphericalHarmonicsL2();
        sh.AddAmbientLight(amb);
        RenderSettings.ambientProbe = sh;
    }

    // ------------------- CASAS Y MOLINO POR DENTRO ------------------------------

    private void prepararInteriores()
    {
        interioresListos = true;
        foreach (GameObject raiz in gameObject.scene.GetRootGameObjects())
        {
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (PatronCasa.IsMatch(t.name))
                {
                    prepararCasa(t);
                }
                else if (t.name == "Molino")
                {
                    prepararMolino(t);
                }
            }
        }

        int n = 0; foreach (Interior i in interiores)
        {
            n += i.luces.Count;
        }

        Debug.Log($"[Goldy Pond] Interiores iluminados: {interiores.Count} ({n} luces).");
    }

    private void prepararCasa(Transform casa)
    {
        if (!limitesDeHijos(casa, "Pared", out Bounds paredes))
        {
            return;
        }

        float suelo = limitesDeHijos(casa, "Suelo", out Bounds s) ? s.max.y : paredes.min.y;
        Vector3 centro = paredes.center;
        Interior interior = new Interior { nombre = casa.name };
        interior.dentro = new Bounds(new Vector3(centro.x, (suelo + paredes.max.y) * 0.5f, centro.z),
                                     new Vector3(paredes.size.x, paredes.max.y - suelo + 1f, paredes.size.z));
        interior.luces.Add(crearLuzInterior($"Luz de {casa.name} (interior)",
            new Vector3(centro.x, suelo + alturaLuzCasa, centro.z), intensidadCasa, alcanceCasa));
        interiores.Add(interior);
    }

    private void prepararMolino(Transform molino)
    {
        if (!limitesDeHijos(molino, "Piso_Planta_Baja", out Bounds piso))
        {
            return;
        }

        Interior interior = new Interior { nombre = molino.name };
        Bounds dentro = piso;
        dentro.Expand(new Vector3(0.3f, 0f, 0.3f));
        dentro.max = new Vector3(dentro.max.x, dentro.max.y + 10f, dentro.max.z);
        interior.dentro = dentro;
        // Una luz en cada farol de pared (2 abajo, 1 arriba), un poco hacia el centro
        // para que el propio farol no le haga sombra.
        foreach (Renderer r in molino.GetComponentsInChildren<Renderer>())
        {
            if (!r.name.StartsWith("Farol_Pared"))
            {
                continue;
            }

            Vector3 p = r.bounds.center;
            Vector3 haciaCentro = Vector3.ProjectOnPlane(piso.center - p, Vector3.up);
            p += haciaCentro.normalized * Mathf.Min(0.45f, haciaCentro.magnitude);
            interior.luces.Add(crearLuzInterior($"Luz de {r.name} (interior)", p, intensidadMolino, alcanceMolino));
        }

        if (interior.luces.Count == 0)
        {
            interior.luces.Add(crearLuzInterior("Luz del molino (interior)", piso.center + Vector3.up * 1.6f, intensidadMolino, alcanceMolino));
        }

        interiores.Add(interior);
    }

    private Light crearLuzInterior(
        string nombre,
        Vector3 posicion,
        float intensidad,
        float alcance
    )
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        go.transform.position = posicion;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = colorInterior;
        l.intensity = intensidad;
        l.range = alcance;
        l.shadows = LightShadows.Soft; // las paredes la detienen: no se sale de la casa
        l.shadowNearPlane = 0.1f;
        l.enabled = false;
        return l;
    }

    private void actualizarInteriores(Vector3 jugador)
    {
        if (!interioresListos)
        {
            prepararInteriores();
        }

        bool adentro = false;
        foreach (Interior i in interiores)
        {
            bool cerca = zona == Zona.Aldea && (i.dentro.ClosestPoint(jugador) - jugador).sqrMagnitude < distanciaEncendido * distanciaEncendido;
            foreach (Light l in i.luces)
            {
                if (l != null)
                {
                    l.enabled = cerca;
                }
            }

            if (zona == Zona.Aldea && i.dentro.Contains(jugador))
            {
                adentro = true;
            }
        }

        // Al entrar sube la luz ambiente poco a poco (y baja al salir).
        mezclaInterior = Mathf.MoveTowards(mezclaInterior, adentro ? 1f : 0f, Time.deltaTime * 2f);
        if (zona == Zona.Aldea && !Mathf.Approximately(mezclaInterior, mezclaAplicada))
        {
            ponerAmbiente(Color.Lerp(ambiente, ambienteInterior, mezclaInterior));
            mezclaAplicada = mezclaInterior;
        }
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

    private void crearLuna()
    {
        GameObject go = new GameObject("Luna (terror)");
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(direccionLuna);
        luna = go.AddComponent<Light>();
        luna.type = LightType.Directional;
        luna.color = colorLuna;
        luna.intensity = intensidadLuna;
        luna.shadows = LightShadows.Soft;
        luna.shadowStrength = 0.85f;
    }
}
