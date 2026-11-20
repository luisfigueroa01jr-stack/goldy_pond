using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    /// <summary>
    /// Pone el componente Elevador en Elevador_Grupo del Subterraneo y le asigna la jaula (Jaula_Base),
    /// la reja doble, los cables, la polea y los engranes del motor.
    /// Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Interaccion.
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigurarElevador
    {
        const string PrefabSubterraneo = "Assets/Prefabs/Subterraneo/Subterraneo_Colores.prefab";
        const string MallaAros = "Assets/Models/Subterraneo/Jaula_Aros_con_entrada.asset";
        static string Clave => $"GoldyPond.Elevador.v3.{Application.dataPath}";

        static ConfigurarElevador()
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

        [MenuItem("Goldy Pond/Interaccion/Elevador (se abre con las 3 palancas)")]
        public static void Aplicar()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSubterraneo) == null) { Debug.LogWarning($"[Goldy Pond] No existe {PrefabSubterraneo}"); return; }
            var raiz = PrefabUtility.LoadPrefabContents(PrefabSubterraneo);
            try
            {
                var todos = raiz.GetComponentsInChildren<Transform>(true);
                Transform Buscar(string prefijo) => todos.FirstOrDefault(t => t.name.StartsWith(prefijo));

                var grupo = Buscar("Elevador_Grupo");
                var jaula = Buscar("Jaula_Base");
                if (grupo == null || jaula == null) { Debug.LogWarning("[Goldy Pond] No encontre Elevador_Grupo / Jaula_Base en el Subterraneo."); return; }

                var e = grupo.GetComponent<Elevador>();
                if (e == null) e = grupo.gameObject.AddComponent<Elevador>();
                e.jaula = jaula;
                e.reja = todos.Where(t => t.name.StartsWith("Puerta_Reja")).Select(t => t.GetComponent<Puerta>()).Where(p => p != null).ToArray();
                var cables = Buscar("Cables_Elevacion");
                e.cables = cables != null ? cables.GetComponent<Renderer>() : null;
                // La polea primero; los dos engranes del motor estan juntos y giran al reves uno del otro.
                var viga = Buscar("Soporte_Viga");
                e.techo = viga != null ? viga.GetComponent<Renderer>() : null;

                // Las riostras (palos diagonales de los postes) se ven metidas en la jaula y la jaula las atravesaria al subir.
                var riostras = Buscar("Soporte_Riostras");
                if (riostras != null) riostras.gameObject.SetActive(false);

                bool aro = QuitarAroDeLaEntrada(Buscar("Jaula_Aros"), e.reja);

                e.engranes = new[] { Buscar("Polea_Principal"), Buscar("Motor_Engrane_Grande"), Buscar("Motor_Engrane_Chico") }
                    .Where(t => t != null).ToArray();

                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabSubterraneo);
                Debug.Log($"[Goldy Pond] Elevador listo (aro de la entrada: {(aro ? "quitado" : "NO")}, riostras: {(riostras != null ? "quitadas" : "no encontradas")}, viga: {(e.techo != null ? "si" : "FALTA")}, reja: {e.reja.Length} hojas, cables: {(e.cables != null ? "si" : "FALTA")}, engranes: {e.engranes.Length}). Se abre con {e.palancasNecesarias} palancas.");
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        /// <summary>
        /// El aro del medio de la jaula cruza la entrada (entre los dos postes de la reja) a la altura de los ojos.
        /// Hace una copia de la malla sin ese tramo (los aros de abajo y de arriba de la reja se quedan)
        /// y la pone en el MeshFilter y el MeshCollider. El modelo de Blender no se toca.
        /// </summary>
        static bool QuitarAroDeLaEntrada(Transform aros, Puerta[] reja)
        {
            var mf = aros != null ? aros.GetComponent<MeshFilter>() : null;
            var hojas = reja.Where(p => p != null && p.GetComponent<Renderer>() != null).ToArray();
            if (mf == null || mf.sharedMesh == null || hojas.Length == 0) return false;

            // La entrada: donde estan las hojas cerradas.
            var b = hojas[0].GetComponent<Renderer>().bounds;
            foreach (var h in hojas) b.Encapsulate(h.GetComponent<Renderer>().bounds);
            var lado = hojas.Length > 1 ? Vector3.ProjectOnPlane(hojas[1].transform.position - hojas[0].transform.position, Vector3.up).normalized : Vector3.right;
            var fuera = Vector3.Cross(Vector3.up, lado).normalized;
            float medioAncho = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(lado.x), 0f, Mathf.Abs(lado.z))));

            var original = AssetDatabase.LoadAssetAtPath<Mesh>(MallaAros);
            var fuente = mf.sharedMesh == original && original != null ? null : mf.sharedMesh;
            if (fuente == null) return true; // ya tiene la malla cortada

            var v = fuente.vertices;
            var nueva = Object.Instantiate(fuente);
            nueva.name = "Jaula_Aros_con_entrada";
            int quitados = 0;
            for (int sub = 0; sub < fuente.subMeshCount; sub++)
            {
                var tris = fuente.GetTriangles(sub);
                var quedan = new System.Collections.Generic.List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var c = aros.TransformPoint((v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) / 3f) - b.center;
                    bool enLaEntrada = Mathf.Abs(Vector3.Dot(c, lado)) < medioAncho - 0.08f   // entre los postes
                                       && Mathf.Abs(Vector3.Dot(c, fuera)) < 0.15f            // en la linea de la reja
                                       && Mathf.Abs(c.y) < b.extents.y - 0.2f;                // ni el aro de abajo ni el de arriba
                    if (enLaEntrada) { quitados++; continue; }
                    quedan.Add(tris[i]); quedan.Add(tris[i + 1]); quedan.Add(tris[i + 2]);
                }
                nueva.SetTriangles(quedan, sub);
            }
            if (quitados == 0) { Object.DestroyImmediate(nueva); return false; }

            if (original != null) AssetDatabase.DeleteAsset(MallaAros);
            AssetDatabase.CreateAsset(nueva, MallaAros);
            mf.sharedMesh = nueva;
            var mc = aros.GetComponent<MeshCollider>();
            if (mc != null) mc.sharedMesh = nueva;
            Debug.Log($"[Goldy Pond] Quite {quitados} triangulos del aro que cruzaba la entrada del elevador.");
            return true;
        }
    }
}
