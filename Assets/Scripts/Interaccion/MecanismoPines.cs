using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mecanismo de los 3 pines (zona 5 del laberinto).
///  - Tiene 3 huecos (1 izquierda, 2 centro, 3 derecha, viendolo de frente). Cualquier pin entra en cualquier hueco:
///    con un pin en la mano, "(E) Colocar pin"; si el hueco ya tiene uno, "(E) Quitar pin" y regresa a la barra.
///  - El orden correcto es 1 Sol, 2 Flor, 3 Luna. Al lograrlo la luz roja se apaga, se prende la verde,
///    se abre la puertita del compartimento de enfrente y ya se puede agarrar lo que tiene adentro (el engranaje).
///  - Mientras el compartimento esta cerrado, lo de adentro no se puede agarrar.
/// Lo pone solo LaberintoAutomatico en "mecanismo pines".
/// </summary>
public class MecanismoPines : MonoBehaviour
{
    public static readonly string[] OrdenCorrecto = { "Pin del Sol", "Pin de la Flor", "Pin de la Luna" };

    [Tooltip("La caja de enfrente (compartimento) con su puertita.")]
    public Transform compartimento;
    public Transform puertaCompartimento;
    public Renderer luzRoja, luzVerde;

    public bool Resuelto { get; private set; }

    readonly string[] puestos = new string[3];
    readonly GameObject[] modelos = new GameObject[3];
    readonly List<ObjetoRecogible> guardados = new List<ObjetoRecogible>();
    Light foco;

    // Lo que ya estaba puesto, por si se recarga la escena (ej. vas al subterraneo y regresas).
    static readonly Dictionary<string, string[]> memoria = new Dictionary<string, string[]>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void Reiniciar() => memoria.Clear(); // cada Play empieza con el mecanismo vacio

    string Clave => $"{gameObject.scene.name}:{transform.position}";

    // Huecos en coordenadas del mecanismo (asi salen de Blender: x a lo ancho, y arriba, -z al frente).
    static readonly Vector3[] Huecos = { new Vector3(-0.38f, 0f, -0.07f), new Vector3(0f, 0f, -0.07f), new Vector3(0.38f, 0f, -0.07f) };
    const float AlturaDiscoPin = 0.21f; // del pie del pin al centro de su disco

