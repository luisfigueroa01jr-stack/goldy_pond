using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoldyPond.EditorTools
{
    // Portal nocturno -> Laberinto:
    // - En el prefab Portal_Nocturno, la "energia" lleva un area invisible (trigger) y un CambioEscena:
    // al acercarte sale "(E) Entrar al portal" y te manda a la escena Laberinto.
    // - En la escena Laberinto pone "Llegada desde el portal" (PuntoLlegada) en el pasillo de la entrada.
    // - Agrega la escena Laberinto a la lista de escenas del juego (Build Profiles).
    // Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Laberinto.
    [InitializeOnLoad]
    public static class ConfigurarPortal
    {
        private const string PREFAB_PORTAL = "Assets/Prefabs/Objetos/Portal_Nocturno.prefab";
        private const string ESCENA_LABERINTO = "Assets/Scenes/Laberinto.unity";
        private const string ID_LLEGADA = "Portal";
        private const string NOMBRE_LLEGADA = "Llegada desde el portal";
        private static string Clave => $"GoldyPond.Portal.v1.{Application.dataPath}";

        static ConfigurarPortal()
        {
            EditorApplication.delayCall += aplicarPendiente;
        }

        private static void aplicarPendiente()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += aplicarPendiente;
                return;
            }

            if (EditorPrefs.GetBool(Clave, false))
            {
                return;
            }

            EditorPrefs.SetBool(Clave, true);
            aplicar();
        }

        [MenuItem("Goldy Pond/Laberinto/Portal nocturno (entrar al laberinto)")]
        public static void aplicar()
        {
            agregarAlBuild();
            prepararPortal();
            prepararLlegada();
        }

        private static void agregarAlBuild()
        {
            System.Collections.Generic.List<EditorBuildSettingsScene> escenas = EditorBuildSettings.scenes.ToList();
            if (escenas.Any(e => e.path == ESCENA_LABERINTO) || !System.IO.File.Exists(ESCENA_LABERINTO))
            {
                return;
            }

            // Despues de Aldea y Subterraneo, antes de SampleScene.
            int i = escenas.FindIndex(e => e.path.EndsWith("SampleScene.unity"));
            escenas.Insert(i < 0 ? escenas.Count : i, new EditorBuildSettingsScene(ESCENA_LABERINTO, true));
            EditorBuildSettings.scenes = escenas.ToArray();
            Debug.Log("[Goldy Pond] Escena Laberinto agregada a la lista de escenas del juego.");
        }

        private static void prepararPortal()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PORTAL) == null)
            {
                Debug.LogWarning($"[Goldy Pond] No existe {PREFAB_PORTAL}");
                return;
            }

            GameObject raiz = PrefabUtility.LoadPrefabContents(PREFAB_PORTAL);
            try
            {
                Transform energia = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "energia");
                MeshFilter mf = energia != null ? energia.GetComponent<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null)
                {
                    Debug.LogWarning("[Goldy Pond] El portal no tiene la pieza \"energia\".");
                    return;
                }

                // Area invisible del tamaño de la energia, gruesa hacia adelante y atras para que salga el aviso al subir los escalones.
                Bounds b = mf.sharedMesh.bounds;
                BoxCollider caja = obtener<BoxCollider>(energia.gameObject);
                caja.isTrigger = true;
                caja.center = b.center;
                Vector3 s = b.size;
                int delgado = s.x <= s.y && s.x <= s.z ? 0 : s.y <= s.z ? 1 : 2;
                s[delgado] = 1.6f;
                caja.size = s;

                CambioEscena cambio = obtener<CambioEscena>(energia.gameObject);
                cambio.texto = "Entrar al portal";
                cambio.escenaDestino = "Laberinto";
                cambio.llegada = ID_LLEGADA;
                cambio.trampilla = null;
                cambio.escalera = null;

                PrefabUtility.SaveAsPrefabAsset(raiz, PREFAB_PORTAL);
                Debug.Log("[Goldy Pond] Portal nocturno listo: (E) Entrar al portal -> Laberinto.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }

        private static T obtener<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void prepararLlegada()
        {
            if (!System.IO.File.Exists(ESCENA_LABERINTO))
            {
                Debug.LogWarning($"[Goldy Pond] No existe {ESCENA_LABERINTO}");
                return;
            }

            Scene abierta = SceneManager.GetSceneByPath(ESCENA_LABERINTO);
            bool laAbriYo = !abierta.isLoaded;
            if (laAbriYo && Enumerable.Range(0, SceneManager.sceneCount).Any(i => string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)))
            {
                Debug.LogWarning("[Goldy Pond] Hay una escena sin guardar abierta; guarda y usa Goldy Pond > Laberinto > Portal nocturno para poner la llegada.");
                return;
            }

            Scene escena = laAbriYo ? EditorSceneManager.OpenScene(ESCENA_LABERINTO, OpenSceneMode.Additive) : abierta;
            try
            {
                PuntoLlegada punto = escena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PuntoLlegada>(true)).FirstOrDefault(p => p.id == ID_LLEGADA);
                if (punto == null)
                {
                    GameObject go = new GameObject(NOMBRE_LLEGADA);
                    SceneManager.MoveGameObjectToScene(go, escena);
                    punto = go.AddComponent<PuntoLlegada>();
                    punto.id = ID_LLEGADA;
                }

                // Pasillo de la entrada (zona 1), mirando hacia adentro del laberinto.
                punto.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -11.4f), Quaternion.identity);
                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
                Debug.Log("[Goldy Pond] Llegada desde el portal puesta en la escena Laberinto.");
            }
            finally
            {
                if (laAbriYo)
                {
                    EditorSceneManager.CloseScene(escena, true);
                }
            }
        }
    }
}
