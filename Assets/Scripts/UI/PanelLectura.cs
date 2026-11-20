using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Muestra una imagen grande al centro de la pantalla (ej. las reglas del juego) con un marco
/// dorado y un boton rojo con X arriba a la derecha. Se cierra con la X, con E o con Esc.
/// Mientras esta abierto el jugador no se mueve ni gira la camara y se ve el cursor.
/// Uso: PanelLectura.Abrir(sprite);
/// Modo "mientras mantienes E" (el pergamino): PanelLectura.Abrir(sprite, true) — sin X ni cursor,
/// y se cierra solo al soltar la E.
/// </summary>
public class PanelLectura : MonoBehaviour
{
    const float AltoImagen = 780f;     // en pixeles de referencia (pantalla de 1080 de alto)
    const float Marco = 26f;           // grosor del marco alrededor de la imagen
    const float TamanoX = 78f;
    const float Animacion = 0.18f;

    static PanelLectura instancia;
    static int cuadroCerrado = -1;

    /// <summary>True mientras esta abierto (y el cuadro en que se cierra, para que esa misma E no lo vuelva a abrir).</summary>
    public static bool Bloqueando => (instancia != null && instancia.gameObject.activeSelf) || Time.frameCount == cuadroCerrado;

    CanvasGroup grupo;
    RectTransform hoja, botonX;
    Image imagen, fondo, marco;
    Shadow sombra;
    RectTransform rtImagen;
    float abiertoDesde;
    bool sostener;

    public static void Abrir(Sprite sprite) => Abrir(sprite, false);

    /// <summary>sostener = true: se ve solo mientras mantienes E (sin boton X ni cursor).</summary>
    public static void Abrir(Sprite sprite, bool sostener)
    {
        if (sprite == null) { Debug.LogWarning("[Goldy Pond] El libro no tiene imagen asignada."); return; }
        if (instancia == null && !Crear()) return;

        var i = instancia;
        i.imagen.sprite = sprite;
        float aspecto = sprite.rect.width / sprite.rect.height;
        // Modo pergamino (sostener): solo la imagen, sin fondo oscuro, sin marco y sin sombra.
        float m = sostener ? 0f : Marco;
        i.fondo.enabled = !sostener;
        i.marco.enabled = !sostener;
        i.sombra.enabled = !sostener;
        i.rtImagen.offsetMin = new Vector2(m, m);
        i.rtImagen.offsetMax = new Vector2(-m, -m);
        i.hoja.sizeDelta = new Vector2(AltoImagen * aspecto + m * 2f, AltoImagen + m * 2f);
        i.abiertoDesde = Time.unscaledTime;
        i.sostener = sostener;
        i.botonX.gameObject.SetActive(!sostener);
        i.gameObject.SetActive(true);
        i.transform.SetAsLastSibling();

        if (sostener) return; // el cursor se queda escondido
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void Cerrar()
    {
        if (instancia == null || !instancia.gameObject.activeSelf) return;
        instancia.gameObject.SetActive(false);
        cuadroCerrado = Time.frameCount;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Aparece con un pequeno "pop".
        float k = Mathf.Clamp01((Time.unscaledTime - abiertoDesde) / Animacion);
        float suave = 1f - Mathf.Pow(1f - k, 3f);
        grupo.alpha = suave;
        hoja.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, suave);

        var teclado = Keyboard.current;
        var control = Gamepad.current;
        if (sostener)
        {
            bool mantiene = (teclado != null && teclado.eKey.isPressed) || (control != null && control.buttonWest.isPressed);
            if (!mantiene) Cerrar();
            return;
        }

        var mouse = Mouse.current;
        bool sobreX = mouse != null && RectTransformUtility.RectangleContainsScreenPoint(botonX, mouse.position.ReadValue(), null);
        botonX.localScale = Vector3.one * (sobreX ? 1.12f : 1f);

        bool cerrar = (teclado != null && (teclado.eKey.wasPressedThisFrame || teclado.escapeKey.wasPressedThisFrame))
                   || (control != null && (control.buttonWest.wasPressedThisFrame || control.buttonEast.wasPressedThisFrame))
                   || (sobreX && mouse.leftButton.wasPressedThisFrame);
        // Que la misma E que lo abrio no lo cierre.
        if (cerrar && Time.unscaledTime - abiertoDesde > 0.05f) Cerrar();
    }

    static bool Crear()
    {
        var barra = BarraHerramientas.Instancia;
        Canvas canvas = barra != null ? barra.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
        {
            var go = new GameObject("HUD Lectura", typeof(Canvas), typeof(CanvasScaler));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var esc = go.GetComponent<CanvasScaler>();
            esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            esc.referenceResolution = new Vector2(1920, 1080);
            esc.matchWidthOrHeight = 1f;
        }
        int capa = canvas.gameObject.layer;

        // Fondo que oscurece un poco el juego.
        var raiz = Nuevo("Panel Lectura", canvas.transform, capa);
        raiz.anchorMin = Vector2.zero; raiz.anchorMax = Vector2.one;
        raiz.offsetMin = raiz.offsetMax = Vector2.zero;
        var oscuro = raiz.gameObject.AddComponent<Image>();
        oscuro.color = new Color(0.05f, 0.03f, 0.08f, 0.45f);
        oscuro.raycastTarget = false;
        var grupo = raiz.gameObject.AddComponent<CanvasGroup>();

        // Marco dorado (la "hoja").
        var hoja = Nuevo("Marco", raiz, capa);
        hoja.anchorMin = hoja.anchorMax = new Vector2(0.5f, 0.5f);
        hoja.anchoredPosition = new Vector2(0f, 40f); // un poco arriba para no tapar la barra
        var marco = hoja.gameObject.AddComponent<Image>();
        if (barra != null && barra.spriteMarco != null)
        {
            marco.sprite = barra.spriteMarco;
            marco.type = Image.Type.Sliced;
            marco.pixelsPerUnitMultiplier = 1f;
        }
        else marco.color = new Color(0.95f, 0.8f, 0.5f);
        marco.raycastTarget = false;
        var sombra = hoja.gameObject.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.45f);
        sombra.effectDistance = new Vector2(0f, -8f);

        // La imagen (las reglas).
        var rtImagen = Nuevo("Imagen", hoja, capa);
        rtImagen.anchorMin = Vector2.zero; rtImagen.anchorMax = Vector2.one;
        rtImagen.offsetMin = new Vector2(Marco, Marco);
        rtImagen.offsetMax = new Vector2(-Marco, -Marco);
        var imagen = rtImagen.gameObject.AddComponent<Image>();
        imagen.preserveAspect = true;
        imagen.raycastTarget = false;

        // Boton rojo con X en la esquina de arriba a la derecha.
        var x = Nuevo("Cerrar (X)", hoja, capa);
        x.anchorMin = x.anchorMax = new Vector2(1f, 1f);
        x.pivot = new Vector2(0.5f, 0.5f);
        x.anchoredPosition = new Vector2(-10f, -10f);
        x.sizeDelta = new Vector2(TamanoX, TamanoX);
        var imgX = x.gameObject.AddComponent<Image>();
        imgX.sprite = Resources.Load<Sprite>("UI/Cerrar");
        imgX.preserveAspect = true;
        if (imgX.sprite == null) imgX.color = new Color(0.7f, 0.12f, 0.14f);

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

    static RectTransform Nuevo(string nombre, Transform padre, int capa)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = capa;
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }
}
