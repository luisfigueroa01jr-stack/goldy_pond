using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mecanismo del segundo piso del molino al que le faltan piezas (2 engranajes).
/// - Al acercarte sin engranaje sale el aviso abajo a la izquierda: "Faltan 2 engranajes".
/// - Con el engranaje seleccionado en la barra: (E) Poner engranaje.
///   El primero va en la ranura y el segundo pegado a el, engranando; giran en sentidos contrarios.
///   Cada uno se quita de la barra al ponerlo.
/// - Cuando estan los dos se abre la trampilla de la planta baja (entrada al subterraneo)
///   y se avisa con AlCompletar (para lo que se mueva despues, como las aspas).
/// </summary>
public class Mecanismo : Interactuable
{
    [Tooltip("Nombre del objeto de la barra que le falta.")]
    public string piezaNecesaria = "Engranaje";
    [Tooltip("Cuantas piezas hay que poner para que funcione.")]
    [Min(1)] public int cantidadNecesaria = 2;
    [Tooltip("Mensaje cuando solo falta una pieza y no se ha puesto ninguna.")]
    public string mensajeFalta = "Falta engranaje";
    public string mensajeSeleccionar = "El mecanismo necesita un engranaje";
    [Tooltip("Hueco donde va la pieza (Ranura_Pieza_Faltante).")]
    public Transform ranura;
    [Tooltip("Lo que se abre al ponerle todas las piezas.")]
    public Trampilla trampilla;
    [Tooltip("A que distancia (metros) sale el aviso.")]
    public float distanciaAviso = 2.2f;
    [Tooltip("Hacia donde mira el frente de la ranura (en coordenadas de la ranura). Lo calcula Goldy Pond > Interaccion.")]
    public Vector3 frenteLocal;
    [Tooltip("Cuanto sobresale el engranaje del tablero, en metros.")]
    public float separacion = 0.015f;
    [Tooltip("Grados por segundo que giran los engranajes ya puestos (0 = quietos).")]
    public float velocidadGiro = 25f;
    [Tooltip("Que tan juntos quedan los dos engranajes (1 = se tocan apenas, menos = los dientes se meten uno en otro).")]
    [Range(0.6f, 1.2f)] public float distanciaEntreEngranajes = 0.9f;

    /// <summary>Se llama una vez, cuando ya estan puestas todas las piezas.</summary>
    public event Action AlCompletar;

    public int Puestas { get; private set; }
    public bool Completo => Puestas >= cantidadNecesaria;

    class Pieza { public Transform t; public Vector3 centro; public float sentido; }
    readonly List<Pieza> piezas = new List<Pieza>();
    bool jugadorCerca;

    // Datos del primer engranaje, para acomodar el segundo junto a el.
    Vector3 ejeGiro, centroPrimero, centroRanura;
    float escalaPieza, diametroPieza;

    public override bool Disponible => !Completo;
    public override string TextoAccion => $"Poner <color=#FFDFA0>{piezaNecesaria.ToLower()}</color>";

    int Restan => Mathf.Max(0, cantidadNecesaria - Puestas);

    string TextoFalta
    {
        get
        {
            string pieza = piezaNecesaria.ToLower();
            if (Restan > 1) return $"Faltan {Restan} {pieza}s";
            return Puestas == 0 ? mensajeFalta : $"Falta 1 {pieza} más";
        }
    }

    public override bool Interactuar(Transform jugador)
    {
        if (Completo) return true;
        var barra = BarraHerramientas.Instancia;
        if (barra == null || barra.ObjetoSeleccionado != piezaNecesaria)
        {
            AvisarQueFalta();
            return true;
        }

        Colocar(Puestas);
        Puestas++;
        barra.Quitar(piezaNecesaria);

        if (Completo)
        {
            if (trampilla != null) trampilla.Abrir();
            AlCompletar?.Invoke();
        }
        else Notificacion.Mostrar(TextoFalta);
        return true;
    }

    /// <summary>Vuelve a poner las piezas que ya estaban (al regresar a la escena). Sin avisos.</summary>
    public void Restaurar(int cuantas)
    {
        bool estabaCompleto = Completo;
        while (Puestas < Mathf.Min(cuantas, cantidadNecesaria))
        {
            Colocar(Puestas);
            Puestas++;
        }
        if (Completo && !estabaCompleto)
        {
            if (trampilla != null) trampilla.Abrir();
            AlCompletar?.Invoke();
        }
    }

