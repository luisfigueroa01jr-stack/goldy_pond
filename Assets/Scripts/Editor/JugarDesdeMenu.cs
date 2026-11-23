using UnityEditor;
using UnityEditor.SceneManagement;

namespace GoldyPond.EditorTools
{
    /// <summary>Menu "Goldy Pond/Jugar desde el menu principal": abre MenuPrincipal y le da Play.</summary>
    public static class JugarDesdeMenu
    {
        [MenuItem("Goldy Pond/Jugar desde el menu principal %#p")]
        public static void Jugar()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/MenuPrincipal.unity");
            EditorApplication.isPlaying = true;
        }

        /// <summary>Para abrir Unity desde la terminal directo jugando: -executeMethod GoldyPond.EditorTools.JugarDesdeMenu.AlAbrir</summary>
        public static void AlAbrir() => EditorApplication.delayCall += Jugar;
    }
}
