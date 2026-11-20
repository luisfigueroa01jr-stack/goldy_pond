using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lo que se tiene que acordar el juego al cambiar de escena (Aldea <-> Subterraneo), porque Unity
/// vuelve a cargar cada escena desde cero:
///  - lo que traes en la barra (y que casilla tenias seleccionada)
///  - que objetos ya recogiste (para que no vuelvan a aparecer)
///  - cuantos engranajes pusiste en cada mecanismo (y si la trampilla quedo abierta)
///  - que puertas con llave ya abriste
///  - en que punto tienes que aparecer al llegar (PuntoLlegada)
/// Solo dura mientras el juego esta abierto (no es un "guardar partida").
/// </summary>
public static class EstadoJuego
{
    struct ObjetoBarra { public string objeto; public int cantidad; public Sprite icono; }

    static readonly List<ObjetoBarra> barra = new List<ObjetoBarra>();
    static int seleccionada;
    static bool hayBarraGuardada;
    static readonly HashSet<string> recogidos = new HashSet<string>();
    static readonly Dictionary<string, int> mecanismos = new Dictionary<string, int>();
    static readonly HashSet<string> puertasAbiertas = new HashSet<string>();
    static readonly HashSet<string> palancas = new HashSet<string>();

    /// <summary>true despues de haber bajado al subterraneo (las palancas de la Aldea solo funcionan asi).</summary>
    public static bool VisitoSubterraneo { get; private set; }

    /// <summary>Cuantas palancas de la Aldea estan activadas (se cuenta al salir de la Aldea).</summary>
    public static int PalancasActivadas => palancas.Count;

    /// <summary>Id del PuntoLlegada donde debe aparecer el jugador en la siguiente escena. Vacio = donde esta en la escena.</summary>
    public static string Llegada { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        // Al darle Play de nuevo en el editor se empieza limpio.
        barra.Clear(); recogidos.Clear(); mecanismos.Clear(); puertasAbiertas.Clear(); palancas.Clear();
        seleccionada = 0; hayBarraGuardada = false; Llegada = null; VisitoSubterraneo = false;
    }

    /// <summary>Nombre unico de un objeto: escena + ruta en la jerarquia.</summary>
    public static string Clave(GameObject go)
    {
        var sb = new StringBuilder(go.name);
        for (var t = go.transform.parent; t != null; t = t.parent) sb.Insert(0, t.name + "/");
        return go.scene.name + ":" + sb;
    }

    public static void MarcarRecogido(GameObject go) => recogidos.Add(Clave(go));

    /// <summary>Guarda todo antes de salir de la escena actual.</summary>
    public static void Guardar()
    {
        var b = BarraHerramientas.Instancia;
        if (b != null)
        {
            barra.Clear();
            foreach (var c in b.casillas)
                barra.Add(c == null || c.Libre
                    ? new ObjetoBarra()
                    : new ObjetoBarra { objeto = c.objeto, cantidad = c.cantidad, icono = c.icono != null ? c.icono.sprite : null });
            seleccionada = b.seleccionada;
            hayBarraGuardada = true;
        }

        foreach (var m in Object.FindObjectsByType<Mecanismo>(FindObjectsInactive.Include))
            mecanismos[Clave(m.gameObject)] = m.Puestas;
        foreach (var p in Object.FindObjectsByType<Puerta>(FindObjectsInactive.Include))
            if (p.ConCerradura && !p.ConSeguro) puertasAbiertas.Add(Clave(p.gameObject));
        foreach (var p in Object.FindObjectsByType<Palanca>(FindObjectsInactive.Include))
            if (p.Activada) palancas.Add(Clave(p.gameObject));

        // Los objetos que traes se ven en la mano y se ponen en el mecanismo copiando el modelo de la escena:
        // hay que conservar ese modelo aunque la escena se borre.
        ObjetoEnMano.ConservarModelos();
    }

    /// <summary>Lo llama RecogiblesAutomaticos al cargar una escena, despues de preparar los recogibles.</summary>
    public static void Restaurar(Scene escena)
    {
        if (escena.name == "Subterraneo") VisitoSubterraneo = true;

        foreach (var p in Object.FindObjectsByType<Palanca>(FindObjectsInactive.Include))
            if (p.gameObject.scene == escena && palancas.Contains(Clave(p.gameObject)))
                p.Activar(false);

        // Objetos ya recogidos: se esconden, pero se guardan como modelo para la mano.
        foreach (var r in Object.FindObjectsByType<ObjetoRecogible>(FindObjectsInactive.Include))
        {
            if (r.gameObject.scene != escena || !recogidos.Contains(Clave(r.gameObject))) continue;
            ObjetoEnMano.Registrar(r.nombre, r.gameObject);
            r.gameObject.SetActive(false);
        }

        foreach (var m in Object.FindObjectsByType<Mecanismo>(FindObjectsInactive.Include))
            if (m.gameObject.scene == escena && mecanismos.TryGetValue(Clave(m.gameObject), out int n))
                m.Restaurar(n);

        foreach (var p in Object.FindObjectsByType<Puerta>(FindObjectsInactive.Include))
            if (p.gameObject.scene == escena && puertasAbiertas.Contains(Clave(p.gameObject)))
                p.QuitarSeguro();

        var b = BarraHerramientas.Instancia;
        if (hayBarraGuardada && b != null && b.gameObject.scene == escena)
        {
            // Cada cosa en la misma casilla donde estaba.
            for (int i = 0; i < barra.Count && i < b.casillas.Length; i++)
                b.Poner(i, barra[i].objeto, barra[i].icono, barra[i].cantidad);
            b.Seleccionar(Mathf.Clamp(seleccionada, 0, b.casillas.Length - 1));
        }
    }
}
