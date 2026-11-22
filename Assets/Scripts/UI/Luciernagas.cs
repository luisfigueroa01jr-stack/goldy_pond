using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puntitos de luz que flotan despacio sobre el fondo del menu, para que no se vea como una imagen quieta.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class Luciernagas : MonoBehaviour
{
    public Sprite imagen;
    public int cantidad = 28;
    public Vector2 tamano = new Vector2(10f, 26f);      // minimo y maximo en pixeles
    public float velocidadSubida = 18f;
    public Color color = new Color(1f, 0.88f, 0.55f, 1f);

    RectTransform area;
    RectTransform[] puntos;
    Image[] imagenes;
    float[] fase;
    float[] vaiven;

    void Start()
    {
        area = (RectTransform)transform;
        puntos = new RectTransform[cantidad];
        imagenes = new Image[cantidad];
        fase = new float[cantidad];
        vaiven = new float[cantidad];

        for (int i = 0; i < cantidad; i++)
        {
            var go = new GameObject("Luciernaga " + (i + 1), typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            imagenes[i] = go.GetComponent<Image>();
            imagenes[i].sprite = imagen;
            imagenes[i].raycastTarget = false;   // que no estorben a los botones
            puntos[i] = go.GetComponent<RectTransform>();
            float t = Random.Range(tamano.x, tamano.y);
            puntos[i].sizeDelta = new Vector2(t, t);
            puntos[i].anchoredPosition = PosicionAlAzar(true);
            fase[i] = Random.Range(0f, 10f);
            vaiven[i] = Random.Range(10f, 30f);
        }
    }

    Vector2 PosicionAlAzar(bool cualquierAltura)
    {
        var r = area.rect;
        float y = cualquierAltura ? Random.Range(r.yMin, r.yMax) : r.yMin - 20f;
        return new Vector2(Random.Range(r.xMin, r.xMax), y);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < cantidad; i++)
        {
            fase[i] += dt;
            var p = puntos[i].anchoredPosition;
            p.y += velocidadSubida * dt;
            p.x += Mathf.Sin(fase[i] * 0.8f) * vaiven[i] * dt;   // se mecen de lado a lado
            if (p.y > area.rect.yMax + 20f) p = PosicionAlAzar(false);  // al salir por arriba vuelven a entrar por abajo
            puntos[i].anchoredPosition = p;

            var c = color;
            c.a = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(fase[i] * 1.3f));  // parpadean
            imagenes[i].color = c;
        }
    }
}
