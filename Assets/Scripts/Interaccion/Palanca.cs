using System;
using UnityEngine;

/// <summary>
/// Palanca de la Aldea. Solo se puede usar despues de haber bajado al subterraneo:
/// antes no sale ningun aviso ni se mueve.
/// Ya bajaste: al acercarte sale "(E) Activar palanca"; con E el mango gira hacia el frente
/// y la base se ilumina (brillo dorado y una lucecita). Se queda activada.
/// La agrega sola RecogiblesAutomaticos a los objetos llamados "Palanca", "Palanca (1)"...
/// </summary>
public class Palanca : Interactuable
{
    [Tooltip("Lo que gira (el tambor con el mango). Vacio = busca Tambor_Madera.")]
    public Transform tambor;
    [Tooltip("La base que se ilumina. Vacio = busca Base_Inferior.")]
    public Renderer piso;
    [Tooltip("Segundos que tarda en moverse.")]
    public float duracion = 0.7f;
    [Tooltip("Grados que gira. 0 = automatico (queda inclinada igual pero del otro lado).")]
    public float angulo = 0f;
    public Color colorLuz = new Color(1f, 0.78f, 0.35f);
    [Tooltip("Que tanto brilla la base.")]
    public float brillo = 1.6f;
    [Tooltip("Intensidad de la lucecita sobre la base.")]
    public float intensidadLuz = 2.5f;

    /// <summary>Se llama cada vez que se activa una palanca (para lo que pase al activar las tres).</summary>
    public static event Action<Palanca> AlActivar;

    public bool Activada { get; private set; }

    Vector3 eje, centro;
    float giro, avance = -1f; // -1 = quieta sin activar
    Quaternion rotInicial;
    Vector3 posInicial;
    Light luz;
    Material[] materiales;

    public override bool Disponible => !Activada && EstadoJuego.VisitoSubterraneo;
    public override string TextoAccion => "Activar <color=#FFDFA0>palanca</color>";

    void Awake()
    {
        if (tambor == null) tambor = Buscar(transform, "Tambor_Madera");
        if (piso == null) { var b = Buscar(transform, "Base_Inferior"); if (b != null) piso = b.GetComponent<Renderer>(); }
        if (tambor == null) { Debug.LogWarning($"[Goldy Pond] {name}: no encontre Tambor_Madera, no se movera."); return; }

        rotInicial = tambor.localRotation;
        posInicial = tambor.localPosition;
        CalcularGiro();
    }

    /// <summary>El eje es el Eje_Metalico (lo mas largo). El giro deja el mango inclinado igual, pero hacia el otro lado.</summary>
    void CalcularGiro()
    {
        centro = tambor.position;
        eje = transform.right;
        var ejeMetal = Buscar(transform, "Eje_Metalico");
        var mf = ejeMetal != null ? ejeMetal.GetComponent<MeshFilter>() : null;
        if (mf != null && mf.sharedMesh != null)
        {
            var s = mf.sharedMesh.bounds.size;
            int i = s.x >= s.y && s.x >= s.z ? 0 : s.y >= s.z ? 1 : 2;
            var e = Vector3.zero; e[i] = 1f;
            eje = ejeMetal.TransformDirection(e).normalized;
            centro = ejeMetal.TransformPoint(mf.sharedMesh.bounds.center);
        }

        giro = angulo;
        if (Mathf.Abs(giro) < 0.01f)
        {
            var agarradera = Buscar(tambor, "Palanca_Agarradera");
            var r = agarradera != null ? agarradera.GetComponent<Renderer>() : null;
            var mango = r != null ? r.bounds.center - centro : Vector3.up;
            float inclinacion = Vector3.SignedAngle(Vector3.ProjectOnPlane(Vector3.up, eje), Vector3.ProjectOnPlane(mango, eje), eje);
            giro = Mathf.Abs(inclinacion) > 5f ? -2f * inclinacion : 60f;
        }
    }

    public override bool Interactuar(Transform jugador)
    {
        if (!Disponible) return false;
        Activar(true);
        return true;
    }

    /// <summary>conAnimacion = false: queda activada de golpe (al regresar a la escena).</summary>
    public void Activar(bool conAnimacion)
    {
        if (Activada) return;
        Activada = true;
        avance = conAnimacion ? 0f : 1f;
        CrearLuz();
        Aplicar(conAnimacion ? 0f : 1f);
        if (conAnimacion) AlActivar?.Invoke(this);
    }

    void Update()
    {
        if (!Activada || avance >= 1f) return;
        avance = Mathf.Min(1f, avance + Time.deltaTime / Mathf.Max(0.05f, duracion));
        Aplicar(avance);
    }

    void Aplicar(float t)
    {
        // Arranca con fuerza y frena al final (como empujando una palanca pesada).
        float k = 1f - Mathf.Pow(1f - t, 3f);
        if (tambor != null)
        {
            tambor.localRotation = rotInicial;
            tambor.localPosition = posInicial;
            tambor.RotateAround(centro, eje, giro * k);
        }

        // La luz sube un poquito despues de que empieza a moverse.
        float l = Mathf.Clamp01((t - 0.3f) / 0.7f);
        if (luz != null) luz.intensity = intensidadLuz * l;
        if (materiales != null)
            foreach (var m in materiales)
                m.SetColor("_EmissionColor", colorLuz * (brillo * l));
    }

    void CrearLuz()
    {
        if (piso != null)
        {
            materiales = piso.materials; // copias solo para esta palanca
            foreach (var m in materiales)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", Color.black);
            }
        }

        var b = piso != null ? piso.bounds : new Bounds(transform.position, Vector3.one);
        var go = new GameObject("Luz palanca");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(b.center.x, b.max.y + 0.15f, b.center.z);
        luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = colorLuz;
        luz.range = Mathf.Max(2f, Mathf.Max(b.size.x, b.size.z) * 2.5f);
        luz.intensity = 0f;
        luz.shadows = LightShadows.None;
    }

    static Transform Buscar(Transform raiz, string prefijo)
    {
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith(prefijo)) return t;
        return null;
    }
}
