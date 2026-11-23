using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PersonajeCaminar : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Metros por segundo al caminar.")]
    public float velocidadCaminar = 2f;
    [Tooltip("Metros por segundo al correr (Shift).")]
    public float velocidadCorrer = 5f;
    [Tooltip("Que tan rapido gira hacia donde camina (grados por segundo).")]
    public float velocidadGiro = 720f;
    [Tooltip("Suaviza los arranques y frenadas.")]
    public float aceleracion = 10f;
    public float gravedad = -20f;

    [Header("Referencias")]
    [Tooltip("Camara que define hacia donde es 'adelante'. Si esta vacia usa Camera.main.")]
    public Transform camara;
    [Tooltip("Animator del modelo. Si esta vacio lo busca en los hijos.")]
    public Animator animator;

    [Header("Animator")]
    [Tooltip("Parametro float del Animator: 0 = quieto, 1 = caminar, 2 = correr.")]
    public string parametroVelocidad = "Velocidad";

    private CharacterController controller;
    private InputAction accionMover;
    private InputAction accionCorrer;
    private float velocidadActual;
    private float velocidadVertical;
    private int hashVelocidad;
    private bool animatorTieneParametro;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (camara == null && Camera.main != null)
        {
            camara = Camera.main.transform;
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
            hashVelocidad = Animator.StringToHash(parametroVelocidad);
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == hashVelocidad && p.type == AnimatorControllerParameterType.Float)
                {
                    animatorTieneParametro = true;
                }
            }

            if (!animatorTieneParametro)
            {
                Debug.LogWarning($"[PersonajeCaminar] El Animator no tiene un float \"{parametroVelocidad}\". El personaje se movera pero no animara.", this);
            }
        }

        if (InputSystem.actions != null)
        {
            accionMover = InputSystem.actions.FindAction("Player/Move");
            accionCorrer = InputSystem.actions.FindAction("Player/Sprint");
        }
    }

    private void OnEnable()
    {
        accionMover?.Enable();
        accionCorrer?.Enable();
    }

    private void Update()
    {
        Vector2 entrada = leerMovimiento();
        bool corriendo = leerCorrer();

        Vector3 adelante = Vector3.forward, derecha = Vector3.right;
        if (camara != null)
        {
            adelante = Vector3.ProjectOnPlane(camara.forward, Vector3.up).normalized;
            derecha = Vector3.ProjectOnPlane(camara.right, Vector3.up).normalized;
        }

        Vector3 direccion = adelante * entrada.y + derecha * entrada.x;
        float intensidad = Mathf.Clamp01(direccion.magnitude);
        if (intensidad > 0.01f)
        {
            direccion /= direccion.magnitude;
        }

        float objetivo = intensidad * (corriendo ? velocidadCorrer : velocidadCaminar);
        velocidadActual = Mathf.MoveTowards(velocidadActual, objetivo, aceleracion * Time.deltaTime * velocidadCorrer);

        if (intensidad > 0.01f)
        {
            Quaternion rotacionObjetivo = Quaternion.LookRotation(direccion, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacionObjetivo, velocidadGiro * Time.deltaTime);
        }

        if (controller.isGrounded && velocidadVertical < 0f)
        {
            velocidadVertical = -2f;
        }

        velocidadVertical += gravedad * Time.deltaTime;

        Vector3 movimiento = transform.forward * velocidadActual + Vector3.up * velocidadVertical;
        controller.Move(movimiento * Time.deltaTime);

        if (animatorTieneParametro)
        {
            float valor = velocidadActual <= velocidadCaminar
                ? velocidadActual / velocidadCaminar
                : 1f + (velocidadActual - velocidadCaminar) / (velocidadCorrer - velocidadCaminar);
            animator.SetFloat(hashVelocidad, valor, 0.1f, Time.deltaTime);
        }
    }

    private Vector2 leerMovimiento()
    {
        if (accionMover != null)
        {
            return Vector2.ClampMagnitude(accionMover.ReadValue<Vector2>(), 1f);
        }

        Keyboard k = Keyboard.current;
        if (k == null)
        {
            return Vector2.zero;
        }

        Vector2 v = Vector2.zero;
        if (k.wKey.isPressed || k.upArrowKey.isPressed)
        {
            v.y += 1;
        }

        if (k.sKey.isPressed || k.downArrowKey.isPressed)
        {
            v.y -= 1;
        }

        if (k.dKey.isPressed || k.rightArrowKey.isPressed)
        {
            v.x += 1;
        }

        if (k.aKey.isPressed || k.leftArrowKey.isPressed)
        {
            v.x -= 1;
        }

        return Vector2.ClampMagnitude(v, 1f);
    }

    private bool leerCorrer()
    {
        if (accionCorrer != null)
        {
            return accionCorrer.IsPressed();
        }

        return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
    }
}
