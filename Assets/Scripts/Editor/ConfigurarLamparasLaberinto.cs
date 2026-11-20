using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    /// <summary>
    /// Le pone una luz calidita (Point Light) a cada lampara colgante del laberinto ("lampara 1".."lampara 17"),
    /// a la altura del vidrio de la linterna. La de la zona 5 (la del centro) tambien hace sombras.
    /// Espera a que exista el prefab del laberinto (se crea cuando Unity importa el FBX).
    /// Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Laberinto.
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigurarLamparasLaberinto
    {
        const string PrefabLaberinto = "Assets/Prefabs/laberinto/laberinto_colores.prefab";
        static readonly Regex PatronLampara = new Regex(@"^lampara \d+$");
        static string Clave => $"GoldyPond.LamparasLaberinto.v1.{Application.dataPath}";

        public static Color colorLuz = new Color(1f, 0.8f, 0.52f);
        public const float alcance = 6.5f;
        public const float intensidad = 1.6f;

        static ConfigurarLamparasLaberinto()
        {
            EditorApplication.delayCall += AplicarPendiente;
            EditorApplication.projectChanged += AplicarPendiente;
        }

        static void AplicarPendiente()
        {
            if (EditorPrefs.GetBool(Clave, false)) { EditorApplication.projectChanged -= AplicarPendiente; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AplicarPendiente;
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabLaberinto) == null) return; // todavia no se importa
            EditorPrefs.SetBool(Clave, true);
            EditorApplication.projectChanged -= AplicarPendiente;
            Aplicar();
        }

        [MenuItem("Goldy Pond/Laberinto/Luz en las lamparas")]
        public static void Aplicar()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabLaberinto) == null) { Debug.LogWarning($"[Goldy Pond] No existe {PrefabLaberinto}"); return; }
            var raiz = PrefabUtility.LoadPrefabContents(PrefabLaberinto);
            try
            {
                var lamparas = raiz.GetComponentsInChildren<Transform>(true).Where(t => PatronLampara.IsMatch(t.name)).ToList();
                if (lamparas.Count == 0) { Debug.LogWarning("[Goldy Pond] No encontre lamparas (\"lampara N\") en el laberinto."); return; }

                // La del centro (zona 5) es la que esta mas cerca del origen del laberinto.
                var centro = lamparas.OrderBy(t => Vector3.ProjectOnPlane(t.position - raiz.transform.position, Vector3.up).sqrMagnitude).First();

                foreach (var lampara in lamparas)
                {
                    var vieja = lampara.Find("Luz");
                    if (vieja != null) Object.DestroyImmediate(vieja.gameObject);

                    var r = lampara.GetComponent<Renderer>();
                    var pos = r != null ? new Vector3(r.bounds.center.x, r.bounds.min.y + 0.32f, r.bounds.center.z) : lampara.position + Vector3.down * 0.76f;

                    var go = new GameObject("Luz");
                    go.transform.SetParent(lampara, false);
                    go.transform.position = pos;
                    var luz = go.AddComponent<Light>();
                    luz.type = LightType.Point;
                    luz.color = colorLuz;
                    luz.range = alcance;
                    luz.intensity = intensidad;
                    luz.shadows = lampara == centro ? LightShadows.Soft : LightShadows.None;
                }
                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabLaberinto);
                Debug.Log($"[Goldy Pond] Luz puesta en {lamparas.Count} lamparas del laberinto.");
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }
    }
}