    void Start()
    {
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject($"hueco {i + 1}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Huecos[i];
            var esfera = go.AddComponent<SphereCollider>();
            esfera.isTrigger = true;
            esfera.radius = 0.19f;
            var h = go.AddComponent<HuecoPin>();
            h.mecanismo = this;
            h.indice = i;
        }

        // Focos: rojo prendido, verde apagado. Una lucecita que cambia de color.
        if (luzVerde != null) Brillo(luzVerde, false);
        if (luzRoja != null)
        {
            Brillo(luzRoja, true);
            var go = new GameObject("Luz del foco");
            go.transform.SetParent(transform, false);
            go.transform.position = luzRoja.bounds.center + (luzVerde != null ? (luzVerde.bounds.center - luzRoja.bounds.center) * 0.5f : Vector3.zero) - transform.forward * 0.1f;
            foco = go.AddComponent<Light>();
            foco.type = LightType.Point;
            foco.range = 1.6f;
            foco.intensity = 1.2f;
            foco.color = new Color(0.95f, 0.2f, 0.18f);
        }

        // Lo que esta dentro del compartimento no se puede agarrar hasta que se abra.
        if (compartimento != null)
        {
            var rc = compartimento.GetComponent<Renderer>();
            if (rc != null)
            {
                var caja = rc.bounds;
                caja.Expand(0.05f);
                foreach (var o in FindObjectsByType<ObjetoRecogible>(FindObjectsInactive.Include))
                {
                    var ro = o.GetComponentInChildren<Renderer>();
                    var centro = ro != null ? ro.bounds.center : o.transform.position;
                    if (!caja.Contains(centro)) continue;
                    guardados.Add(o);
                    foreach (var c in o.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                }
                if (guardados.Count > 0)
                    Debug.Log($"[Goldy Pond] Compartimento: adentro tiene {string.Join(", ", guardados.ConvertAll(o => o.name))}");
            }
        }

        if (memoria.TryGetValue(Clave, out var antes))
            for (int i = 0; i < 3; i++)
                if (!string.IsNullOrEmpty(antes[i])) Poner(i, antes[i], false);
        Revisar(false);
    }

    public string En(int i) => puestos[i];

    public string Texto(int i)
    {
        if (!string.IsNullOrEmpty(puestos[i])) return $"Quitar <color=#FFDFA0>{puestos[i]}</color>";
        var enMano = PinEnLaMano();
        return enMano != null ? $"Colocar <color=#FFDFA0>{enMano}</color>" : "Colocar pin";
    }

    static string PinEnLaMano()
    {
        var sel = BarraHerramientas.Instancia != null ? BarraHerramientas.Instancia.ObjetoSeleccionado : null;
        return System.Array.IndexOf(OrdenCorrecto, sel) >= 0 ? sel : null;
    }

    /// <summary>E sobre un hueco: pone el pin que traes en la mano, o quita el que ya esta.</summary>
    public bool Usar(int i)
    {
        if (Resuelto) return true;
        var barra = BarraHerramientas.Instancia;
        if (barra == null) return false;

        if (!string.IsNullOrEmpty(puestos[i]))
        {
            var nombre = puestos[i];
            if (!barra.Agregar(nombre, Resources.Load<Sprite>($"Iconos/{nombre}")))
            {
                Notificacion.Mostrar("La barra esta llena");
                return true;
            }
            if (modelos[i] != null) Destroy(modelos[i]);
            modelos[i] = null;
            puestos[i] = null;
            Guardar();
            return true;
        }

        var pin = PinEnLaMano();
        if (pin == null)
        {
            Notificacion.Mostrar("El mecanismo necesita un pin");
            return true;
        }
        barra.Quitar(pin);
        Poner(i, pin, true);
        Revisar(true);
        return true;
    }

    void Poner(int i, string nombre, bool guardar)
    {
        puestos[i] = nombre;
        var copia = ObjetoEnMano.CrearCopia(nombre);
        if (copia != null)
        {
            copia.name = $"{nombre} (puesto)";
            copia.transform.SetParent(transform, false);
            copia.transform.localScale = Vector3.one; // mismo tamano que en Blender: el disco entra justo en el hueco
            copia.transform.localRotation = Quaternion.identity;
            copia.transform.localPosition = Huecos[i] + new Vector3(0f, -AlturaDiscoPin, -0.015f);
            // Si en la escena se movio la pieza de adentro del pin (y no su raiz), la regresamos al centro:
            // asi el pin siempre queda dentro de su hueco.
            foreach (Transform hijo in copia.transform)
            {
                hijo.localPosition = Vector3.zero;
                hijo.localRotation = Quaternion.identity;
                hijo.localScale = Vector3.one;
            }
            copia.SetActive(true);
            modelos[i] = copia;
        }
        if (guardar) Guardar();
    }

    void Guardar() => memoria[Clave] = (string[])puestos.Clone();

    void Revisar(bool conAnimacion)
    {
        for (int i = 0; i < 3; i++)
            if (puestos[i] != OrdenCorrecto[i]) return;
        Resuelto = true;
        if (luzRoja != null) Brillo(luzRoja, false);
        if (luzVerde != null) Brillo(luzVerde, true);
        if (foco != null) foco.color = new Color(0.3f, 0.95f, 0.3f);
        foreach (var o in guardados)
            if (o != null)
                foreach (var c in o.GetComponentsInChildren<Collider>(true)) c.enabled = true;
        if (puertaCompartimento != null) StartCoroutine(AbrirPuerta(conAnimacion ? 1.2f : 0f));
        if (conAnimacion) Notificacion.Mostrar("¡Se abrió el compartimento!");
    }

    IEnumerator AbrirPuerta(float duracion)
    {
        // La bisagra es el origen de la puertita (lado derecho); el borde libre sale hacia el cuarto.
        var desde = puertaCompartimento.localRotation;
        var hasta = desde * Quaternion.Euler(0f, -105f, 0f);
        for (float t = 0f; t < 1f && duracion > 0f; t += Time.deltaTime / duracion)
        {
            puertaCompartimento.localRotation = Quaternion.Slerp(desde, hasta, t * t * (3f - 2f * t));
            yield return null;
        }
        puertaCompartimento.localRotation = hasta;
    }

    readonly Dictionary<Renderer, Color> colorOriginal = new Dictionary<Renderer, Color>();

    void Brillo(Renderer r, bool prendido)
    {
        var m = r.material; // copia propia, no cambia los otros objetos con el mismo color
        if (!colorOriginal.TryGetValue(r, out var baseColor))
            colorOriginal[r] = baseColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
        if (prendido)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", baseColor * 2f);
            m.SetColor("_BaseColor", baseColor);
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
            m.SetColor("_BaseColor", baseColor * 0.35f);
        }
    }
}

/// <summary>Uno de los 3 huecos del mecanismo de pines.</summary>
public class HuecoPin : Interactuable
{
    public MecanismoPines mecanismo;
    public int indice;

    public override bool Disponible => mecanismo != null && !mecanismo.Resuelto;
    public override string TextoAccion => mecanismo != null ? mecanismo.Texto(indice) : "";
    public override bool Interactuar(Transform jugador) => mecanismo != null && mecanismo.Usar(indice);
}
