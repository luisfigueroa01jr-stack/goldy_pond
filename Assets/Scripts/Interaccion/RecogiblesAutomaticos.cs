using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

// Al darle Play (y al cargar cada escena) busca la Llave, el Engranaje, la Linterna y el Pergamino
// y, si les falta, les pone un BoxCollider y ObjetoRecogible con su icono (de Resources/Iconos).
// Asi se pueden recoger con E aunque se hayan puesto en la escena desde el .fbx y no desde el prefab.
// Escribe en la Console cuales quedaron listos y cuales no encontro.
public static class RecogiblesAutomaticos
{
    private static readonly string[] Objetos = { "Llave", "Engranaje", "Linterna", "Pergamino" };
    // "Llave" o "Llave (1)", pero no "Pergamino_Mesa" (ese es parte del escenario).
    private static readonly Regex PatronNombre = new Regex(@"^(Llave|Engranaje|Linterna|Pergamino|Hoja|Pin_Sol|Pin_Luna|Pin_Flor)( \(\d+\))?$");
    // Como se llaman en la barra (y su icono en Resources/Iconos).
    private static readonly Dictionary<string, string> NombreEnBarra = new Dictionary<string, string>
    {
        { "Pin_Sol", "Pin del Sol" }, { "Pin_Luna", "Pin de la Luna" }, { "Pin_Flor", "Pin de la Flor" },
    };
    // "Palanca", "Palanca (1)"... pero no el cuadro "Las tres palancas son la llave".
    private static readonly Regex PatronPalanca = new Regex(@"^Palanca( \(\d+\))?$");
    // El portal nocturno puesto desde el .fbx o desde el prefab: "Portal_Nocturno", "Portal_Nocturno (1)"...
    private static readonly Regex PatronPortal = new Regex(@"^Portal_Nocturno( \(\d+\))?$");
    // El libro que se lee con E (el del laberinto). No confundir con "libros" de las estanterias.
    private static readonly Regex PatronLibro = new Regex(@"^Libro( \(\d+\))?$");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void iniciar()
    {
        SceneManager.sceneLoaded -= alCargarEscena;
        SceneManager.sceneLoaded += alCargarEscena;
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
        if (!escena.isLoaded)
        {
            return;
        }

        LaberintoAutomatico.preparar(escena);
        portales.Clear();
        List<string> encontrados = new List<string>();
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            buscar(raiz.transform, encontrados);
        }

        conectarPortales(escena);

        // Si vienes de otra escena: barra, objetos ya recogidos, engranajes puestos, puertas abiertas.
        EstadoJuego.restaurar(escena);

        List<string> faltan = new List<string>();
        foreach (string n in Objetos)
        {
            if (!encontrados.Exists(e => e.StartsWith(n)))
            {
                faltan.Add(n);
            }
        }

        if (encontrados.Count == 0 && faltan.Count == Objetos.Length)
        {
            return; // escena sin objetos (ej. menu)
        }

