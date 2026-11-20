using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoldyPond.EditorTools
{
    /// <summary>
    /// Portal nocturno -> Laberinto:
    ///  - En el prefab Portal_Nocturno, la "energia" lleva un area invisible (trigger) y un CambioEscena:
    ///    al acercarte sale "(E) Entrar al portal" y te manda a la escena Laberinto.
    ///  - En la escena Laberinto pone "Llegada desde el portal" (PuntoLlegada) en el pasillo de la entrada.
    ///  - Agrega la escena Laberinto a la lista de escenas del juego (Build Profiles).
    /// Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Laberinto.
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigurarPortal
    {
        const string PrefabPortal = "Assets/Prefabs/Objetos/Portal_Nocturno.prefab";
        const string EscenaLaberinto = "Assets/Scenes/Laberinto.unity";
        const string IdLlegada = "Portal";
        const string NombreLlegada = "Llegada desde el portal";
        static string Clave => $"GoldyPond.Portal.v1.{Application.dataPath}";

        static ConfigurarPortal()
        {
            EditorApplication.delayCall += AplicarPendiente;
        }

        static void AplicarPendiente()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AplicarPendiente;
                return;
            }
            if (EditorPrefs.GetBool(Clave, false)) return;
            EditorPrefs.SetBool(Clave, true);
            Aplicar();
        }

        [MenuItem("Goldy Pond/Laberinto/Portal nocturno (entrar al laberinto)")]
        public static void Aplicar()
        {
            AgregarAlBuild();
            PrepararPortal();
            PrepararLlegada();
        }

        static void AgregarAlBuild()
        {
            var escenas = EditorBuildSettings.scenes.ToList();
            if (escenas.Any(e => e.path == EscenaLaberinto) || !System.IO.File.Exists(EscenaLaberinto)) return;
            // Despues de Aldea y Subterraneo, antes de SampleScene.
            int i = escenas.FindIndex(e => e.path.EndsWith("SampleScene.unity"));
            escenas.Insert(i < 0 ? escenas.Count : i, new EditorBuildSettingsScene(EscenaLaberinto, true));
            EditorBuildSettings.scenes = escenas.ToArray();
            Debug.Log("[Goldy Pond] Escena Laberinto agregada a la lista de escenas del juego.");
        }

        static void PrepararPortal()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPortal) == null) { Debug.LogWarning($"[Goldy Pond] No existe {PrefabPortal}"); return; }
            var raiz = PrefabUtility.LoadPrefabContents(PrefabPortal);
            try
            {
                var energia = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "energia");
                var mf = energia != null ? energia.GetComponent<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null) { Debug.LogWarning("[Goldy Pond] El portal no tiene la pieza \"energia\"."); return; }

                // Area invisible del tamaño de la energia, gruesa hacia adelante y atras para que salga el aviso al subir los escalones.
                var b = mf.sharedMesh.bounds;
                var caja = Obtener<BoxCollider>(energia.gameObject);
                caja.isTrigger = true;
                caja.center = b.center;
                var s = b.size;
                int delgado = s.x <= s.y && s.x <= s.z ? 0 : s.y <= s.z ? 1 : 2;
                s[delgado] = 1.6f;
                caja.size = s;

                var cambio = Obtener<CambioEscena>(energia.gameObject);
                cambio.texto = "Entrar al portal";
                cambio.escenaDestino = "Laberinto";
                cambio.llegada = IdLlegada;
                cambio.trampilla = null;
                cambio.escalera = null;

                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabPortal);
                Debug.Log("[Goldy Pond] Portal nocturno listo: (E) Entrar al portal -> Laberinto.");
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        static T Obtener<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static void PrepararLlegada()
        {
            if (!System.IO.File.Exists(EscenaLaberinto)) { Debug.LogWarning($"[Goldy Pond] No existe {EscenaLaberinto}"); return; }
            var abierta = SceneManager.GetSceneByPath(EscenaLaberinto);
            bool laAbriYo = !abierta.isLoaded;
            if (laAbriYo && Enumerable.Range(0, SceneManager.sceneCount).Any(i => string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)))
            {
                Debug.LogWarning("[Goldy Pond] Hay una escena sin guardar abierta; guarda y usa Goldy Pond > Laberinto > Portal nocturno para poner la llegada.");
                return;
            }
            var escena = laAbriYo ? EditorSceneManager.OpenScene(EscenaLaberinto, OpenSceneMode.Additive) : abierta;
            try
            {
                var punto = escena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PuntoLlegada>(true)).FirstOrDefault(p => p.id == IdLlegada);
                if (punto == null)
                {
                    var go = new GameObject(NombreLlegada);
                    SceneManager.MoveGameObjectToScene(go, escena);
                    punto = go.AddComponent<PuntoLlegada>();
                    punto.id = IdLlegada;
                }
                // Pasillo de la entrada (zona 1), mirando hacia adentro del laberinto.
                punto.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -11.4f), Quaternion.identity);
                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
                Debug.Log("[Goldy Pond] Llegada desde el portal puesta en la escena Laberinto.");
            }
            finally
            {
                if (laAbriYo) EditorSceneManager.CloseScene(escena, true);
            }
        }
    }
}
