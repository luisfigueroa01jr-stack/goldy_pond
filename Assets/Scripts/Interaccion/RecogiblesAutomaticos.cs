using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Al darle Play (y al cargar cada escena) busca la Llave, el Engranaje, la Linterna y el Pergamino
/// y, si les falta, les pone un BoxCollider y ObjetoRecogible con su icono (de Resources/Iconos).
/// Asi se pueden recoger con E aunque se hayan puesto en la escena desde el .fbx y no desde el prefab.
/// Escribe en la Console cuales quedaron listos y cuales no encontro.
/// </summary>
public static class RecogiblesAutomaticos
{
    static readonly string[] Objetos = { "Llave", "Engranaje", "Linterna", "Pergamino" };
    // "Llave" o "Llave (1)", pero no "Pergamino_Mesa" (ese es parte del escenario).
    static readonly Regex PatronNombre = new Regex(@"^(Llave|Engranaje|Linterna|Pergamino|Hoja|Pin_Sol|Pin_Luna|Pin_Flor)( \(\d+\))?$");
    // Como se llaman en la barra (y su icono en Resources/Iconos).
    static readonly Dictionary<string, string> NombreEnBarra = new Dictionary<string, string>
    {
        { "Pin_Sol", "Pin del Sol" }, { "Pin_Luna", "Pin de la Luna" }, { "Pin_Flor", "Pin de la Flor" },
    };
    // "Palanca", "Palanca (1)"... pero no el cuadro "Las tres palancas son la llave".
    static readonly Regex PatronPalanca = new Regex(@"^Palanca( \(\d+\))?$");
    // El portal nocturno puesto desde el .fbx o desde el prefab: "Portal_Nocturno", "Portal_Nocturno (1)"...
    static readonly Regex PatronPortal = new Regex(@"^Portal_Nocturno( \(\d+\))?$");
    // El libro que se lee con E (el del laberinto). No confundir con "libros" de las estanterias.
    static readonly Regex PatronLibro = new Regex(@"^Libro( \(\d+\))?$");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Revisar(SceneManager.GetSceneAt(i));
    }

    static void AlCargarEscena(Scene escena, LoadSceneMode modo) => Revisar(escena);

    static void Revisar(Scene escena)
    {
        if (!escena.isLoaded) return;
        LaberintoAutomatico.Preparar(escena);
        portales.Clear();
        var encontrados = new List<string>();
        foreach (var raiz in escena.GetRootGameObjects())
            Buscar(raiz.transform, encontrados);
        ConectarPortales(escena);

        // Si vienes de otra escena: barra, objetos ya recogidos, engranajes puestos, puertas abiertas.
        EstadoJuego.Restaurar(escena);

        var faltan = new List<string>();
        foreach (var n in Objetos)
            if (!encontrados.Exists(e => e.StartsWith(n))) faltan.Add(n);

        if (encontrados.Count == 0 && faltan.Count == Objetos.Length) return; // escena sin objetos (ej. menu)
        Debug.Log($"[Goldy Pond] Recogibles en '{escena.name}': {string.Join(", ", encontrados)}"
                  + (faltan.Count > 0 ? $"  |  NO estan en la escena: {string.Join(", ", faltan)}" : ""));
    }

    static void Buscar(Transform t, List<string> encontrados)
    {
        // Un portal nocturno: por su nombre, o porque trae la pieza "energia" (aunque lo hayas renombrado).
        if (PatronPortal.IsMatch(t.name) || (t.Find("energia") != null && t.Find("portal") != null))
        {
            PrepararPortal(t.gameObject);
            return;
        }
        if (PatronLibro.IsMatch(t.name))
        {
            PrepararLibro(t.gameObject);
            return;
        }
        if (PatronPalanca.IsMatch(t.name))
        {
            PrepararPalanca(t.gameObject);
            return;
        }
        var m = PatronNombre.Match(t.name);
        if (m.Success)
        {
            var nombre = m.Groups[1].Value;
            if (NombreEnBarra.TryGetValue(nombre, out var bonito)) nombre = bonito;
            encontrados.Add(Preparar(t.gameObject, nombre));
            return; // sus hijos son partes del mismo modelo
        }
        for (int i = 0; i < t.childCount; i++)
            Buscar(t.GetChild(i), encontrados);
    }

    /// <summary>
    /// "(E) Entrar al portal" -> escena Laberinto. A la "energia" le pone un area invisible (trigger)
    /// y un CambioEscena; a las piedras ("portal") un collider para no atravesarlas.
    /// </summary>
    static readonly List<GameObject> portales = new List<GameObject>();

    static void PrepararPortal(GameObject go)
    {
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            if (mf.name == "energia")
            {
                var caja = mf.GetComponent<BoxCollider>();
                if (caja == null)
                {
                    var b = mf.sharedMesh.bounds;
                    caja = mf.gameObject.AddComponent<BoxCollider>();
                    caja.center = b.center;
                    var s = b.size;
                    int delgado = s.x <= s.y && s.x <= s.z ? 0 : s.y <= s.z ? 1 : 2;
                    s[delgado] = 1.6f; // gruesa hacia adelante y atras: sale el aviso al subir los escalones
                    caja.size = s;
                }
                caja.isTrigger = true;
                if (mf.GetComponent<CambioEscena>() == null) mf.gameObject.AddComponent<CambioEscena>();
            }
            else if (mf.GetComponent<Collider>() == null)
            {
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
        }
        portales.Add(go);
    }

    /// <summary>
    /// A donde lleva cada portal:
    ///  - En el laberinto (su escena, o metido en esta): "(E) Ir a la Aldea".
    ///  - Afuera: "(E) Entrar al portal".
    /// Si el laberinto esta en la misma escena te teletransporta; si no, carga la otra escena.
    /// </summary>
    static void ConectarPortales(Scene escena)
    {
        bool mismaEscena = LaberintoAutomatico.HayEn(escena) && escena.name != "Laberinto";
        GameObject deAfuera = portales.Find(p => !LaberintoAutomatico.Contiene(p.transform.position));
        foreach (var go in portales)
        {
            bool enLaberinto = escena.name == "Laberinto" || (mismaEscena && go != deAfuera && LaberintoAutomatico.Contiene(go.transform.position));
            var cambio = go.GetComponentInChildren<CambioEscena>(true);
            if (cambio == null) continue;
            cambio.texto = enLaberinto ? "Ir a la Aldea" : "Entrar al portal";
            cambio.escenaDestino = enLaberinto ? "Aldea" : "Laberinto";
            cambio.llegada = "Portal";
            // Cada lugar con su barra; solo el engranaje cruza (Inventarios).
            cambio.inventarioDesde = enLaberinto ? "Laberinto" : "Aldea";
            cambio.inventarioHacia = enLaberinto ? "Aldea" : "Laberinto";
            cambio.destinoEnEscena = null;
            if (mismaEscena)
                cambio.destinoEnEscena = enLaberinto ? (deAfuera != null ? LlegadaFrente(deAfuera) : null) : LaberintoAutomatico.Entrada(escena);
            else if (!enLaberinto)
                LlegadaFrente(go); // al volver del laberinto apareces aqui
            Debug.Log($"[Goldy Pond] Portal listo: '{go.name}' -> (E) {cambio.texto}" + (cambio.destinoEnEscena != null ? " (en la misma escena)" : ""));
        }
    }

    /// <summary>Punto al pie de los escalones del portal, mirando hacia afuera (el frente del portal es su -Z).</summary>
    static Transform LlegadaFrente(GameObject portal)
    {
        var ya = portal.transform.Find("Llegada del portal");
        if (ya != null) return ya;
        var llegada = new GameObject("Llegada del portal");
        llegada.transform.SetParent(portal.transform, false);
        llegada.transform.localPosition = new Vector3(0f, 0.05f, -2.6f);
        llegada.transform.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        // Si la escena no tiene ya su llegada "Portal", esta sirve para cuando vienes de la escena Laberinto.
        bool yaHay = false;
        foreach (var raiz in portal.scene.GetRootGameObjects())
            foreach (var p in raiz.GetComponentsInChildren<PuntoLlegada>(true))
                if (p.id == "Portal") yaHay = true;
        if (!yaHay) llegada.gameObject.AddComponent<PuntoLlegada>().id = "Portal";
        return llegada.transform;
    }

    /// <summary>"(E) Leer": muestra la imagen "libro del laberinto" (la trae MensajesObjetos del laberinto).</summary>
    static void PrepararLibro(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>(true) == null)
            AjustarCaja(go.AddComponent<BoxCollider>());
        var libro = go.GetComponent<Libro>();
        if (libro == null)
        {
            libro = go.AddComponent<Libro>();
            libro.nombre = "";
        }
        if (libro.imagen == null) libro.imagen = MensajesObjetos.De("Libro del laberinto");
        Debug.Log($"[Goldy Pond] Libro listo: '{go.name}' -> (E) Leer" + (libro.imagen == null ? "  (OJO: no encontre la imagen \"libro del laberinto\")" : ""));
    }

    /// <summary>Le pone la caja (trigger, para que el rayo de la camara la detecte) y el componente Palanca.</summary>
    static void PrepararPalanca(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>(true) == null)
        {
            var caja = go.AddComponent<BoxCollider>();
            caja.isTrigger = true;
            AjustarCaja(caja);
        }
        if (go.GetComponent<Palanca>() == null) go.AddComponent<Palanca>();
    }

    /// <summary>Le pone collider y ObjetoRecogible si le faltan. Devuelve un resumen para el log.</summary>
    static string Preparar(GameObject go, string nombre)
    {
        if (go.GetComponentInChildren<Collider>(true) == null)
            AjustarCaja(go.AddComponent<BoxCollider>());

        var r = go.GetComponent<ObjetoRecogible>();
        if (r == null)
        {
            r = go.AddComponent<ObjetoRecogible>();
            r.nombre = nombre;
        }
        // La llave del laberinto es otra: la de la Aldea no abre el ultimo cuarto.
        if (nombre == "Llave" && (go.scene.name == "Laberinto" || LaberintoAutomatico.Contiene(go.transform.position))) r.nombre = LaberintoAutomatico.LlaveLaberinto;
        if (r.icono == null) r.icono = Resources.Load<Sprite>($"Iconos/{nombre}");
        // Hay dos engranajes: solo se puede traer uno a la vez, hasta ponerlo en el mecanismo del molino.
        if (nombre == "Engranaje")
        {
            r.soloUnoALaVez = true;
            if (string.IsNullOrEmpty(r.mensajeYaTienes))
                r.mensajeYaTienes = "Ya traes un engranaje. Ponlo en el mecanismo primero";
        }

        var estado = !go.activeInHierarchy ? "desactivado" : r.icono == null ? "listo, sin icono" : "listo";
        return $"{go.name} ({estado})";
    }

    /// <summary>Hace que la caja envuelva todas las mallas del objeto (en su espacio local), un poco mas grande para apuntarle facil.</summary>
    public static void AjustarCaja(BoxCollider caja)
    {
        var t = caja.transform;
        bool hay = false;
        var b = new Bounds();
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var esquina = mb.center + Vector3.Scale(mb.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var local = t.InverseTransformPoint(mf.transform.TransformPoint(esquina));
                if (!hay) { b = new Bounds(local, Vector3.zero); hay = true; }
                else b.Encapsulate(local);
            }
        }
        if (!hay) return;
        caja.center = b.center;
        caja.size = b.size * 1.15f + Vector3.one * 0.02f;
    }
}
