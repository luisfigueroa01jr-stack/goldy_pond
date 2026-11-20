using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cada lugar (Aldea / Laberinto) tiene su propia barra:
/// al cruzar el portal lo que traes se queda guardado en el lugar de donde saliste
/// y recuperas lo que habias dejado en el otro. Solo el Engranaje cruza contigo
/// (lo sacas del laberinto para ponerlo en el molino).
/// </summary>
public static class Inventarios
{
    /// <summary>Objetos que si se llevan de un lugar a otro.</summary>
    static readonly string[] QueCruzan = { "Engranaje" };

    class Casilla { public string objeto; public Sprite icono; public int cantidad; }
    static readonly Dictionary<string, Casilla[]> guardadas = new Dictionary<string, Casilla[]>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => guardadas.Clear();

    static bool Cruza(string objeto)
    {
        if (string.IsNullOrEmpty(objeto)) return false;
        foreach (var q in QueCruzan) if (objeto.StartsWith(q)) return true;
        return false;
    }

    /// <summary>Guarda la barra en "desde" (menos lo que cruza) y pone la de "hacia" con lo que cruza.</summary>
    public static void Cambiar(string desde, string hacia)
    {
        var barra = BarraHerramientas.Instancia;
        if (barra == null || desde == hacia) return;

        int n = barra.casillas.Length;
        var aqui = new Casilla[n];
        var cruzan = new List<Casilla>();
        for (int i = 0; i < n; i++)
        {
            var c = barra.casillas[i];
            if (c.Libre) continue;
            var copia = new Casilla { objeto = c.objeto, icono = c.icono != null ? c.icono.sprite : null, cantidad = c.cantidad };
            if (Cruza(c.objeto)) cruzan.Add(copia); else aqui[i] = copia;
        }
        guardadas[desde] = aqui;

        guardadas.TryGetValue(hacia, out var alla);
        for (int i = 0; i < n; i++)
        {
            var c = alla != null && i < alla.Length ? alla[i] : null;
            barra.Poner(i, c?.objeto, c?.icono, c?.cantidad ?? 0);
        }
        foreach (var c in cruzan)
            if (!barra.Agregar(c.objeto, c.icono, c.cantidad))
                Notificacion.Mostrar("La barra esta llena");
    }
}
