using UnityEngine;
using UnityEngine.InputSystem;

public class CamaraSeguir : MonoBehaviour
{
    public Transform objetivo;
    [Tooltip("Altura del punto que mira la camara (mas o menos el pecho).")]
    public float alturaMirada = 1.4f;
    public float distancia = 4f;
    public float distanciaMin = 1.5f;
    public float distanciaMax = 8f;
    public float sensibilidad = 0.2f;
    public float suavizado = 10f;

    private float yaw;
    private float pitch = 15f;

    private void Start()
    {
        if (objetivo != null)
        {
            yaw = objetivo.eulerAngles.y;
        }
    }

    private void LateUpdate()
    {
        if (objetivo == null)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * sensibilidad;
                pitch = Mathf.Clamp(pitch - delta.y * sensibilidad, -10f, 70f);
            }

            float rueda = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(rueda) > 0.01f)
            {
                distancia = Mathf.Clamp(distancia - Mathf.Sign(rueda) * 0.5f, distanciaMin, distanciaMax);
            }
        }

        Vector3 foco = objetivo.position + Vector3.up * alturaMirada;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 posicionDeseada = foco - rot * Vector3.forward * distancia;

        float t = 1f - Mathf.Exp(-suavizado * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, posicionDeseada, t);
        transform.LookAt(foco);
    }
}
