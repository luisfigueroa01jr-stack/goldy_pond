using UnityEngine;

/// <summary>
/// Mecanismo del segundo piso del molino al que le falta una pieza.
/// - Al acercarte sin el engranaje sale el aviso abajo a la izquierda: "Falta engranaje".
/// - Con el engranaje seleccionado en la barra: (E) Poner engranaje.
///   El engranaje aparece en la ranura girando despacio, se quita de la barra
///   y se abre la trampilla de la planta baja (entrada al subterraneo).
/// </summary>
public class Mecanismo : Interactuable
{
    [Tooltip("Nombre del objeto de la barra que le falta.")]
    public string piezaNecesaria = "Engranaje";
    public string mensajeFalta = "Falta engranaje";
    public string mensajeSeleccionar = "Selecciona el engranaje en la barra";
    [Tooltip("Hueco donde va la pieza (Ranura_Pieza_Faltante).")]
    public Transform ranura;
    [Tooltip("Lo que se abre al ponerle la pieza.")]
    public Trampilla trampilla;
    [Tooltip("A que distancia (metros) sale el aviso.")]
    public float distanciaAviso = 2.2f;
    [Tooltip("Hacia donde mira el frente de la ranura (en coordenadas de la ranura). Lo calcula Goldy Pond > Interaccion.")]
    public Vector3 frenteLocal;
    [Tooltip("Cuanto sobresale el engranaje del tablero, en metros.")]
    public float separacion = 0.015f;
    [Tooltip("Grados por segundo que gira el engranaje ya puesto (0 = quieto).")]
    public float velocidadGiro = 25f;

    public bool Completo { get; private set; }

    Transform piezaPuesta;
    Vector3 ejeGiro, centroGiro;
    bool jugadorCerca;

    public override bool Disponible => !Completo;
    public override string TextoAccion => $"Poner <color=#FFDFA0>{piezaNecesaria.ToLower()}</color>";

    public override bool Interactuar(Transform jugador)
    {
        if (Completo) return true;
        var barra = BarraHerramientas.Instancia;
        if (barra == null || barra.ObjetoSeleccionado != piezaNecesaria)
        {
            AvisarQueFalta();
            return true;
        }
        Colocar();
        barra.Quitar(piezaNecesaria);
        if (trampilla != null) trampilla.Abrir();
        return true;
    }

    void AvisarQueFalta()
    {
        var barra = BarraHerramientas.Instancia;
        bool laTraes = barra != null && barra.Tiene(piezaNecesaria);
        Notificacion.Mostrar(laTraes ? mensajeSeleccionar : mensajeFalta);
    }

    void Update()
    {
        if (Completo)
        {
            if (piezaPuesta != null && velocidadGiro != 0f)
                piezaPuesta.RotateAround(centroGiro, ejeGiro, velocidadGiro * Time.deltaTime);
            return;
        }

        var jugador = JugadorPrimeraPersona.Instancia;
        if (jugador == null) return;
        var centro = ranura != null ? ranura.position : transform.position;
        var diferencia = jugador.transform.position - centro;
        bool cerca = Vector3.ProjectOnPlane(diferencia, Vector3.up).magnitude < distanciaAviso
                     && Mathf.Abs(diferencia.y) < 2.5f;

        var barra = BarraHerramientas.Instancia;
        bool enLaMano = barra != null && barra.ObjetoSeleccionado == piezaNecesaria;
        if (cerca && !jugadorCerca && !enLaMano) AvisarQueFalta();
        jugadorCerca = cerca;
    }

    /// <summary>Pone una copia del engranaje en la ranura, de frente y del tamano del hueco.</summary>
    void Colocar()
    {
        Completo = true;
        var destino = ranura != null ? ranura : transform;

        // El "fondo" de la ranura: su lado mas delgado en horizontal. El engranaje se pone de frente a ese lado.
        var mf = destino.GetComponent<MeshFilter>();
        var b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.3f);
        ejeGiro = destino.forward;
        float delgado = float.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            var eje = Vector3.zero; eje[i] = b.size[i];
            var enMundo = destino.TransformVector(eje);
            if (Mathf.Abs(enMundo.normalized.y) > 0.5f) continue; // ese es el alto
            if (enMundo.magnitude < delgado) { delgado = enMundo.magnitude; ejeGiro = enMundo.normalized; }
        }
        // Si ya se sabe el frente real de la ranura (calculado con sus caras), se usa ese.
        if (frenteLocal.sqrMagnitude > 0.0001f) ejeGiro = destino.TransformDirection(frenteLocal).normalized;
        var centro = destino.TransformPoint(b.center);
        var rend = destino.GetComponent<Renderer>();
        float alto = rend != null ? rend.bounds.size.y : 0.3f;
        if (rend != null) centro = rend.bounds.center;
        centroGiro = centro;

        var copia = ObjetoEnMano.CrearCopia(piezaNecesaria);
        if (copia == null) return;
        piezaPuesta = copia.transform;
        piezaPuesta.SetParent(destino.parent, true);
        piezaPuesta.localScale = Vector3.one;
        piezaPuesta.rotation = Quaternion.identity;
        copia.SetActive(true);

        // El lado mas delgado del engranaje (su grosor) apunta hacia afuera de la ranura.
        var mfPieza = copia.GetComponentInChildren<MeshFilter>();
        var grosorLocal = Vector3.up;
        if (mfPieza != null && mfPieza.sharedMesh != null)
        {
            var bp = mfPieza.sharedMesh.bounds.size;
            int i = bp.x <= bp.y && bp.x <= bp.z ? 0 : bp.y <= bp.z ? 1 : 2;
            var eje = Vector3.zero; eje[i] = 1f;
            grosorLocal = mfPieza.transform.TransformDirection(eje);
        }
        piezaPuesta.rotation = Quaternion.FromToRotation(grosorLocal, ejeGiro);

        var lim = ObjetoEnMano.Limites(copia);
        float mayor = Mathf.Max(lim.size.x, lim.size.y, lim.size.z);
        if (mayor > 0.0001f) piezaPuesta.localScale = Vector3.one * (alto * 0.85f / mayor);
        lim = ObjetoEnMano.Limites(copia);
        // Centrado en la ranura y un poquito hacia afuera del tablero, de frente.
        float grosor = Mathf.Abs(Vector3.Dot(lim.size, new Vector3(Mathf.Abs(ejeGiro.x), Mathf.Abs(ejeGiro.y), Mathf.Abs(ejeGiro.z))));
        centroGiro = centro + ejeGiro * (separacion + grosor * 0.25f);
        piezaPuesta.position += centroGiro - lim.center;
    }
}
