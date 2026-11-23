using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cuenta regresiva abajo a la derecha (la crea sola la Caceria):
///  - Descanso: en blanco (1:00, 0:59...). Los ultimos 3 segundos: 3, 2, 1 grandes y con "latido".
///  - En el 0: "¡EMPIEZA LA CACERÍA!" en rojo (y empieza la musica).
///  - Cacería: en rojo, desde que los villanos empiezan a moverse. Se pausa mientras estas escondida.
///  - Cacería infinita: "¡EMPIEZA LA CACERÍA INFINITA!" y luego "CACERÍA INFINITA" que crece, se encoge y vibra.
/// </summary>
public class RelojCaceria : MonoBehaviour
{
    static readonly Color Blanco = new Color(1f, 0.97f, 0.92f);
    static readonly Color Rojo = new Color(0.95f, 0.12f, 0.1f);
    const int TamanoNormal = 64;
    const int TamanoGrande = 150;

    Text texto;          // abajo a la derecha (reloj, 3-2-1)
    Outline borde;
    RectTransform rt;
    Text textoCentro;    // ya no se usa: el aviso va abajo a la derecha como las cuentas
    RectTransform rtCentro, rtLienzo;
    float empiezaHasta = -1f;
    int ultimoNumero = -1;
    float popDesde;
    Vector2 posBase;

    public static RelojCaceria Crear(Transform padre)
    {
        var lienzo = new GameObject("Reloj Caceria", typeof(Canvas), typeof(CanvasScaler));
        lienzo.transform.SetParent(padre, false);
        var canvas = lienzo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var esc = lienzo.GetComponent<CanvasScaler>();
        esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        esc.referenceResolution = new Vector2(1920, 1080);
        esc.matchWidthOrHeight = 1f;

        var t = NuevoTexto("Reloj", lienzo.transform, new Vector2(1f, 0f), TextAnchor.LowerRight, out var borde);
        var r = t.rectTransform;
        r.anchoredPosition = new Vector2(-40f, 36f);
        r.sizeDelta = new Vector2(400f, 190f);
        // Si no cabe (ventana angosta) se hace mas chico en vez de encimarse en la barra de objetos.
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 20;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;

        var tc = NuevoTexto("Aviso centro", lienzo.transform, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, out _);
        tc.rectTransform.anchoredPosition = new Vector2(0f, 170f);
        tc.rectTransform.sizeDelta = new Vector2(1500f, 220f);
        tc.resizeTextForBestFit = true;
        tc.resizeTextMinSize = 30;
        tc.resizeTextMaxSize = 120;
        tc.horizontalOverflow = HorizontalWrapMode.Wrap;
        tc.verticalOverflow = VerticalWrapMode.Truncate;

        var reloj = lienzo.AddComponent<RelojCaceria>();
        reloj.texto = t;
        reloj.borde = borde;
        reloj.rt = r;
        reloj.textoCentro = tc;
        reloj.rtCentro = tc.rectTransform;
        reloj.rtLienzo = (RectTransform)lienzo.transform;
        reloj.posBase = r.anchoredPosition;
        t.enabled = false;
        tc.enabled = false;
        return reloj;
    }

