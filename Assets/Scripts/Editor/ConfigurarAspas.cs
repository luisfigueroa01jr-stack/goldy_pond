using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    /// <summary>
    /// Pone el componente Aspas en el cubo de las aspas del molino (el padre de Aspa_01..Aspa_04)
    /// y lo conecta con el Mecanismo del segundo piso: al poner los dos engranajes empiezan a girar.
    /// El eje se saca de la posicion de las aspas (perpendicular a ellas, hacia afuera del molino).
    /// Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Interaccion.
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigurarAspas
    {
        const string PrefabAldea = "Assets/Prefabs/Aldea/Goldy_Pond_Colores.prefab";
        static string Clave => $"GoldyPond.Aspas.v1.{Application.dataPath}";

        static ConfigurarAspas()
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

        [MenuItem("Goldy Pond/Interaccion/Aspas del molino (giran al completar el mecanismo)")]
        public static void Aplicar()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAldea) == null) { Debug.LogWarning($"[Goldy Pond] No existe {PrefabAldea}"); return; }
            var raiz = PrefabUtility.LoadPrefabContents(PrefabAldea);
            try
            {
                var todos = raiz.GetComponentsInChildren<Transform>(true);
                var aspas = Enumerable.Range(1, 4)
                    .Select(i => todos.FirstOrDefault(t => t.name.StartsWith($"Aspa_0{i}")))
                    .ToArray();
                if (aspas.Any(a => a == null)) { Debug.LogWarning("[Goldy Pond] No encontre las 4 aspas (Aspa_01..Aspa_04)."); return; }
                var cubo = aspas[0].parent;
                if (cubo == null || aspas.Any(a => a.parent != cubo)) { Debug.LogWarning("[Goldy Pond] Las aspas no comparten el mismo padre."); return; }

                var mecanismo = todos.Select(t => t.GetComponent<Mecanismo>()).FirstOrDefault(m => m != null);
                if (mecanismo == null) Debug.LogWarning("[Goldy Pond] No encontre el Mecanismo del molino; las aspas no sabran cuando girar.");

                // Centro de cada aspa (con su tela) y el centro de las 4 juntas.
                var centros = aspas.Select(Centro).ToArray();
                var centro = centros.Aggregate(Vector3.zero, (s, c) => s + c) / centros.Length;
                // El eje es perpendicular al plano de las aspas (dos aspas vecinas forman 90 grados).
                var eje = Vector3.Cross(centros[0] - centro, centros[1] - centro).normalized;
                // Que apunte hacia afuera del molino.
                var molino = todos.FirstOrDefault(t => t.name == "Molino");
                var piso = todos.FirstOrDefault(t => t.parent == molino && t.name.StartsWith("Piso_Planta_Baja"));
                var centroMolino = piso != null && piso.GetComponent<Renderer>() != null ? piso.GetComponent<Renderer>().bounds.center : (molino != null ? molino.position : raiz.transform.position);
                if (Vector3.Dot(eje, Vector3.ProjectOnPlane(centro - centroMolino, Vector3.up)) < 0f) eje = -eje;

                var a = cubo.GetComponent<Aspas>();
                if (a == null) a = cubo.gameObject.AddComponent<Aspas>();
                a.mecanismo = mecanismo;
                a.ejeLocal = cubo.InverseTransformDirection(eje).normalized;
                a.centroLocal = cubo.InverseTransformPoint(centro);

                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabAldea);
                Debug.Log($"[Goldy Pond] Aspas del molino listas en '{cubo.name}' (centro a {centro.y:0.0} m, eje {eje.x:0.00},{eje.y:0.00},{eje.z:0.00}, mecanismo: {(mecanismo != null ? "si" : "FALTA")}).");
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        static Vector3 Centro(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return t.position;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b.center;
        }
    }
}
