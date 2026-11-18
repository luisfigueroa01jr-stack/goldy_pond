using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mensajito abajo a la izquierda de la pantalla (ej. "La puerta esta cerrada. Necesitas una llave").
/// Entra deslizandose, se queda unos segundos y se desvanece. Usa el mismo marco dorado y la fuente
/// de la barra de herramientas. Uso: Notificacion.Mostrar("texto");
/// </summary>
public class Notificacion : MonoBehaviour
{
    const float Entrada = 0.25f, Salida = 0.45f, Deslizar = 40f;
    static readonly Vector2 Posicion = new Vector2(32f, 32f);

    static Notificacion actual;

    CanvasGroup grupo;
    RectTransform rt;
    Text texto;
    Image icono;
    float inicio, duracion;

    /// <summary>Muestra el mensaje (si ya hay uno, lo reemplaza y reinicia el tiempo).</summary>
    public static void Mostrar(string mensaje, Sprite icono = null, float segundos = 3.5f)
    {
        if (actual == null && !Crear()) return;
        bool yaVisible = actual.gameObject.activeSelf && actual.Visibilidad() > 0.9f && actual.texto.text == mensaje;
        actual.texto.text = mensaje;
        actual.icono.sprite = icono;
        actual.icono.gameObject.SetActive(icono != null);
        actual.duracion = segundos;
        // Si ya se esta viendo el mismo mensaje solo alarga el tiempo, sin volver a animar la entrada.
        actual.inicio = yaVisible ? Time.unscaledTime - Entrada : Time.unscaledTime;
        actual.gameObject.SetActive(true);
        actual.transform.SetAsLastSibling();
    }

    float Visibilidad() => grupo != null ? grupo.alpha : 0f;

    void Update()
    {
        float t = Time.unscaledTime - inicio;
        float alfa, x;
        if (t < Entrada)
        {
            float k = 1f - Mathf.Pow(1f - t / Entrada, 3f); // frena al llegar
            alfa = k; x = -Deslizar * (1f - k);
        }
        else if (t < Entrada + duracion) { alfa = 1f; x = 0f; }
        else
        {
            float k = Mathf.Clamp01((t - Entrada - duracion) / Salida);
            alfa = 1f - k; x = 0f;
            if (k >= 1f) { gameObject.SetActive(false); return; }
        }
        grupo.alpha = alfa;
        rt.anchoredPosition = Posicion + new Vector2(x, 0f);
    }

    static bool Crear()
    {
        var barra = BarraHerramientas.Instancia;
        Canvas canvas = barra != null ? barra.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
        {
            var go = new GameObject("HUD Notificaciones", typeof(Canvas), typeof(CanvasScaler));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var esc = go.GetComponent<CanvasScaler>();
            esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            esc.referenceResolution = new Vector2(1920, 1080);
            esc.matchWidthOrHeight = 0.5f;
        }

        var raiz = new GameObject("Notificacion", typeof(RectTransform), typeof(CanvasGroup));
        raiz.layer = canvas.gameObject.layer;
        var rt = (RectTransform)raiz.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero; // esquina de abajo a la izquierda
        rt.anchoredPosition = Posicion;

        var fondo = raiz.AddComponent<Image>();
        if (barra != null && barra.spriteMarco != null)
        {
            fondo.sprite = barra.spriteMarco;
            fondo.type = Image.Type.Sliced;
            fondo.pixelsPerUnitMultiplier = 2.4f; // marco delgadito
        }
        else fondo.color = new Color(0.25f, 0.13f, 0.12f, 0.9f);
        fondo.raycastTarget = false;

        var fila = raiz.AddComponent<HorizontalLayoutGroup>();
        fila.padding = new RectOffset(18, 22, 10, 12);
        fila.spacing = 10f;
        fila.childAlignment = TextAnchor.MiddleLeft;
        fila.childControlWidth = fila.childControlHeight = true;
        fila.childForceExpandWidth = fila.childForceExpandHeight = false;
        var ajuste = raiz.AddComponent<ContentSizeFitter>();
        ajuste.horizontalFit = ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var goIcono = new GameObject("Icono", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        goIcono.layer = raiz.layer;
        goIcono.transform.SetParent(rt, false);
        var icono = goIcono.GetComponent<Image>();
        icono.preserveAspect = true;
        icono.raycastTarget = false;
        var le = goIcono.GetComponent<LayoutElement>();
        le.preferredWidth = le.preferredHeight = 26f;

        var goTexto = new GameObject("Texto", typeof(RectTransform));
        goTexto.layer = raiz.layer;
        goTexto.transform.SetParent(rt, false);
        var texto = goTexto.AddComponent<Text>();
        texto.font = barra != null && barra.fuente != null ? barra.fuente : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = 21;
        texto.color = new Color(1f, 0.95f, 0.88f);
        texto.alignment = TextAnchor.MiddleLeft;
        texto.supportRichText = true;
        texto.raycastTarget = false;
        var sombra = goTexto.AddComponent<Shadow>();
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
