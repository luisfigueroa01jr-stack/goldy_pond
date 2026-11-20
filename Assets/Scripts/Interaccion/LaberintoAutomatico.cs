using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// El laberinto puede estar en su escena (Laberinto) o metido en otra (ej. a un lado de la Aldea),
/// puesto desde el prefab o desde el .fbx. Al cargar la escena (lo llama RecogiblesAutomaticos):
///  - Puertas: "puerta cuarto 2", "puerta cuarto 3", "puerta pasillo" con E;
///    "puerta zona 5" cerrada hasta traer la "Llave del laberinto" en la mano.
///  - Todos los cajones ("... cajon ...") se abren con E.
///  - Luz en cada lampara ("lampara N").
///  - Mecanismo de los 3 pines (MecanismoPines) con el compartimento del engranaje.
/// Tambien sabe si un punto esta dentro del laberinto y donde esta su entrada (para el portal).
/// </summary>
public static class LaberintoAutomatico
{
    public const string LlaveLaberinto = "Llave del laberinto";
    static readonly Regex PatronRaiz = new Regex(@"^laberinto_colores( \(\d+\))?$");
    static readonly Regex PatronLampara = new Regex(@"^lampara \d+$");
    static readonly string[] PuertasLibres = { "puerta cuarto 2", "puerta cuarto 3", "puerta pasillo" };

    class Datos { public Transform raiz; public Bounds limites; }
    static readonly List<Datos> laberintos = new List<Datos>();

    public static void Preparar(Scene escena)
    {
        laberintos.RemoveAll(d => d.raiz == null);
        foreach (var r in escena.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (PatronRaiz.IsMatch(t.name) && !laberintos.Exists(d => d.raiz == t))
                    Configurar(t);
    }

    static void Configurar(Transform raiz)
    {
        var renders = raiz.GetComponentsInChildren<Renderer>(true);
        if (renders.Length == 0) return;
        var b = renders[0].bounds;
        foreach (var r in renders) b.Encapsulate(r.bounds);
        laberintos.Add(new Datos { raiz = raiz, limites = b });

        int puertas = 0, cajones = 0, luces = 0;
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
        {
            bool libre = System.Array.IndexOf(PuertasLibres, t.name) >= 0, final = t.name == "puerta zona 5";
            if ((libre || final) && t.GetComponent<Puerta>() == null)
            {
                var p = t.gameObject.AddComponent<Puerta>();
                p.nombre = "puerta";
                if (final) p.objetoNecesario = LlaveLaberinto;
                puertas++;
            }
            else if (t.name.Contains("cajon") && t.GetComponent<MeshFilter>() != null && t.GetComponent<Cajon>() == null)
            {
                var c = t.gameObject.AddComponent<Cajon>();
                if (t.name.Contains("estanteria")) c.distancia = 0.24f;
                cajones++;
            }
            else if (PatronLampara.IsMatch(t.name) && t.GetComponentInChildren<Light>(true) == null)
            {
                var r = t.GetComponent<Renderer>();
                var go = new GameObject("Luz");
                go.transform.SetParent(t, false);
                go.transform.position = r != null ? new Vector3(r.bounds.center.x, r.bounds.min.y + 0.32f * raiz.lossyScale.y, r.bounds.center.z) : t.position;
                var luz = go.AddComponent<Light>();
                luz.type = LightType.Point;
                luz.color = new Color(1f, 0.8f, 0.52f);
                luz.range = 6.5f * raiz.lossyScale.y;
                luz.intensity = 1.6f;
                luces++;
            }
        }
        // Mecanismo de los 3 pines y el compartimento de enfrente.
        Transform Hijo(string n) { foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
        var mecanismo = Hijo("mecanismo pines");
        if (mecanismo != null && mecanismo.GetComponent<MecanismoPines>() == null)
        {
            var m = mecanismo.gameObject.AddComponent<MecanismoPines>();
            m.compartimento = Hijo("compartimento");
            m.puertaCompartimento = Hijo("puerta compartimento");
            var roja = Hijo("luz roja"); var verde = Hijo("luz verde");
            m.luzRoja = roja != null ? roja.GetComponent<Renderer>() : null;
            m.luzVerde = verde != null ? verde.GetComponent<Renderer>() : null;
            Debug.Log("[Goldy Pond] Mecanismo de los 3 pines listo (orden: 1 Sol, 2 Flor, 3 Luna).");
        }

        if (puertas + cajones + luces > 0)
            Debug.Log($"[Goldy Pond] Laberinto '{raiz.name}' en '{raiz.gameObject.scene.name}': {puertas} puertas, {cajones} cajones, {luces} luces.");
    }

    /// <summary>Hay un laberinto en esta escena.</summary>
    public static bool HayEn(Scene escena) => laberintos.Exists(d => d.raiz != null && d.raiz.gameObject.scene == escena);

    /// <summary>El punto esta dentro del laberinto (o justo en su entrada).</summary>
    public static bool Contiene(Vector3 p)
    {
        foreach (var d in laberintos)
        {
            if (d.raiz == null) continue;
            var b = d.limites;
            b.Expand(new Vector3(5f, 2f, 5f));
            if (b.Contains(p)) return true;
        }
        return false;
    }

    /// <summary>Punto de llegada adentro del pasillo de la entrada (zona 1), mirando hacia adentro.</summary>
    public static Transform Entrada(Scene escena)
    {
        var d = laberintos.Find(x => x.raiz != null && x.raiz.gameObject.scene == escena);
        if (d == null) return null;
        var ya = d.raiz.Find("Llegada al laberinto");
        if (ya != null) return ya;
        var go = new GameObject("Llegada al laberinto");
        go.transform.SetParent(d.raiz, false);
        go.transform.localPosition = new Vector3(0f, 0.05f, -9f);   // pasillo de la entrada (en Blender y = -9)
        go.transform.localRotation = Quaternion.identity;            // mirando hacia el centro (+Z)
        return go.transform;
    }
}
