using UnityEngine;
using UnityEngine.UI;

// Mensajito abajo a la izquierda de la pantalla (ej. "La puerta esta cerrada. Necesitas una llave").
// Entra deslizandose, se queda unos segundos y se desvanece. Usa el mismo marco dorado y la fuente
// de la barra de herramientas. Uso: Notificacion.Mostrar("texto");
public class Notificacion : MonoBehaviour
{
    private const float ENTRADA = 0.25f, SALIDA = 0.45f, DESLIZAR = 40f;
    private static readonly Vector2 Posicion = new Vector2(32f, 32f);

    private static Notificacion actual;

    private CanvasGroup grupo;
    private RectTransform rt;
    private Text texto;
    private Image icono;
    private float inicio, duracion;

    // Muestra el mensaje (si ya hay uno, lo reemplaza y reinicia el tiempo).
    public static void mostrar(string mensaje, Sprite icono = null, float segundos = 3.5f)
    {
        if (actual == null && !crear())
        {
            return;
        }

        bool yaVisible = actual.gameObject.activeSelf && actual.visibilidad() > 0.9f && actual.texto.text == mensaje;
        actual.texto.text = mensaje;
        actual.icono.sprite = icono;
        actual.icono.gameObject.SetActive(icono != null);
        actual.duracion = segundos;
        // Si ya se esta viendo el mismo mensaje solo alarga el tiempo, sin volver a animar la entrada.
        actual.inicio = yaVisible ? Time.unscaledTime - ENTRADA : Time.unscaledTime;
        actual.gameObject.SetActive(true);
        actual.transform.SetAsLastSibling();
    }

    private float visibilidad()
    {
        return grupo != null ? grupo.alpha : 0f;
    }

    private void Update()
    {
        float t = Time.unscaledTime - inicio;
        float alfa, x;
        if (t < ENTRADA)
        {
            float k = 1f - Mathf.Pow(1f - t / ENTRADA, 3f); // frena al llegar
            alfa = k; x = -DESLIZAR * (1f - k);
        }
        else if (t < ENTRADA + duracion)
        {
            alfa = 1f;
            x = 0f;
        }
        else
        {
            float k = Mathf.Clamp01((t - ENTRADA - duracion) / SALIDA);
            alfa = 1f - k; x = 0f;
            if (k >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }
        }

        grupo.alpha = alfa;
        rt.anchoredPosition = Posicion + new Vector2(x, 0f);
    }

    private static bool crear()
    {
        BarraHerramientas barra = BarraHerramientas.Instancia;
        Canvas canvas = barra != null ? barra.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
        {
            GameObject go = new GameObject("HUD Notificaciones", typeof(Canvas), typeof(CanvasScaler));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler esc = go.GetComponent<CanvasScaler>();
            esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            esc.referenceResolution = new Vector2(1920, 1080);
            esc.matchWidthOrHeight = 0.5f;
        }

        GameObject raiz = new GameObject("Notificacion", typeof(RectTransform), typeof(CanvasGroup));
        raiz.layer = canvas.gameObject.layer;
        RectTransform rt = (RectTransform)raiz.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero; // esquina de abajo a la izquierda
        rt.anchoredPosition = Posicion;

        Image fondo = raiz.AddComponent<Image>();
        if (barra != null && barra.spriteMarco != null)
        {
            fondo.sprite = barra.spriteMarco;
            fondo.type = Image.Type.Sliced;
            fondo.pixelsPerUnitMultiplier = 2.4f; // marco delgadito
        }
        else
        {
            fondo.color = new Color(0.25f, 0.13f, 0.12f, 0.9f);
        }

        fondo.raycastTarget = false;

        HorizontalLayoutGroup fila = raiz.AddComponent<HorizontalLayoutGroup>();
        fila.padding = new RectOffset(18, 22, 10, 12);
        fila.spacing = 10f;
        fila.childAlignment = TextAnchor.MiddleLeft;
        fila.childControlWidth = fila.childControlHeight = true;
        fila.childForceExpandWidth = fila.childForceExpandHeight = false;
        ContentSizeFitter ajuste = raiz.AddComponent<ContentSizeFitter>();
        ajuste.horizontalFit = ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject goIcono = new GameObject("Icono", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        goIcono.layer = raiz.layer;
        goIcono.transform.SetParent(rt, false);
        Image icono = goIcono.GetComponent<Image>();
        icono.preserveAspect = true;
        icono.raycastTarget = false;
        LayoutElement le = goIcono.GetComponent<LayoutElement>();
        le.preferredWidth = le.preferredHeight = 26f;

        GameObject goTexto = new GameObject("Texto", typeof(RectTransform));
        goTexto.layer = raiz.layer;
        goTexto.transform.SetParent(rt, false);
        Text texto = goTexto.AddComponent<Text>();
        texto.font = barra != null && barra.fuente != null ? barra.fuente : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = 21;
        texto.color = new Color(1f, 0.95f, 0.88f);
        texto.alignment = TextAnchor.MiddleLeft;
        texto.supportRichText = true;
        texto.raycastTarget = false;
        Shadow sombra = goTexto.AddComponent<Shadow>();
        sombra.effectColor = new Color(0.2f, 0.1f, 0.2f, 0.7f);
        sombra.effectDistance = new Vector2(1f, -1f);

        actual = raiz.AddComponent<Notificacion>();
        actual.grupo = raiz.GetComponent<CanvasGroup>();
        actual.grupo.blocksRaycasts = actual.grupo.interactable = false;
        actual.rt = rt;
        actual.texto = texto;
        actual.icono = icono;
        raiz.SetActive(false);
        return true;
    }
}