        Debug.Log($"[Goldy Pond] Recogibles en '{escena.name}': {string.Join(", ", encontrados)}"
                  + (faltan.Count > 0 ? $"  |  NO estan en la escena: {string.Join(", ", faltan)}" : ""));
    }

    private static void buscar(Transform t, List<string> encontrados)
    {
        // Un portal nocturno: por su nombre, o porque trae la pieza "energia" (aunque lo hayas renombrado).
        if (PatronPortal.IsMatch(t.name) || (t.Find("energia") != null && t.Find("portal") != null))
        {
            prepararPortal(t.gameObject);
            return;
        }

        if (PatronLibro.IsMatch(t.name))
        {
            prepararLibro(t.gameObject);
            return;
        }

        if (PatronPalanca.IsMatch(t.name))
        {
            prepararPalanca(t.gameObject);
            return;
        }

        Match m = PatronNombre.Match(t.name);
        if (m.Success)
        {
            string nombre = m.Groups[1].Value;
            if (NombreEnBarra.TryGetValue(nombre, out string bonito))
            {
                nombre = bonito;
            }

            encontrados.Add(preparar(t.gameObject, nombre));
            return; // sus hijos son partes del mismo modelo
        }

        for (int i = 0; i < t.childCount; i++)
        {
            buscar(t.GetChild(i), encontrados);
        }
    }

    // "(E) Entrar al portal" -> escena Laberinto. A la "energia" le pone un area invisible (trigger)
    // y un CambioEscena; a las piedras ("portal") un collider para no atravesarlas.
    private static readonly List<GameObject> portales = new List<GameObject>();

    private static void prepararPortal(GameObject go)
    {
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null)
            {
                continue;
            }

            if (mf.name == "energia")
            {
                BoxCollider caja = mf.GetComponent<BoxCollider>();
                if (caja == null)
                {
                    Bounds b = mf.sharedMesh.bounds;
                    caja = mf.gameObject.AddComponent<BoxCollider>();
                    caja.center = b.center;
                    Vector3 s = b.size;
                    int delgado = s.x <= s.y && s.x <= s.z ? 0 : s.y <= s.z ? 1 : 2;
                    s[delgado] = 1.6f; // gruesa hacia adelante y atras: sale el aviso al subir los escalones
                    caja.size = s;
                }

                caja.isTrigger = true;
                if (mf.GetComponent<CambioEscena>() == null)
                {
                    mf.gameObject.AddComponent<CambioEscena>();
                }
            }
            else if (mf.GetComponent<Collider>() == null)
            {
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
        }

        portales.Add(go);
    }

    // A donde lleva cada portal:
    // - En el laberinto (su escena, o metido en esta): "(E) Ir a la Aldea".
    // - Afuera: "(E) Entrar al portal".
    // Si el laberinto esta en la misma escena te teletransporta; si no, carga la otra escena.
    private static void conectarPortales(Scene escena)
    {
        bool mismaEscena = LaberintoAutomatico.hayEn(escena) && escena.name != "Laberinto";
        GameObject deAfuera = portales.Find(p => !LaberintoAutomatico.contiene(p.transform.position));
        foreach (GameObject go in portales)
        {
            bool enLaberinto = escena.name == "Laberinto" || (mismaEscena && go != deAfuera && LaberintoAutomatico.contiene(go.transform.position));
            CambioEscena cambio = go.GetComponentInChildren<CambioEscena>(true);
            if (cambio == null)
            {
                continue;
            }

            cambio.texto = enLaberinto ? "Ir a la Aldea" : "Entrar al portal";
            cambio.escenaDestino = enLaberinto ? "Aldea" : "Laberinto";
            cambio.llegada = "Portal";
            // Cada lugar con su barra; solo el engranaje cruza (Inventarios).
            cambio.inventarioDesde = enLaberinto ? "Laberinto" : "Aldea";
            cambio.inventarioHacia = enLaberinto ? "Aldea" : "Laberinto";
            cambio.destinoEnEscena = null;
            if (mismaEscena)
            {
                cambio.destinoEnEscena = enLaberinto ? (deAfuera != null ? llegadaFrente(deAfuera) : null) : LaberintoAutomatico.obtenerEntrada(escena);
            }
            else if (!enLaberinto)
            {
                llegadaFrente(go); // al volver del laberinto apareces aqui
            }

            Debug.Log($"[Goldy Pond] Portal listo: '{go.name}' -> (E) {cambio.texto}" + (cambio.destinoEnEscena != null ? " (en la misma escena)" : ""));
        }
    }

    // Punto al pie de los escalones del portal, mirando hacia afuera (el frente del portal es su -Z).
    private static Transform llegadaFrente(GameObject portal)
    {
        Transform ya = portal.transform.Find("Llegada del portal");
        if (ya != null)
        {
            return ya;
        }

        GameObject llegada = new GameObject("Llegada del portal");
        llegada.transform.SetParent(portal.transform, false);
        llegada.transform.localPosition = new Vector3(0f, 0.05f, -2.6f);
        llegada.transform.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        // Si la escena no tiene ya su llegada "Portal", esta sirve para cuando vienes de la escena Laberinto.
        bool yaHay = false;
        foreach (GameObject raiz in portal.scene.GetRootGameObjects())
        {
            foreach (PuntoLlegada p in raiz.GetComponentsInChildren<PuntoLlegada>(true))
            {
                if (p.id == "Portal")
                {
                    yaHay = true;
                }
            }
        }

        if (!yaHay)
        {
            llegada.gameObject.AddComponent<PuntoLlegada>().id = "Portal";
        }

        return llegada.transform;
    }

    // "(E) Leer": muestra la imagen "libro del laberinto" (la trae MensajesObjetos del laberinto).
    private static void prepararLibro(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>(true) == null)
        {
            ajustarCaja(go.AddComponent<BoxCollider>());
        }

        Libro libro = go.GetComponent<Libro>();
        if (libro == null)
        {
            libro = go.AddComponent<Libro>();
            libro.nombre = "";
        }

        if (libro.imagen == null)
        {
            libro.imagen = MensajesObjetos.obtenerImagen("Libro del laberinto");
        }

        libro.soloImagen = true; // el libro del niño: solo la imagen, sin marco dorado ni fondo morado
        Debug.Log($"[Goldy Pond] Libro listo: '{go.name}' -> (E) Leer" + (libro.imagen == null ? "  (OJO: no encontre la imagen \"libro del laberinto\")" : ""));
    }

    // Le pone la caja (trigger, para que el rayo de la camara la detecte) y el componente Palanca.
    private static void prepararPalanca(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>(true) == null)
        {
            BoxCollider caja = go.AddComponent<BoxCollider>();
            caja.isTrigger = true;
            ajustarCaja(caja);
        }

        if (go.GetComponent<Palanca>() == null)
        {
            go.AddComponent<Palanca>();
        }
    }

    // Le pone collider y ObjetoRecogible si le faltan. Devuelve un resumen para el log.
    private static string preparar(GameObject go, string nombre)
    {
        if (go.GetComponentInChildren<Collider>(true) == null)
        {
            ajustarCaja(go.AddComponent<BoxCollider>());
        }

        ObjetoRecogible r = go.GetComponent<ObjetoRecogible>();
        if (r == null)
        {
            r = go.AddComponent<ObjetoRecogible>();
            r.nombre = nombre;
        }

        // La llave del laberinto es otra: la de la Aldea no abre el ultimo cuarto.
        if (nombre == "Llave" && (go.scene.name == "Laberinto" || LaberintoAutomatico.contiene(go.transform.position)))
        {
            r.nombre = LaberintoAutomatico.LLAVE_LABERINTO;
        }

        if (r.icono == null)
        {
            r.icono = Resources.Load<Sprite>($"Iconos/{nombre}");
        }

        // Hay dos engranajes: solo se puede traer uno a la vez, hasta ponerlo en el mecanismo del molino.
        if (nombre == "Engranaje")
        {
            r.soloUnoALaVez = true;
            if (string.IsNullOrEmpty(r.mensajeYaTienes))
            {
                r.mensajeYaTienes = "Ya traes un engranaje. Ponlo en el mecanismo primero";
            }
        }

        string estado = !go.activeInHierarchy ? "desactivado" : r.icono == null ? "listo, sin icono" : "listo";
        return $"{go.name} ({estado})";
    }

    // Hace que la caja envuelva todas las mallas del objeto (en su espacio local), un poco mas grande para apuntarle facil.
    public static void ajustarCaja(BoxCollider caja)
    {
        Transform t = caja.transform;
        bool hay = false;
        Bounds b = new Bounds();
        foreach (MeshFilter mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null)
            {
                continue;
            }

            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 esquina = mb.center + Vector3.Scale(mb.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = t.InverseTransformPoint(mf.transform.TransformPoint(esquina));
                if (!hay)
                {
                    b = new Bounds(local, Vector3.zero);
                    hay = true;
                }
                else
                {
                    b.Encapsulate(local);
                }
            }
        }

        if (!hay)
        {
            return;
        }

        caja.center = b.center;
        caja.size = b.size * 1.15f + Vector3.one * 0.02f;
    }
}
