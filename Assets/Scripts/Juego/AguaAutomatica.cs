using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Agua de verdad (shader Resources/Agua/Agua.shader) al darle Play:
// ALDEA: la fuente (pila de abajo, taza mediana y taza chica, con el agua cayendo de cada taza)
// y la laguna (superficie + la cascada que cae de la repisa).
// SUBTERRANEO: el lago (Agua_Lago) y las dos cascadas dejan de ser azul liso y se vuelven agua.
// No cambia las escenas. Las medidas salen de los modelos (Blender), asi funciona aunque esten movidos.
public static class AguaAutomatica
{
    private static Material superficie, cascada, superficieSubterraneo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void iniciar()
    {
        SceneManager.sceneLoaded -= alCargar;
        SceneManager.sceneLoaded += alCargar;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            preparar(SceneManager.GetSceneAt(i));
        }
    }

    private static void alCargar(Scene escena, LoadSceneMode modo)
    {
        preparar(escena);
    }

    private static void preparar(Scene escena)
    {
        if (!escena.isLoaded || (escena.name != "Aldea" && escena.name != "Subterraneo"))
        {
            return;
        }

        if (!crearMateriales())
        {
            return;
        }

        List<Transform> todos = new List<Transform>();
        foreach (GameObject r in escena.GetRootGameObjects())
        {
            todos.AddRange(r.GetComponentsInChildren<Transform>(true));
        }

        if (escena.name == "Aldea")
        {
            int fuentes = 0, lagunas = 0;
            foreach (Transform t in todos)
            {
                if (t.name == "Cylinder.030" && ancestro(t, "Fuente") != null && prepararFuente(t, todos))
                {
                    fuentes++;
                }

                if (t.name.StartsWith("Laguna") && t.Find("Roca_Borde_1.001") != null && prepararLaguna(t))
                {
                    lagunas++;
                }
            }

            Debug.Log($"[Goldy Pond] Agua en la Aldea: {fuentes} fuente(s), {lagunas} laguna(s).");
        }
        else
        {
            int n = 0;
            foreach (Transform t in todos)
            {
                Renderer r = t.GetComponent<Renderer>();
                if (r == null)
                {
                    continue;
                }

                if (t.name.StartsWith("Agua_Lago"))
                {
                    r.sharedMaterials = repetir(superficieSubterraneo, r.sharedMaterials.Length);
                    n++;
                }
                else if (t.name.StartsWith("Cascada_Izquierda") || t.name.StartsWith("Cascada_Derecha"))
                {
                    r.sharedMaterials = repetir(cascada, r.sharedMaterials.Length);
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    n++;
                }
            }

            Debug.Log($"[Goldy Pond] Agua en el Subterraneo: {n} pieza(s) convertidas en agua.");
        }
    }

    private static bool crearMateriales()
    {
        if (superficie != null)
        {
            return true;
        }

        Shader shader = Resources.Load<Shader>("Agua/Agua");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("[Goldy Pond] No se encontro el shader del agua (Resources/Agua/Agua).");
            return false;
        }

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

    private static Material[] repetir(Material m, int n) { Material[] a = new Material[Mathf.Max(1, n)]; for (int i = 0; i < a.Length; i++)
    {
        a[i] = m;
    }return a; }

    private static Transform ancestro(Transform t, string inicio)
    {
        for (Transform p = t.parent; p != null; p = p.parent)
        {
            if (p.name.StartsWith(inicio))
            {
                return p;
            }
        }

        return null;
    }

    // ------------------- FUENTE ------------------------------
    // Medidas del .blend (Goldy_Pond_Colores, grupo Fuente): pila Cylinder.030 de z 0.16 a 0.79, radio interior 2.34;
    // taza mediana Cylinder.035 (borde r 1.44 en z 2.28), taza chica Cylinder.037 (borde r 0.75 en z 3.25).
    private static bool prepararFuente(Transform pila, List<Transform> todos)
    {
        Renderer r = pila.GetComponent<Renderer>();
        if (r == null)
        {
            return false;
        }

        Transform raiz = ancestro(pila, "Fuente");
        if (raiz.Find("Agua de la fuente") != null)
        {
            return false;
        }

        Bounds b = r.bounds;
        float s = b.size.y / 0.63f;                    // metros de Unity por metro del .blend
        float alturaEn(float z)
        {
            return b.min.y + (z - 0.16f) * s;
        }

        Vector3 c = new Vector3(b.center.x, 0f, b.center.z);

        Transform agua = new GameObject("Agua de la fuente").transform;
        agua.SetParent(raiz, false);
        disco(agua, "Agua pila", new Vector3(c.x, alturaEn(0.72f), c.z), 2.40f * s, superficie);
        disco(agua, "Agua taza mediana", new Vector3(c.x, alturaEn(2.22f), c.z), 1.30f * s, superficie);
        disco(agua, "Agua taza chica", new Vector3(c.x, alturaEn(3.20f), c.z), 0.66f * s, superficie);
        // El agua que se desborda y cae de cada taza.
        cortina(agua, "Cae de la taza mediana", c, 1.47f * s, alturaEn(2.28f), alturaEn(0.72f), cascada);
        cortina(agua, "Cae de la taza chica", c, 0.78f * s, alturaEn(3.25f), alturaEn(2.22f), cascada);
        // Chorrito que sale de la punta dorada y cae a la taza chica.
        cortina(agua, "Chorro de arriba", c, 0.07f * s, alturaEn(4.35f), alturaEn(3.20f), cascada);
        return true;
    }

    // ------------------- LAGUNA ------------------------------
    // En el .blend: Terreno_Laguna.001 de z -1.0 a 0.12 (el suelo esta en 0); el agua va en z -0.08.
    private static bool prepararLaguna(Transform laguna)
    {
        if (laguna.Find("Agua de la laguna") != null)
        {
            return false;
        }

        List<Vector3> rocas = new List<Vector3>();
        Renderer terreno = null, repisa = null;
        foreach (Transform h in laguna)
        {
            Renderer r = h.GetComponent<Renderer>();
            if (r == null)
            {
                continue;
            }

            if (h.name.StartsWith("Roca_Borde_"))
            {
                rocas.Add(r.bounds.center);
            }
            else if (h.name.StartsWith("Terreno_Laguna"))
            {
                terreno = r;
            }
            else if (h.name.StartsWith("Cascada_Repisa"))
            {
                repisa = r;
            }
        }

        if (rocas.Count == 0 || terreno == null)
        {
            return false;
        }

        Vector3 c = Vector3.zero; foreach (Vector3 p in rocas)
        {
            c += p;
        }c /= rocas.Count;
        // Radio promedio del circulo de rocas: el hueco mide ~3 m y el borde del terreno sube entre 3 y 4 m;
        // mas grande se sale por fuera (afuera el terreno baja otra vez).
        float radio = 0f; foreach (Vector3 p in rocas)
        {
            radio += Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z));
        }

        radio /= rocas.Count;
        Bounds tb = terreno.bounds;
        float s = tb.size.y / 1.12f;
        float yAgua = tb.min.y + (1.0f - 0.08f) * s;

        Transform agua = new GameObject("Agua de la laguna").transform;
        agua.SetParent(laguna, false);
        disco(agua, "Agua laguna", new Vector3(c.x, yAgua, c.z), radio * 0.95f, superficie);

        if (repisa != null)
        {
            Bounds rb = repisa.bounds;
            Vector3 hacia = new Vector3(c.x - rb.center.x, 0f, c.z - rb.center.z).normalized;
            Vector3 arriba = new Vector3(rb.center.x, rb.max.y - 0.02f * s, rb.center.z) + hacia * (0.55f * s);
            caida(agua, "Cascada de la laguna", arriba, hacia, yAgua, 1.0f * s, cascada);
        }

        return true;
    }

    // ------------------- MALLAS ------------------------------

    private static void disco(
        Transform padre,
        string nombre,
        Vector3 centro,
        float radio,
        Material m
    )
    {
        const int LADOS = 48;
        Vector3[] v = new Vector3[LADOS + 1]; Vector3[] n = new Vector3[LADOS + 1]; int[] tri = new int[LADOS * 3];
        v[0] = Vector3.zero; n[0] = Vector3.up;
        for (int i = 0; i < LADOS; i++)
        {
            float a = i * Mathf.PI * 2f / LADOS;
            v[i + 1] = new Vector3(Mathf.Cos(a) * radio, 0f, Mathf.Sin(a) * radio); n[i + 1] = Vector3.up;
            tri[i * 3] = 0; tri[i * 3 + 1] = (i + 1) % LADOS + 1; tri[i * 3 + 2] = i + 1;
        }

        crear(padre, nombre, centro, v, n, tri, m);
    }

    private static void cortina(
        Transform padre,
        string nombre,
        Vector3 centro,
        float radio,
        float yArriba,
        float yAbajo,
        Material m
    )
    {
        const int LADOS = 40, PISOS = 6;
        List<Vector3> v = new List<Vector3>(); List<Vector3> n = new List<Vector3>(); List<int> tri = new List<int>();
        for (int j = 0; j <= PISOS; j++)
        {
            float k = j / (float)PISOS;
            float y = Mathf.Lerp(yArriba, yAbajo, k) - yAbajo;
            float rr = radio * (1f + 0.06f * k);          // se abre un poquito al caer
            for (int i = 0; i <= LADOS; i++)
            {
                float a = i * Mathf.PI * 2f / LADOS;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(d * rr + Vector3.up * y); n.Add(d);
            }
        }

        for (int j = 0; j < PISOS; j++)
        {
            for (int i = 0; i < LADOS; i++)
            {
                int a = j * (LADOS + 1) + i, b2 = a + LADOS + 1;
                tri.AddRange(new[] { a, b2, a + 1, a + 1, b2, b2 + 1 });
            }
        }

        crear(padre, nombre, new Vector3(centro.x, yAbajo, centro.z), v.ToArray(), n.ToArray(), tri.ToArray(), m);
    }

    private static void caida(
        Transform padre,
        string nombre,
        Vector3 arriba,
        Vector3 hacia,
        float yAbajo,
        float ancho,
        Material m
    )
    {
        const int PISOS = 8;
        Vector3 lado = Vector3.Cross(Vector3.up, hacia).normalized;
        List<Vector3> v = new List<Vector3>(); List<Vector3> n = new List<Vector3>(); List<int> tri = new List<int>();
        float alto = arriba.y - yAbajo;
        for (int j = 0; j <= PISOS; j++)
        {
            float k = j / (float)PISOS;
            // Sale un poco hacia adelante y cae (curva).
            Vector3 p = hacia * (Mathf.Sqrt(k) * 0.45f * alto * 0.5f) + Vector3.down * (k * alto);
            v.Add(p - lado * ancho * 0.5f); v.Add(p + lado * ancho * 0.5f);
            n.Add(hacia); n.Add(hacia);
        }

        for (int j = 0; j < PISOS; j++)
        {
            int a = j * 2;
            tri.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
        }

        crear(padre, nombre, arriba, v.ToArray(), n.ToArray(), tri.ToArray(), m);
    }

    private static void crear(
        Transform padre,
        string nombre,
        Vector3 posicion,
        Vector3[] v,
        Vector3[] n,
        int[] tri,
        Material m
    )
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetPositionAndRotation(posicion, Quaternion.identity);
        go.transform.SetParent(padre, true); // conserva su tamano real aunque el modelo tenga escala
        Mesh malla = new Mesh { name = nombre };
        malla.vertices = v; malla.normals = n; malla.triangles = tri;
        malla.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }
}
