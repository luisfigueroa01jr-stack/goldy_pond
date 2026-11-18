using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Barra de herramientas (hotbar) de la parte de abajo de la pantalla.
/// Teclas 1-5 o la rueda del mouse (LB/RB en control) cambian la casilla seleccionada.
/// Otros scripts guardan objetos con Agregar(...) y preguntan con Tiene(...) / ObjetoSeleccionado.
/// La parte visual se crea con Construir() (lo hace el menu Goldy Pond > Interfaz).
/// </summary>
public class BarraHerramientas : MonoBehaviour
{
    [Serializable]
    public class Casilla
    {
        public string objeto;      // vacio = casilla libre
        public int cantidad;
        public Image fondo;
        public Image icono;
        public Text numero;
        public Text textoCantidad;
        public bool Libre => string.IsNullOrEmpty(objeto);
    }

    public static BarraHerramientas Instancia { get; private set; }

    [Header("Sprites")]
    public Sprite spriteMarco;
    public Sprite spriteCasilla;
    public Sprite spriteCasillaSeleccionada;
    public Sprite spriteLaurel;
    [Tooltip("Vacio = fuente integrada de Unity.")]
    public Font fuente;

    [Header("Tamano (en pixeles de una pantalla 1920x1080)")]
    [Range(1, 9)] public int numeroCasillas = 5;
    public float tamanoCasilla = 96f;
    public float separacion = 14f;
    public float relleno = 18f;
    public float distanciaAbajo = 28f;
    public float tamanoLaurel = 92f;
    [Tooltip("Los trigos/laureles a los lados de la barra.")]
    public bool mostrarLaureles = false;

    [Header("Seleccion")]
    public int seleccionada = 0;
    public float escalaSeleccionada = 1.08f;
    public Color colorNumero = new Color(1f, 0.93f, 0.82f, 0.85f);
    public Color colorNumeroSeleccionado = new Color(1f, 0.88f, 0.62f, 1f);

    public Casilla[] casillas = new Casilla[0];

    /// <summary>Se llama con el indice nuevo cada vez que cambia la casilla seleccionada.</summary>
    public event Action<int> AlCambiarSeleccion;
    /// <summary>Se llama cuando se agrega o quita algo de la barra.</summary>
    public event Action AlCambiarContenido;

    public string ObjetoSeleccionado => Valida(seleccionada) ? casillas[seleccionada].objeto : null;

    // El sprite de casilla tiene 16 px de brillo alrededor de un cuadro de 128 (lienzo de 160).
    const float FactorBrillo = 160f / 128f;

    void Awake()
    {
        Instancia = this;
        if (!mostrarLaureles) QuitarLaureles();
        if (casillas.Length != numeroCasillas || Array.Exists(casillas, c => c == null || c.fondo == null))
            Construir();
        Refrescar(true);
    }

