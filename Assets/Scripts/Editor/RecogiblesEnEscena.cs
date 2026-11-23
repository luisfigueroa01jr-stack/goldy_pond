
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoldyPond.EditorTools
{
    [InitializeOnLoad]
    public static class RecogiblesEnEscena
    {
        private static readonly string[] Objetos = { "Llave", "Engranaje", "Linterna", "Pergamino" };
        private const string CARPETA_ICONOS = "Assets/Resources/Iconos";
        private static bool pendiente;

        static RecogiblesEnEscena()
        {
            EditorApplication.hierarchyChanged += programar;
            EditorSceneManager.sceneOpened += (_, __) => programar();
            programar();
        }

        private static void programar()
        {
            if (pendiente)
            {
                return;
            }

            pendiente = true;
            EditorApplication.delayCall += () => { pendiente = false; revisar(false); };
        }

        [MenuItem("Goldy Pond/Interaccion/Hacer recogibles los objetos de la escena")]
        private static void menu()
        {
            revisar(true);
        }

        private static void revisar(bool avisar)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            int arreglados = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene escena = SceneManager.GetSceneAt(i);
                if (!escena.isLoaded)
                {
                    continue;
                }

                foreach (GameObject raiz in escena.GetRootGameObjects())
                {
                    foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                    {
                        if (arreglar(t.gameObject))
                        {
                            arreglados++;
                        }
                    }
                }
            }

            // La barra ya no lleva los laureles de los lados: los borra de las escenas abiertas.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene escena = SceneManager.GetSceneAt(i);
                if (!escena.isLoaded)
                {
                    continue;
                }

                foreach (GameObject raiz in escena.GetRootGameObjects())
                {
                    foreach (BarraHerramientas barra in raiz.GetComponentsInChildren<BarraHerramientas>(true))
                    {
                        if (barra.mostrarLaureles)
                        {
                            continue;
                        }

                        bool tiene = false;
                        foreach (Transform h in barra.transform)
                        {
                            if (h.name.StartsWith("Laurel"))
                            {
                                tiene = true;
                            }
                        }

                        if (!tiene)
                        {
                            continue;
                        }

                        barra.quitarLaureles();
                        EditorSceneManager.MarkSceneDirty(escena);
                        Debug.Log($"[Goldy Pond] Quite los laureles de la barra en '{escena.name}'. Guarda la escena (Ctrl+S).");
                    }
                }
            }

            if (arreglados > 0 || avisar)
            {
                Debug.Log($"[Goldy Pond] {arreglados} objetos de la escena listos para recoger con E. Guarda la escena (Ctrl+S).");
            }
        }

        // Si es la raiz de una copia de uno de los objetos y le falta algo, lo agrega.
        private static bool arreglar(GameObject go)
        {
            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go))
            {
                return false;
            }

            string nombre = nombreObjeto(go);
            if (nombre == null)
            {
                return false;
            }

            bool cambio = false;

            if (go.GetComponentInChildren<Collider>(true) == null)
            {
                BoxCollider caja = Undo.AddComponent<BoxCollider>(go);
                RecogiblesAutomaticos.ajustarCaja(caja);
                cambio = true;
            }

            ObjetoRecogible r = go.GetComponent<ObjetoRecogible>();
            if (r == null)
            {
                r = Undo.AddComponent<ObjetoRecogible>(go);
                r.nombre = nombre;
                cambio = true;
            }

            if (r.icono == null)
            {
                r.icono = AssetDatabase.LoadAssetAtPath<Sprite>($"{CARPETA_ICONOS}/{nombre}.png");
                if (r.icono != null)
                {
                    cambio = true;
                }
            }

            if (cambio)
            {
                EditorUtility.SetDirty(r);
                EditorSceneManager.MarkSceneDirty(go.scene);
            }

            return cambio;
        }

        private static string nombreObjeto(GameObject go)
        {
            string ruta = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (string.IsNullOrEmpty(ruta))
            {
                return null;
            }

            if (!ruta.StartsWith("Assets/Models/Objetos/") && !ruta.StartsWith("Assets/Prefabs/Objetos/"))
            {
                return null;
            }

            string archivo = System.IO.Path.GetFileNameWithoutExtension(ruta);
            foreach (string n in Objetos)
            {
                if (archivo == n)
                {
                    return n;
                }
            }

            return null;
        }

    }
}
