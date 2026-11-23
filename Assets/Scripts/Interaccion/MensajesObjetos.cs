using System;
using System.Collections.Generic;
using UnityEngine;

// Objetos de la barra que traen un mensaje (ej. el Pergamino de William Minerva).
// Con el objeto seleccionado en la mano sale "(E) Ver mensaje"; mientras mantienes E se ve la imagen.
// Va en el modelo de cada escena (Aldea y Subterraneo) para que funcione en las dos.
// Lo configura Goldy Pond > Interaccion > Mapa y pergamino.
public class MensajesObjetos : MonoBehaviour
{
    [Serializable]
    public class Mensaje
    {
        [Tooltip("Nombre del objeto en la barra (ej. Pergamino).")]
        public string objeto;
        [Tooltip("Imagen que se ve al mantener E.")]
        public Sprite imagen;
    }

    public Mensaje[] mensajes = new Mensaje[0];

    private static readonly Dictionary<string, Sprite> imagenes = new Dictionary<string, Sprite>();

    private void Awake()
    {
        foreach (Mensaje m in mensajes)
        {
            if (m != null && !string.IsNullOrEmpty(m.objeto) && m.imagen != null)
            {
                imagenes[m.objeto] = m.imagen;
            }
        }
    }

    // La imagen del mensaje de ese objeto, o null si no trae mensaje.
    public static Sprite obtenerImagen(string objeto)
    {
        return !string.IsNullOrEmpty(objeto) && imagenes.TryGetValue(objeto, out Sprite s) && s != null ? s : null;
    }
}
