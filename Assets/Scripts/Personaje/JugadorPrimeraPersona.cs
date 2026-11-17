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

    [Header("Vista")]
    [Tooltip("Camara hija. Si esta vacia usa la primera camara en los hijos.")]
    public Camera camara;
    [Tooltip("Grados por pixel de mouse.")]
    public float sensibilidadMouse = 0.1f;
    [Tooltip("Grados por segundo con el stick del control.")]
    public float sensibilidadControl = 150f;
    public float limiteVertical = 85f;

    CharacterController controller;
    InputAction accionMover, accionMirar, accionCorrer, accionSaltar;
    Vector3 velocidadPlano;
    float velocidadVertical;
    float pitch;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (camara == null) camara = GetComponentInChildren<Camera>();

        if (InputSystem.actions != null)
        {
            accionMover = InputSystem.actions.FindAction("Player/Move");
            accionMirar = InputSystem.actions.FindAction("Player/Look");
            accionCorrer = InputSystem.actions.FindAction("Player/Sprint");
            accionSaltar = InputSystem.actions.FindAction("Player/Jump");
        }
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
        ManejarCursor();
        Mirar();
        Mover();
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

        controller.Move((velocidadPlano + Vector3.up * velocidadVertical) * Time.deltaTime);
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
