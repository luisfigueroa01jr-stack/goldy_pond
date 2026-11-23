using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    // Puertas y cajones del laberinto (prefab laberinto_colores):
    // - "puerta cuarto 2", "puerta cuarto 3", "puerta pasillo": se abren con E.
    // - "puerta zona 5" (el ultimo cuarto): cerrada, se abre con la llave del laberinto en la mano.
    // - Todos los cajones de los escritorios y las estanterias ("... cajon ..."): se abren con E.
    // Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Laberinto.
    [InitializeOnLoad]
    public static class ConfigurarPuertasLaberinto
    {
        private const string PREFAB_LABERINTO = "Assets/Prefabs/laberinto/laberinto_colores.prefab";
        public const string LLAVE_LABERINTO = "Llave del laberinto";
        private static readonly string[] PuertasLibres = { "puerta cuarto 2", "puerta cuarto 3", "puerta pasillo" };
        private const string PUERTA_FINAL = "puerta zona 5";
        private static string Clave => $"GoldyPond.PuertasLaberinto.v1.{Application.dataPath}";

        static ConfigurarPuertasLaberinto()
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

        [MenuItem("Goldy Pond/Laberinto/Puertas y cajones")]
        public static void aplicar()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_LABERINTO) == null)
            {
                Debug.LogWarning($"[Goldy Pond] No existe {PREFAB_LABERINTO}");
                return;
            }

            GameObject raiz = PrefabUtility.LoadPrefabContents(PREFAB_LABERINTO);
            try
            {
                Transform[] todos = raiz.GetComponentsInChildren<Transform>(true);
                int puertas = 0, cajones = 0;

                foreach (Transform t in todos)
                {
                    bool libre = PuertasLibres.Contains(t.name), final = t.name == PUERTA_FINAL;
                    if (!libre && !final)
                    {
                        continue;
                    }

                    Puerta p = obtener<Puerta>(t.gameObject);
                    p.nombre = "puerta";
                    p.calcularBisagra = false;        // el origen ya esta en la bisagra
                    p.alejarseDelJugador = true;
                    if (final)
                    {
                        p.objetoNecesario = LLAVE_LABERINTO;
                        p.enLaMano = true;
                        p.abrirAlAcercarse = true;
                        p.gastarObjeto = false;
                        p.mensajeCerrada = "La puerta está cerrada. Necesitas una llave";
                    }

                    puertas++;
                }

                foreach (Transform t in todos.Where(t => t.name.Contains("cajon") && t.GetComponent<MeshFilter>() != null))
                {
                    Cajon c = obtener<Cajon>(t.gameObject);
                    c.nombre = "cajón";
                    // La estanteria tiene un cajon mas profundo y largo.
                    c.distancia = t.name.Contains("estanteria") ? 0.24f : 0.22f;
                    cajones++;
                }

                PrefabUtility.SaveAsPrefabAsset(raiz, PREFAB_LABERINTO);
                Debug.Log($"[Goldy Pond] Laberinto: {puertas} puertas (la de la zona 5 con llave) y {cajones} cajones listos.");
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
    }
}
