using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    /// <summary>
    /// Conecta la Aldea con el Subterraneo:
    ///  - Agrega Aldea y Subterraneo a la lista de escenas (File > Build Profiles), Aldea primero.
    ///  - Subterraneo: la Escalera_Entrada_Molino se puede trepar (Escalera), arriba sale "(E) Subir a la Aldea"
    ///    (CambioEscena) y ahi apareces al bajar por la trampilla (PuntoLlegada "Escalera").
    ///  - Aldea: sobre la trampilla del molino sale "(E) Entrar al subterráneo" cuando ya esta abierta,
    ///    y junto a ella apareces al regresar (PuntoLlegada "Trampilla").
    /// Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Interaccion.
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigurarSubterraneo
    {
        const string EscenaAldea = "Assets/Scenes/Aldea.unity";
        const string EscenaSubterraneo = "Assets/Scenes/Subterraneo.unity";
        const string PrefabAldea = "Assets/Prefabs/Aldea/Goldy_Pond_Colores.prefab";
        const string PrefabSubterraneo = "Assets/Prefabs/Subterraneo/Subterraneo_Colores.prefab";

        static string Clave => $"GoldyPond.Subterraneo.v1.{Application.dataPath}";

        static ConfigurarSubterraneo()
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

        [MenuItem("Goldy Pond/Interaccion/Conectar Aldea y Subterraneo (trampilla y escalera)")]
        public static void Aplicar()
        {
            AgregarEscenas();
            ConfigurarEscalera();
            ConfigurarTrampilla();
        }

        static void AgregarEscenas()
        {
            var lista = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(EscenaAldea, true),
                new EditorBuildSettingsScene(EscenaSubterraneo, true),
            };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != EscenaAldea && s.path != EscenaSubterraneo) lista.Add(s);
            EditorBuildSettings.scenes = lista.ToArray();
            Debug.Log("[Goldy Pond] Escenas en el Build: " + string.Join(", ", lista.Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))));
        }

        static void ConfigurarEscalera()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSubterraneo) == null) { Debug.LogWarning($"[Goldy Pond] No existe {PrefabSubterraneo}"); return; }
            var raiz = PrefabUtility.LoadPrefabContents(PrefabSubterraneo);
            try
            {
                var t = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name.StartsWith("Escalera_Entrada_Molino"));
                var mf = t != null ? t.GetComponent<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null) { Debug.LogWarning("[Goldy Pond] No encontre Escalera_Entrada_Molino en el Subterraneo."); return; }

                var puntos = mf.sharedMesh.vertices.Select(v => t.TransformPoint(v)).ToArray();
                float minY = puntos.Min(p => p.y), maxY = puntos.Max(p => p.y);
                var abajo = Promedio(puntos.Where(p => p.y < minY + 0.05f));
                var arriba = Promedio(puntos.Where(p => p.y > maxY - 0.05f));
                // La escalera esta recargada: la punta se va hacia la pared. Uno se sube del otro lado.
                var inclinacion = Vector3.ProjectOnPlane(arriba - abajo, Vector3.up);
                var frente = inclinacion.sqrMagnitude > 0.0025f
                    ? -inclinacion.normalized
                    : Vector3.ProjectOnPlane(raiz.transform.position - abajo, Vector3.up).normalized;

                var e = Obtener<Escalera>(t);
                e.abajoLocal = t.InverseTransformPoint(abajo);
                e.arribaLocal = t.InverseTransformPoint(arriba);
                e.frenteLocal = t.InverseTransformDirection(frente);

                // Sin collider el jugador no la "ve" para mostrar el aviso de arriba.
                if (t.GetComponentInChildren<Collider>(true) == null)
                {
                    var caja = t.gameObject.AddComponent<BoxCollider>();
                    caja.isTrigger = true;
                    RecogiblesAutomaticos.AjustarCaja(caja);
                }

                var c = Obtener<CambioEscena>(t);
                c.texto = "Subir a la Aldea";
                c.escenaDestino = "Aldea";
                c.llegada = "Trampilla";
                c.escalera = e;
                c.trampilla = null;

                var l = Obtener<PuntoLlegada>(t);
                l.id = "Escalera";
                l.escalera = e;

                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabSubterraneo);
                Debug.Log($"[Goldy Pond] Escalera del subterraneo lista: de {abajo.y:0.00} a {arriba.y:0.00} m de alto, frente {frente.x:0.00},{frente.z:0.00}.");
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        static void ConfigurarTrampilla()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAldea) == null) { Debug.LogWarning($"[Goldy Pond] No existe {PrefabAldea}"); return; }
            var raiz = PrefabUtility.LoadPrefabContents(PrefabAldea);
            try
            {
                Transform marco = null, piso = null;
                Trampilla trampilla = null;
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (t.parent == null || t.parent.name != "Molino") continue;
                    if (t.name.StartsWith("Trampilla_Marco")) marco = t;
                    if (t.name.StartsWith("Piso_Planta_Baja")) piso = t;
                    if (t.name.StartsWith("Trampilla_Tapa")) trampilla = t.GetComponent<Trampilla>();
                }
                var rMarco = marco != null ? marco.GetComponent<Renderer>() : null;
                if (rMarco == null || trampilla == null) { Debug.LogWarning("[Goldy Pond] No encontre la trampilla del molino (marco o tapa con Trampilla)."); return; }

                var b = rMarco.bounds;
                var molino = marco.parent;

                // Caja invisible encima del marco: ahi sale "(E) Entrar al subterráneo".
                var entrada = Hijo(molino, "Entrada Subterraneo");
                entrada.SetPositionAndRotation(new Vector3(b.center.x, b.max.y, b.center.z), Quaternion.identity);
                var caja = Obtener<BoxCollider>(entrada);
                caja.isTrigger = true;
                var escala = entrada.lossyScale;
                var tamano = new Vector3(b.size.x, 1.2f, b.size.z);
                caja.size = new Vector3(tamano.x / escala.x, tamano.y / escala.y, tamano.z / escala.z);
                caja.center = new Vector3(0f, 0.6f / escala.y, 0f);
                var c = Obtener<CambioEscena>(entrada);
                c.texto = "Entrar al subterráneo";
                c.escenaDestino = "Subterraneo";
                c.llegada = "Escalera";
                c.trampilla = trampilla;
                c.escalera = null;

                // Al regresar: parado junto a la trampilla, del lado del centro del molino, dandole la espalda.
                var haciaCentro = piso != null ? Vector3.ProjectOnPlane(piso.GetComponent<Renderer>().bounds.center - b.center, Vector3.up) : Vector3.forward;
                if (haciaCentro.sqrMagnitude < 0.0001f) haciaCentro = Vector3.forward;
                haciaCentro.Normalize();
                var llegada = Hijo(molino, "Llegada desde Subterraneo");
                var pie = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z) + haciaCentro * (Mathf.Max(b.extents.x, b.extents.z) + 0.45f);
                llegada.SetPositionAndRotation(pie, Quaternion.LookRotation(haciaCentro, Vector3.up));
                var l = Obtener<PuntoLlegada>(llegada);
                l.id = "Trampilla";
                l.escalera = null;

                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabAldea);
                Debug.Log("[Goldy Pond] Trampilla del molino: '(E) Entrar al subterráneo' listo, y punto de llegada junto a ella.");
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        static T Obtener<T>(Transform t) where T : Component
        {
            var c = t.GetComponent<T>();
            return c != null ? c : t.gameObject.AddComponent<T>();
        }

        static Transform Hijo(Transform padre, string nombre)
        {
            var t = padre.Find(nombre);
            if (t != null) return t;
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            return go.transform;
        }

        static Vector3 Promedio(IEnumerable<Vector3> puntos)
        {
            var lista = puntos.ToList();
            return lista.Count == 0 ? Vector3.zero : lista.Aggregate(Vector3.zero, (s, p) => s + p) / lista.Count;
        }
    }
}
