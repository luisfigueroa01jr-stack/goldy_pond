using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

// Lugar del laberinto donde te puedes esconder con "(E) Ocultarse": los armarios, debajo de la cama
// y detras de los barriles del rincon. Lo agrega solo LaberintoAutomatico al cargar la escena.
// Al ocultarte la camara se mete rapidito al escondite; desde ahi puedes mirar un poco a los lados
// (lo que hay enfrente) y con E sales a donde estabas.
public class Escondite : Interactuable
{
    public enum Tipo { Armario, Cama, Barril }

    public Tipo tipo;
    [Tooltip("Como aparece en el aviso: (E) Ocultarse <nombre>.")]
    public string nombre = "aqui";
    [Tooltip("Donde quedan los ojos (la camara) al estar escondida.")]
    public Vector3 punto;
    [Tooltip("Hacia donde miras desde el escondite.")]
    public Vector3 frente = Vector3.forward;
    [Tooltip("Cuanto puedes voltear a cada lado (grados).")]
    public float limiteGiro = 55f;
    [Tooltip("Cuanto puedes ver hacia arriba / hacia abajo (grados).")]
    public float limiteArriba = 25f, limiteAbajo = 30f;

    public override string TextoAccion => $"Ocultarse <color=#FFDFA0>{nombre}</color>";
    public override bool Disponible
    {
        get { JugadorPrimeraPersona j = JugadorPrimeraPersona.Instancia; return j == null || !j.Oculto; }
    }

    public override bool interactuar(Transform jugador)
    {
        JugadorPrimeraPersona j = jugador != null ? jugador.GetComponent<JugadorPrimeraPersona>() : null;
        return j != null && j.ocultarse(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(punto, 0.08f);
        Gizmos.DrawLine(punto, punto + frente.normalized * 0.8f);
    }

    // ------------------- PREPARAR LOS ESCONDITES DEL LABERINTO ------------------------------

    private static readonly Regex PatronArmario = new Regex(@"^armario( \d+)?(\.\d+)?$");
    private static readonly Regex PatronBarril = new Regex(@"^(caja )?barril \d+(\.\d+)?$");
    // El rincon de los barriles (coordenadas del laberinto, como en Blender: x, y -> Unity x, z).
    private static readonly Vector3 RinconBarriles = new Vector3(2.3f, 0f, -8.6f);

    // Agrega Escondite a los armarios, la cama y los barriles del rincon. Devuelve cuantos.
    public static int prepararEn(Transform laberinto)
    {
        int n = 0;
        Vector3 centro = laberinto.position;
        Vector3 rincon = laberinto.TransformPoint(RinconBarriles);
        foreach (Transform t in laberinto.GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<Escondite>() != null)
            {
                continue;
            }

            Renderer r = t.GetComponent<Renderer>();
            if (r == null)
            {
                continue;
            }

            if (PatronArmario.IsMatch(t.name))
            {
                prepararArmario(t, r);
                n++;
            }
            else if (t.name == "cama")
            {
                prepararCama(t, r, centro);
                n++;
            }
            else if (PatronBarril.IsMatch(t.name))
            {
                Vector3 d = r.bounds.center - rincon; d.y = 0f;
                if (d.magnitude < 2.8f && prepararBarril(t, r, centro))
                {
                    n++;
                }
            }
        }

        return n;
    }

    private static Escondite agregar(Transform t, Tipo tipo, string nombre)
    {
        Escondite e = t.gameObject.AddComponent<Escondite>();
        e.tipo = tipo;
        e.nombre = nombre;
        if (t.GetComponentInChildren<Collider>() == null)
        {
            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf != null)
            {
                t.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
            else
            {
                t.gameObject.AddComponent<BoxCollider>();
            }
        }

        return e;
    }

