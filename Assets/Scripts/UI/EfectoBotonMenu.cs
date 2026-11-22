using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Hace que el boton crezca un poquito cuando pasas el mouse encima (el brillo dorado lo hace el Button con Color Tint).
/// </summary>
public class EfectoBotonMenu : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float escalaEncima = 1.05f;
    public float velocidad = 12f;

    Vector3 escalaNormal;
    Vector3 escalaObjetivo;

    void Awake()
    {
        escalaNormal = transform.localScale;
        escalaObjetivo = escalaNormal;
    }

    void OnDisable() => transform.localScale = escalaObjetivo = escalaNormal;

    public void OnPointerEnter(PointerEventData datos) => escalaObjetivo = escalaNormal * escalaEncima;

    public void OnPointerExit(PointerEventData datos) => escalaObjetivo = escalaNormal;

    void Update()
    {
        // Lerp = acercarse poco a poco a la escala objetivo (se ve suave, no de golpe).
        transform.localScale = Vector3.Lerp(transform.localScale, escalaObjetivo, velocidad * Time.unscaledDeltaTime);
    }
}
