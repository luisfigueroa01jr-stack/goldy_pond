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

/// <summary>
/// Arma la escena MenuPrincipal (pantalla de inicio de Goldy Pond) y la pone como la primera del juego.
/// Menu: Goldy Pond > Crear menu principal.
/// El fondo es el diseno del menu; encima van botones reales de Unity (invisibles) en la misma posicion
/// que los botones dibujados, que brillan en dorado al pasar el mouse.
/// </summary>
public static class CrearMenuPrincipal
{
    const string RutaEscena = "Assets/Scenes/MenuPrincipal.unity";
    const string CarpetaUI = "Assets/UI/MenuPrincipal/";
    const string FuenteTitulo = "Assets/Fonts/NotoSerifDisplay-Bold.ttf";
    const string FuenteTexto = "Assets/Fonts/NotoSerifDisplay-SemiBold.ttf";

    static readonly Color Dorado = new Color32(0xF2, 0xD5, 0x8C, 0xFF);
    static readonly Color Crema = new Color32(0xF4, 0xE9, 0xD2, 0xFF);
    static readonly Color Vino = new Color32(0x6E, 0x1F, 0x35, 0xFF);
    static readonly Color Sombra = new Color(0.05f, 0.02f, 0.1f, 0.85f);

    // Posicion de cada boton dibujado en el fondo (pixeles en la imagen de 1920x1080, desde arriba a la izquierda).
    static readonly RectInt Jugar = new RectInt(712, 443, 496, 90);
    static readonly RectInt Opciones = new RectInt(762, 552, 396, 75);
    static readonly RectInt Creditos = new RectInt(762, 642, 396, 76);
    static readonly RectInt Salir = new RectInt(762, 734, 396, 76);

    static Font titulo, texto;

