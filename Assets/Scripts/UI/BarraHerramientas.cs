using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Barra de herramientas (hotbar) de la parte de abajo de la pantalla.
// Teclas 1-5 o la rueda del mouse (LB/RB en control) cambian la casilla seleccionada.
// Otros scripts guardan objetos con Agregar(...) y preguntan con Tiene(...) / ObjetoSeleccionado.
// La parte visual se crea con Construir() (lo hace el menu Goldy Pond > Interfaz).
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

    // Se llama con el indice nuevo cada vez que cambia la casilla seleccionada.
    public event Action<int> AlCambiarSeleccion;
    // Se llama cuando se agrega o quita algo de la barra.
    public event Action AlCambiarContenido;

    public string ObjetoSeleccionado => valida(seleccionada) ? casillas[seleccionada].objeto : null;

    // El sprite de casilla tiene 16 px de brillo alrededor de un cuadro de 128 (lienzo de 160).
    private const float FACTOR_BRILLO = 160f / 128f;

    private void Awake()
    {
        Instancia = this;
        if (!mostrarLaureles)
        {
            quitarLaureles();
        }

        if (casillas.Length != numeroCasillas || Array.Exists(casillas, c => c == null || c.fondo == null))
        {
            construir();
        }

        refrescar(true);
    }

    // Borra los laureles que ya estaban construidos en la escena.
    public void quitarLaureles()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject hijo = transform.GetChild(i).gameObject;
            if (!hijo.name.StartsWith("Laurel"))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(hijo);
            }else
            {
                DestroyImmediate(hijo);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instancia == this)
        {
            Instancia = null;
        }
    }

    private void Update()
    {
        int nueva = seleccionada;

        Keyboard teclado = Keyboard.current;
        if (teclado != null)
        {
            for (int i = 0; i < Mathf.Min(numeroCasillas, 9); i++)
            {
                if (teclado[Key.Digit1 + i].wasPressedThisFrame || teclado[Key.Numpad1 + i].wasPressedThisFrame)
                {
                    nueva = i;
                }
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            float rueda = mouse.scroll.ReadValue().y;
            if (rueda < -0.1f)
            {
                nueva = seleccionada + 1;
            }
            else if (rueda > 0.1f)
            {
                nueva = seleccionada - 1;
            }
        }

        Gamepad control = Gamepad.current;
        if (control != null)
        {
            if (control.rightShoulder.wasPressedThisFrame)
            {
                nueva = seleccionada + 1;
            }

            if (control.leftShoulder.wasPressedThisFrame)
            {
                nueva = seleccionada - 1;
            }
        }

        nueva = (nueva % numeroCasillas + numeroCasillas) % numeroCasillas;
        if (nueva != seleccionada)
        {
            seleccionar(nueva);
        }

        animarSeleccion(Time.unscaledDeltaTime);
    }

    // ------------------- API PARA OTROS SCRIPTS ------------------------------

    public void seleccionar(int indice)
    {
        if (!valida(indice))
        {
            return;
        }

        seleccionada = indice;
        refrescar(false);
        AlCambiarSeleccion?.Invoke(indice);
    }

    // Pone directamente un objeto en una casilla (objeto vacio = casilla libre). Lo usa EstadoJuego al cambiar de escena.
    public void poner(
        int indice,
        string objeto,
        Sprite icono,
        int cantidad
    )
    {
        if (!valida(indice))
        {
            return;
        }

        Casilla c = casillas[indice];
        bool vacia = string.IsNullOrEmpty(objeto) || cantidad <= 0;
        c.objeto = vacia ? null : objeto;
        c.cantidad = vacia ? 0 : cantidad;
        c.icono.sprite = vacia ? null : icono;
        refrescar(false);
        AlCambiarContenido?.Invoke();
    }

    // Guarda un objeto. Si ya existe se suma la cantidad. Devuelve false si la barra esta llena.
    public bool agregar(string objeto, Sprite icono, int cantidad = 1)
    {
        int i = buscar(objeto);
        if (i < 0)
        {
            i = Array.FindIndex(casillas, c => c.Libre);
        }

        if (i < 0)
        {
            return false;
        }

        Casilla c = casillas[i];
        if (c.Libre)
        {
            c.objeto = objeto;
            c.cantidad = 0;
        }

        c.cantidad += cantidad;
        if (icono != null)
        {
            c.icono.sprite = icono;
        }

        refrescar(false);
        AlCambiarContenido?.Invoke();
        return true;
    }

    // Quita una cantidad del objeto; si llega a 0 la casilla queda libre.
    public bool quitar(string objeto, int cantidad = 1)
    {
        int i = buscar(objeto);
        if (i < 0)
        {
            return false;
        }

        Casilla c = casillas[i];
        c.cantidad -= cantidad;
        if (c.cantidad <= 0)
        {
            c.objeto = null;
            c.cantidad = 0;
            c.icono.sprite = null;
        }

        refrescar(false);
        AlCambiarContenido?.Invoke();
        return true;
    }

    public bool tieneObjeto(string objeto)
    {
        return buscar(objeto) >= 0;
    }

    public int obtenerCantidad(string objeto)
    {
        int i = buscar(objeto);
        return i < 0 ? 0 : casillas[i].cantidad;
    }

    private int buscar(string objeto)
    {
        return string.IsNullOrEmpty(objeto) ? -1 : Array.FindIndex(casillas, c => c.objeto == objeto);
    }

    private bool valida(int i)
    {
        return i >= 0 && i < casillas.Length && casillas[i] != null;
    }

    // ------------------- VISUAL ------------------------------

    private void refrescar(bool inmediato)
    {
        for (int i = 0; i < casillas.Length; i++)
        {
            Casilla c = casillas[i];
            if (c == null || c.fondo == null)
            {
                continue;
            }

            bool sel = i == seleccionada;
            c.fondo.sprite = sel ? spriteCasillaSeleccionada : spriteCasilla;
            c.numero.color = sel ? colorNumeroSeleccionado : colorNumero;
            c.icono.enabled = !c.Libre && c.icono.sprite != null;
            c.textoCantidad.text = c.cantidad > 1 ? c.cantidad.ToString() : "";
            if (inmediato)
            {
                c.fondo.rectTransform.localScale = Vector3.one * (sel ? escalaSeleccionada : 1f);
            }
        }
    }

    private void animarSeleccion(float dt)
    {
        foreach (Casilla c in casillas)
        {
            if (c == null || c.fondo == null)
            {
                continue;
            }

            float objetivo = ReferenceEquals(c, casillas[seleccionada]) ? escalaSeleccionada : 1f;
            RectTransform t = c.fondo.rectTransform;
            t.localScale = Vector3.one * Mathf.Lerp(t.localScale.x, objetivo, 1f - Mathf.Exp(-18f * dt));
        }
    }

    // Crea (o vuelve a crear) todos los hijos visuales de la barra.
    public void construir()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject hijo = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(hijo);
            }else
            {
                DestroyImmediate(hijo);
            }
        }

        Font letra = fuente != null ? fuente : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        float ancho = numeroCasillas * tamanoCasilla + (numeroCasillas - 1) * separacion + 2 * relleno;
        float alto = tamanoCasilla + 2 * relleno;

        RectTransform raiz = (RectTransform)transform;
        raiz.anchorMin = raiz.anchorMax = new Vector2(0.5f, 0f);
        raiz.pivot = new Vector2(0.5f, 0f);
        raiz.anchoredPosition = new Vector2(0f, distanciaAbajo);
        raiz.sizeDelta = new Vector2(ancho, alto);

        Image marco = crearImagen("Marco", raiz, spriteMarco, Image.Type.Sliced);
        estirar(marco.rectTransform);

        // Laureles a los lados (apagados por defecto).
        if (mostrarLaureles)
        {
            crearLaureles(raiz);
        }

        casillas = new Casilla[numeroCasillas];
        for (int i = 0; i < numeroCasillas; i++)
        {
            Casilla c = new Casilla();
            float x = relleno + tamanoCasilla * 0.5f + i * (tamanoCasilla + separacion);

            RectTransform hueco = new GameObject($"Casilla {i + 1}", typeof(RectTransform)).GetComponent<RectTransform>();
            hueco.SetParent(raiz, false);
            colocar(hueco, new Vector2(0f, 0.5f), new Vector2(x, 0f), Vector2.one * tamanoCasilla, Vector2.one * 0.5f);

            c.fondo = crearImagen("Fondo", hueco, spriteCasilla, Image.Type.Sliced);
            colocar(c.fondo.rectTransform, Vector2.one * 0.5f, Vector2.zero, Vector2.one * tamanoCasilla * FACTOR_BRILLO, Vector2.one * 0.5f);

            c.icono = crearImagen("Icono", c.fondo.rectTransform, null, Image.Type.Simple);
            c.icono.preserveAspect = true;
            colocar(c.icono.rectTransform, Vector2.one * 0.5f, new Vector2(0f, tamanoCasilla * 0.06f),
                    Vector2.one * tamanoCasilla * 0.62f, Vector2.one * 0.5f);
            c.icono.enabled = false;

            c.numero = crearTexto("Numero", c.fondo.rectTransform, letra, (i + 1).ToString(), Mathf.RoundToInt(tamanoCasilla * 0.24f), TextAnchor.LowerCenter);
            colocar(c.numero.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -tamanoCasilla * 0.30f),
                    new Vector2(tamanoCasilla * 0.6f, tamanoCasilla * 0.3f), new Vector2(0.5f, 0.5f));

            c.textoCantidad = crearTexto("Cantidad", c.fondo.rectTransform, letra, "", Mathf.RoundToInt(tamanoCasilla * 0.2f), TextAnchor.UpperRight);
            colocar(c.textoCantidad.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(tamanoCasilla * 0.22f, tamanoCasilla * 0.30f),
                    new Vector2(tamanoCasilla * 0.4f, tamanoCasilla * 0.25f), new Vector2(0.5f, 0.5f));

            casillas[i] = c;
        }

        seleccionada = Mathf.Clamp(seleccionada, 0, numeroCasillas - 1);
        refrescar(true);
    }

    private void crearLaureles(RectTransform raiz)
    {
        float altoLaurel = tamanoLaurel * 176f / 128f;
        Image izq = crearImagen("Laurel izquierdo", raiz, spriteLaurel, Image.Type.Simple);
        colocar(izq.rectTransform, new Vector2(0f, 0f), new Vector2(-tamanoLaurel * 0.45f, -altoLaurel * 0.12f),
                new Vector2(tamanoLaurel, altoLaurel), new Vector2(0.5f, 0f));
        Image der = crearImagen("Laurel derecho", raiz, spriteLaurel, Image.Type.Simple);
        colocar(der.rectTransform, new Vector2(1f, 0f), new Vector2(tamanoLaurel * 0.45f, -altoLaurel * 0.12f),
                new Vector2(tamanoLaurel, altoLaurel), new Vector2(0.5f, 0f));
        der.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

    }

    private static Image crearImagen(
        string nombre,
        Transform padre,
        Sprite sprite,
        Image.Type tipo
    )
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(padre, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = tipo;
        img.raycastTarget = false;
        return img;
    }

    private static Text crearTexto(
        string nombre,
        Transform padre,
        Font letra,
        string texto,
        int tamano,
        TextAnchor alineacion
    )
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(padre, false);
        Text t = go.GetComponent<Text>();
        t.font = letra;
        t.text = texto;
        t.fontSize = tamano;
        t.alignment = alineacion;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        Shadow sombra = go.AddComponent<Shadow>();
        sombra.effectColor = new Color(0.2f, 0.1f, 0.2f, 0.6f);
        sombra.effectDistance = new Vector2(1f, -1f);
        return t;
    }

    private static void estirar(RectTransform t)
    {
        t.anchorMin = Vector2.zero;
        t.anchorMax = Vector2.one;
        t.offsetMin = t.offsetMax = Vector2.zero;
    }

    private static void colocar(
        RectTransform t,
        Vector2 ancla,
        Vector2 posicion,
        Vector2 tamano,
        Vector2 pivote
    )
    {
        t.anchorMin = t.anchorMax = ancla;
        t.pivot = pivote;
        t.anchoredPosition = posicion;
        t.sizeDelta = tamano;
    }
}
