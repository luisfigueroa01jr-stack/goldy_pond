using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Muestra una imagen grande al centro de la pantalla (ej. las reglas del juego) con un marco
// dorado y un boton rojo con X arriba a la derecha. Se cierra con la X, con E o con Esc.
// Mientras esta abierto el jugador no se mueve ni gira la camara y se ve el cursor.
// Uso: PanelLectura.Abrir(sprite);
// Modo "mientras mantienes E" (el pergamino): PanelLectura.Abrir(sprite, true) — sin X ni cursor,
// y se cierra solo al soltar la E.
public class PanelLectura : MonoBehaviour
{
    private const float ALTO_IMAGEN = 780f;     // en pixeles de referencia (pantalla de 1080 de alto)
    private const float MARCO = 26f;           // grosor del marco alrededor de la imagen
    private const float TAMANO_X = 78f;
    private const float ANIMACION = 0.18f;

    private static PanelLectura instancia;
    private static int cuadroCerrado = -1;

    // True mientras esta abierto (y el cuadro en que se cierra, para que esa misma E no lo vuelva a abrir).
    public static bool Bloqueando => (instancia != null && instancia.gameObject.activeSelf) || Time.frameCount == cuadroCerrado;

    private CanvasGroup grupo;
    private RectTransform hoja, botonX;
    private Image imagen, fondo, marco;
    private Shadow sombra;
    private RectTransform rtImagen;
    private float abiertoDesde;
    private bool sostener;

    // Se avisa cada vez que se cierra (la Caceria empieza al cerrar el libro de la plaza).
    public static event System.Action Cerrado;

    public static void abrirPanel(Sprite sprite)
    {
        abrirPanel(sprite, false);
    }

    // sostener = true: se ve solo mientras mantienes E (sin boton X ni cursor).
    public static void abrirPanel(Sprite sprite, bool sostener)
    {
        abrirPanel(sprite, sostener, false);
    }

