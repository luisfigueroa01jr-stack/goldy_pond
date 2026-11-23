using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

// El laberinto puede estar en su escena (Laberinto) o metido en otra (ej. a un lado de la Aldea),
// puesto desde el prefab o desde el .fbx. Al cargar la escena (lo llama RecogiblesAutomaticos):
// - Puertas: "puerta cuarto 2", "puerta cuarto 3", "puerta pasillo" con E;
// "puerta zona 5" cerrada hasta traer la "Llave del laberinto" en la mano.
// - Todos los cajones ("... cajon ...") se abren con E.
// - Luz en cada lampara ("lampara N").
// - Mecanismo de los 3 pines (MecanismoPines) con el compartimento del engranaje.
// Tambien sabe si un punto esta dentro del laberinto y donde esta su entrada (para el portal).
public static class LaberintoAutomatico
{
    public const string LLAVE_LABERINTO = "Llave del laberinto";
    private static readonly Regex PatronRaiz = new Regex(@"^laberinto_colores( \(\d+\))?$");
    private static readonly Regex PatronLampara = new Regex(@"^lampara \d+$");
    private static readonly string[] PuertasLibres = { "puerta cuarto 2", "puerta cuarto 3", "puerta pasillo" };

    private class Datos { public Transform raiz; public Bounds limites; }
    private static readonly List<Datos> laberintos = new List<Datos>();

    // Unity puede tener apagado "Reload Domain": sin esto, en el segundo Play se acordaba del laberinto
    // del Play anterior y ya no le ponia puertas, cajones, luces ni el mecanismo.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void reiniciar()
    {
        laberintos.Clear();
    }

    public static void preparar(Scene escena)
    {
        laberintos.RemoveAll(d => d.raiz == null);
        foreach (GameObject r in escena.GetRootGameObjects())
        {
            foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
            {
                if (PatronRaiz.IsMatch(t.name))
                {
                    laberintos.RemoveAll(d => d.raiz == t);
                    configurar(t); // solo agrega lo que falte
                }
            }
        }
    }

    private static void configurar(Transform raiz)
    {
        Renderer[] renders = raiz.GetComponentsInChildren<Renderer>(true);
        if (renders.Length == 0)
        {
            return;
        }

        Bounds b = renders[0].bounds;
        foreach (Renderer r in renders)
        {
            b.Encapsulate(r.bounds);
        }

        laberintos.Add(new Datos { raiz = raiz, limites = b });

        int puertas = 0, cajones = 0, luces = 0;
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            bool libre = System.Array.IndexOf(PuertasLibres, t.name) >= 0, final = t.name == "puerta zona 5";
            if ((libre || final) && t.GetComponent<Puerta>() == null)
            {
                Puerta p = t.gameObject.AddComponent<Puerta>();
                p.nombre = "puerta";
                if (final)
                {
                    p.objetoNecesario = LLAVE_LABERINTO;
                }

                puertas++;
            }
            else if (t.name.Contains("cajon") && t.GetComponent<MeshFilter>() != null && t.GetComponent<Cajon>() == null)
            {
                Cajon c = t.gameObject.AddComponent<Cajon>();
                if (t.name.Contains("estanteria"))
                {
                    c.distancia = 0.24f;
                }

                cajones++;
            }
            else if (PatronLampara.IsMatch(t.name) && t.GetComponentInChildren<Light>(true) == null)
            {
                Renderer r = t.GetComponent<Renderer>();
                GameObject go = new GameObject("Luz");
                go.transform.SetParent(t, false);
                go.transform.position = r != null ? new Vector3(r.bounds.center.x, r.bounds.min.y + 0.32f * raiz.lossyScale.y, r.bounds.center.z) : t.position;
                Light luz = go.AddComponent<Light>();
                luz.type = LightType.Point;
                luz.color = new Color(1f, 0.8f, 0.52f);
                luz.range = 6.5f * raiz.lossyScale.y;
                luz.intensity = 1.6f;
                luces++;
            }
        }

        // Mecanismo de los 3 pines y el compartimento de enfrente.
        Transform hijo(string n) { foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == n)
            {
                return t;
            }
        }return null; }
        Transform mecanismo = hijo("mecanismo pines");
        if (mecanismo != null && mecanismo.GetComponent<MecanismoPines>() == null)
        {
            MecanismoPines m = mecanismo.gameObject.AddComponent<MecanismoPines>();
            m.compartimento = hijo("compartimento");
            m.puertaCompartimento = hijo("puerta compartimento");
            Transform roja = hijo("luz roja"); Transform verde = hijo("luz verde");
            m.luzRoja = roja != null ? roja.GetComponent<Renderer>() : null;
            m.luzVerde = verde != null ? verde.GetComponent<Renderer>() : null;
            Debug.Log("[Goldy Pond] Mecanismo de los 3 pines listo (orden: 1 Sol, 2 Flor, 3 Luna).");
        }

        int escondites = Escondite.prepararEn(raiz);

        if (puertas + cajones + luces + escondites > 0)
        {
            Debug.Log($"[Goldy Pond] Laberinto '{raiz.name}' en '{raiz.gameObject.scene.name}': {puertas} puertas, {cajones} cajones, {luces} luces, {escondites} escondites.");
        }
    }

    // El laberinto donde esta el punto (o null) y sus limites.
    public static Transform raizQueContiene(Vector3 p, out Bounds limites)
    {
        foreach (Datos d in laberintos)
        {
            if (d.raiz == null)
            {
                continue;
            }

            Bounds b = d.limites;
            b.Expand(new Vector3(5f, 2f, 5f));
            if (b.Contains(p))
            {
                limites = d.limites;
                return d.raiz;
            }
        }

        limites = default;
        return null;
    }

    // Hay un laberinto en esta escena.
    public static bool hayEn(Scene escena)
    {
        return laberintos.Exists(d => d.raiz != null && d.raiz.gameObject.scene == escena);
    }

    // El punto esta dentro del laberinto (o justo en su entrada).
    public static bool contiene(Vector3 p)
    {
        foreach (Datos d in laberintos)
        {
            if (d.raiz == null)
            {
                continue;
            }

            Bounds b = d.limites;
            b.Expand(new Vector3(5f, 2f, 5f));
            if (b.Contains(p))
            {
                return true;
            }
        }

        return false;
    }

    // Punto de llegada adentro del pasillo de la entrada (zona 1), mirando hacia adentro.
    public static Transform obtenerEntrada(Scene escena)
    {
        Datos d = laberintos.Find(x => x.raiz != null && x.raiz.gameObject.scene == escena);
        if (d == null)
        {
            return null;
        }

        Transform ya = d.raiz.Find("Llegada al laberinto");
        if (ya != null)
        {
            return ya;
        }

        GameObject go = new GameObject("Llegada al laberinto");
        go.transform.SetParent(d.raiz, false);
        go.transform.localPosition = new Vector3(0f, 0.05f, -9f);   // pasillo de la entrada (en Blender y = -9)
        go.transform.localRotation = Quaternion.identity;            // mirando hacia el centro (+Z)
        return go.transform;
    }
}
