using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Arma la escena MenuPrincipal (pantalla de inicio de Goldy Pond) y la pone como la primera del juego.
// Menu: Goldy Pond > Crear menu principal.
// El fondo es el diseno del menu; encima van botones reales de Unity (invisibles) en la misma posicion
// que los botones dibujados, que brillan en dorado al pasar el mouse.
public static class CrearMenuPrincipal
{
    private const string RUTA_ESCENA = "Assets/Scenes/MenuPrincipal.unity";
    private const string CARPETA_UI = "Assets/UI/MenuPrincipal/";
    private const string FUENTE_TITULO = "Assets/Fonts/NotoSerifDisplay-Bold.ttf";
    private const string FUENTE_TEXTO = "Assets/Fonts/NotoSerifDisplay-SemiBold.ttf";

    private static readonly Color Dorado = new Color32(0xF2, 0xD5, 0x8C, 0xFF);
    private static readonly Color Crema = new Color32(0xF4, 0xE9, 0xD2, 0xFF);
    private static readonly Color Vino = new Color32(0x6E, 0x1F, 0x35, 0xFF);
    private static readonly Color Sombra = new Color(0.05f, 0.02f, 0.1f, 0.85f);

    // Posicion de cada boton dibujado en el fondo (pixeles en la imagen de 1920x1080, desde arriba a la izquierda).
    private static readonly RectInt Jugar = new RectInt(712, 443, 496, 90);
    private static readonly RectInt Opciones = new RectInt(762, 552, 396, 75);
    private static readonly RectInt Creditos = new RectInt(762, 642, 396, 76);
    private static readonly RectInt Salir = new RectInt(762, 734, 396, 76);

    private static Font titulo, texto;

