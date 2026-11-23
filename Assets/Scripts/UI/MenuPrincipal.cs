using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu de inicio de Goldy Pond. Cada boton llama a un metodo publico (se conectan en el On Click() del Inspector):
// - Jugar: funde a negro y carga la Aldea.
// - Opciones: abre el panel con los dos sliders de volumen.
// - Creditos: abre el panel con los nombres del equipo.
// - Salir: cierra el juego.
// Esc cierra el panel que este abierto.
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

    private bool cargando;

    private void Start()
    {
        // Por si se llega al menu con el juego en pausa o con el mouse escondido (primera persona).
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        cerrarPaneles();

        // Los sliders empiezan con el volumen guardado y avisan cuando los mueves.
        sliderJuego.SetValueWithoutNotify(ConfiguracionSonido.VolumenJuego);
        sliderAnimacion.SetValueWithoutNotify(ConfiguracionSonido.VolumenAnimacion);
        sliderJuego.onValueChanged.AddListener(cambiarVolumenJuego);
        sliderAnimacion.onValueChanged.AddListener(cambiarVolumenAnimacion);
        actualizarPorcentajes();

        // El menu aparece desde negro.
        if (fundido != null)
        {
            StartCoroutine(fundir(1f, 0f));
        }
    }

    private void Update()
    {
        Keyboard teclado = Keyboard.current;
        if (teclado != null && teclado.escapeKey.wasPressedThisFrame)
        {
            cerrarPaneles();
        }
    }

    // ------------------- BOTONES ------------------------------

    public void jugar()
    {
        if (cargando)
        {
            return; // evita cargar dos veces si le das doble clic
        }

        cargando = true;
        StartCoroutine(cargarJuego());
    }

    public void abrirOpciones()
    {
        panelCreditos.SetActive(false);
        panelOpciones.SetActive(true);
    }

    public void abrirCreditos()
    {
        panelOpciones.SetActive(false);
        panelCreditos.SetActive(true);
    }

    public void cerrarPaneles()
    {
        panelOpciones.SetActive(false);
        panelCreditos.SetActive(false);
    }

    public void salir()
    {
#if UNITY_EDITOR
        // Dentro de Unity, Application.Quit() no hace nada; asi se detiene el Play.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------- SONIDO ------------------------------

    private void cambiarVolumenJuego(float valor)
    {
        ConfiguracionSonido.VolumenJuego = valor;
        actualizarPorcentajes();
    }

    private void cambiarVolumenAnimacion(float valor)
    {
        ConfiguracionSonido.VolumenAnimacion = valor;
        actualizarPorcentajes();
    }

    private void actualizarPorcentajes()
    {
        porcentajeJuego.text = Mathf.RoundToInt(sliderJuego.value * 100f) + "%";
        porcentajeAnimacion.text = Mathf.RoundToInt(sliderAnimacion.value * 100f) + "%";
    }

    // ------------------- TRANSICION ------------------------------

    private IEnumerator cargarJuego()
    {
        if (fundido != null)
        {
            yield return fundir(0f, 1f);
        }

        SceneManager.LoadScene(escenaJuego);
    }

    private IEnumerator fundir(float desde, float hasta)
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
