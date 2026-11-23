using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    // Conecta la Aldea con el Subterraneo:
    // - Agrega Aldea y Subterraneo a la lista de escenas (File > Build Profiles), Aldea primero.
    // - Subterraneo: la Escalera_Entrada_Molino se puede trepar (Escalera), arriba sale "(E) Subir a la Aldea"
    // (CambioEscena) y ahi apareces al bajar por la trampilla (PuntoLlegada "Escalera").
    // - Aldea: sobre la trampilla del molino sale "(E) Entrar al subterraneo" cuando ya esta abierta,
    // y junto a ella apareces al regresar (PuntoLlegada "Trampilla").
    // Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Interaccion.
    [InitializeOnLoad]
    public static class ConfigurarSubterraneo
    {
        private const string ESCENA_ALDEA = "Assets/Scenes/Aldea.unity";
        private const string ESCENA_SUBTERRANEO = "Assets/Scenes/Subterraneo.unity";
        private const string PREFAB_ALDEA = "Assets/Prefabs/Aldea/Goldy_Pond_Colores.prefab";
        private const string PREFAB_SUBTERRANEO = "Assets/Prefabs/Subterraneo/Subterraneo_Colores.prefab";

        private static string Clave => $"GoldyPond.Subterraneo.v1.{Application.dataPath}";

        static ConfigurarSubterraneo()
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

        [MenuItem("Goldy Pond/Interaccion/Conectar Aldea y Subterraneo (trampilla y escalera)")]
        public static void aplicar()
        {
            agregarEscenas();
            configurarEscalera();
            configurarTrampilla();
        }

        private static void agregarEscenas()
        {
            List<EditorBuildSettingsScene> lista = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ESCENA_ALDEA, true),
                new EditorBuildSettingsScene(ESCENA_SUBTERRANEO, true),
            };
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s.path != ESCENA_ALDEA && s.path != ESCENA_SUBTERRANEO)
                {
                    lista.Add(s);
                }
            }

            EditorBuildSettings.scenes = lista.ToArray();
            Debug.Log("[Goldy Pond] Escenas en el Build: " + string.Join(", ", lista.Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))));
        }

        private static void configurarEscalera()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_SUBTERRANEO) == null)
            {
                Debug.LogWarning($"[Goldy Pond] No existe {PREFAB_SUBTERRANEO}");
                return;
            }

            GameObject raiz = PrefabUtility.LoadPrefabContents(PREFAB_SUBTERRANEO);
            try
            {
                Transform t = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name.StartsWith("Escalera_Entrada_Molino"));
                MeshFilter mf = t != null ? t.GetComponent<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null)
                {
                    Debug.LogWarning("[Goldy Pond] No encontre Escalera_Entrada_Molino en el Subterraneo.");
                    return;
                }

                Vector3[] puntos = mf.sharedMesh.vertices.Select(v => t.TransformPoint(v)).ToArray();
                float minY = puntos.Min(p => p.y), maxY = puntos.Max(p => p.y);
                Vector3 abajo = promedio(puntos.Where(p => p.y < minY + 0.05f));
                Vector3 arriba = promedio(puntos.Where(p => p.y > maxY - 0.05f));
                // La escalera esta recargada: la punta se va hacia la pared. Uno se sube del otro lado.
                Vector3 inclinacion = Vector3.ProjectOnPlane(arriba - abajo, Vector3.up);
                Vector3 frente = inclinacion.sqrMagnitude > 0.0025f
                    ? -inclinacion.normalized
                    : Vector3.ProjectOnPlane(raiz.transform.position - abajo, Vector3.up).normalized;

                Escalera e = obtener<Escalera>(t);
                e.abajoLocal = t.InverseTransformPoint(abajo);
                e.arribaLocal = t.InverseTransformPoint(arriba);
                e.frenteLocal = t.InverseTransformDirection(frente);

                // Sin collider el jugador no la "ve" para mostrar el aviso de arriba.
                if (t.GetComponentInChildren<Collider>(true) == null)
                {
                    BoxCollider caja = t.gameObject.AddComponent<BoxCollider>();
                    caja.isTrigger = true;
                    RecogiblesAutomaticos.ajustarCaja(caja);
                }

                CambioEscena c = obtener<CambioEscena>(t);
                c.texto = "Subir a la Aldea";
                c.escenaDestino = "Aldea";
                c.llegada = "Trampilla";
                c.escalera = e;
                c.trampilla = null;

                PuntoLlegada l = obtener<PuntoLlegada>(t);
                l.id = "Escalera";
                l.escalera = e;

                PrefabUtility.SaveAsPrefabAsset(raiz, PREFAB_SUBTERRANEO);
                Debug.Log($"[Goldy Pond] Escalera del subterraneo lista: de {abajo.y:0.00} a {arriba.y:0.00} m de alto, frente {frente.x:0.00},{frente.z:0.00}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }

        private static void configurarTrampilla()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_ALDEA) == null)
            {
                Debug.LogWarning($"[Goldy Pond] No existe {PREFAB_ALDEA}");
                return;
            }

            GameObject raiz = PrefabUtility.LoadPrefabContents(PREFAB_ALDEA);
            try
            {
                Transform marco = null, piso = null;
                Trampilla trampilla = null;
                foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (t.parent == null || t.parent.name != "Molino")
                    {
                        continue;
                    }

                    if (t.name.StartsWith("Trampilla_Marco"))
                    {
                        marco = t;
                    }

                    if (t.name.StartsWith("Piso_Planta_Baja"))
                    {
                        piso = t;
                    }

                    if (t.name.StartsWith("Trampilla_Tapa"))
                    {
                        trampilla = t.GetComponent<Trampilla>();
                    }
                }

                Renderer rMarco = marco != null ? marco.GetComponent<Renderer>() : null;
                if (rMarco == null || trampilla == null)
                {
                    Debug.LogWarning("[Goldy Pond] No encontre la trampilla del molino (marco o tapa con Trampilla).");
                    return;
                }

                Bounds b = rMarco.bounds;
                Transform molino = marco.parent;

                // Caja invisible encima del marco: ahi sale "(E) Entrar al subterráneo".
                Transform entrada = hijo(molino, "Entrada Subterraneo");
                entrada.SetPositionAndRotation(new Vector3(b.center.x, b.max.y, b.center.z), Quaternion.identity);
                BoxCollider caja = obtener<BoxCollider>(entrada);
                caja.isTrigger = true;
                Vector3 escala = entrada.lossyScale;
                Vector3 tamano = new Vector3(b.size.x, 1.2f, b.size.z);
                caja.size = new Vector3(tamano.x / escala.x, tamano.y / escala.y, tamano.z / escala.z);
                caja.center = new Vector3(0f, 0.6f / escala.y, 0f);
                CambioEscena c = obtener<CambioEscena>(entrada);
                c.texto = "Entrar al subterráneo";
                c.escenaDestino = "Subterraneo";
                c.llegada = "Escalera";
                c.trampilla = trampilla;
                c.escalera = null;

                // Al regresar: parado junto a la trampilla, del lado del centro del molino, dandole la espalda.
                Vector3 haciaCentro = piso != null ? Vector3.ProjectOnPlane(piso.GetComponent<Renderer>().bounds.center - b.center, Vector3.up) : Vector3.forward;
                if (haciaCentro.sqrMagnitude < 0.0001f)
                {
                    haciaCentro = Vector3.forward;
                }

                haciaCentro.Normalize();
                Transform llegada = hijo(molino, "Llegada desde Subterraneo");
                Vector3 pie = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z) + haciaCentro * (Mathf.Max(b.extents.x, b.extents.z) + 0.45f);
                llegada.SetPositionAndRotation(pie, Quaternion.LookRotation(haciaCentro, Vector3.up));
                PuntoLlegada l = obtener<PuntoLlegada>(llegada);
                l.id = "Trampilla";
                l.escalera = null;

                PrefabUtility.SaveAsPrefabAsset(raiz, PREFAB_ALDEA);
                Debug.Log("[Goldy Pond] Trampilla del molino: '(E) Entrar al subterráneo' listo, y punto de llegada junto a ella.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }

        private static T obtener<T>(Transform t) where T : Component
        {
            T c = t.GetComponent<T>();
            return c != null ? c : t.gameObject.AddComponent<T>();
        }

        private static Transform hijo(Transform padre, string nombre)
        {
            Transform t = padre.Find(nombre);
            if (t != null)
            {
                return t;
            }

            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            return go.transform;
        }

        private static Vector3 promedio(IEnumerable<Vector3> puntos)
        {
            List<Vector3> lista = puntos.ToList();
            return lista.Count == 0 ? Vector3.zero : lista.Aggregate(Vector3.zero, (s, p) => s + p) / lista.Count;
        }
    }
}
