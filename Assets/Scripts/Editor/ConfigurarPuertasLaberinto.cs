using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    /// <summary>
    /// Puertas y cajones del laberinto (prefab laberinto_colores):
    ///  - "puerta cuarto 2", "puerta cuarto 3", "puerta pasillo": se abren con E.
    ///  - "puerta zona 5" (el ultimo cuarto): cerrada, se abre con la llave del laberinto en la mano.
    ///  - Todos los cajones de los escritorios y las estanterias ("... cajon ..."): se abren con E.
    /// Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Laberinto.
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigurarPuertasLaberinto
    {
        const string PrefabLaberinto = "Assets/Prefabs/laberinto/laberinto_colores.prefab";
        public const string LlaveLaberinto = "Llave del laberinto";
        static readonly string[] PuertasLibres = { "puerta cuarto 2", "puerta cuarto 3", "puerta pasillo" };
        const string PuertaFinal = "puerta zona 5";
        static string Clave => $"GoldyPond.PuertasLaberinto.v1.{Application.dataPath}";

        static ConfigurarPuertasLaberinto()
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

        [MenuItem("Goldy Pond/Laberinto/Puertas y cajones")]
        public static void Aplicar()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabLaberinto) == null) { Debug.LogWarning($"[Goldy Pond] No existe {PrefabLaberinto}"); return; }
            var raiz = PrefabUtility.LoadPrefabContents(PrefabLaberinto);
            try
            {
                var todos = raiz.GetComponentsInChildren<Transform>(true);
                int puertas = 0, cajones = 0;

                foreach (var t in todos)
                {
                    bool libre = PuertasLibres.Contains(t.name), final = t.name == PuertaFinal;
                    if (!libre && !final) continue;
                    var p = Obtener<Puerta>(t.gameObject);
                    p.nombre = "puerta";
                    p.calcularBisagra = false;        // el origen ya esta en la bisagra
                    p.alejarseDelJugador = true;
                    if (final)
                    {
                        p.objetoNecesario = LlaveLaberinto;
                        p.enLaMano = true;
                        p.abrirAlAcercarse = true;
                        p.gastarObjeto = false;
                        p.mensajeCerrada = "La puerta está cerrada. Necesitas una llave";
                    }
                    puertas++;
                }

                foreach (var t in todos.Where(t => t.name.Contains("cajon") && t.GetComponent<MeshFilter>() != null))
                {
                    var c = Obtener<Cajon>(t.gameObject);
                    c.nombre = "cajón";
                    // La estanteria tiene un cajon mas profundo y largo.
                    c.distancia = t.name.Contains("estanteria") ? 0.24f : 0.22f;
                    cajones++;
                }

                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabLaberinto);
                Debug.Log($"[Goldy Pond] Laberinto: {puertas} puertas (la de la zona 5 con llave) y {cajones} cajones listos.");
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        static T Obtener<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
    }
}
