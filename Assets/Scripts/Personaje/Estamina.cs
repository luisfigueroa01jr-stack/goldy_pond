using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Estamina del jugador (la agrega sola JugadorPrimeraPersona). Barra arriba de la barra de objetos.
///  - Correr gasta estamina; saltar cuesta un poquito.
///  - Al dejar de correr, despues de un momento se recupera (mas rapido si estas quieta).
///  - Si se acaba: "agotada", no puedes correr hasta recuperar un poco y caminas mas lento.
/// Con estos numeros, corriendo y caminando a ratos vas en promedio ~3.9 m/s:
/// un poco mas rapido que los villanos (~3.3 m/s), asi que se puede huir pero sin descuidarse.
/// </summary>
public class Estamina : MonoBehaviour
{
    [Header("Estamina")]
    public float maxima = 100f;
    [Tooltip("Cuanto gasta por segundo corriendo (100 / 22 = unos 4.5 s corriendo).")]
    public float gastoCorrer = 22f;
    [Tooltip("Cuanto cuesta cada salto.")]
    public float gastoSalto = 12f;

    [Header("Recuperacion")]
    [Tooltip("Segundos despues de dejar de correr para empezar a recuperarse.")]
    public float espera = 0.8f;
    [Tooltip("Recuperacion por segundo caminando.")]
    public float recuperarCaminando = 14f;
    [Tooltip("Recuperacion por segundo quieta.")]
    public float recuperarQuieta = 22f;

    [Header("Agotada")]
    [Tooltip("Si se acaba, no puedes volver a correr hasta tener este porcentaje (0-1).")]
    [Range(0f, 1f)] public float minimoParaCorrer = 0.35f;
    [Tooltip("Que tan lento caminas agotada (1 = normal).")]
    [Range(0.4f, 1f)] public float caminarAgotada = 0.8f;

    public float Actual { get; private set; }
    public float Porcentaje => Actual / maxima;
    public bool Agotada { get; private set; }
    public bool PuedeCorrer => Activa && !Agotada && Actual > 0f;

    /// <summary>En el laberinto no hay estamina: no se corre (se camina normal) y no sale la barra.</summary>
    public bool Activa { get; private set; } = true;

    float ultimoGasto = -10f;
    int cuadroCorriendo = -1;
    bool moviendose;

    // HUD
    CanvasGroup grupo;
    Image relleno, fondo;
    float visible;

    void Awake()
    {
        Actual = maxima;
        CrearBarra();
    }

    /// <summary>Lo llama el jugador cada cuadro que se mueve. corriendo = Shift + moviendose + con estamina.</summary>
    public void Avisar(bool corriendo, bool seMueve)
    {
        moviendose = seMueve;
        if (!corriendo) return;
        cuadroCorriendo = Time.frameCount;
        Gastar(gastoCorrer * Time.deltaTime);
    }

    /// <summary>Gasta para saltar. false si no alcanza.</summary>
    public bool Saltar()
    {
        if (Agotada || Actual < gastoSalto * 0.5f) return false;
        Gastar(gastoSalto);
        return true;
    }

    void Gastar(float cuanto)
    {
        Actual = Mathf.Max(0f, Actual - cuanto);
        ultimoGasto = Time.time;
        if (Actual <= 0f) Agotada = true;
    }

    void Update()
    {
        Activa = !LaberintoAutomatico.Contiene(transform.position);
        if (!Activa)
        {
            // Al salir del laberinto vuelves con la estamina llena.
            Actual = maxima; Agotada = false; visible = 0f;
            if (grupo != null) grupo.alpha = Mathf.MoveTowards(grupo.alpha, 0f, Time.deltaTime * 4f);
            return;
        }
        bool corriendoAhora = cuadroCorriendo >= Time.frameCount - 1;
        if (!corriendoAhora && Time.time - ultimoGasto >= espera && Actual < maxima)
        {
            Actual = Mathf.Min(maxima, Actual + (moviendose ? recuperarCaminando : recuperarQuieta) * Time.deltaTime);
            if (Agotada && Porcentaje >= minimoParaCorrer) Agotada = false;
        }
        moviendose = false; // si el jugador no avisa (leyendo el libro, en el elevador...) cuenta como quieta
        ActualizarBarra();
    }

    // ---------- Barra ----------

    void CrearBarra()
    {
        var lienzo = new GameObject("HUD Estamina", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        lienzo.transform.SetParent(transform, false);
        var canvas = lienzo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        var esc = lienzo.GetComponent<CanvasScaler>();
        esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        esc.referenceResolution = new Vector2(1920, 1080);
        esc.matchWidthOrHeight = 1f;
        grupo = lienzo.GetComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.blocksRaycasts = false;

        // Marco oscuro, justo arriba de la barra de objetos (que termina a ~160 px del suelo).
        fondo = Imagen("Fondo", lienzo.transform, new Color(0.08f, 0.05f, 0.04f, 0.75f));
        var rf = fondo.rectTransform;
        rf.anchorMin = rf.anchorMax = rf.pivot = new Vector2(0.5f, 0f);
        rf.anchoredPosition = new Vector2(0f, 172f);
        rf.sizeDelta = new Vector2(340f, 18f);
        var borde = fondo.gameObject.AddComponent<Outline>();
        borde.effectColor = new Color(0.95f, 0.8f, 0.5f, 0.8f);
        borde.effectDistance = new Vector2(2f, -2f);

        relleno = Imagen("Relleno", fondo.transform, Color.white);
        var rr = relleno.rectTransform;
        rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
        rr.offsetMin = new Vector2(3f, 3f); rr.offsetMax = new Vector2(-3f, -3f);
        rr.pivot = new Vector2(0f, 0.5f);
    }

    static Image Imagen(string nombre, Transform padre, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    void ActualizarBarra()
    {
        if (relleno == null) return;
        float p = Porcentaje;
        relleno.rectTransform.localScale = new Vector3(p, 1f, 1f);

        Color dorado = new Color(1f, 0.84f, 0.42f), naranja = new Color(1f, 0.52f, 0.15f), rojo = new Color(0.95f, 0.15f, 0.1f);
        Color c = p > 0.5f ? Color.Lerp(naranja, dorado, (p - 0.5f) * 2f) : Color.Lerp(rojo, naranja, p * 2f);
        if (Agotada) c = Color.Lerp(rojo, new Color(0.45f, 0.05f, 0.05f), 0.5f + 0.5f * Mathf.Sin(Time.time * 12f)); // parpadea
        relleno.color = c;

        // Se ve mientras no este llena; ya llena se desvanece despues de un ratito.
        if (p < 0.999f) visible = 1.2f; else visible -= Time.deltaTime;
        float objetivo = visible > 0f ? 1f : 0f;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, objetivo, Time.deltaTime * 4f);
    }
}
