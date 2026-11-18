using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    /// <summary>
    /// Agrega el componente Puerta (abrir/cerrar con E) a:
    ///  - las 7 Puerta_Principal de las casas de la Aldea
    ///  - la reja doble (Puerta_Reja_Izquierda/Derecha) del Subterraneo, que se abren juntas.
    /// Se hace en los prefabs, asi las escenas lo heredan. Se aplica solo una vez;
    /// despues se puede repetir desde Goldy Pond > Interaccion.
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigurarPuertas
    {
        const string PrefabAldea = "Assets/Prefabs/Aldea/Goldy_Pond_Colores.prefab";
        const string PrefabSubterraneo = "Assets/Prefabs/Subterraneo/Subterraneo_Colores.prefab";

        static string Clave => $"GoldyPond.Puertas.v1.{Application.dataPath}";

        static ConfigurarPuertas()
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

        [MenuItem("Goldy Pond/Interaccion/Configurar puertas (abrir y cerrar con E)")]
        public static void Aplicar()
        {
            int n = Configurar(PrefabAldea, "Puerta_Principal", "puerta", false);
            n += Configurar(PrefabSubterraneo, "Puerta_Reja", "reja", true);
            Debug.Log($"[Goldy Pond] {n} puertas se abren y cierran con E.");
        }

        static int Configurar(string rutaPrefab, string prefijo, string nombre, bool abrirJuntas)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab) == null)
            {
                Debug.LogWarning($"[Goldy Pond] No existe {rutaPrefab}");
                return 0;
            }

            var raiz = PrefabUtility.LoadPrefabContents(rutaPrefab);
            try
            {
                var puertas = new List<Puerta>();
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith(prefijo)) continue;

                    var p = t.GetComponent<Puerta>();
                    if (p == null)
                    {
                        p = t.gameObject.AddComponent<Puerta>();
                        p.nombre = nombre;
                    }
                    // Sin collider el rayo de la camara no la detecta.
                    if (t.GetComponentInChildren<Collider>() == null)
                        t.gameObject.AddComponent<BoxCollider>();
                    puertas.Add(p);
                }

                if (abrirJuntas && puertas.Count > 1)
                    foreach (var p in puertas)
                        p.companeras = puertas.FindAll(o => o != p).ToArray();

                if (puertas.Count == 0)
                    Debug.LogWarning($"[Goldy Pond] No encontre objetos que empiecen con '{prefijo}' en {rutaPrefab}");
                else
                    PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
                return puertas.Count;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }
    }
}
