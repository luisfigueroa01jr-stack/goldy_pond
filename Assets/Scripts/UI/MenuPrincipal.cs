using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu de inicio de Goldy Pond. Cada boton llama a un metodo publico (se conectan en el On Click() del Inspector):
///  - Jugar: funde a negro y carga la Aldea.
///  - Opciones: abre el panel con los dos sliders de volumen.
///  - Creditos: abre el panel con los nombres del equipo.
///  - Salir: cierra el juego.
/// Esc cierra el panel que este abierto.
/// </summary>
public class MenuPrincipal : MonoBehaviour
{
    [Header("Escena donde empieza el juego")]
    public string escenaJuego = "Aldea";

    [Header("Paneles")]
    public GameObject panelOpciones;
    public GameObject panelCreditos;

    [Header("Opciones de sonido")]
    public Slider sliderJuego;
    public Slider sliderAnimacion;
    public Text porcentajeJuego;
    public Text porcentajeAnimacion;

    [Header("Fundido a negro")]
    public CanvasGroup fundido;
    public float duracionFundido = 0.6f;

    bool cargando;

    void Start()
    {
        // Por si se llega al menu con el juego en pausa o con el mouse escondido (primera persona).
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        CerrarPaneles();

        // Los sliders empiezan con el volumen guardado y avisan cuando los mueves.
        sliderJuego.SetValueWithoutNotify(ConfiguracionSonido.VolumenJuego);
        sliderAnimacion.SetValueWithoutNotify(ConfiguracionSonido.VolumenAnimacion);
        sliderJuego.onValueChanged.AddListener(CambiarVolumenJuego);
        sliderAnimacion.onValueChanged.AddListener(CambiarVolumenAnimacion);
        ActualizarPorcentajes();

        // El menu aparece desde negro.
        if (fundido != null) StartCoroutine(Fundir(1f, 0f));
    }

    void Update()
    {
        var teclado = Keyboard.current;
        if (teclado != null && teclado.escapeKey.wasPressedThisFrame) CerrarPaneles();
    }

    // ---------- Botones ----------

    public void Jugar()
    {
        if (cargando) return;   // evita cargar dos veces si le das doble clic
        cargando = true;
        StartCoroutine(CargarJuego());
    }

    public void AbrirOpciones()
    {
        panelCreditos.SetActive(false);
        panelOpciones.SetActive(true);
    }

    public void AbrirCreditos()
    {
        panelOpciones.SetActive(false);
        panelCreditos.SetActive(true);
    }

    public void CerrarPaneles()
    {
        panelOpciones.SetActive(false);
        panelCreditos.SetActive(false);
    }

    public void Salir()
    {
#if UNITY_EDITOR
        // Dentro de Unity, Application.Quit() no hace nada; asi se detiene el Play.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Sonido ----------

    void CambiarVolumenJuego(float valor)
    {
        ConfiguracionSonido.VolumenJuego = valor;
        ActualizarPorcentajes();
    }

    void CambiarVolumenAnimacion(float valor)
    {
        ConfiguracionSonido.VolumenAnimacion = valor;
        ActualizarPorcentajes();
    }

    void ActualizarPorcentajes()
    {
        porcentajeJuego.text = Mathf.RoundToInt(sliderJuego.value * 100f) + "%";
        porcentajeAnimacion.text = Mathf.RoundToInt(sliderAnimacion.value * 100f) + "%";
    }

    // ---------- Transicion ----------

    IEnumerator CargarJuego()
    {
        if (fundido != null) yield return Fundir(0f, 1f);
        SceneManager.LoadScene(escenaJuego);
    }

    IEnumerator Fundir(float desde, float hasta)
    {
        fundido.blocksRaycasts = true;   // mientras funde no se puede hacer clic
        for (float t = 0f; t < duracionFundido; t += Time.unscaledDeltaTime)
        {
            fundido.alpha = Mathf.Lerp(desde, hasta, t / duracionFundido);
            yield return null;
        }
        fundido.alpha = hasta;
        fundido.blocksRaycasts = hasta > 0.5f;
    }
}
