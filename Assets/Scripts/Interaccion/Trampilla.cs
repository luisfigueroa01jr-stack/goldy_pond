using UnityEngine;

// La tapa de la trampilla del molino (entrada al subterraneo).
// En la escena esta cerrada (acostada sobre el marco). Cuando se pone el engranaje en el mecanismo,
// el Mecanismo llama a Abrir() y la tapa queda levantada, como estaba en el modelo de Blender.
// Sin animacion: al bajar ya se ve abierta.
public class Trampilla : MonoBehaviour
{
    [Tooltip("Posicion y giro de la tapa abierta (los del modelo de Blender). Los guarda Goldy Pond > Interaccion.")]
    public Vector3 posicionAbierta;
    public Quaternion rotacionAbierta = Quaternion.identity;

    public bool Abierta { get; private set; }

    public void abrir()
    {
        if (Abierta)
        {
            return;
        }

        transform.localPosition = posicionAbierta;
        transform.localRotation = rotacionAbierta;
        Abierta = true;
    }
}