    /// <summary>Borra los laureles que ya estaban construidos en la escena.</summary>
    public void QuitarLaureles()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var hijo = transform.GetChild(i).gameObject;
            if (!hijo.name.StartsWith("Laurel")) continue;
            if (Application.isPlaying) Destroy(hijo); else DestroyImmediate(hijo);
        }
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    void Update()
    {
        int nueva = seleccionada;

        var teclado = Keyboard.current;
        if (teclado != null)
            for (int i = 0; i < Mathf.Min(numeroCasillas, 9); i++)
                if (teclado[Key.Digit1 + i].wasPressedThisFrame || teclado[Key.Numpad1 + i].wasPressedThisFrame)
                    nueva = i;

        var mouse = Mouse.current;
        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            float rueda = mouse.scroll.ReadValue().y;
            if (rueda < -0.1f) nueva = seleccionada + 1;
            else if (rueda > 0.1f) nueva = seleccionada - 1;
        }

        var control = Gamepad.current;
        if (control != null)
        {
            if (control.rightShoulder.wasPressedThisFrame) nueva = seleccionada + 1;
            if (control.leftShoulder.wasPressedThisFrame) nueva = seleccionada - 1;
        }

        nueva = (nueva % numeroCasillas + numeroCasillas) % numeroCasillas;
        if (nueva != seleccionada) Seleccionar(nueva);

        AnimarSeleccion(Time.unscaledDeltaTime);
    }

    // ---------- API para otros scripts ----------

    public void Seleccionar(int indice)
    {
        if (!Valida(indice)) return;
        seleccionada = indice;
        Refrescar(false);
        AlCambiarSeleccion?.Invoke(indice);
    }

    /// <summary>Guarda un objeto. Si ya existe se suma la cantidad. Devuelve false si la barra esta llena.</summary>
    public bool Agregar(string objeto, Sprite icono, int cantidad = 1)
    {
        int i = Buscar(objeto);
        if (i < 0) i = Array.FindIndex(casillas, c => c.Libre);
        if (i < 0) return false;

        var c = casillas[i];
        if (c.Libre) { c.objeto = objeto; c.cantidad = 0; }
        c.cantidad += cantidad;
        if (icono != null) c.icono.sprite = icono;
        Refrescar(false);
        AlCambiarContenido?.Invoke();
        return true;
    }

    /// <summary>Quita una cantidad del objeto; si llega a 0 la casilla queda libre.</summary>
    public bool Quitar(string objeto, int cantidad = 1)
    {
        int i = Buscar(objeto);
        if (i < 0) return false;
        var c = casillas[i];
        c.cantidad -= cantidad;
        if (c.cantidad <= 0) { c.objeto = null; c.cantidad = 0; c.icono.sprite = null; }
        Refrescar(false);
        AlCambiarContenido?.Invoke();
        return true;
    }

    public bool Tiene(string objeto) => Buscar(objeto) >= 0;
    public int Cantidad(string objeto) { int i = Buscar(objeto); return i < 0 ? 0 : casillas[i].cantidad; }

    int Buscar(string objeto) =>
        string.IsNullOrEmpty(objeto) ? -1 : Array.FindIndex(casillas, c => c.objeto == objeto);

    bool Valida(int i) => i >= 0 && i < casillas.Length && casillas[i] != null;

    // ---------- Visual ----------

    void Refrescar(bool inmediato)
    {
        for (int i = 0; i < casillas.Length; i++)
        {
            var c = casillas[i];
            if (c == null || c.fondo == null) continue;
            bool sel = i == seleccionada;
            c.fondo.sprite = sel ? spriteCasillaSeleccionada : spriteCasilla;
            c.numero.color = sel ? colorNumeroSeleccionado : colorNumero;
            c.icono.enabled = !c.Libre && c.icono.sprite != null;
            c.textoCantidad.text = c.cantidad > 1 ? c.cantidad.ToString() : "";
            if (inmediato)
                c.fondo.rectTransform.localScale = Vector3.one * (sel ? escalaSeleccionada : 1f);
        }
    }

    void AnimarSeleccion(float dt)
    {
        foreach (var c in casillas)
        {
            if (c == null || c.fondo == null) continue;
            float objetivo = ReferenceEquals(c, casillas[seleccionada]) ? escalaSeleccionada : 1f;
            var t = c.fondo.rectTransform;
            t.localScale = Vector3.one * Mathf.Lerp(t.localScale.x, objetivo, 1f - Mathf.Exp(-18f * dt));
        }
    }

    /// <summary>Crea (o vuelve a crear) todos los hijos visuales de la barra.</summary>
    public void Construir()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var hijo = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(hijo); else DestroyImmediate(hijo);
        }

        var letra = fuente != null ? fuente : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        float ancho = numeroCasillas * tamanoCasilla + (numeroCasillas - 1) * separacion + 2 * relleno;
        float alto = tamanoCasilla + 2 * relleno;

        var raiz = (RectTransform)transform;
        raiz.anchorMin = raiz.anchorMax = new Vector2(0.5f, 0f);
        raiz.pivot = new Vector2(0.5f, 0f);
        raiz.anchoredPosition = new Vector2(0f, distanciaAbajo);
        raiz.sizeDelta = new Vector2(ancho, alto);

        var marco = CrearImagen("Marco", raiz, spriteMarco, Image.Type.Sliced);
        Estirar(marco.rectTransform);

        // Laureles a los lados (apagados por defecto).
        if (mostrarLaureles) CrearLaureles(raiz);

        casillas = new Casilla[numeroCasillas];
        for (int i = 0; i < numeroCasillas; i++)
        {
            var c = new Casilla();
            float x = relleno + tamanoCasilla * 0.5f + i * (tamanoCasilla + separacion);

            var hueco = new GameObject($"Casilla {i + 1}", typeof(RectTransform)).GetComponent<RectTransform>();
            hueco.SetParent(raiz, false);
            Colocar(hueco, new Vector2(0f, 0.5f), new Vector2(x, 0f), Vector2.one * tamanoCasilla, Vector2.one * 0.5f);

            c.fondo = CrearImagen("Fondo", hueco, spriteCasilla, Image.Type.Sliced);
            Colocar(c.fondo.rectTransform, Vector2.one * 0.5f, Vector2.zero, Vector2.one * tamanoCasilla * FactorBrillo, Vector2.one * 0.5f);

            c.icono = CrearImagen("Icono", c.fondo.rectTransform, null, Image.Type.Simple);
            c.icono.preserveAspect = true;
            Colocar(c.icono.rectTransform, Vector2.one * 0.5f, new Vector2(0f, tamanoCasilla * 0.06f),
                    Vector2.one * tamanoCasilla * 0.62f, Vector2.one * 0.5f);
            c.icono.enabled = false;

            c.numero = CrearTexto("Numero", c.fondo.rectTransform, letra, (i + 1).ToString(), Mathf.RoundToInt(tamanoCasilla * 0.24f), TextAnchor.LowerCenter);
            Colocar(c.numero.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -tamanoCasilla * 0.30f),
                    new Vector2(tamanoCasilla * 0.6f, tamanoCasilla * 0.3f), new Vector2(0.5f, 0.5f));

            c.textoCantidad = CrearTexto("Cantidad", c.fondo.rectTransform, letra, "", Mathf.RoundToInt(tamanoCasilla * 0.2f), TextAnchor.UpperRight);
            Colocar(c.textoCantidad.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(tamanoCasilla * 0.22f, tamanoCasilla * 0.30f),
                    new Vector2(tamanoCasilla * 0.4f, tamanoCasilla * 0.25f), new Vector2(0.5f, 0.5f));

            casillas[i] = c;
        }
        seleccionada = Mathf.Clamp(seleccionada, 0, numeroCasillas - 1);
        Refrescar(true);
    }

    void CrearLaureles(RectTransform raiz)
    {
        float altoLaurel = tamanoLaurel * 176f / 128f;
        var izq = CrearImagen("Laurel izquierdo", raiz, spriteLaurel, Image.Type.Simple);
        Colocar(izq.rectTransform, new Vector2(0f, 0f), new Vector2(-tamanoLaurel * 0.45f, -altoLaurel * 0.12f),
                new Vector2(tamanoLaurel, altoLaurel), new Vector2(0.5f, 0f));
        var der = CrearImagen("Laurel derecho", raiz, spriteLaurel, Image.Type.Simple);
        Colocar(der.rectTransform, new Vector2(1f, 0f), new Vector2(tamanoLaurel * 0.45f, -altoLaurel * 0.12f),
                new Vector2(tamanoLaurel, altoLaurel), new Vector2(0.5f, 0f));
        der.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

    }

    static Image CrearImagen(string nombre, Transform padre, Sprite sprite, Image.Type tipo)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(padre, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = tipo;
        img.raycastTarget = false;
        return img;
    }

    static Text CrearTexto(string nombre, Transform padre, Font letra, string texto, int tamano, TextAnchor alineacion)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(padre, false);
        var t = go.GetComponent<Text>();
        t.font = letra;
        t.text = texto;
        t.fontSize = tamano;
        t.alignment = alineacion;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var sombra = go.AddComponent<Shadow>();
        sombra.effectColor = new Color(0.2f, 0.1f, 0.2f, 0.6f);
        sombra.effectDistance = new Vector2(1f, -1f);
        return t;
    }

    static void Estirar(RectTransform t)
    {
        t.anchorMin = Vector2.zero;
        t.anchorMax = Vector2.one;
        t.offsetMin = t.offsetMax = Vector2.zero;
    }

    static void Colocar(RectTransform t, Vector2 ancla, Vector2 posicion, Vector2 tamano, Vector2 pivote)
    {
        t.anchorMin = t.anchorMax = ancla;
        t.pivot = pivote;
        t.anchoredPosition = posicion;
        t.sizeDelta = tamano;
    }
}
