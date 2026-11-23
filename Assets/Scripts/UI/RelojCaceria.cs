using UnityEngine;
using UnityEngine.UI;

// Cuenta regresiva abajo a la derecha (la crea sola la Caceria):
// - Descanso: en blanco (1:00, 0:59...). Los ultimos 3 segundos: 3, 2, 1 grandes y con "latido".
// - En el 0: "¡EMPIEZA LA CACERIA!" en rojo (y empieza la musica).
// - Caceria: en rojo, desde que los villanos empiezan a moverse. Se pausa mientras estas escondida.
// - Caceria infinita: "¡EMPIEZA LA CACERIA INFINITA!" y luego "CACERIA INFINITA" que crece, se encoge y vibra.
public class RelojCaceria : MonoBehaviour
{
    private static readonly Color Blanco = new Color(1f, 0.97f, 0.92f);
    private static readonly Color Rojo = new Color(0.95f, 0.12f, 0.1f);
    private const int TAMANO_NORMAL = 64;
    private const int TAMANO_GRANDE = 150;

    private Text texto;          // abajo a la derecha (reloj, 3-2-1)
    private Outline borde;
    private RectTransform rt;
    private Text textoCentro;    // ya no se usa: el aviso va abajo a la derecha como las cuentas
    private RectTransform rtCentro, rtLienzo;
    private float empiezaHasta = -1f;
    private int ultimoNumero = -1;
    private float popDesde;
    private Vector2 posBase;

    public static RelojCaceria crear(Transform padre)
    {
        GameObject lienzo = new GameObject("Reloj Caceria", typeof(Canvas), typeof(CanvasScaler));
        lienzo.transform.SetParent(padre, false);
        Canvas canvas = lienzo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        CanvasScaler esc = lienzo.GetComponent<CanvasScaler>();
        esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        esc.referenceResolution = new Vector2(1920, 1080);
        esc.matchWidthOrHeight = 1f;

        Text t = nuevoTexto("Reloj", lienzo.transform, new Vector2(1f, 0f), TextAnchor.LowerRight, out Outline borde);
        RectTransform r = t.rectTransform;
        r.anchoredPosition = new Vector2(-40f, 36f);
        r.sizeDelta = new Vector2(400f, 190f);
        // Si no cabe (ventana angosta) se hace mas chico en vez de encimarse en la barra de objetos.
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 20;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;

        Text tc = nuevoTexto("Aviso centro", lienzo.transform, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, out _);
        tc.rectTransform.anchoredPosition = new Vector2(0f, 170f);
        tc.rectTransform.sizeDelta = new Vector2(1500f, 220f);
        tc.resizeTextForBestFit = true;
        tc.resizeTextMinSize = 30;
        tc.resizeTextMaxSize = 120;
        tc.horizontalOverflow = HorizontalWrapMode.Wrap;
        tc.verticalOverflow = VerticalWrapMode.Truncate;

        RelojCaceria reloj = lienzo.AddComponent<RelojCaceria>();
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

    private static Text nuevoTexto(
        string nombre,
        Transform padre,
        Vector2 ancla,
        TextAnchor alineacion,
        out Outline borde
    )
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(padre, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = r.pivot = ancla;
        Text t = go.GetComponent<Text>();
        BarraHerramientas barra = BarraHerramientas.Instancia;
        t.font = barra != null && barra.fuente != null ? barra.fuente : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.alignment = alineacion;
        t.raycastTarget = false;
        t.fontStyle = FontStyle.Bold;
        borde = go.AddComponent<Outline>();
        borde.effectColor = new Color(0f, 0f, 0f, 0.85f);
        borde.effectDistance = new Vector2(3f, -3f);
        Shadow sombra = go.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.5f);
        sombra.effectDistance = new Vector2(0f, -6f);
        return t;
    }

    // Ancho libre a la derecha de la barra de objetos (en pixeles del lienzo).
    private float anchoLibre()
    {
        float anchoPantalla = rtLienzo.rect.width;
        BarraHerramientas b = BarraHerramientas.Instancia;
        float anchoBarra = b != null
            ? b.numeroCasillas * b.tamanoCasilla + (b.numeroCasillas - 1) * b.separacion + 2f * b.relleno
            : 620f;
        return Mathf.Clamp((anchoPantalla - anchoBarra) * 0.5f - 40f - 30f, 120f, 420f);
    }