    private static Vector3 plano(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    // Adentro del armario, mirando hacia las puertas (que estan entreabiertas).
    private static void prepararArmario(Transform t, Renderer r)
    {
        Bounds b = r.bounds;
        // El frente: hacia donde estan las puertas (hijos del armario).
        Vector3 puertas = Vector3.zero; int k = 0;
        foreach (Transform h in t)
        {
            Renderer rh = h.GetComponent<Renderer>();
            if (rh != null)
            {
                puertas += rh.bounds.center;
                k++;
            }
        }

        Vector3 frente = k > 0 ? plano(puertas / k - b.center) : plano(-t.forward);
        Escondite e = agregar(t, Tipo.Armario, "en el armario");
        e.frente = frente;
        float y = Mathf.Min(b.min.y + 1.05f, b.max.y - 0.45f);
        e.punto = new Vector3(b.center.x, y, b.center.z) - frente * 0.08f;
        e.limiteGiro = 45f; e.limiteArriba = 20f; e.limiteAbajo = 30f;
    }

    // Acostada debajo de la cama, mirando hacia el lado mas abierto del cuarto.
    private static void prepararCama(Transform t, Renderer r, Vector3 centroLaberinto)
    {
        Bounds b = r.bounds;
        MeshFilter mf = t.GetComponent<MeshFilter>();
        // Los lados largos miran hacia el eje corto de la cama.
        Vector3 eje = t.right;
        if (mf != null && mf.sharedMesh != null)
        {
            Vector3 s = mf.sharedMesh.bounds.size;
            eje = s.x < s.z ? t.right : t.forward;
        }

        eje = plano(eje);
        // De los dos lados, el que tiene mas espacio libre enfrente (y si empatan, el que ve al centro).
        float espacioLibre(Vector3 dir)
        {
            Vector3 desde = new Vector3(b.center.x, b.min.y + 0.2f, b.center.z) + dir * (ancho(b, dir) + 0.05f);
            return Physics.Raycast(desde, dir, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 8f;
        }

        float a = espacioLibre(eje), c = espacioLibre(-eje);
        Vector3 frente = Mathf.Abs(a - c) > 0.3f ? (a > c ? eje : -eje)
                   : (Vector3.Dot(eje, centroLaberinto - b.center) >= 0f ? eje : -eje);

        Escondite e = agregar(t, Tipo.Cama, "debajo de la cama");
        e.frente = frente;
        e.punto = new Vector3(b.center.x, b.min.y + 0.17f, b.center.z) + frente * Mathf.Max(0f, ancho(b, frente) - 0.2f);
        e.limiteGiro = 50f; e.limiteArriba = 8f; e.limiteAbajo = 12f;
    }

    // Agachada detras del barril, entre el barril y la pared, asomandote por encima.
    private static bool prepararBarril(Transform t, Renderer r, Vector3 centroLaberinto)
    {
        Bounds b = r.bounds;
        Vector3 afuera = plano(b.center - centroLaberinto);   // la pared queda hacia afuera del laberinto
        float radio = Mathf.Max(b.extents.x, b.extents.z);
        Vector3 desde = new Vector3(b.center.x, b.min.y + 0.4f, b.center.z) + afuera * (radio + 0.05f);
        if (!Physics.Raycast(desde, afuera, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        float d = hit.distance;
        if (d < 0.45f || d > 1.6f)
        {
            return false; // sin hueco o demasiado lejos de la pared
        }

        Vector3 lugar = desde + afuera * Mathf.Clamp(d * 0.5f, 0.22f, d - 0.22f);
        if (Physics.CheckSphere(lugar + Vector3.up * 0.1f, 0.15f, ~0, QueryTriggerInteraction.Ignore))
        {
            return false; // hay algo ahi
        }

        Escondite e = agregar(t, Tipo.Barril, "detrás del barril");
        e.frente = -afuera;
        e.punto = new Vector3(lugar.x, b.min.y + 0.72f, lugar.z);
        e.limiteGiro = 70f; e.limiteArriba = 25f; e.limiteAbajo = 30f;
        return true;
    }

    private static float ancho(Bounds b, Vector3 dir)
    {
        return Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
    }

    // ------------------- OSCURECER LAS ORILLAS MIENTRAS ESTAS ESCONDIDA ------------------------------

    private static Image vineta;

    public static void oscurecer(float alfa)
    {
        if (vineta == null)
        {
            if (alfa <= 0f)
            {
                return;
            }

            GameObject go = new GameObject("Vineta Escondite", typeof(Canvas));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;
            GameObject img = new GameObject("Orillas", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(go.transform, false);
            RectTransform rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            vineta = img.GetComponent<Image>();
            vineta.raycastTarget = false;
            vineta.sprite = spriteOrillas();
        }

        vineta.color = new Color(1f, 1f, 1f, alfa);
        vineta.enabled = alfa > 0.001f;
    }

    private static Sprite spriteOrillas()
    {
        const int N = 128;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1.25f, Mathf.Sqrt(u * u + v * v)));
                px[y * N + x] = new Color32(5, 3, 8, (byte)(a * 235));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
    }
}
