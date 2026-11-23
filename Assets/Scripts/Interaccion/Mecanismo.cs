using System;
using System.Collections.Generic;
using UnityEngine;

// Mecanismo del segundo piso del molino al que le faltan piezas (2 engranajes).
// - Al acercarte sin engranaje sale el aviso abajo a la izquierda: "Faltan 2 engranajes".
// - Con el engranaje seleccionado en la barra: (E) Poner engranaje.
// El primero va en la ranura y el segundo pegado a el, engranando; giran en sentidos contrarios.
// Cada uno se quita de la barra al ponerlo.
// - Cuando estan los dos se abre la trampilla de la planta baja (entrada al subterraneo)
// y se avisa con AlCompletar (para lo que se mueva despues, como las aspas).
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

    // Se llama una vez, cuando ya estan puestas todas las piezas.
    public event Action AlCompletar;

    public int Puestas { get; private set; }
    public bool Completo => Puestas >= cantidadNecesaria;

    private class Pieza { public Transform t; public Vector3 centro; public float sentido; }
    private readonly List<Pieza> piezas = new List<Pieza>();
    private bool jugadorCerca;

    // Datos del primer engranaje, para acomodar el segundo junto a el.
    private Vector3 ejeGiro, centroPrimero, centroRanura;
    private float escalaPieza, diametroPieza;

    public override bool Disponible => !Completo;
    public override string TextoAccion => $"Poner <color=#FFDFA0>{piezaNecesaria.ToLower()}</color>";

    private int Restan => Mathf.Max(0, cantidadNecesaria - Puestas);

    private string TextoFalta
    {
        get
        {
            string pieza = piezaNecesaria.ToLower();
            if (Restan > 1)
            {
                return $"Faltan {Restan} {pieza}s";
            }

            return Puestas == 0 ? mensajeFalta : $"Falta 1 {pieza} más";
        }
    }

    public override bool interactuar(Transform jugador)
    {
        if (Completo)
        {
            return true;
        }

        BarraHerramientas barra = BarraHerramientas.Instancia;
        if (barra == null || barra.ObjetoSeleccionado != piezaNecesaria)
        {
            avisarQueFalta();
            return true;
        }

        colocar(Puestas);
        Puestas++;
        barra.quitar(piezaNecesaria);

        if (Completo)
        {
            if (trampilla != null)
            {
                trampilla.abrir();
            }

            AlCompletar?.Invoke();
        }
        else
        {
            Notificacion.mostrar(TextoFalta);
        }

        return true;
    }

    // Vuelve a poner las piezas que ya estaban (al regresar a la escena). Sin avisos.
    public void restaurar(int cuantas)
    {
        bool estabaCompleto = Completo;
        while (Puestas < Mathf.Min(cuantas, cantidadNecesaria))
        {
            colocar(Puestas);
            Puestas++;
        }

        if (Completo && !estabaCompleto)
        {
            if (trampilla != null)
            {
                trampilla.abrir();
            }

            AlCompletar?.Invoke();
        }
    }

    private void avisarQueFalta()
    {
        BarraHerramientas barra = BarraHerramientas.Instancia;
        bool laTraes = barra != null && barra.tieneObjeto(piezaNecesaria);
        Notificacion.mostrar(laTraes ? mensajeSeleccionar : TextoFalta);
    }

    private void Update()
    {
        if (velocidadGiro != 0f)
        {
            foreach (Pieza p in piezas)
            {
                if (p.t != null)
                {
                    p.t.RotateAround(p.centro, ejeGiro, velocidadGiro * p.sentido * Time.deltaTime);
                }
            }
        }

        if (Completo)
        {
            return;
        }

        JugadorPrimeraPersona jugador = JugadorPrimeraPersona.Instancia;
        if (jugador == null)
        {
            return;
        }

        Vector3 centro = ranura != null ? ranura.position : transform.position;
        Vector3 diferencia = jugador.transform.position - centro;
        bool cerca = Vector3.ProjectOnPlane(diferencia, Vector3.up).magnitude < distanciaAviso
                     && Mathf.Abs(diferencia.y) < 2.5f;

        BarraHerramientas barra = BarraHerramientas.Instancia;
        bool enLaMano = barra != null && barra.ObjetoSeleccionado == piezaNecesaria;
        if (cerca && !jugadorCerca && !enLaMano)
        {
            avisarQueFalta();
        }

        jugadorCerca = cerca;
    }

    private void colocar(int indice)
    {
        if (indice == 0)
        {
            colocarPrimero();
        }
        else
        {
            colocarJunto(indice);
        }
    }

    // Pone una copia del engranaje en la ranura, de frente y del tamano del hueco.
    private void colocarPrimero()
    {
        Transform destino = ranura != null ? ranura : transform;

        // El "fondo" de la ranura: su lado mas delgado en horizontal. El engranaje se pone de frente a ese lado.
        MeshFilter mf = destino.GetComponent<MeshFilter>();
        Bounds b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.3f);
        ejeGiro = destino.forward;
        float delgado = float.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            Vector3 eje = Vector3.zero; eje[i] = b.size[i];
            Vector3 enMundo = destino.TransformVector(eje);
            if (Mathf.Abs(enMundo.normalized.y) > 0.5f)
            {
                continue; // ese es el alto
            }

            if (enMundo.magnitude < delgado)
            {
                delgado = enMundo.magnitude;
                ejeGiro = enMundo.normalized;
            }
        }

        // Si ya se sabe el frente real de la ranura (calculado con sus caras), se usa ese.
        if (frenteLocal.sqrMagnitude > 0.0001f)
        {
            ejeGiro = destino.TransformDirection(frenteLocal).normalized;
        }

        Vector3 centro = destino.TransformPoint(b.center);
        Renderer rend = destino.GetComponent<Renderer>();
        float alto = rend != null ? rend.bounds.size.y : 0.3f;
        if (rend != null)
        {
            centro = rend.bounds.center;
        }

        centroRanura = centro; // la ranura esta al ras de la cara del tablero

        GameObject copia = crearPieza(destino.parent);
        if (copia == null)
        {
            return;
        }

        Bounds lim = ObjetoEnMano.limites(copia);
        float mayor = Mathf.Max(lim.size.x, lim.size.y, lim.size.z);
        if (mayor > 0.0001f)
        {
            copia.transform.localScale = Vector3.one * (alto * 0.85f / mayor);
        }

        escalaPieza = copia.transform.localScale.x;
        diametroPieza = alto * 0.85f;

        lim = ObjetoEnMano.limites(copia);
        // Centrado en la ranura y un poquito hacia afuera del tablero, de frente.
        centroPrimero = centro + ejeGiro * (separacion + obtenerGrosor(lim) * 0.25f);
        copia.transform.position += centroPrimero - lim.center;
        piezas.Add(new Pieza { t = copia.transform, centro = centroPrimero, sentido = 1f });
    }

    // Pone el siguiente engranaje pegado al primero, sobre el tablero (hacia su centro),
    // para que se vean engranados. Gira al reves que el de junto.
    private void colocarJunto(int indice)
    {
        if (piezas.Count == 0)
        {
            colocarPrimero();
            return;
        }

        // Direccion sobre la cara del tablero: del primer engranaje hacia el centro del tablero.
        Renderer rendTablero = GetComponent<Renderer>();
        Vector3 haciaTablero = rendTablero != null ? rendTablero.bounds.center - centroPrimero : -Vector3.up;
        Vector3 dir = Vector3.ProjectOnPlane(haciaTablero, ejeGiro);
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector3.ProjectOnPlane(-Vector3.up, ejeGiro);
        }

        dir.Normalize();
        // El tercero (si algun dia se pide) iria al otro lado del primero.
        if (indice % 2 == 0)
        {
            dir = -dir;
        }

        int vuelta = (indice + 1) / 2;

        GameObject copia = crearPieza(ranura != null ? ranura.parent : transform.parent);
        if (copia == null)
        {
            return;
        }

        copia.transform.localScale = Vector3.one * escalaPieza;
        // Medio diente girado, para que los dientes se acomoden entre si.
        Bounds lim = ObjetoEnMano.limites(copia);
        copia.transform.RotateAround(lim.center, ejeGiro, 15f);

        lim = ObjetoEnMano.limites(copia);
        Vector3 centro = centroPrimero + dir * diametroPieza * distanciaEntreEngranajes * vuelta;
        // Fuera de la ranura queda la cara del tablero (al ras de la ranura): que no se meta en la madera.
        float frente = Vector3.Dot(centroRanura - centroPrimero, ejeGiro);
        float fondo = Vector3.Dot(centro - centroPrimero, ejeGiro);
        float minimo = frente + separacion + obtenerGrosor(lim) * 0.5f;
        if (fondo < minimo)
        {
            centro += ejeGiro * (minimo - fondo);
        }

        copia.transform.position += centro - lim.center;
        piezas.Add(new Pieza { t = copia.transform, centro = centro, sentido = indice % 2 == 0 ? 1f : -1f });
    }

    // Copia del engranaje con su grosor apuntando hacia afuera de la ranura.
    private GameObject crearPieza(Transform padre)
    {
        GameObject copia = ObjetoEnMano.crearCopia(piezaNecesaria);
        if (copia == null)
        {
            return null;
        }

        copia.name = $"{piezaNecesaria} puesto {piezas.Count + 1}";
        Transform t = copia.transform;
        t.SetParent(padre, true);
        t.localScale = Vector3.one;
        t.rotation = Quaternion.identity;
        copia.SetActive(true);

        // El lado mas delgado del engranaje (su grosor) apunta hacia afuera de la ranura.
        MeshFilter mfPieza = copia.GetComponentInChildren<MeshFilter>();
        Vector3 grosorLocal = Vector3.up;
        if (mfPieza != null && mfPieza.sharedMesh != null)
        {
            Vector3 bp = mfPieza.sharedMesh.bounds.size;
            int i = bp.x <= bp.y && bp.x <= bp.z ? 0 : bp.y <= bp.z ? 1 : 2;
            Vector3 eje = Vector3.zero; eje[i] = 1f;
            grosorLocal = mfPieza.transform.TransformDirection(eje);
        }

        t.rotation = Quaternion.FromToRotation(grosorLocal, ejeGiro);
        return copia;
    }

    private float obtenerGrosor(Bounds lim)
    {
        return Mathf.Abs(Vector3.Dot(lim.size, new Vector3(Mathf.Abs(ejeGiro.x), Mathf.Abs(ejeGiro.y), Mathf.Abs(ejeGiro.z))));
    }
}
