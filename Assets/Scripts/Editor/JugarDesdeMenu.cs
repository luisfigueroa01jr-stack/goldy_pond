using UnityEditor;
using UnityEditor.SceneManagement;

namespace GoldyPond.EditorTools
{
    // Menu "Goldy Pond/Jugar desde el menu principal": abre MenuPrincipal y le da Play.
    public static class JugarDesdeMenu
    {
        [MenuItem("Goldy Pond/Jugar desde el menu principal %#p")]
        public static void jugar()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene("Assets/Scenes/MenuPrincipal.unity");
            EditorApplication.isPlaying = true;
        }

        // Para abrir Unity desde la terminal directo jugando: -executeMethod GoldyPond.EditorTools.JugarDesdeMenu.alAbrir
        public static void alAbrir()
        {
            EditorApplication.delayCall += jugar;
        }
    }
}
