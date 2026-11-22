
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoldyPond.EditorTools
{
    [InitializeOnLoad]
    public static class RecogiblesEnEscena
    {
        static readonly string[] Objetos = { "Llave", "Engranaje", "Linterna", "Pergamino" };
        const string CarpetaIconos = "Assets/Resources/Iconos";
        static bool pendiente;

        static RecogiblesEnEscena()
        {
            EditorApplication.hierarchyChanged += Programar;
            EditorSceneManager.sceneOpened += (_, __) => Programar();
            Programar();
        }

        static void Programar()
        {
            if (pendiente) return;
            pendiente = true;
            EditorApplication.delayCall += () => { pendiente = false; Revisar(false); };
        }

        [MenuItem("Goldy Pond/Interaccion/Hacer recogibles los objetos de la escena")]
        static void Menu() => Revisar(true);

        static void Revisar(bool avisar)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            int arreglados = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var escena = SceneManager.GetSceneAt(i);
                if (!escena.isLoaded) continue;
                foreach (var raiz in escena.GetRootGameObjects())
                    foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                        if (Arreglar(t.gameObject)) arreglados++;
            }

            // La barra ya no lleva los laureles de los lados: los borra de las escenas abiertas.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var escena = SceneManager.GetSceneAt(i);
                if (!escena.isLoaded) continue;
                foreach (var raiz in escena.GetRootGameObjects())
                    foreach (var barra in raiz.GetComponentsInChildren<BarraHerramientas>(true))
                    {
                        if (barra.mostrarLaureles) continue;
                        bool tiene = false;
                        foreach (Transform h in barra.transform) if (h.name.StartsWith("Laurel")) tiene = true;
                        if (!tiene) continue;
                        barra.QuitarLaureles();
                        EditorSceneManager.MarkSceneDirty(escena);
                        Debug.Log($"[Goldy Pond] Quite los laureles de la barra en '{escena.name}'. Guarda la escena (Ctrl+S).");
                    }
            }

            if (arreglados > 0 || avisar)
                Debug.Log($"[Goldy Pond] {arreglados} objetos de la escena listos para recoger con E. Guarda la escena (Ctrl+S).");
        }

        /// <summary>Si es la raiz de una copia de uno de los objetos y le falta algo, lo agrega.</summary>
        static bool Arreglar(GameObject go)
        {
            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) return false;
            var nombre = NombreObjeto(go);
            if (nombre == null) return false;

            bool cambio = false;

            if (go.GetComponentInChildren<Collider>(true) == null)
            {
                var caja = Undo.AddComponent<BoxCollider>(go);
                RecogiblesAutomaticos.AjustarCaja(caja);
                cambio = true;
            }

            var r = go.GetComponent<ObjetoRecogible>();
            if (r == null)
            {
                r = Undo.AddComponent<ObjetoRecogible>(go);
                r.nombre = nombre;
                cambio = true;
            }
            if (r.icono == null)
            {
                r.icono = AssetDatabase.LoadAssetAtPath<Sprite>($"{CarpetaIconos}/{nombre}.png");
                if (r.icono != null) cambio = true;
            }

            if (cambio)
            {
                EditorUtility.SetDirty(r);
                EditorSceneManager.MarkSceneDirty(go.scene);
            }
            return cambio;
        }

        static string NombreObjeto(GameObject go)
        {
            var ruta = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (string.IsNullOrEmpty(ruta)) return null;
            if (!ruta.StartsWith("Assets/Models/Objetos/") && !ruta.StartsWith("Assets/Prefabs/Objetos/")) return null;
            var archivo = System.IO.Path.GetFileNameWithoutExtension(ruta);
            foreach (var n in Objetos)
                if (archivo == n) return n;
            return null;
        }

    }
}