    // soloImagen = true: sin marco, sin fondo oscuro y sin X; se cierra con E o Esc.
    public static void abrirPanel(Sprite sprite, bool sostener, bool soloImagen)
    {
        if (sprite == null)
        {
            Debug.LogWarning("[Goldy Pond] El libro no tiene imagen asignada.");
            return;
        }

        if (instancia == null && !crear())
        {
            return;
        }

        PanelLectura i = instancia;
        i.imagen.sprite = sprite;
        float aspecto = sprite.rect.width / sprite.rect.height;
        // Modo pergamino (sostener): solo la imagen, sin fondo oscuro, sin marco y sin sombra.
        bool sinAdornos = sostener || soloImagen;
        float m = sinAdornos ? 0f : MARCO;
        i.fondo.enabled = !sinAdornos;
        i.marco.enabled = !sinAdornos;
        i.sombra.enabled = !sinAdornos;
        i.rtImagen.offsetMin = new Vector2(m, m);
        i.rtImagen.offsetMax = new Vector2(-m, -m);
        i.hoja.sizeDelta = new Vector2(ALTO_IMAGEN * aspecto + m * 2f, ALTO_IMAGEN + m * 2f);
        i.abiertoDesde = Time.unscaledTime;
        i.sostener = sostener;
        i.botonX.gameObject.SetActive(!sinAdornos);
        i.gameObject.SetActive(true);
        i.transform.SetAsLastSibling();

        if (sinAdornos)
        {
            return; // el cursor se queda escondido
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void cerrarPanel()
    {
        if (instancia == null || !instancia.gameObject.activeSelf)
        {
            return;
        }

        instancia.gameObject.SetActive(false);
        cuadroCerrado = Time.frameCount;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Cerrado?.Invoke();
    }

    private void Update()
    {
        // Aparece con un pequeno "pop".
        float k = Mathf.Clamp01((Time.unscaledTime - abiertoDesde) / ANIMACION);
        float suave = 1f - Mathf.Pow(1f - k, 3f);
        grupo.alpha = suave;
        hoja.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, suave);

        Keyboard teclado = Keyboard.current;
        Gamepad control = Gamepad.current;
        if (sostener)
        {
            bool mantiene = (teclado != null && teclado.eKey.isPressed) || (control != null && control.buttonWest.isPressed);
            if (!mantiene)
            {
                cerrarPanel();
            }

            return;
        }

        Mouse mouse = Mouse.current;
        bool sobreX = mouse != null && botonX.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(botonX, mouse.position.ReadValue(), null);
        botonX.localScale = Vector3.one * (sobreX ? 1.12f : 1f);

        bool cerrar = (teclado != null && (teclado.eKey.wasPressedThisFrame || teclado.escapeKey.wasPressedThisFrame))
                   || (control != null && (control.buttonWest.wasPressedThisFrame || control.buttonEast.wasPressedThisFrame))
                   || (sobreX && mouse.leftButton.wasPressedThisFrame);
        // Que la misma E que lo abrio no lo cierre.
        if (cerrar && Time.unscaledTime - abiertoDesde > 0.05f)
        {
            cerrarPanel();
        }
    }

    private static bool crear()
    {
        BarraHerramientas barra = BarraHerramientas.Instancia;
        Canvas canvas = barra != null ? barra.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
        {
            GameObject go = new GameObject("HUD Lectura", typeof(Canvas), typeof(CanvasScaler));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler esc = go.GetComponent<CanvasScaler>();
            esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            esc.referenceResolution = new Vector2(1920, 1080);
            esc.matchWidthOrHeight = 1f;
        }

        int capa = canvas.gameObject.layer;

        // Fondo que oscurece un poco el juego.
        RectTransform raiz = nuevo("Panel Lectura", canvas.transform, capa);
        raiz.anchorMin = Vector2.zero; raiz.anchorMax = Vector2.one;
        raiz.offsetMin = raiz.offsetMax = Vector2.zero;
        Image oscuro = raiz.gameObject.AddComponent<Image>();
        oscuro.color = new Color(0.05f, 0.03f, 0.08f, 0.45f);
        oscuro.raycastTarget = false;
        CanvasGroup grupo = raiz.gameObject.AddComponent<CanvasGroup>();

        // Marco dorado (la "hoja").
        RectTransform hoja = nuevo("Marco", raiz, capa);
        hoja.anchorMin = hoja.anchorMax = new Vector2(0.5f, 0.5f);
        hoja.anchoredPosition = new Vector2(0f, 40f); // un poco arriba para no tapar la barra
        Image marco = hoja.gameObject.AddComponent<Image>();
        if (barra != null && barra.spriteMarco != null)
        {
            marco.sprite = barra.spriteMarco;
            marco.type = Image.Type.Sliced;
            marco.pixelsPerUnitMultiplier = 1f;
        }
        else
        {
            marco.color = new Color(0.95f, 0.8f, 0.5f);
        }

        marco.raycastTarget = false;
        Shadow sombra = hoja.gameObject.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.45f);
        sombra.effectDistance = new Vector2(0f, -8f);

        // La imagen (las reglas).
        RectTransform rtImagen = nuevo("Imagen", hoja, capa);
        rtImagen.anchorMin = Vector2.zero; rtImagen.anchorMax = Vector2.one;
        rtImagen.offsetMin = new Vector2(MARCO, MARCO);
        rtImagen.offsetMax = new Vector2(-MARCO, -MARCO);
        Image imagen = rtImagen.gameObject.AddComponent<Image>();
        imagen.preserveAspect = true;
        imagen.raycastTarget = false;

        // Boton rojo con X en la esquina de arriba a la derecha.
        RectTransform x = nuevo("Cerrar (X)", hoja, capa);
        x.anchorMin = x.anchorMax = new Vector2(1f, 1f);
        x.pivot = new Vector2(0.5f, 0.5f);
        x.anchoredPosition = new Vector2(-10f, -10f);
        x.sizeDelta = new Vector2(TAMANO_X, TAMANO_X);
        Image imgX = x.gameObject.AddComponent<Image>();
        imgX.sprite = Resources.Load<Sprite>("UI/Cerrar");
        imgX.preserveAspect = true;
        if (imgX.sprite == null)
        {
            imgX.color = new Color(0.7f, 0.12f, 0.14f);
        }

        instancia = raiz.gameObject.AddComponent<PanelLectura>();
        instancia.grupo = grupo;
        instancia.hoja = hoja;
        instancia.botonX = x;
        instancia.imagen = imagen;
        instancia.fondo = oscuro;
        instancia.marco = marco;
        instancia.sombra = sombra;
        instancia.rtImagen = rtImagen;
        raiz.gameObject.SetActive(false);
        return true;
    }

    private static RectTransform nuevo(string nombre, Transform padre, int capa)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.layer = capa;
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }
}
