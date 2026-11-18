using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Jugador en primera persona: mouse/stick derecho para mirar, WASD/stick izquierdo para moverse,
/// Shift para correr y Espacio para saltar. La camara debe ser hija de este objeto (a la altura de los ojos).
/// Esc libera el cursor; clic izquierdo lo vuelve a capturar.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class JugadorPrimeraPersona : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidadCaminar = 3f;
    public float velocidadCorrer = 6f;
    [Tooltip("Que tan rapido llega a la velocidad objetivo (m/s por segundo).")]
    public float aceleracion = 20f;
    public float alturaSalto = 1f;
    public float gravedad = -20f;

    [Header("Tamano (se aplica al darle Play)")]
    [Tooltip("Altura total del jugador en metros. Las puertas de las casas miden 1.76 m.")]
    [Range(0.8f, 1.8f)] public float estatura = 1.2f;
    [Tooltip("Que tan ancho es (radio de la capsula). Mas delgado sube mejor la escalera de caracol del molino.")]
    [Range(0.15f, 0.4f)] public float radioCapsula = 0.2f;
    [Tooltip("Que tanto suaviza la camara al subir o bajar escalones (0 = sin suavizar).")]
    [Range(0f, 30f)] public float suavizadoEscalones = 12f;
    [Tooltip("Que tan abajo de la coronilla van los ojos (la camara).")]
    [Range(0f, 0.3f)] public float ojosBajoCoronilla = 0.1f;

    [Header("Vista")]
    [Tooltip("Camara hija. Si esta vacia usa la primera camara en los hijos.")]
    public Camera camara;
    [Tooltip("Grados por pixel de mouse.")]
    public float sensibilidadMouse = 0.1f;
    [Tooltip("Grados por segundo con el stick del control.")]
    public float sensibilidadControl = 150f;
    public float limiteVertical = 85f;

    [Header("Interactuar: recoger objetos y abrir puertas (tecla E)")]
    [Tooltip("Distancia maxima desde los ojos para usar lo que estas mirando.")]
    public float distanciaRecoger = 2.5f;
    [Tooltip("Radio alrededor de los pies para recoger objetos chiquitos en el suelo sin apuntarles exacto.")]
    public float radioCercano = 1.3f;

    /// <summary>El jugador de la escena (lo usan las puertas con llave para saber si llegaste).</summary>
    public static JugadorPrimeraPersona Instancia { get; private set; }

    CharacterController controller;
    Interactuable objetoCercano;
    string mensajeFallo;
    GameObject avisoRecoger;
    UnityEngine.UI.Text textoAviso;
    UnityEngine.UI.Image iconoAviso;
    float tiempoMensajeFallo;
    InputAction accionMover, accionMirar, accionCorrer, accionSaltar;
    Vector3 velocidadPlano;
    float velocidadVertical;
    float pitch;
    float alturaOjos, ultimaAltura, desfaseCamara;

    void Awake()
    {
        Instancia = this;
        controller = GetComponent<CharacterController>();
        if (camara == null) camara = GetComponentInChildren<Camera>();
        AplicarTamano();

        if (InputSystem.actions != null)
        {
            accionMover = InputSystem.actions.FindAction("Player/Move");
            accionMirar = InputSystem.actions.FindAction("Player/Look");
            accionCorrer = InputSystem.actions.FindAction("Player/Sprint");
            accionSaltar = InputSystem.actions.FindAction("Player/Jump");
        }
    }

    /// <summary>Ajusta la capsula (pies en el mismo lugar) y la altura de la camara.</summary>
    void AplicarTamano()
    {
        controller.height = estatura;
        controller.radius = Mathf.Min(radioCapsula, estatura * 0.5f);
        controller.center = new Vector3(0f, estatura * 0.5f, 0f);
        // Los escalones del molino suben 24 cm: el paso maximo tiene que ser mayor.
        controller.stepOffset = Mathf.Clamp(estatura * 0.3f, 0.3f, 0.4f);
        alturaOjos = estatura - ojosBajoCoronilla;
        if (camara != null)
        {
            var p = camara.transform.localPosition;
            camara.transform.localPosition = new Vector3(p.x, alturaOjos, p.z);
            if (camara.GetComponent<ObjetoEnMano>() == null) camara.gameObject.AddComponent<ObjetoEnMano>();
        }
        ultimaAltura = transform.position.y;
    }

    void OnEnable()
    {
        accionMover?.Enable();
        accionMirar?.Enable();
        accionCorrer?.Enable();
        accionSaltar?.Enable();
        CapturarCursor(true);
    }

    void OnDisable() => CapturarCursor(false);

    void Update()
    {
        // Leyendo el libro: no te mueves, no giras la camara y no se ve el aviso.
        if (PanelLectura.Bloqueando)
        {
            if (avisoRecoger != null) avisoRecoger.SetActive(false);
            return;
        }
        ManejarCursor();
        Mirar();
        Mover();
        Interactuar();
    }

    // ---------- Interactuar (recoger objetos, abrir puertas) ----------

    void Interactuar()
    {
        objetoCercano = BuscarInteractuable();

        var teclado = Keyboard.current;
        var control = Gamepad.current;
        bool presionoE = (teclado != null && teclado.eKey.wasPressedThisFrame)
                      || (control != null && control.buttonWest.wasPressedThisFrame);

        if (objetoCercano != null && presionoE && Cursor.lockState == CursorLockMode.Locked)
        {
            if (!objetoCercano.Interactuar(transform))
            {
                mensajeFallo = objetoCercano.MensajeFallo;
                if (!string.IsNullOrEmpty(mensajeFallo)) tiempoMensajeFallo = Time.time + 1.5f;
            }
            if (objetoCercano != null && !objetoCercano.isActiveAndEnabled) objetoCercano = null; // ya se recogio
        }

        MostrarAviso();
    }

    /// <summary>Lo que tienes enfrente (rayo desde la camara) o, si no, lo mas cercano frente a tus pies.</summary>
    Interactuable BuscarInteractuable()
    {
        if (camara != null)
        {
            var ojos = camara.transform;
            if (Physics.SphereCast(ojos.position, 0.15f, ojos.forward, out var golpe, distanciaRecoger, ~0, QueryTriggerInteraction.Collide))
            {
                var r = golpe.collider.GetComponentInParent<Interactuable>();
                if (r != null && r.Disponible) return r;
            }
        }

        Interactuable mejor = null;
        float mejorDistancia = float.MaxValue;
        var centro = transform.position + Vector3.up * 0.5f;
        foreach (var col in Physics.OverlapSphere(centro, radioCercano, ~0, QueryTriggerInteraction.Collide))
        {
            var r = col.GetComponentInParent<Interactuable>();
            if (r == null || !r.Disponible) continue;
            var hacia = Vector3.ProjectOnPlane(col.bounds.center - transform.position, Vector3.up);
            if (hacia.sqrMagnitude > 0.04f && Vector3.Dot(transform.forward, hacia.normalized) < 0.3f) continue; // detras de ti
            float d = hacia.sqrMagnitude;
            if (d < mejorDistancia) { mejorDistancia = d; mejor = r; }
        }
        return mejor;
    }

    void MostrarAviso()
    {
        bool fallo = Time.time < tiempoMensajeFallo;
        bool mostrar = (objetoCercano != null || fallo) && Cursor.lockState == CursorLockMode.Locked;
        if (mostrar && avisoRecoger == null) CrearAviso();
        if (avisoRecoger == null) return;

        avisoRecoger.SetActive(mostrar);
        if (!mostrar) return;
        textoAviso.text = fallo
            ? mensajeFallo
            : $"<b>(E)</b>  {objetoCercano.TextoAccion}";
        var icono = fallo ? null : objetoCercano.IconoAccion;
        iconoAviso.sprite = icono;
        iconoAviso.gameObject.SetActive(icono != null);
    }

    /// <summary>Crea la ventanita en el mismo Canvas de la barra, con el mismo marco dorado.</summary>
    void CrearAviso()
    {
        var barra = BarraHerramientas.Instancia;
        if (barra == null) return;
        var canvas = barra.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        avisoRecoger = new GameObject("Aviso Recoger", typeof(RectTransform));
        avisoRecoger.layer = canvas.gameObject.layer;
        var rt = (RectTransform)avisoRecoger.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -110f);

        var fondo = avisoRecoger.AddComponent<UnityEngine.UI.Image>();
        fondo.sprite = barra.spriteMarco;
        fondo.type = UnityEngine.UI.Image.Type.Sliced;
        fondo.pixelsPerUnitMultiplier = 2f; // marco mas delgado que el de la barra
        fondo.raycastTarget = false;

        var ajuste = avisoRecoger.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        ajuste.padding = new RectOffset(26, 26, 10, 12);
        ajuste.childAlignment = TextAnchor.MiddleCenter;
        ajuste.spacing = 12f;
        ajuste.childControlWidth = ajuste.childControlHeight = true;
        ajuste.childForceExpandWidth = ajuste.childForceExpandHeight = false;
        var tamano = avisoRecoger.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        tamano.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        tamano.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        // Iconito opcional a la izquierda (ej. el ojo del libro).
        var goIcono = new GameObject("Icono", typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement));
        goIcono.layer = avisoRecoger.layer;
        goIcono.transform.SetParent(rt, false);
        iconoAviso = goIcono.AddComponent<UnityEngine.UI.Image>();
        iconoAviso.preserveAspect = true;
        iconoAviso.raycastTarget = false;
        var tamanoIcono = goIcono.GetComponent<UnityEngine.UI.LayoutElement>();
        tamanoIcono.preferredWidth = 44f;
        tamanoIcono.preferredHeight = 30f;
        goIcono.SetActive(false);

        var go = new GameObject("Texto", typeof(RectTransform));
        go.layer = avisoRecoger.layer;
        go.transform.SetParent(rt, false);
        textoAviso = go.AddComponent<UnityEngine.UI.Text>();
        textoAviso.font = barra.fuente != null ? barra.fuente : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textoAviso.fontSize = 26;
        textoAviso.color = new Color(1f, 0.95f, 0.88f);
        textoAviso.alignment = TextAnchor.MiddleCenter;
        textoAviso.supportRichText = true;
        textoAviso.raycastTarget = false;
        var sombra = go.AddComponent<UnityEngine.UI.Shadow>();
        sombra.effectColor = new Color(0.2f, 0.1f, 0.2f, 0.7f);
        sombra.effectDistance = new Vector2(1f, -1f);
    }

    void ManejarCursor()
    {
        var teclado = Keyboard.current;
        var mouse = Mouse.current;
        if (teclado != null && teclado.escapeKey.wasPressedThisFrame)
            CapturarCursor(false);
        else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            CapturarCursor(true);
    }

    static void CapturarCursor(bool capturar)
    {
        Cursor.lockState = capturar ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !capturar;
    }

    void Mirar()
    {

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        Vector2 delta = Vector2.zero;
        if (accionMirar != null)
        {
            delta = accionMirar.ReadValue<Vector2>();
            bool esControl = accionMirar.activeControl != null && accionMirar.activeControl.device is Gamepad;
            delta *= esControl ? sensibilidadControl * Time.deltaTime : sensibilidadMouse;
        }
        else if (Mouse.current != null)
        {
            delta = Mouse.current.delta.ReadValue() * sensibilidadMouse;
        }

        transform.Rotate(0f, delta.x, 0f);
        pitch = Mathf.Clamp(pitch - delta.y, -limiteVertical, limiteVertical);
        if (camara != null)
            camara.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Mover()
    {
        Vector2 entrada = LeerMovimiento();
        bool corriendo = accionCorrer != null ? accionCorrer.IsPressed()
                       : Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        Vector3 deseada = (transform.right * entrada.x + transform.forward * entrada.y)
                          * (corriendo ? velocidadCorrer : velocidadCaminar);
        velocidadPlano = Vector3.MoveTowards(velocidadPlano, deseada, aceleracion * Time.deltaTime);

        if (controller.isGrounded)
        {
            if (velocidadVertical < 0f) velocidadVertical = -2f;
            bool saltar = accionSaltar != null ? accionSaltar.WasPressedThisFrame()
                        : Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            if (saltar) velocidadVertical = Mathf.Sqrt(alturaSalto * -2f * gravedad);
        }
        velocidadVertical += gravedad * Time.deltaTime;

        bool estabaEnSuelo = controller.isGrounded;
        controller.Move((velocidadPlano + Vector3.up * velocidadVertical) * Time.deltaTime);

        // Al bajar escalones: en vez de "brincar" de uno en uno, se pega al escalon de abajo.
        if (estabaEnSuelo && !controller.isGrounded && velocidadVertical <= 0f)
        {
            var pies = transform.position + Vector3.up * (controller.radius + 0.05f);
            if (Physics.SphereCast(pies, controller.radius * 0.9f, Vector3.down, out _,
                                   controller.stepOffset + 0.1f, ~0, QueryTriggerInteraction.Ignore))
            {
                controller.Move(Vector3.down * (controller.stepOffset + 0.1f));
                velocidadVertical = -2f;
            }
        }

        SuavizarCamara();
    }

    /// <summary>Cuando el cuerpo sube o baja de golpe un escalon, la camara lo sigue suave.</summary>
    void SuavizarCamara()
    {
        float salto = transform.position.y - ultimaAltura;
        ultimaAltura = transform.position.y;
        if (camara == null) return;

        if (suavizadoEscalones > 0f && controller.isGrounded && Mathf.Abs(salto) > 0.02f && Mathf.Abs(salto) < 0.5f)
            desfaseCamara = Mathf.Clamp(desfaseCamara - salto, -0.45f, 0.45f);
        desfaseCamara = suavizadoEscalones > 0f
            ? Mathf.Lerp(desfaseCamara, 0f, 1f - Mathf.Exp(-suavizadoEscalones * Time.deltaTime))
            : 0f;

        var p = camara.transform.localPosition;
        camara.transform.localPosition = new Vector3(p.x, alturaOjos + desfaseCamara, p.z);
    }

    Vector2 LeerMovimiento()
    {
        if (accionMover != null) return Vector2.ClampMagnitude(accionMover.ReadValue<Vector2>(), 1f);
        var k = Keyboard.current;
        if (k == null) return Vector2.zero;
        Vector2 v = Vector2.zero;
        if (k.wKey.isPressed) v.y += 1;
        if (k.sKey.isPressed) v.y -= 1;
        if (k.dKey.isPressed) v.x += 1;
        if (k.aKey.isPressed) v.x -= 1;
        return Vector2.ClampMagnitude(v, 1f);
    }
}
