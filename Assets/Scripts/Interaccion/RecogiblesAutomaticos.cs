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
    static readonly Regex PatronNombre = new Regex(@"^(Llave|Engranaje|Linterna|Pergamino)( \(\d+\))?$");
    // "Palanca", "Palanca (1)"... pero no el cuadro "Las tres palancas son la llave".
    static readonly Regex PatronPalanca = new Regex(@"^Palanca( \(\d+\))?$");

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
        var encontrados = new List<string>();
        foreach (var raiz in escena.GetRootGameObjects())
            Buscar(raiz.transform, encontrados);

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
        if (PatronPalanca.IsMatch(t.name))
        {
            PrepararPalanca(t.gameObject);
            return;
        }
        var m = PatronNombre.Match(t.name);
        if (m.Success)
        {
            encontrados.Add(Preparar(t.gameObject, m.Groups[1].Value));
            return; // sus hijos son partes del mismo modelo
        }
        for (int i = 0; i < t.childCount; i++)
            Buscar(t.GetChild(i), encontrados);
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