    void AvisarQueFalta()
    {
        var barra = BarraHerramientas.Instancia;
        bool laTraes = barra != null && barra.Tiene(piezaNecesaria);
        Notificacion.Mostrar(laTraes ? mensajeSeleccionar : TextoFalta);
    }

    void Update()
    {
        if (velocidadGiro != 0f)
            foreach (var p in piezas)
                if (p.t != null) p.t.RotateAround(p.centro, ejeGiro, velocidadGiro * p.sentido * Time.deltaTime);

        if (Completo) return;

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

    void Colocar(int indice)
    {
        if (indice == 0) ColocarPrimero();
        else ColocarJunto(indice);
    }

    /// <summary>Pone una copia del engranaje en la ranura, de frente y del tamano del hueco.</summary>
    void ColocarPrimero()
    {
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
        centroRanura = centro; // la ranura esta al ras de la cara del tablero

        var copia = CrearPieza(destino.parent);
        if (copia == null) return;

        var lim = ObjetoEnMano.Limites(copia);
        float mayor = Mathf.Max(lim.size.x, lim.size.y, lim.size.z);
        if (mayor > 0.0001f) copia.transform.localScale = Vector3.one * (alto * 0.85f / mayor);
        escalaPieza = copia.transform.localScale.x;
        diametroPieza = alto * 0.85f;

        lim = ObjetoEnMano.Limites(copia);
        // Centrado en la ranura y un poquito hacia afuera del tablero, de frente.
        centroPrimero = centro + ejeGiro * (separacion + Grosor(lim) * 0.25f);
        copia.transform.position += centroPrimero - lim.center;
        piezas.Add(new Pieza { t = copia.transform, centro = centroPrimero, sentido = 1f });
    }

    /// <summary>
    /// Pone el siguiente engranaje pegado al primero, sobre el tablero (hacia su centro),
    /// para que se vean engranados. Gira al reves que el de junto.
    /// </summary>
    void ColocarJunto(int indice)
    {
        if (piezas.Count == 0) { ColocarPrimero(); return; }

        // Direccion sobre la cara del tablero: del primer engranaje hacia el centro del tablero.
        var rendTablero = GetComponent<Renderer>();
        var haciaTablero = rendTablero != null ? rendTablero.bounds.center - centroPrimero : -Vector3.up;
        var dir = Vector3.ProjectOnPlane(haciaTablero, ejeGiro);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.ProjectOnPlane(-Vector3.up, ejeGiro);
        dir.Normalize();
        // El tercero (si algun dia se pide) iria al otro lado del primero.
        if (indice % 2 == 0) dir = -dir;
        int vuelta = (indice + 1) / 2;

        var copia = CrearPieza(ranura != null ? ranura.parent : transform.parent);
        if (copia == null) return;
        copia.transform.localScale = Vector3.one * escalaPieza;
        // Medio diente girado, para que los dientes se acomoden entre si.
        var lim = ObjetoEnMano.Limites(copia);
        copia.transform.RotateAround(lim.center, ejeGiro, 15f);

        lim = ObjetoEnMano.Limites(copia);
        var centro = centroPrimero + dir * diametroPieza * distanciaEntreEngranajes * vuelta;
        // Fuera de la ranura queda la cara del tablero (al ras de la ranura): que no se meta en la madera.
        float frente = Vector3.Dot(centroRanura - centroPrimero, ejeGiro);
        float fondo = Vector3.Dot(centro - centroPrimero, ejeGiro);
        float minimo = frente + separacion + Grosor(lim) * 0.5f;
        if (fondo < minimo) centro += ejeGiro * (minimo - fondo);

        copia.transform.position += centro - lim.center;
        piezas.Add(new Pieza { t = copia.transform, centro = centro, sentido = indice % 2 == 0 ? 1f : -1f });
    }

    /// <summary>Copia del engranaje con su grosor apuntando hacia afuera de la ranura.</summary>
    GameObject CrearPieza(Transform padre)
    {
        var copia = ObjetoEnMano.CrearCopia(piezaNecesaria);
        if (copia == null) return null;
        copia.name = $"{piezaNecesaria} puesto {piezas.Count + 1}";
        var t = copia.transform;
        t.SetParent(padre, true);
        t.localScale = Vector3.one;
        t.rotation = Quaternion.identity;
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
        t.rotation = Quaternion.FromToRotation(grosorLocal, ejeGiro);
        return copia;
    }

    float Grosor(Bounds lim) =>
        Mathf.Abs(Vector3.Dot(lim.size, new Vector3(Mathf.Abs(ejeGiro.x), Mathf.Abs(ejeGiro.y), Mathf.Abs(ejeGiro.z))));
}
