using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    // Le pone una luz calidita (Point Light) a cada lampara colgante del laberinto ("lampara 1".."lampara 17"),
    // a la altura del vidrio de la linterna. La de la zona 5 (la del centro) tambien hace sombras.
    // Espera a que exista el prefab del laberinto (se crea cuando Unity importa el FBX).
    // Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Laberinto.
    [InitializeOnLoad]
    public static class ConfigurarLamparasLaberinto
    {
        private const string PREFAB_LABERINTO = "Assets/Prefabs/laberinto/laberinto_colores.prefab";
        private static readonly Regex PatronLampara = new Regex(@"^lampara \d+$");
        private static string Clave => $"GoldyPond.LamparasLaberinto.v1.{Application.dataPath}";

        public static Color colorLuz = new Color(1f, 0.8f, 0.52f);
        public const float ALCANCE = 6.5f;
        public const float INTENSIDAD = 1.6f;

        static ConfigurarLamparasLaberinto()
        {
            EditorApplication.delayCall += aplicarPendiente;
            EditorApplication.projectChanged += aplicarPendiente;
        }

        private static void aplicarPendiente()
        {
            if (EditorPrefs.GetBool(Clave, false))
            {
                EditorApplication.projectChanged -= aplicarPendiente;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += aplicarPendiente;
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_LABERINTO) == null)
            {
                return; // todavia no se importa
            }

            EditorPrefs.SetBool(Clave, true);
            EditorApplication.projectChanged -= aplicarPendiente;
            aplicar();
        }

        [MenuItem("Goldy Pond/Laberinto/Luz en las lamparas")]
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
                System.Collections.Generic.List<Transform> lamparas = raiz.GetComponentsInChildren<Transform>(true).Where(t => PatronLampara.IsMatch(t.name)).ToList();
                if (lamparas.Count == 0)
                {
                    Debug.LogWarning("[Goldy Pond] No encontre lamparas (\"lampara N\") en el laberinto.");
                    return;
                }

                // La del centro (zona 5) es la que esta mas cerca del origen del laberinto.
                Transform centro = lamparas.OrderBy(t => Vector3.ProjectOnPlane(t.position - raiz.transform.position, Vector3.up).sqrMagnitude).First();

                foreach (Transform lampara in lamparas)
                {
                    Transform vieja = lampara.Find("Luz");
                    if (vieja != null)
                    {
                        Object.DestroyImmediate(vieja.gameObject);
                    }

                    Renderer r = lampara.GetComponent<Renderer>();
                    Vector3 pos = r != null ? new Vector3(r.bounds.center.x, r.bounds.min.y + 0.32f, r.bounds.center.z) : lampara.position + Vector3.down * 0.76f;

                    GameObject go = new GameObject("Luz");
                    go.transform.SetParent(lampara, false);
                    go.transform.position = pos;
                    Light luz = go.AddComponent<Light>();
                    luz.type = LightType.Point;
                    luz.color = colorLuz;
                    luz.range = ALCANCE;
                    luz.intensity = INTENSIDAD;
                    luz.shadows = lampara == centro ? LightShadows.Soft : LightShadows.None;
                }

                PrefabUtility.SaveAsPrefabAsset(raiz, PREFAB_LABERINTO);
                Debug.Log($"[Goldy Pond] Luz puesta en {lamparas.Count} lamparas del laberinto.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }
    }
}