    [MenuItem("Goldy Pond/Crear menu principal")]
    public static void Crear()
    {
        ConfigurarImagenes();
        titulo = AssetDatabase.LoadAssetAtPath<Font>(FuenteTitulo);
        texto = AssetDatabase.LoadAssetAtPath<Font>(FuenteTexto);

        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camara (solo para que haya AudioListener y fondo negro).
        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera";
        var c = cam.GetComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = Color.black;

        // EventSystem con el modulo del Input System nuevo (el proyecto usa el Input System).
        new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));

        // Canvas que se adapta a cualquier resolucion tomando 1920x1080 como referencia.
        var canvasGO = new GameObject("Canvas Menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var escalador = canvasGO.GetComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920, 1080);
        escalador.matchWidthOrHeight = 0.5f;

        var menu = canvasGO.AddComponent<MenuPrincipal>();

        // Fondo con el diseno. "Envelope Parent" = siempre llena la pantalla sin deformarse.
        var fondo = CrearImagen("Fondo", canvasGO.transform, Sprite("Fondo_Menu.png"), Color.white);
        Estirar(fondo.rectTransform);
        fondo.raycastTarget = false;
        var ajuste = fondo.gameObject.AddComponent<AspectRatioFitter>();
        ajuste.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        ajuste.aspectRatio = 16f / 9f;

        // Luciernagas encima del fondo (debajo de los botones).
        var luces = new GameObject("Luciernagas", typeof(RectTransform), typeof(Luciernagas));
        luces.transform.SetParent(fondo.transform, false);
        Estirar((RectTransform)luces.transform);
        luces.GetComponent<Luciernagas>().imagen = Sprite("Luciernaga.png");

        // Botones sobre los dibujados.
        var bJugar = CrearBotonSobreFondo("Boton Jugar", fondo.transform, Jugar);
        var bOpciones = CrearBotonSobreFondo("Boton Opciones", fondo.transform, Opciones);
        var bCreditos = CrearBotonSobreFondo("Boton Creditos", fondo.transform, Creditos);
        var bSalir = CrearBotonSobreFondo("Boton Salir", fondo.transform, Salir);
        UnityEventTools.AddPersistentListener(bJugar.onClick, menu.Jugar);
        UnityEventTools.AddPersistentListener(bOpciones.onClick, menu.AbrirOpciones);
        UnityEventTools.AddPersistentListener(bCreditos.onClick, menu.AbrirCreditos);
        UnityEventTools.AddPersistentListener(bSalir.onClick, menu.Salir);

        // ---------- Panel de Opciones ----------
        var opciones = CrearPanel("Panel Opciones", canvasGO.transform, "Opciones", new Vector2(980, 560), out var cajaOp);
        menu.panelOpciones = opciones;
        menu.sliderJuego = CrearFilaVolumen("Sonido del juego", cajaOp, 70, out menu.porcentajeJuego);
        menu.sliderAnimacion = CrearFilaVolumen("Sonido de animación", cajaOp, -50, out menu.porcentajeAnimacion);
        var volverOp = CrearBotonCrema("Boton Volver", cajaOp, "Volver", new Vector2(0, -200));
        UnityEventTools.AddPersistentListener(volverOp.onClick, menu.CerrarPaneles);

        // ---------- Panel de Creditos ----------
        var creditos = CrearPanel("Panel Creditos", canvasGO.transform, "Créditos", new Vector2(980, 600), out var cajaCr);
        menu.panelCreditos = creditos;
        CrearTexto("Subtitulo", cajaCr, texto, "Desarrollado por", 30, Dorado, new Vector2(0, 135), new Vector2(800, 50), FontStyle.Italic);
        string[] nombres = { "Paulina Cancino Vichi", "Luis Armando Figueroa Baeza", "Camila Andrea Jaimes Martinez" };
        for (int i = 0; i < nombres.Length; i++)
            CrearTexto("Nombre " + (i + 1), cajaCr, texto, nombres[i], 40, Crema, new Vector2(0, 65 - i * 70), new Vector2(860, 60));
        var volverCr = CrearBotonCrema("Boton Volver", cajaCr, "Volver", new Vector2(0, -220));
        UnityEventTools.AddPersistentListener(volverCr.onClick, menu.CerrarPaneles);

        // ---------- Fundido a negro (hasta arriba de todo) ----------
        var negro = CrearImagen("Fundido", canvasGO.transform, null, Color.black);
        Estirar(negro.rectTransform);
        var grupo = negro.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.blocksRaycasts = false;
        menu.fundido = grupo;

        opciones.SetActive(false);
        creditos.SetActive(false);

        EditorSceneManager.SaveScene(escena, RutaEscena);
        PonerPrimeraEnBuild();
        Debug.Log("[Goldy Pond] Menu principal creado en " + RutaEscena + " y puesto como escena 0.");
    }

    // ---------- Piezas ----------

    static Button CrearBotonSobreFondo(string nombre, Transform fondo, RectInt px)
    {
        // Zona de clic invisible del tamano del boton dibujado (anclas en % del fondo, asi escala con el).
        var raiz = CrearImagen(nombre, fondo, null, new Color(1, 1, 1, 0));
        var rt = raiz.rectTransform;
        rt.anchorMin = new Vector2(px.xMin / 1920f, 1f - px.yMax / 1080f);
        rt.anchorMax = new Vector2(px.xMax / 1920f, 1f - px.yMin / 1080f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // Aro dorado difuminado que aparece al pasar el mouse.
        var brillo = CrearImagen("Brillo", raiz.transform, Sprite("Brillo_Boton.png"), Color.white);
        Estirar(brillo.rectTransform);
        brillo.rectTransform.offsetMin = new Vector2(-px.width * 0.06f, -px.height * 0.33f);
        brillo.rectTransform.offsetMax = new Vector2(px.width * 0.06f, px.height * 0.33f);
        brillo.raycastTarget = false;

        var boton = raiz.gameObject.AddComponent<Button>();
        boton.targetGraphic = brillo;
        var colores = boton.colors;
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

    static GameObject CrearPanel(string nombre, Transform canvas, string tituloPanel, Vector2 tamano, out Transform caja)
    {
        // Capa oscura que tapa el menu y bloquea los clics de atras.
        var velo = CrearImagen(nombre, canvas, null, new Color(0.03f, 0.01f, 0.08f, 0.6f));
        Estirar(velo.rectTransform);

        var marco = CrearImagen("Marco", velo.transform, Sprite("Panel_Menu.png"), Color.white);
        marco.type = Image.Type.Sliced;
        marco.rectTransform.sizeDelta = tamano;
        caja = marco.transform;

        CrearTexto("Titulo", caja, titulo, tituloPanel, 64, Dorado, new Vector2(0, tamano.y / 2 - 75), new Vector2(tamano.x - 80, 90));
        return velo.gameObject;
    }

    static Slider CrearFilaVolumen(string etiqueta, Transform caja, float y, out Text porcentaje)
    {
        CrearTexto("Etiqueta " + etiqueta, caja, texto, etiqueta, 34, Crema, new Vector2(0, y + 38), new Vector2(760, 50));

        var go = new GameObject("Slider " + etiqueta, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(caja, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = new Vector2(-50, y - 18);
        rt.sizeDelta = new Vector2(600, 30);

        var riel = CrearImagen("Riel", go.transform, Sprite("Slider_Riel.png"), Color.white);
        riel.type = Image.Type.Sliced;
        Estirar(riel.rectTransform);

        var areaRelleno = new GameObject("Area Relleno", typeof(RectTransform));
        areaRelleno.transform.SetParent(go.transform, false);
        Estirar((RectTransform)areaRelleno.transform);
        var relleno = CrearImagen("Relleno", areaRelleno.transform, Sprite("Slider_Relleno.png"), Color.white);
        relleno.type = Image.Type.Sliced;
        Estirar(relleno.rectTransform);

        var areaPerilla = new GameObject("Area Perilla", typeof(RectTransform));
        areaPerilla.transform.SetParent(go.transform, false);
        Estirar((RectTransform)areaPerilla.transform);
        ((RectTransform)areaPerilla.transform).offsetMin = new Vector2(16, 0);
        ((RectTransform)areaPerilla.transform).offsetMax = new Vector2(-16, 0);
        var perilla = CrearImagen("Perilla", areaPerilla.transform, Sprite("Slider_Perilla.png"), Color.white);
        perilla.rectTransform.sizeDelta = new Vector2(48, 48);

        var slider = go.GetComponent<Slider>();
        slider.fillRect = relleno.rectTransform;
        slider.handleRect = perilla.rectTransform;
        slider.targetGraphic = perilla;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };

        porcentaje = CrearTexto("Porcentaje " + etiqueta, caja, texto, "100%", 32, Dorado, new Vector2(330, y - 18), new Vector2(120, 50));
        return slider;
    }

    static Button CrearBotonCrema(string nombre, Transform caja, string letrero, Vector2 posicion)
    {
        var img = CrearImagen(nombre, caja, Sprite("Boton_Crema.png"), Color.white);
        img.type = Image.Type.Sliced;
        img.rectTransform.anchoredPosition = posicion;
        img.rectTransform.sizeDelta = new Vector2(300, 72);
        CrearTexto("Texto", img.transform, titulo, letrero, 36, Vino, Vector2.zero, new Vector2(280, 66), FontStyle.Normal, false);

        var boton = img.gameObject.AddComponent<Button>();
        var colores = boton.colors;
        colores.highlightedColor = new Color(1f, 0.93f, 0.78f);
        colores.pressedColor = new Color(0.85f, 0.75f, 0.6f);
        boton.colors = colores;
        img.gameObject.AddComponent<EfectoBotonMenu>();
        return boton;
    }

    static Image CrearImagen(string nombre, Transform padre, Sprite sprite, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(padre, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        return img;
    }

    static Text CrearTexto(string nombre, Transform padre, Font letra, string contenido, int tamano, Color color,
                           Vector2 posicion, Vector2 caja, FontStyle estilo = FontStyle.Normal, bool conSombra = true)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(padre, false);
        var t = go.GetComponent<Text>();
        t.font = letra;
        t.text = contenido;
        t.fontSize = tamano;
        t.fontStyle = estilo;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = caja;
        if (conSombra)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = Sombra;
            s.effectDistance = new Vector2(2, -2);
        }
        return t;
    }

    static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Sprite Sprite(string archivo) => AssetDatabase.LoadAssetAtPath<Sprite>(CarpetaUI + archivo);

    // ---------- Importacion de imagenes ----------

    static void ConfigurarImagenes()
    {
        var bordes = new Dictionary<string, Vector4>
        {
            { "Panel_Menu.png", new Vector4(44, 44, 44, 44) },
            { "Boton_Crema.png", new Vector4(34, 34, 34, 34) },
            { "Slider_Riel.png", new Vector4(14, 13, 14, 13) },
            { "Slider_Relleno.png", new Vector4(14, 13, 14, 13) },
        };
        foreach (var ruta in Directory.GetFiles(CarpetaUI, "*.png"))
        {
            var importador = (TextureImporter)AssetImporter.GetAtPath(ruta.Replace('\\', '/'));
            if (importador == null) continue;
            importador.textureType = TextureImporterType.Sprite;
            importador.spriteImportMode = SpriteImportMode.Single;
            importador.mipmapEnabled = false;
            importador.alphaIsTransparency = true;
            importador.maxTextureSize = 2048;
            importador.textureCompression = TextureImporterCompression.Uncompressed;   // el fondo se ve nitido, sin cuadritos
            var archivo = Path.GetFileName(ruta);
            importador.spriteBorder = bordes.TryGetValue(archivo, out var b) ? b : Vector4.zero;
            importador.SaveAndReimport();
        }
    }

    static void PonerPrimeraEnBuild()
    {
        var lista = EditorBuildSettings.scenes.Where(s => s.path != RutaEscena).ToList();
        lista.Insert(0, new EditorBuildSettingsScene(RutaEscena, true));
        EditorBuildSettings.scenes = lista.ToArray();
    }
}