    static Text NuevoTexto(string nombre, Transform padre, Vector2 ancla, TextAnchor alineacion, out Outline borde)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(padre, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = r.pivot = ancla;
        var t = go.GetComponent<Text>();
        var barra = BarraHerramientas.Instancia;
        t.font = barra != null && barra.fuente != null ? barra.fuente : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.alignment = alineacion;
        t.raycastTarget = false;
        t.fontStyle = FontStyle.Bold;
        borde = go.AddComponent<Outline>();
        borde.effectColor = new Color(0f, 0f, 0f, 0.85f);
        borde.effectDistance = new Vector2(3f, -3f);
        var sombra = go.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.5f);
        sombra.effectDistance = new Vector2(0f, -6f);
        return t;
    }

    /// <summary>Ancho libre a la derecha de la barra de objetos (en pixeles del lienzo).</summary>
    float AnchoLibre()
    {
        float anchoPantalla = rtLienzo.rect.width;
        var b = BarraHerramientas.Instancia;
        float anchoBarra = b != null
            ? b.numeroCasillas * b.tamanoCasilla + (b.numeroCasillas - 1) * b.separacion + 2f * b.relleno
            : 620f;
        return Mathf.Clamp((anchoPantalla - anchoBarra) * 0.5f - 40f - 30f, 120f, 420f);
    }

    /// <summary>Muestra "¡EMPIEZA LA CACERÍA!" unos segundos.</summary>
    public void MostrarEmpieza(float segundos)
    {
        empiezaHasta = Time.time + segundos;
        popDesde = Time.time;
    }

    void Update()
    {
        var c = Caceria.Instancia;
        float escala = 1f;
        // En el laberinto no se ven los tiempos (ahi la caceria y el descanso estan en pausa).
        var j = JugadorPrimeraPersona.Instancia;
        if (j != null && LaberintoAutomatico.Contiene(j.transform.position))
        {
            texto.enabled = false;
            textoCentro.enabled = false;
            return;
        }
        rt.sizeDelta = new Vector2(AnchoLibre(), rt.sizeDelta.y);
        rt.anchoredPosition = posBase;

        textoCentro.enabled = false;

        if (Caceria.Infinita)
        {
            bool inicio = Time.time < empiezaHasta;
            Mostrar(inicio ? "¡EMPIEZA LA\nCACERÍA INFINITA!" : "CACERÍA\nINFINITA", Rojo, 80);
            // Late (crece y se encoge) y vibra un poquito, sin pasarse encima de la barra.
            float latido = 1f + 0.07f * Mathf.Sin(Time.unscaledTime * 6.5f);
            escala = (inicio ? Mathf.Lerp(1f, Pop(0.35f), 0.3f) : 1f) * latido;
            rt.anchoredPosition = posBase + Random.insideUnitCircle * 2.5f;
            rt.localScale = Vector3.one * escala;
            return;
        }

        // "¡EMPIEZA LA CACERÍA!" en el mismo lugar que las cuentas (abajo a la derecha), en dos renglones
        // para que quepa junto a la barra de objetos sin encimarse.
        if (Time.time < empiezaHasta)
        {
            Mostrar("¡EMPIEZA\nLA CACERÍA!", Rojo, 80);
            float falta = empiezaHasta - Time.time;
            texto.color = new Color(Rojo.r, Rojo.g, Rojo.b, Mathf.Clamp01(falta / 0.5f)); // se desvanece al final
            escala = Mathf.Lerp(1f, Pop(0.35f), 0.3f) * (1f + 0.03f * Mathf.Sin(Time.time * 10f)); // crece poquito: no toca la barra
        }
        else if (Caceria.FaseActual == Caceria.Fase.Descanso)
        {
            int s = Mathf.CeilToInt(Caceria.Restante);
            if (s <= 3 && s >= 1)
            {
                // 3, 2, 1: grandes, cada uno "cae" y se va desvaneciendo.
                if (s != ultimoNumero) { ultimoNumero = s; popDesde = Time.time; }
                Mostrar(s.ToString(), Blanco, TamanoGrande);
                float k = Mathf.Clamp01(Time.time - popDesde);
                escala = Mathf.Lerp(1.6f, 1f, 1f - Mathf.Pow(1f - Mathf.Clamp01(k / 0.25f), 3f));
                texto.color = new Color(Blanco.r, Blanco.g, Blanco.b, Mathf.Lerp(1f, 0.35f, k));
            }
            else
            {
                ultimoNumero = -1;
                Mostrar(Formato(Caceria.Restante), Blanco, TamanoNormal);
            }
        }
        else if (Caceria.FaseActual == Caceria.Fase.Caceria && c != null && c.Liberados)
        {
            Mostrar(Formato(Caceria.Restante), Rojo, TamanoNormal);
            // Ultimos 10 segundos: late un poquito.
            if (Caceria.Restante <= 10f) escala = 1f + 0.06f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
        }
        else texto.enabled = false;

        rt.localScale = Vector3.one * escala;
    }

    float Pop(float duracion)
    {
        float k = Mathf.Clamp01((Time.time - popDesde) / duracion);
        return Mathf.Lerp(1.5f, 1f, 1f - Mathf.Pow(1f - k, 3f));
    }

    void Mostrar(string s, Color color, int tamano)
    {
        texto.enabled = true;
        texto.text = s;
        texto.color = color;
        texto.fontSize = tamano;
        texto.resizeTextMaxSize = tamano; // con "best fit" este es el tamano real (si cabe)
    }

    static string Formato(float segundos)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(segundos));
        return $"{s / 60}:{s % 60:00}";
    }
}