    [MenuItem("Goldy Pond/Crear menu principal")]
    public static void crear()
    {
        configurarImagenes();
        titulo = AssetDatabase.LoadAssetAtPath<Font>(FUENTE_TITULO);
        texto = AssetDatabase.LoadAssetAtPath<Font>(FUENTE_TEXTO);

        Scene escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camara (solo para que haya AudioListener y fondo negro).
        GameObject cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera";
        Camera c = cam.GetComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = Color.black;

        // EventSystem con el modulo del Input System nuevo (el proyecto usa el Input System).
        new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));

        // Canvas que se adapta a cualquier resolucion tomando 1920x1080 como referencia.
        GameObject canvasGO = new GameObject("Canvas Menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler escalador = canvasGO.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920, 1080);
        escalador.matchWidthOrHeight = 0.5f;

        MenuPrincipal menu = canvasGO.AddComponent<MenuPrincipal>();

        // Fondo con el diseno. "Envelope Parent" = siempre llena la pantalla sin deformarse.
        Image fondo = crearImagen("Fondo", canvasGO.transform, cargarSprite("Fondo_Menu.png"), Color.white);
        estirar(fondo.rectTransform);
        fondo.raycastTarget = false;
        AspectRatioFitter ajuste = fondo.gameObject.AddComponent<AspectRatioFitter>();
        ajuste.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        ajuste.aspectRatio = 16f / 9f;

        // Luciernagas encima del fondo (debajo de los botones).
        GameObject luces = new GameObject("Luciernagas", typeof(RectTransform), typeof(Luciernagas));
        luces.transform.SetParent(fondo.transform, false);
        estirar((RectTransform)luces.transform);
        luces.GetComponent<Luciernagas>().imagen = cargarSprite("Luciernaga.png");

        // Botones sobre los dibujados.
        Button bJugar = crearBotonSobreFondo("Boton Jugar", fondo.transform, Jugar);
        Button bOpciones = crearBotonSobreFondo("Boton Opciones", fondo.transform, Opciones);
        Button bCreditos = crearBotonSobreFondo("Boton Creditos", fondo.transform, Creditos);
        Button bSalir = crearBotonSobreFondo("Boton Salir", fondo.transform, Salir);
        UnityEventTools.AddPersistentListener(bJugar.onClick, menu.jugar);
        UnityEventTools.AddPersistentListener(bOpciones.onClick, menu.abrirOpciones);
        UnityEventTools.AddPersistentListener(bCreditos.onClick, menu.abrirCreditos);
        UnityEventTools.AddPersistentListener(bSalir.onClick, menu.salir);

        // ---------- Panel de Opciones ----------
        GameObject opciones = crearPanel("Panel Opciones", canvasGO.transform, "Opciones", new Vector2(980, 560), out Transform cajaOp);
        menu.panelOpciones = opciones;
        menu.sliderJuego = crearFilaVolumen("Sonido del juego", cajaOp, 70, out menu.porcentajeJuego);
        menu.sliderAnimacion = crearFilaVolumen("Sonido de animación", cajaOp, -50, out menu.porcentajeAnimacion);
        Button volverOp = crearBotonCrema("Boton Volver", cajaOp, "Volver", new Vector2(0, -200));
        UnityEventTools.AddPersistentListener(volverOp.onClick, menu.cerrarPaneles);

        // ---------- Panel de Creditos ----------
        GameObject creditos = crearPanel("Panel Creditos", canvasGO.transform, "Créditos", new Vector2(980, 600), out Transform cajaCr);
        menu.panelCreditos = creditos;
        crearTexto("Subtitulo", cajaCr, texto, "Desarrollado por", 30, Dorado, new Vector2(0, 135), new Vector2(800, 50), FontStyle.Italic);
        string[] nombres = { "Paulina Cancino Vichi", "Luis Armando Figueroa Baeza", "Camila Andrea Jaimes Martinez" };
        for (int i = 0; i < nombres.Length; i++)
        {
            crearTexto("Nombre " + (i + 1), cajaCr, texto, nombres[i], 40, Crema, new Vector2(0, 65 - i * 70), new Vector2(860, 60));
        }

        Button volverCr = crearBotonCrema("Boton Volver", cajaCr, "Volver", new Vector2(0, -220));
        UnityEventTools.AddPersistentListener(volverCr.onClick, menu.cerrarPaneles);

        // ---------- Fundido a negro (hasta arriba de todo) ----------
        Image negro = crearImagen("Fundido", canvasGO.transform, null, Color.black);
        estirar(negro.rectTransform);
        CanvasGroup grupo = negro.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.blocksRaycasts = false;
        menu.fundido = grupo;

        opciones.SetActive(false);
        creditos.SetActive(false);

        EditorSceneManager.SaveScene(escena, RUTA_ESCENA);
        ponerPrimeraEnBuild();
        Debug.Log("[Goldy Pond] Menu principal creado en " + RUTA_ESCENA + " y puesto como escena 0.");
    }

    // ------------------- PIEZAS ------------------------------

    private static Button crearBotonSobreFondo(string nombre, Transform fondo, RectInt px)
    {
        // Zona de clic invisible del tamano del boton dibujado (anclas en % del fondo, asi escala con el).
        Image raiz = crearImagen(nombre, fondo, null, new Color(1, 1, 1, 0));
        RectTransform rt = raiz.rectTransform;
        rt.anchorMin = new Vector2(px.xMin / 1920f, 1f - px.yMax / 1080f);
        rt.anchorMax = new Vector2(px.xMax / 1920f, 1f - px.yMin / 1080f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // Aro dorado difuminado que aparece al pasar el mouse.
        Image brillo = crearImagen("Brillo", raiz.transform, cargarSprite("Brillo_Boton.png"), Color.white);
        estirar(brillo.rectTransform);
        brillo.rectTransform.offsetMin = new Vector2(-px.width * 0.06f, -px.height * 0.33f);
        brillo.rectTransform.offsetMax = new Vector2(px.width * 0.06f, px.height * 0.33f);
        brillo.raycastTarget = false;

        Button boton = raiz.gameObject.AddComponent<Button>();
        boton.targetGraphic = brillo;
        ColorBlock colores = boton.colors;
        colores.normalColor = new Color(1f, 1f, 1f, 0f);
        colores.highlightedColor = new Color(1f, 0.86f, 0.5f, 0.85f);
        colores.selectedColor = new Color(1f, 1f, 1f, 0f);
        colores.pressedColor = new Color(1f, 0.95f, 0.75f, 1f);
        colores.fadeDuration = 0.12f;
        boton.colors = colores;
        boton.navigation = new Navigation { mode = Navigation.Mode.None };

        raiz.gameObject.AddComponent<EfectoBotonMenu>();
        return boton;
    }

    private static GameObject crearPanel(
        string nombre,
        Transform canvas,
        string tituloPanel,
        Vector2 tamano,
        out Transform caja
    )
    {
        // Capa oscura que tapa el menu y bloquea los clics de atras.
        Image velo = crearImagen(nombre, canvas, null, new Color(0.03f, 0.01f, 0.08f, 0.6f));
        estirar(velo.rectTransform);

        Image marco = crearImagen("Marco", velo.transform, cargarSprite("Panel_Menu.png"), Color.white);
        marco.type = Image.Type.Sliced;
        marco.rectTransform.sizeDelta = tamano;
        caja = marco.transform;

        crearTexto("Titulo", caja, titulo, tituloPanel, 64, Dorado, new Vector2(0, tamano.y / 2 - 75), new Vector2(tamano.x - 80, 90));
        return velo.gameObject;
    }

    private static Slider crearFilaVolumen(
        string etiqueta,
        Transform caja,
        float y,
        out Text porcentaje
    )
    {
        crearTexto("Etiqueta " + etiqueta, caja, texto, etiqueta, 34, Crema, new Vector2(0, y + 38), new Vector2(760, 50));

        GameObject go = new GameObject("Slider " + etiqueta, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(caja, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchoredPosition = new Vector2(-50, y - 18);
        rt.sizeDelta = new Vector2(600, 30);

        Image riel = crearImagen("Riel", go.transform, cargarSprite("Slider_Riel.png"), Color.white);
        riel.type = Image.Type.Sliced;
        estirar(riel.rectTransform);

        GameObject areaRelleno = new GameObject("Area Relleno", typeof(RectTransform));
        areaRelleno.transform.SetParent(go.transform, false);
        estirar((RectTransform)areaRelleno.transform);
        Image relleno = crearImagen("Relleno", areaRelleno.transform, cargarSprite("Slider_Relleno.png"), Color.white);
        relleno.type = Image.Type.Sliced;
        estirar(relleno.rectTransform);

        GameObject areaPerilla = new GameObject("Area Perilla", typeof(RectTransform));
        areaPerilla.transform.SetParent(go.transform, false);
        estirar((RectTransform)areaPerilla.transform);
        ((RectTransform)areaPerilla.transform).offsetMin = new Vector2(16, 0);
        ((RectTransform)areaPerilla.transform).offsetMax = new Vector2(-16, 0);
        Image perilla = crearImagen("Perilla", areaPerilla.transform, cargarSprite("Slider_Perilla.png"), Color.white);
        perilla.rectTransform.sizeDelta = new Vector2(48, 48);

        Slider slider = go.GetComponent<Slider>();
        slider.fillRect = relleno.rectTransform;
        slider.handleRect = perilla.rectTransform;
        slider.targetGraphic = perilla;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };

        porcentaje = crearTexto("Porcentaje " + etiqueta, caja, texto, "100%", 32, Dorado, new Vector2(330, y - 18), new Vector2(120, 50));
        return slider;
    }

    private static Button crearBotonCrema(
        string nombre,
        Transform caja,
        string letrero,
        Vector2 posicion
    )
    {
        Image img = crearImagen(nombre, caja, cargarSprite("Boton_Crema.png"), Color.white);
        img.type = Image.Type.Sliced;
        img.rectTransform.anchoredPosition = posicion;
        img.rectTransform.sizeDelta = new Vector2(300, 72);
        crearTexto("Texto", img.transform, titulo, letrero, 36, Vino, Vector2.zero, new Vector2(280, 66), FontStyle.Normal, false);

        Button boton = img.gameObject.AddComponent<Button>();
        ColorBlock colores = boton.colors;
        colores.highlightedColor = new Color(1f, 0.93f, 0.78f);
        colores.pressedColor = new Color(0.85f, 0.75f, 0.6f);
        boton.colors = colores;
        img.gameObject.AddComponent<EfectoBotonMenu>();
        return boton;
    }

    private static Image crearImagen(
        string nombre,
        Transform padre,
        Sprite sprite,
        Color color
    )
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(padre, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        return img;
    }

    private static Text crearTexto(string nombre, Transform padre, Font letra, string contenido, int tamano, Color color,
                           Vector2 posicion, Vector2 caja, FontStyle estilo = FontStyle.Normal, bool conSombra = true)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(padre, false);
        Text t = go.GetComponent<Text>();
        t.font = letra;
        t.text = contenido;
        t.fontSize = tamano;
        t.fontStyle = estilo;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        RectTransform rt = t.rectTransform;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = caja;
        if (conSombra)
        {
            Shadow s = go.AddComponent<Shadow>();
            s.effectColor = Sombra;
            s.effectDistance = new Vector2(2, -2);
        }

        return t;
    }

    private static void estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Sprite cargarSprite(string archivo)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(CARPETA_UI + archivo);
    }

    // ------------------- IMPORTACION DE IMAGENES ------------------------------

    private static void configurarImagenes()
    {
        Dictionary<string, Vector4> bordes = new Dictionary<string, Vector4>
        {
            { "Panel_Menu.png", new Vector4(44, 44, 44, 44) },
            { "Boton_Crema.png", new Vector4(34, 34, 34, 34) },
            { "Slider_Riel.png", new Vector4(14, 13, 14, 13) },
            { "Slider_Relleno.png", new Vector4(14, 13, 14, 13) },
        };
        foreach (string ruta in Directory.GetFiles(CARPETA_UI, "*.png"))
        {
            TextureImporter importador = (TextureImporter)AssetImporter.GetAtPath(ruta.Replace('\\', '/'));
            if (importador == null)
            {
                continue;
            }

            importador.textureType = TextureImporterType.Sprite;
            importador.spriteImportMode = SpriteImportMode.Single;
            importador.mipmapEnabled = false;
            importador.alphaIsTransparency = true;
            importador.maxTextureSize = 2048;
            importador.textureCompression = TextureImporterCompression.Uncompressed;   // el fondo se ve nitido, sin cuadritos
            string archivo = Path.GetFileName(ruta);
            importador.spriteBorder = bordes.TryGetValue(archivo, out Vector4 b) ? b : Vector4.zero;
            importador.SaveAndReimport();
        }
    }

    private static void ponerPrimeraEnBuild()
    {
        List<EditorBuildSettingsScene> lista = EditorBuildSettings.scenes.Where(s => s.path != RUTA_ESCENA).ToList();
        lista.Insert(0, new EditorBuildSettingsScene(RUTA_ESCENA, true));
        EditorBuildSettings.scenes = lista.ToArray();
    }
}
