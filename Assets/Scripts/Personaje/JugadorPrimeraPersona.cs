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
    Estamina estamina;
    /// <summary>La estamina (correr gasta, se recupera caminando o quieta).</summary>
    public Estamina Estamina => estamina;
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
    Vector2 entrada;
    Escalera escalera;   // la escalera de mano a la que vas agarrado (null = caminando)
    float tEscalera;     // 0 = hasta abajo, 1 = hasta arriba

    /// <summary>WASD / stick izquierdo de este cuadro (y = adelante).</summary>
    public Vector2 Entrada => entrada;
    public bool Escalando => escalera != null;

    /// <summary>
    /// Lo usa el elevador: no te mueves ni usas E (solo miras), el cuerpo no choca
    /// y otro script puede moverte con transform.position.
    /// </summary>
    public bool Inmovil
    {
        get => inmovil;
        set
        {
            inmovil = value;
            if (escalera != null) escalera = null;
            controller.enabled = !value;
            velocidadPlano = Vector3.zero;
            velocidadVertical = value ? 0f : -2f;
            ultimaAltura = transform.position.y;
            if (value && avisoRecoger != null) avisoRecoger.SetActive(false);
        }
    }
    bool inmovil;

    /// <summary>En las escenas (retumbe, el jefe que cae, el final): el mouse no mueve la camara, la mueve otro script.</summary>
    public bool BloquearMirar { get; set; }
    public Escalera EscaleraActual => escalera;
    /// <summary>0 = pie de la escalera, 1 = hasta arriba.</summary>
    public float AlturaEnEscalera => tEscalera;

    void Awake()
    {
        Instancia = this;
        controller = GetComponent<CharacterController>();
        estamina = GetComponent<Estamina>();
        if (estamina == null) estamina = gameObject.AddComponent<Estamina>();
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
        if (entrandoSaliendo) return;      // metiendose o saliendo de un escondite
        if (!BloquearMirar) Mirar();
        if (escondite != null)
        {
            // Escondida: solo miras (con limite) y con E sales.
            var tec = Keyboard.current; var ctl = Gamepad.current;
            bool e = (tec != null && tec.eKey.wasPressedThisFrame) || (ctl != null && ctl.buttonWest.wasPressedThisFrame);
            if (e && Cursor.lockState == CursorLockMode.Locked) StartCoroutine(SalirDelEscondite());
            return;
        }
        if (inmovil) { ultimaAltura = transform.position.y; return; }
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

        // En la Aldea, antes de leer el libro de la plaza no se puede usar nada mas
        // (ni recoger objetos ni abrir puertas). Empieza al cerrar el libro (primera caceria).
        if (objetoCercano != null && Caceria.BloqueaInteracciones && !Caceria.EsLibroDeLaPlaza(objetoCercano.transform))
        {
            if (presionoE && Cursor.lockState == CursorLockMode.Locked)
                Notificacion.Mostrar("Primero lee el libro del centro de la aldea");
            objetoCercano = null;
        }

        // Sin nada enfrente, pero con un objeto que trae mensaje en la mano (el pergamino): mantener E para verlo.
        var mensaje = objetoCercano == null ? MensajesObjetos.De(BarraHerramientas.Instancia?.ObjetoSeleccionado) : null;
        if (mensaje != null && presionoE && Cursor.lockState == CursorLockMode.Locked)
        {
            PanelLectura.Abrir(mensaje, true);
            if (avisoRecoger != null) avisoRecoger.SetActive(false);
            return;
        }

        if (objetoCercano != null && presionoE && Cursor.lockState == CursorLockMode.Locked)
        {
            if (!objetoCercano.Interactuar(transform))
            {
                mensajeFallo = objetoCercano.MensajeFallo;
                if (!string.IsNullOrEmpty(mensajeFallo)) tiempoMensajeFallo = Time.time + 1.5f;
            }
            if (objetoCercano != null && !objetoCercano.isActiveAndEnabled) objetoCercano = null; // ya se recogio
        }

        MostrarAviso(mensaje != null);
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

    static Sprite ojo;

    void MostrarAviso(bool verMensaje)
    {
        bool fallo = Time.time < tiempoMensajeFallo;
        bool mostrar = (objetoCercano != null || fallo || verMensaje) && Cursor.lockState == CursorLockMode.Locked;
        if (mostrar && avisoRecoger == null) CrearAviso();
        if (avisoRecoger == null) return;

        avisoRecoger.SetActive(mostrar);
        if (!mostrar) return;
        if (ojo == null) ojo = Resources.Load<Sprite>("UI/Ojo");
        textoAviso.text = fallo ? mensajeFallo
            : objetoCercano != null ? $"<b>(E)</b>  {objetoCercano.TextoAccion}"
            : "<b>(E)</b>  Ver <color=#FFDFA0>mensaje</color>";
        var icono = fallo ? null : objetoCercano != null ? objetoCercano.IconoAccion : ojo;
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
        if (escondite != null)
        {
            // Desde el escondite solo ves lo que tienes enfrente (y un poco a los lados).
            float baseYaw = Quaternion.LookRotation(escondite.frente).eulerAngles.y;
            float rel = Mathf.Clamp(Mathf.DeltaAngle(baseYaw, transform.eulerAngles.y), -escondite.limiteGiro, escondite.limiteGiro);
            transform.rotation = Quaternion.Euler(0f, baseYaw + rel, 0f);
            pitch = Mathf.Clamp(pitch, -escondite.limiteArriba, escondite.limiteAbajo);
        }
        if (camara != null)
            camara.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // ---------- Escondites (armarios, debajo de la cama, detras de barriles) ----------

    Escondite escondite;
    bool entrandoSaliendo;
    Vector3 posAntesEscondite;
    Quaternion rotAntesEscondite;
    float pitchAntesEscondite, nearAntes;

    /// <summary>Esta escondida (en un armario, debajo de la cama...).</summary>
    public bool Oculto => escondite != null;

    /// <summary>Se mete al escondite rapidito. false si no se puede ahora.</summary>
    public bool Ocultarse(Escondite e)
    {
        if (e == null || escondite != null || entrandoSaliendo || inmovil || escalera != null) return false;
        StartCoroutine(EntrarAlEscondite(e));
        return true;
    }

    System.Collections.IEnumerator EntrarAlEscondite(Escondite e)
    {
        entrandoSaliendo = true;
        escondite = e;
        posAntesEscondite = transform.position;
        rotAntesEscondite = transform.rotation;
        pitchAntesEscondite = pitch;
        Inmovil = true;
        if (avisoRecoger != null) avisoRecoger.SetActive(false);
        if (camara != null) { nearAntes = camara.nearClipPlane; camara.nearClipPlane = 0.03f; } // para no "cortar" las paredes de cerca

        var ojos = camara != null ? camara.transform.position - transform.position : Vector3.up * alturaOjos;
        var desdeP = transform.position; var hastaP = e.punto - ojos;
        var desdeR = transform.rotation; var hastaR = Quaternion.LookRotation(e.frente);
        float desdePitch = pitch, hastaPitch = e.tipo == Escondite.Tipo.Cama ? 0f : 5f;
        yield return Transicion(desdeP, hastaP, desdeR, hastaR, desdePitch, hastaPitch, 0.32f, true);

        entrandoSaliendo = false;
        Notificacion.Mostrar("Escondida. Presiona E para salir", null, 2.5f);
    }

    System.Collections.IEnumerator SalirDelEscondite()
    {
        entrandoSaliendo = true;
        yield return Transicion(transform.position, posAntesEscondite, transform.rotation, rotAntesEscondite,
                                pitch, Mathf.Clamp(pitchAntesEscondite, -20f, 20f), 0.28f, false);
        if (camara != null) camara.nearClipPlane = nearAntes;
        escondite = null;
        Inmovil = false;
        entrandoSaliendo = false;
    }

    /// <summary>Movimiento rapido con frenado al final y un pequeno "agachon" a medio camino.</summary>
    System.Collections.IEnumerator Transicion(Vector3 p0, Vector3 p1, Quaternion r0, Quaternion r1, float pitch0, float pitch1, float duracion, bool entrando)
    {
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / duracion);
            float k = 1f - Mathf.Pow(1f - t, 3f);
            float agachon = Mathf.Sin(t * Mathf.PI) * 0.12f;
            transform.SetPositionAndRotation(Vector3.Lerp(p0, p1, k) + Vector3.down * agachon, Quaternion.Slerp(r0, r1, k));
            pitch = Mathf.Lerp(pitch0, pitch1, k);
            if (camara != null) camara.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            Escondite.Oscurecer(entrando ? k * 0.85f : (1f - k) * 0.85f);
            yield return null;
        }
        ultimaAltura = transform.position.y;
    }

    // ---------- Escalera de mano ----------

    /// <summary>Te agarra a la escalera en la altura t (0 = abajo, 1 = arriba). Arriba ademas te voltea hacia ella.</summary>
    public void EmpezarEscalar(Escalera e, float t)
    {
        if (e == null) return;
        escalera = e;
        tEscalera = Mathf.Clamp01(t);
        velocidadPlano = Vector3.zero;
        velocidadVertical = 0f;
        controller.enabled = false; // en la escalera no choca: la sigue exacto
        transform.position = PiesEnEscalera(tEscalera);
        if (t >= 1f)
        {
            transform.rotation = Quaternion.LookRotation(-e.Frente, Vector3.up);
            pitch = 0f;
            if (camara != null) camara.transform.localRotation = Quaternion.identity;
        }
        ultimaAltura = transform.position.y;
        desfaseCamara = 0f;
    }

    public void SoltarEscalera()
    {
        if (escalera == null) return;
        transform.position += escalera.Frente * 0.1f; // un pasito hacia atras para no quedar metido
        escalera = null;
        controller.enabled = true;
        velocidadVertical = -2f;
        ultimaAltura = transform.position.y;
    }

    /// <summary>Pone al jugador en un lugar (al llegar de otra escena), mirando hacia "adelante".</summary>
    public void Teletransportar(Vector3 pies, Vector3 adelante)
    {
        escalera = null;
        controller.enabled = false;
        transform.position = pies;
        if (adelante.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(adelante, Vector3.up);
        controller.enabled = true;
        velocidadPlano = Vector3.zero;
        velocidadVertical = -2f;
        ultimaAltura = transform.position.y;
    }

    /// <summary>Donde van los pies: abajo pisando el suelo, arriba con los ojos a la altura de la punta.</summary>
    Vector3 PiesEnEscalera(float t)
    {
        var abajo = escalera.Punto(0f);
        var arriba = escalera.Punto(1f) - Vector3.up * (estatura * 0.85f);
        return Vector3.Lerp(abajo, arriba, t);
    }

    void Escalar()
    {
        var abajo = escalera.Punto(0f);
        var arriba = escalera.Punto(1f) - Vector3.up * (estatura * 0.85f);
        float largo = Mathf.Max(0.5f, Vector3.Distance(abajo, arriba));

        bool saltar = accionSaltar != null ? accionSaltar.WasPressedThisFrame()
                    : Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (saltar) { SoltarEscalera(); return; }

        // Hasta abajo y sigues bajando: te sueltas.
        if (tEscalera <= 0f && entrada.y < -0.1f) { SoltarEscalera(); return; }

        tEscalera = Mathf.Clamp01(tEscalera + entrada.y * escalera.velocidad * Time.deltaTime / largo);
        transform.position = PiesEnEscalera(tEscalera);
        ultimaAltura = transform.position.y;
        desfaseCamara = 0f;
    }

    void Mover()
    {
        entrada = LeerMovimiento();
        if (escalera != null) { Escalar(); return; }
        bool quiereCorrer = accionCorrer != null ? accionCorrer.IsPressed()
                          : Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        bool seMueve = entrada.sqrMagnitude > 0.01f;
        // Correr gasta estamina; sin estamina (agotada) no se puede correr y se camina mas lento.
        bool corriendo = quiereCorrer && seMueve && estamina.PuedeCorrer;
        estamina.Avisar(corriendo, seMueve);
        float caminar = estamina.Activa && estamina.Agotada ? velocidadCaminar * estamina.caminarAgotada : velocidadCaminar;

        Vector3 deseada = (transform.right * entrada.x + transform.forward * entrada.y)
                          * (corriendo ? velocidadCorrer : caminar);
        velocidadPlano = Vector3.MoveTowards(velocidadPlano, deseada, aceleracion * Time.deltaTime);

        if (controller.isGrounded)
        {
            if (velocidadVertical < 0f) velocidadVertical = -2f;
            bool saltar = accionSaltar != null ? accionSaltar.WasPressedThisFrame()
                        : Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            if (saltar && (!estamina.Activa || estamina.Saltar())) velocidadVertical = Mathf.Sqrt(alturaSalto * -2f * gravedad);
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
