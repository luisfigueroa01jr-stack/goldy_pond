using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Agua de verdad (shader Resources/Agua/Agua.shader) al darle Play:
/// ALDEA: la fuente (pila de abajo, taza mediana y taza chica, con el agua cayendo de cada taza)
///        y la laguna (superficie + la cascada que cae de la repisa).
/// SUBTERRANEO: el lago (Agua_Lago) y las dos cascadas dejan de ser azul liso y se vuelven agua.
/// No cambia las escenas. Las medidas salen de los modelos (Blender), asi funciona aunque esten movidos.
/// </summary>
public static class AguaAutomatica
{
    static Material superficie, cascada, superficieSubterraneo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        SceneManager.sceneLoaded -= AlCargar;
        SceneManager.sceneLoaded += AlCargar;
        for (int i = 0; i < SceneManager.sceneCount; i++) Preparar(SceneManager.GetSceneAt(i));
    }

    static void AlCargar(Scene escena, LoadSceneMode modo) => Preparar(escena);

    static void Preparar(Scene escena)
    {
        if (!escena.isLoaded || (escena.name != "Aldea" && escena.name != "Subterraneo")) return;
        if (!CrearMateriales()) return;
        var todos = new List<Transform>();
        foreach (var r in escena.GetRootGameObjects()) todos.AddRange(r.GetComponentsInChildren<Transform>(true));

        if (escena.name == "Aldea")
        {
            int fuentes = 0, lagunas = 0;
            foreach (var t in todos)
            {
                if (t.name == "Cylinder.030" && Ancestro(t, "Fuente") != null && Fuente(t, todos)) fuentes++;
                if (t.name.StartsWith("Laguna") && t.Find("Roca_Borde_1.001") != null && Laguna(t)) lagunas++;
            }
            Debug.Log($"[Goldy Pond] Agua en la Aldea: {fuentes} fuente(s), {lagunas} laguna(s).");
        }
        else
        {
            int n = 0;
            foreach (var t in todos)
            {
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                if (t.name.StartsWith("Agua_Lago")) { r.sharedMaterials = Repetir(superficieSubterraneo, r.sharedMaterials.Length); n++; }
                else if (t.name.StartsWith("Cascada_Izquierda") || t.name.StartsWith("Cascada_Derecha"))
                { r.sharedMaterials = Repetir(cascada, r.sharedMaterials.Length); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; n++; }
            }
            Debug.Log($"[Goldy Pond] Agua en el Subterraneo: {n} pieza(s) convertidas en agua.");
        }
    }

    static bool CrearMateriales()
    {
        if (superficie != null) return true;
        var shader = Resources.Load<Shader>("Agua/Agua");
        if (shader == null || !shader.isSupported) { Debug.LogWarning("[Goldy Pond] No se encontro el shader del agua (Resources/Agua/Agua)."); return false; }
        superficie = new Material(shader) { name = "Agua (superficie)" };
        superficieSubterraneo = new Material(shader) { name = "Agua (lago subterraneo)" };
        superficieSubterraneo.SetColor("_ColorProfundo", new Color(0.03f, 0.16f, 0.26f));
        superficieSubterraneo.SetColor("_ColorClaro", new Color(0.18f, 0.55f, 0.7f));
        superficieSubterraneo.SetFloat("_Escala", 2.4f);
        cascada = new Material(shader) { name = "Agua (cascada)" };
        cascada.SetFloat("_Cascada", 1f);
        cascada.SetFloat("_Transparencia", 0.6f);
        return true;
    }

    static Material[] Repetir(Material m, int n) { var a = new Material[Mathf.Max(1, n)]; for (int i = 0; i < a.Length; i++) a[i] = m; return a; }

    static Transform Ancestro(Transform t, string inicio)
    {
        for (var p = t.parent; p != null; p = p.parent) if (p.name.StartsWith(inicio)) return p;
        return null;
    }

    // ---------------- Fuente ----------------
    // Medidas del .blend (Goldy_Pond_Colores, grupo Fuente): pila Cylinder.030 de z 0.16 a 0.79, radio interior 2.34;
    // taza mediana Cylinder.035 (borde r 1.44 en z 2.28), taza chica Cylinder.037 (borde r 0.75 en z 3.25).
    static bool Fuente(Transform pila, List<Transform> todos)
    {
        var r = pila.GetComponent<Renderer>();
        if (r == null) return false;
        var raiz = Ancestro(pila, "Fuente");
        if (raiz.Find("Agua de la fuente") != null) return false;
        var b = r.bounds;
        float s = b.size.y / 0.63f;                    // metros de Unity por metro del .blend
        float Y(float z) => b.min.y + (z - 0.16f) * s;
        var c = new Vector3(b.center.x, 0f, b.center.z);

        var agua = new GameObject("Agua de la fuente").transform;
        agua.SetParent(raiz, false);
        Disco(agua, "Agua pila", new Vector3(c.x, Y(0.72f), c.z), 2.40f * s, superficie);
        Disco(agua, "Agua taza mediana", new Vector3(c.x, Y(2.22f), c.z), 1.30f * s, superficie);
        Disco(agua, "Agua taza chica", new Vector3(c.x, Y(3.20f), c.z), 0.66f * s, superficie);
        // El agua que se desborda y cae de cada taza.
        Cortina(agua, "Cae de la taza mediana", c, 1.47f * s, Y(2.28f), Y(0.72f), cascada);
        Cortina(agua, "Cae de la taza chica", c, 0.78f * s, Y(3.25f), Y(2.22f), cascada);
        // Chorrito que sale de la punta dorada y cae a la taza chica.
        Cortina(agua, "Chorro de arriba", c, 0.07f * s, Y(4.35f), Y(3.20f), cascada);
        return true;
    }

    // ---------------- Laguna ----------------
    // En el .blend: Terreno_Laguna.001 de z -1.0 a 0.12 (el suelo esta en 0); el agua va en z -0.08.
    static bool Laguna(Transform laguna)
    {
        if (laguna.Find("Agua de la laguna") != null) return false;
        var rocas = new List<Vector3>();
        Renderer terreno = null, repisa = null;
        foreach (Transform h in laguna)
        {
            var r = h.GetComponent<Renderer>();
            if (r == null) continue;
            if (h.name.StartsWith("Roca_Borde_")) rocas.Add(r.bounds.center);
            else if (h.name.StartsWith("Terreno_Laguna")) terreno = r;
            else if (h.name.StartsWith("Cascada_Repisa")) repisa = r;
        }
        if (rocas.Count == 0 || terreno == null) return false;
        var c = Vector3.zero; foreach (var p in rocas) c += p; c /= rocas.Count;
        // Radio promedio del circulo de rocas: el hueco mide ~3 m y el borde del terreno sube entre 3 y 4 m;
        // mas grande se sale por fuera (afuera el terreno baja otra vez).
        float radio = 0f; foreach (var p in rocas) radio += Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z));
        radio /= rocas.Count;
        var tb = terreno.bounds;
        float s = tb.size.y / 1.12f;
        float yAgua = tb.min.y + (1.0f - 0.08f) * s;

        var agua = new GameObject("Agua de la laguna").transform;
        agua.SetParent(laguna, false);
        Disco(agua, "Agua laguna", new Vector3(c.x, yAgua, c.z), radio * 0.95f, superficie);

        if (repisa != null)
        {
            var rb = repisa.bounds;
            var hacia = new Vector3(c.x - rb.center.x, 0f, c.z - rb.center.z).normalized;
            var arriba = new Vector3(rb.center.x, rb.max.y - 0.02f * s, rb.center.z) + hacia * (0.55f * s);
            Caida(agua, "Cascada de la laguna", arriba, hacia, yAgua, 1.0f * s, cascada);
        }
        return true;
    }

    // ---------------- Mallas ----------------

    static void Disco(Transform padre, string nombre, Vector3 centro, float radio, Material m)
    {
        const int lados = 48;
        var v = new Vector3[lados + 1]; var n = new Vector3[lados + 1]; var tri = new int[lados * 3];
        v[0] = Vector3.zero; n[0] = Vector3.up;
        for (int i = 0; i < lados; i++)
        {
            float a = i * Mathf.PI * 2f / lados;
            v[i + 1] = new Vector3(Mathf.Cos(a) * radio, 0f, Mathf.Sin(a) * radio); n[i + 1] = Vector3.up;
            tri[i * 3] = 0; tri[i * 3 + 1] = (i + 1) % lados + 1; tri[i * 3 + 2] = i + 1;
        }
        Crear(padre, nombre, centro, v, n, tri, m);
    }

    static void Cortina(Transform padre, string nombre, Vector3 centro, float radio, float yArriba, float yAbajo, Material m)
    {
        const int lados = 40, pisos = 6;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
        for (int j = 0; j <= pisos; j++)
        {
            float k = j / (float)pisos;
            float y = Mathf.Lerp(yArriba, yAbajo, k) - yAbajo;
            float rr = radio * (1f + 0.06f * k);          // se abre un poquito al caer
            for (int i = 0; i <= lados; i++)
            {
                float a = i * Mathf.PI * 2f / lados;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(d * rr + Vector3.up * y); n.Add(d);
            }
        }
        for (int j = 0; j < pisos; j++)
            for (int i = 0; i < lados; i++)
            {
                int a = j * (lados + 1) + i, b2 = a + lados + 1;
                tri.AddRange(new[] { a, b2, a + 1, a + 1, b2, b2 + 1 });
            }
        Crear(padre, nombre, new Vector3(centro.x, yAbajo, centro.z), v.ToArray(), n.ToArray(), tri.ToArray(), m);
    }

    static void Caida(Transform padre, string nombre, Vector3 arriba, Vector3 hacia, float yAbajo, float ancho, Material m)
    {
        const int pisos = 8;
        var lado = Vector3.Cross(Vector3.up, hacia).normalized;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
        float alto = arriba.y - yAbajo;
        for (int j = 0; j <= pisos; j++)
        {
            float k = j / (float)pisos;
            // Sale un poco hacia adelante y cae (curva).
            var p = hacia * (Mathf.Sqrt(k) * 0.45f * alto * 0.5f) + Vector3.down * (k * alto);
            v.Add(p - lado * ancho * 0.5f); v.Add(p + lado * ancho * 0.5f);
            n.Add(hacia); n.Add(hacia);
        }
        for (int j = 0; j < pisos; j++) { int a = j * 2; tri.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 }); }
        Crear(padre, nombre, arriba, v.ToArray(), n.ToArray(), tri.ToArray(), m);
    }

    static void Crear(Transform padre, string nombre, Vector3 posicion, Vector3[] v, Vector3[] n, int[] tri, Material m)
    {
        var go = new GameObject(nombre);
        go.transform.SetPositionAndRotation(posicion, Quaternion.identity);
        go.transform.SetParent(padre, true); // conserva su tamano real aunque el modelo tenga escala
        var malla = new Mesh { name = nombre };
        malla.vertices = v; malla.normals = n; malla.triangles = tri;
        malla.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }
}
