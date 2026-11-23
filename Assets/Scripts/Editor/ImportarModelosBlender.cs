using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GoldyPond.EditorTools
{
    // Integra los modelos exportados desde Blender (Assets/Models/**.fbx):
    // - Ajustes de importacion la primera vez que entra cada FBX.
    // - Materiales URP compartidos en Assets/Materials/Paleta con los colores
    // exactos de Blender (Blender/paleta_materiales.json).
    // - Prefabs (variantes del modelo) en Assets/Prefabs con collider.
    // - Escenas Aldea y Subterraneo si todavia no existen.
    // Todo es idempotente: lo que ya existe no se sobrescribe, asi que los
    // ajustes hechos a mano en materiales, prefabs o escenas se respetan.
    [InitializeOnLoad]
    public class ImportarModelosBlender : AssetPostprocessor
    {
        private const string CARPETA_MODELOS = "Assets/Models/";
        private const string CARPETA_PALETA = "Assets/Materials/Paleta";
        private const string CARPETA_PREFABS = "Assets/Prefabs";
        private const string CARPETA_ESCENAS = "Assets/Scenes";
        private const string RUTA_PALETA = "Blender/paleta_materiales.json";

        // Escenarios completos: llevan colliders de malla y se marcan como estaticos.
        private static readonly Dictionary<string, string> Escenarios = new Dictionary<string, string>
        {
            { "Goldy_Pond_Colores", "Aldea" },
            { "Subterraneo_Colores", "Subterraneo" },
            { "laberinto_colores", "Laberinto" },
        };

        // Objetos grandes por los que se camina (no llevan la caja que envuelve todo):
        // collider de malla en cada pieza, menos las que se nombran aqui (se atraviesan).
        private static readonly Dictionary<string, string[]> ConColliderDeMalla = new Dictionary<string, string[]>
        {
            { "Portal_Nocturno", new[] { "energia" } },
        };

        // Materiales que en las referencias son luz (ventanas, faroles, vidrio de la linterna).
        private static readonly Dictionary<string, Color> Emisivos = new Dictionary<string, Color>
        {
            { "Amarillo", new Color(1f, 0.78f, 0.45f) * 1.5f },
            { "Amarillo claro", new Color(1f, 0.827f, 0.478f) * 2f }, // #FFD37A, "Luz (emision)" de la linterna
            { "Morado claro", new Color(0.608f, 0.435f, 1f) * 2f },   // #9B6FFF, orilla de la energia del portal nocturno
            { "Azul portal", new Color(0.306f, 0.651f, 1f) * 2f },    // #4EA6FF, rayitos del portal nocturno
            { "Rojo", new Color(0.890f, 0.204f, 0.188f) * 2f },       // #E33430, foco del mecanismo de los 3 pines (bloqueado)
            { "Verde brillante", new Color(0.263f, 0.545f, 0.192f) * 2f }, // #438B31, foco (desbloqueado)
        };

        // Agua del lago del subterraneo: translucida.
        private static readonly Dictionary<string, float> Translucidos = new Dictionary<string, float>
        {
            { "Azul", 0.75f },
        };

        private static bool pendiente;

        static ImportarModelosBlender()
        {
            programar();
        }

        // ------------------- IMPORTACION ------------------------------

        private void onPreprocessModel()
        {
            if (!assetPath.StartsWith(CARPETA_MODELOS) || !(assetImporter is ModelImporter mi))
            {
                return;
            }

            // Solo en la primera importacion, para no pisar ajustes hechos despues en el Inspector.
            if (!mi.importSettingsMissing)
            {
                return;
            }

            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true; // el FBX llega con Z arriba (Blender); Unity lo convierte a Y arriba dentro de las mallas y la raiz queda en (0,0,0)
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importNormals = ModelImporterNormals.Import;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.addCollider = Escenarios.ContainsKey(Path.GetFileNameWithoutExtension(assetPath));
        }

        private static void onPostprocessAllAssets(
            string[] importados,
            string[] borrados,
            string[] movidos,
            string[] origenMovidos
        )
        {
            if (importados.Any(esModelo) || importados.Any(p => p.EndsWith(".cs")))
            {
                programar();
            }
        }

        private static bool esModelo(string ruta)
        {
            return ruta.StartsWith(CARPETA_MODELOS) && ruta.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void programar()
        {
            if (pendiente || Application.isBatchMode)
            {
                return;
            }

            pendiente = true;
            EditorApplication.delayCall += () =>
            {
                pendiente = false;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    programar();
                    return;
                }

                procesarTodo(crearEscenas: true);
            };
        }

        // ------------------- ENTRADAS MANUALES ------------------------------

        [MenuItem("Goldy Pond/Modelos de Blender/Actualizar materiales, prefabs y escenas")]
        private static void menuActualizar()
        {
            procesarTodo(crearEscenas: true);
        }

        // Para Unity en modo batch: -executeMethod GoldyPond.EditorTools.ImportarModelosBlender.procesarBatch
        public static void procesarBatch()
        {
            procesarTodo(crearEscenas: true);
            AssetDatabase.SaveAssets();
        }

        // ------------------- PROCESO PRINCIPAL ------------------------------

        private static void procesarTodo(bool crearEscenas)
        {
            List<string> modelos = AssetDatabase.FindAssets("t:Model", new[] { CARPETA_MODELOS.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(esModelo)
                .OrderBy(p => p)
                .ToList();
            if (modelos.Count == 0)
            {
                return;
            }

            Dictionary<string, MaterialBlender> paleta = cargarPaleta();
            foreach (string ruta in modelos)
            {
                remapearMateriales(ruta, paleta);
            }

            foreach (string ruta in modelos)
            {
                crearPrefab(ruta);
            }

            if (crearEscenas)
            {
                foreach (KeyValuePair<string, string> par in Escenarios)
                {
                    crearEscena(par.Key, par.Value);
                }
            }

            AssetDatabase.SaveAssets();
        }

        // ------------------- MATERIALES ------------------------------

        [System.Serializable]
        private class MaterialBlender
        {
            public string nombre;
            public float[] color_lineal;
            public float rugosidad = 0.5f;
            public float metal;
        }

        [System.Serializable]
        private class PaletaBlender
        {
            public List<MaterialBlender> materiales = new List<MaterialBlender>();
        }

        private static Dictionary<string, MaterialBlender> cargarPaleta()
        {
            string ruta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, RUTA_PALETA);
            if (!File.Exists(ruta))
            {
                Debug.LogWarning($"[Goldy Pond] No se encontro {RUTA_PALETA}; se usaran los colores que trae el FBX.");
                return new Dictionary<string, MaterialBlender>();
            }

            PaletaBlender paleta = JsonUtility.FromJson<PaletaBlender>(File.ReadAllText(ruta));
            return paleta.materiales.Where(m => !string.IsNullOrEmpty(m.nombre)).GroupBy(m => m.nombre)
                .ToDictionary(g => g.Key, g => g.First());
        }

        // Asigna a cada material del FBX su material compartido de la paleta. Devuelve true si hubo que reimportar.
        private static bool remapearMateriales(
            string rutaModelo,
            Dictionary<string, MaterialBlender> paleta
        )
        {
            if (!(AssetImporter.GetAtPath(rutaModelo) is ModelImporter importer))
            {
                return false;
            }

            HashSet<string> yaMapeados = new HashSet<string>(importer.GetExternalObjectMap()
                .Where(kv => kv.Key.type == typeof(Material) && kv.Value != null)
                .Select(kv => kv.Key.name));
            List<Material> embebidos = AssetDatabase.LoadAllAssetsAtPath(rutaModelo).OfType<Material>()
                .Where(m => !yaMapeados.Contains(m.name))
                .ToList();
            if (embebidos.Count == 0)
            {
                return false;
            }

            foreach (Material embebido in embebidos)
            {
                Material compartido = obtenerMaterial(embebido.name, embebido, paleta);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embebido.name), compartido);
            }

            importer.SaveAndReimport();
            return true;
        }

        private static Material obtenerMaterial(
            string nombre,
            Material respaldo,
            Dictionary<string, MaterialBlender> paleta
        )
        {
            crearCarpeta(CARPETA_PALETA);
            string ruta = $"{CARPETA_PALETA}/{nombreArchivo(nombre)}.mat";
            Material existente = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (existente != null)
            {
                return existente;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? respaldo.shader;
            Material mat = new Material(shader) { name = nombre };

            Color color;
            float suavidad = 0.5f, metal = 0f;
            if (paleta.TryGetValue(nombre, out MaterialBlender mb) && mb.color_lineal != null && mb.color_lineal.Length >= 3)
            {
                // Blender guarda el color en espacio lineal; los colores de material en Unity van en sRGB.
                color = new Color(mb.color_lineal[0], mb.color_lineal[1], mb.color_lineal[2], 1f).gamma;
                suavidad = 1f - Mathf.Clamp01(mb.rugosidad);
                metal = Mathf.Clamp01(mb.metal);
            }
            else
            {
                color = respaldo.HasProperty("_BaseColor") ? respaldo.GetColor("_BaseColor")
                      : respaldo.HasProperty("_Color") ? respaldo.color : Color.white;
            }

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", suavidad);
            mat.SetFloat("_Metallic", metal);

            if (Emisivos.TryGetValue(nombre, out Color emision))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emision);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (Translucidos.TryGetValue(nombre, out float alfa))
            {
                color.a = alfa;
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_Smoothness", 0.9f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }

            AssetDatabase.CreateAsset(mat, ruta);
            return mat;
        }

        // ------------------- PREFABS ------------------------------

        private static void crearPrefab(string rutaModelo)
        {
            string nombre = Path.GetFileNameWithoutExtension(rutaModelo);
            string sub = Path.GetDirectoryName(rutaModelo).Replace('\\', '/').Substring(CARPETA_MODELOS.Length).TrimStart('/');
            string carpeta = string.IsNullOrEmpty(sub) ? CARPETA_PREFABS : $"{CARPETA_PREFABS}/{sub}";
            string ruta = $"{carpeta}/{nombre}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) != null)
            {
                return;
            }

            GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(rutaModelo);
            if (modelo == null)
            {
                return;
            }

            crearCarpeta(carpeta);

            Scene escena = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject raiz = (GameObject)PrefabUtility.InstantiatePrefab(modelo, escena);
                raiz.name = nombre;

                if (Escenarios.ContainsKey(nombre))
                {
                    const StaticEditorFlags ESTATICO = StaticEditorFlags.BatchingStatic
                        | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic
                        | StaticEditorFlags.ReflectionProbeStatic;
                    foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                    {
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, ESTATICO);
                    }
                }
                else if (ConColliderDeMalla.TryGetValue(nombre, out string[] sinCollider))
                {
                    foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (!sinCollider.Contains(mf.name))
                        {
                            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                        }
                    }
                }
                else
                {
                    // Objeto interactivo: un BoxCollider que envuelve todo el modelo para poder detectarlo/recogerlo.
                    Bounds limites = limitesLocales(raiz);
                    BoxCollider caja = raiz.AddComponent<BoxCollider>();
                    caja.center = limites.center;
                    caja.size = limites.size;

                    if (nombre == "Linterna")
                    {
                        GameObject luz = new GameObject("Luz");
                        luz.transform.SetParent(raiz.transform, false);
                        luz.transform.localPosition = limites.center;
                        Light l = luz.AddComponent<Light>();
                        l.type = LightType.Point;
                        l.color = new Color(1f, 0.827f, 0.478f); // #FFD37A
                        l.range = 8f;
                        l.intensity = 3f;
                        l.shadows = LightShadows.Soft;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(escena);
            }
        }

        private static Bounds limitesLocales(GameObject raiz)
        {
            Renderer[] renders = raiz.GetComponentsInChildren<Renderer>();
            if (renders.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one * 0.1f);
            }

            Bounds b = renders[0].bounds;
            foreach (Renderer r in renders)
            {
                b.Encapsulate(r.bounds);
            }

            // La raiz esta en el origen sin rotacion ni escala: limites del mundo = locales.
            return b;
        }

        // ------------------- ESCENAS ------------------------------

        private static void crearEscena(string nombreModelo, string nombreEscena)
        {
            string rutaEscena = $"{CARPETA_ESCENAS}/{nombreEscena}.unity";
            string rutaPrefab = AssetDatabase.FindAssets($"t:Prefab {nombreModelo}", new[] { CARPETA_PREFABS })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == nombreModelo);
            if (File.Exists(rutaEscena) || rutaPrefab == null)
            {
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
            crearCarpeta(CARPETA_ESCENAS);

            // Escena aditiva para no cerrar ni ensuciar la que tenga abierta el usuario.
            // Unity no lo permite si hay una escena sin titulo abierta: en batch no hay nada que perder,
            // en el editor se deja para despues (menu Goldy Pond > Modelos de Blender).
            bool haySinTitulo = Enumerable.Range(0, SceneManager.sceneCount)
                .Any(i => string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path));
            if (haySinTitulo && !Application.isBatchMode)
            {
                Debug.LogWarning($"[Goldy Pond] No se creo {rutaEscena} porque hay una escena sin guardar abierta. " +
                                 "Guardala o abre otra y usa Goldy Pond > Modelos de Blender > Actualizar materiales, prefabs y escenas.");
                return;
            }

            NewSceneMode modo = haySinTitulo ? NewSceneMode.Single : NewSceneMode.Additive;
            Scene escena = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, modo);
            try
            {
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
                Bounds b = limitesLocales(inst);

                foreach (GameObject go in escena.GetRootGameObjects())
                {
                    Camera cam = go.GetComponent<Camera>();
                    if (cam == null)
                    {
                        continue;
                    }

                    // Vista general elevada, como en las imagenes de referencia.
                    float dist = Mathf.Max(b.extents.x, b.extents.z);
                    cam.transform.position = b.center + new Vector3(0f, dist * 0.9f, -dist * 1.4f);
                    cam.transform.LookAt(b.center);
                    cam.farClipPlane = Mathf.Max(1000f, dist * 6f);
                    if (nombreModelo == "Subterraneo_Colores")
                    {
                        // La cueva es cerrada: misma camara que en Blender ("Camara", (0,-13.6,5.2), 76.8 grados en X).
                        cam.transform.SetPositionAndRotation(new Vector3(0f, 5.2f, -13.6f), Quaternion.Euler(13.2f, 0f, 0f));
                        cam.clearFlags = CameraClearFlags.SolidColor;
                        cam.backgroundColor = new Color(0.08f, 0.05f, 0.12f);
                    }
                }

                if (nombreModelo == "Subterraneo_Colores")
                {
                    // Cueva: luz principal tenue y morada, como el ambiente de la referencia.
                    foreach (Light luz in escena.GetRootGameObjects().SelectMany(g => g.GetComponents<Light>()))
                    {
                        if (luz.type != LightType.Directional)
                        {
                            continue;
                        }

                        luz.color = new Color(0.75f, 0.62f, 0.95f);
                        luz.intensity = 0.6f;
                    }
                }

                // Jugador en primera persona; la camara panoramica queda desactivada.
                CrearJugadorPrimeraPersona.configurar(escena, nombreEscena);

                EditorSceneManager.SaveScene(escena, rutaEscena);
                Debug.Log($"[Goldy Pond] Escena creada: {rutaEscena}");
            }
            finally
            {
                if (modo == NewSceneMode.Additive)
                {
                    EditorSceneManager.CloseScene(escena, true);
                }
            }
        }

        // ------------------- UTILIDADES ------------------------------

        private static void crearCarpeta(string ruta)
        {
            if (AssetDatabase.IsValidFolder(ruta))
            {
                return;
            }

            string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
            crearCarpeta(padre);
            AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
        }

        private static string nombreArchivo(string nombre)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                nombre = nombre.Replace(c, '_');
            }

            return nombre;
        }
    }
}