    // Muestra "¡EMPIEZA LA CACERIA!" unos segundos.
    public void mostrarEmpieza(float segundos)
    {
        empiezaHasta = Time.time + segundos;
        popDesde = Time.time;
    }

    private void Update()
    {
        Caceria c = Caceria.Instancia;
        float escala = 1f;
        // En el laberinto no se ven los tiempos (ahi la caceria y el descanso estan en pausa).
        JugadorPrimeraPersona j = JugadorPrimeraPersona.Instancia;
        if (j != null && LaberintoAutomatico.contiene(j.transform.position))
        {
            texto.enabled = false;
            textoCentro.enabled = false;
            return;
        }

        rt.sizeDelta = new Vector2(anchoLibre(), rt.sizeDelta.y);
        rt.anchoredPosition = posBase;

        textoCentro.enabled = false;

        if (Caceria.Infinita)
        {
            bool inicio = Time.time < empiezaHasta;
            mostrar(inicio ? "¡EMPIEZA LA\nCACERÍA INFINITA!" : "CACERÍA\nINFINITA", Rojo, 80);
            // Late (crece y se encoge) y vibra un poquito, sin pasarse encima de la barra.
            float latido = 1f + 0.07f * Mathf.Sin(Time.unscaledTime * 6.5f);
            escala = (inicio ? Mathf.Lerp(1f, pop(0.35f), 0.3f) : 1f) * latido;
            rt.anchoredPosition = posBase + Random.insideUnitCircle * 2.5f;
            rt.localScale = Vector3.one * escala;
            return;
        }

        // "¡EMPIEZA LA CACERIA!" en el mismo lugar que las cuentas (abajo a la derecha), en dos renglones
        // para que quepa junto a la barra de objetos sin encimarse.
        if (Time.time < empiezaHasta)
        {
            mostrar("¡EMPIEZA\nLA CACERÍA!", Rojo, 80);
            float falta = empiezaHasta - Time.time;
            texto.color = new Color(Rojo.r, Rojo.g, Rojo.b, Mathf.Clamp01(falta / 0.5f)); // se desvanece al final
            escala = Mathf.Lerp(1f, pop(0.35f), 0.3f) * (1f + 0.03f * Mathf.Sin(Time.time * 10f)); // crece poquito: no toca la barra
        }
        else if (Caceria.FaseActual == Caceria.Fase.Descanso)
        {
            int s = Mathf.CeilToInt(Caceria.Restante);
            if (s <= 3 && s >= 1)
            {
                // 3, 2, 1: grandes, cada uno "cae" y se va desvaneciendo.
                if (s != ultimoNumero)
                {
                    ultimoNumero = s;
                    popDesde = Time.time;
                }

                mostrar(s.ToString(), Blanco, TAMANO_GRANDE);
                float k = Mathf.Clamp01(Time.time - popDesde);
                escala = Mathf.Lerp(1.6f, 1f, 1f - Mathf.Pow(1f - Mathf.Clamp01(k / 0.25f), 3f));
                texto.color = new Color(Blanco.r, Blanco.g, Blanco.b, Mathf.Lerp(1f, 0.35f, k));
            }
            else
            {
                ultimoNumero = -1;
                mostrar(formato(Caceria.Restante), Blanco, TAMANO_NORMAL);
            }
        }
        else if (Caceria.FaseActual == Caceria.Fase.Caceria && c != null && c.Liberados)
        {
            mostrar(formato(Caceria.Restante), Rojo, TAMANO_NORMAL);
            // Ultimos 10 segundos: late un poquito.
            if (Caceria.Restante <= 10f)
            {
                escala = 1f + 0.06f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
            }
        }
        else
        {
            texto.enabled = false;
        }

        rt.localScale = Vector3.one * escala;
    }

    private float pop(float duracion)
    {
        float k = Mathf.Clamp01((Time.time - popDesde) / duracion);
        return Mathf.Lerp(1.5f, 1f, 1f - Mathf.Pow(1f - k, 3f));
    }

    private void mostrar(string s, Color color, int tamano)
    {
        texto.enabled = true;
        texto.text = s;
        texto.color = color;
        texto.fontSize = tamano;
        texto.resizeTextMaxSize = tamano; // con "best fit" este es el tamano real (si cabe)
    }

    private static string formato(float segundos)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(segundos));
        return $"{s / 60}:{s % 60:00}";
    }
}
