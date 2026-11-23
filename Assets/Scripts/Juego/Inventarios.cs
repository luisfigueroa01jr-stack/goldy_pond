using System.Collections.Generic;
using UnityEngine;

// Cada lugar (Aldea / Laberinto) tiene su propia barra:
// al cruzar el portal lo que traes se queda guardado en el lugar de donde saliste
// y recuperas lo que habias dejado en el otro. Solo el Engranaje cruza contigo
// (lo sacas del laberinto para ponerlo en el molino).
public static class Inventarios
{
    // Objetos que si se llevan de un lugar a otro.
    private static readonly string[] QueCruzan = { "Engranaje" };

    private class Casilla { public string objeto; public Sprite icono; public int cantidad; }
    private static readonly Dictionary<string, Casilla[]> guardadas = new Dictionary<string, Casilla[]>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void reiniciar()
    {
        guardadas.Clear();
    }

    private static bool cruzaEscenas(string objeto)
    {
        if (string.IsNullOrEmpty(objeto))
        {
            return false;
        }

        foreach (string q in QueCruzan)
        {
            if (objeto.StartsWith(q))
            {
                return true;
            }
        }

        return false;
    }

    // Guarda la barra en "desde" (menos lo que cruza) y pone la de "hacia" con lo que cruza.
    public static void cambiar(string desde, string hacia)
    {
        BarraHerramientas barra = BarraHerramientas.Instancia;
        if (barra == null || desde == hacia)
        {
            return;
        }

        int n = barra.casillas.Length;
        Casilla[] aqui = new Casilla[n];
        List<Casilla> cruzan = new List<Casilla>();
        for (int i = 0; i < n; i++)
        {
            BarraHerramientas.Casilla c = barra.casillas[i];
            if (c.Libre)
            {
                continue;
            }

            Casilla copia = new Casilla { objeto = c.objeto, icono = c.icono != null ? c.icono.sprite : null, cantidad = c.cantidad };
            if (cruzaEscenas(c.objeto))
            {
                cruzan.Add(copia);
            }else
            {
                aqui[i] = copia;
            }
        }

        guardadas[desde] = aqui;

        guardadas.TryGetValue(hacia, out Casilla[] alla);
        for (int i = 0; i < n; i++)
        {
            Casilla c = alla != null && i < alla.Length ? alla[i] : null;
            barra.poner(i, c?.objeto, c?.icono, c?.cantidad ?? 0);
        }

        foreach (Casilla c in cruzan)
        {
            if (!barra.agregar(c.objeto, c.icono, c.cantidad))
            {
                Notificacion.mostrar("La barra esta llena");
            }
        }
    }
}
