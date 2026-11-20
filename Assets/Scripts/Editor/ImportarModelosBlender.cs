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
    /// <summary>
    /// Integra los modelos exportados desde Blender (Assets/Models/**.fbx):
    ///  - Ajustes de importacion la primera vez que entra cada FBX.
    ///  - Materiales URP compartidos en Assets/Materials/Paleta con los colores
    ///    exactos de Blender (Blender/paleta_materiales.json).
    ///  - Prefabs (variantes del modelo) en Assets/Prefabs con collider.
    ///  - Escenas Aldea y Subterraneo si todavia no existen.
    /// Todo es idempotente: lo que ya existe no se sobrescribe, asi que los
    /// ajustes hechos a mano en materiales, prefabs o escenas se respetan.
    /// </summary>
    [InitializeOnLoad]
    public class ImportarModelosBlender : AssetPostprocessor
    {
        const string CarpetaModelos = "Assets/Models/";
        const string CarpetaPaleta = "Assets/Materials/Paleta";
        const string CarpetaPrefabs = "Assets/Prefabs";
        const string CarpetaEscenas = "Assets/Scenes";
        const string RutaPaleta = "Blender/paleta_materiales.json";

        // Escenarios completos: llevan colliders de malla y se marcan como estaticos.
        static readonly Dictionary<string, string> Escenarios = new Dictionary<string, string>
        {
            { "Goldy_Pond_Colores", "Aldea" },
            { "Subterraneo_Colores", "Subterraneo" },
            { "laberinto_colores", "Laberinto" },
        };

        // Objetos grandes por los que se camina (no llevan la caja que envuelve todo):
        // collider de malla en cada pieza, menos las que se nombran aqui (se atraviesan).
        static readonly Dictionary<string, string[]> ConColliderDeMalla = new Dictionary<string, string[]>
        {
            { "Portal_Nocturno", new[] { "energia" } },
        };

        // Materiales que en las referencias son luz (ventanas, faroles, vidrio de la linterna).
        static readonly Dictionary<string, Color> Emisivos = new Dictionary<string, Color>
        {
            { "Amarillo", new Color(1f, 0.78f, 0.45f) * 1.5f },
            { "Amarillo claro", new Color(1f, 0.827f, 0.478f) * 2f }, // #FFD37A, "Luz (emision)" de la linterna
            { "Morado claro", new Color(0.608f, 0.435f, 1f) * 2f },   // #9B6FFF, orilla de la energia del portal nocturno
            { "Azul portal", new Color(0.306f, 0.651f, 1f) * 2f },    // #4EA6FF, rayitos del portal nocturno
            { "Rojo", new Color(0.890f, 0.204f, 0.188f) * 2f },       // #E33430, foco del mecanismo de los 3 pines (bloqueado)
            { "Verde brillante", new Color(0.263f, 0.545f, 0.192f) * 2f }, // #438B31, foco (desbloqueado)
        };

        // Agua del lago del subterraneo: translucida.
        static readonly Dictionary<string, float> Translucidos = new Dictionary<string, float>
        {
            { "Azul", 0.75f },
        };

        static bool pendiente;

        static ImportarModelosBlender()
        {
            Programar();
        }

        // ---------- Importacion ----------

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(CarpetaModelos) || !(assetImporter is ModelImporter mi))
                return;
            // Solo en la primera importacion, para no pisar ajustes hechos despues en el Inspector.
            if (!mi.importSettingsMissing)
                return;

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

        static void OnPostprocessAllAssets(string[] importados, string[] borrados, string[] movidos, string[] origenMovidos)
        {
            if (importados.Any(EsModelo) || importados.Any(p => p.EndsWith(".cs")))
                Programar();
        }

        static bool EsModelo(string ruta) =>
            ruta.StartsWith(CarpetaModelos) && ruta.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        static void Programar()
        {
            if (pendiente || Application.isBatchMode)
                return;
            pendiente = true;
            EditorApplication.delayCall += () =>
            {
                pendiente = false;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Programar();
                    return;
                }
                ProcesarTodo(crearEscenas: true);
            };
        }

        // ---------- Entradas manuales ----------

        [MenuItem("Goldy Pond/Modelos de Blender/Actualizar materiales, prefabs y escenas")]
        static void MenuActualizar() => ProcesarTodo(crearEscenas: true);

        /// <summary>Para Unity en modo batch: -executeMethod GoldyPond.EditorTools.ImportarModelosBlender.ProcesarBatch</summary>
        public static void ProcesarBatch()
        {
            ProcesarTodo(crearEscenas: true);
            AssetDatabase.SaveAssets();
        }

        // ---------- Proceso principal ----------

        static void ProcesarTodo(bool crearEscenas)
        {
            var modelos = AssetDatabase.FindAssets("t:Model", new[] { CarpetaModelos.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(EsModelo)
                .OrderBy(p => p)
                .ToList();
            if (modelos.Count == 0)
                return;

            var paleta = CargarPaleta();
            foreach (var ruta in modelos)
                RemapearMateriales(ruta, paleta);

            foreach (var ruta in modelos)
                CrearPrefab(ruta);

            if (crearEscenas)
                foreach (var par in Escenarios)
                    CrearEscena(par.Key, par.Value);

            AssetDatabase.SaveAssets();
        }

        // ---------- Materiales ----------

        [System.Serializable]
        class MaterialBlender
        {
            public string nombre;
            public float[] color_lineal;
            public float rugosidad = 0.5f;
            public float metal;
        }

        [System.Serializable]
        class PaletaBlender
        {
            public List<MaterialBlender> materiales = new List<MaterialBlender>();
        }

        static Dictionary<string, MaterialBlender> CargarPaleta()
        {
            var ruta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, RutaPaleta);
            if (!File.Exists(ruta))
            {
                Debug.LogWarning($"[Goldy Pond] No se encontro {RutaPaleta}; se usaran los colores que trae el FBX.");
                return new Dictionary<string, MaterialBlender>();
            }
            var paleta = JsonUtility.FromJson<PaletaBlender>(File.ReadAllText(ruta));
            return paleta.materiales.Where(m => !string.IsNullOrEmpty(m.nombre)).GroupBy(m => m.nombre)
                .ToDictionary(g => g.Key, g => g.First());
        }

        /// <summary>Asigna a cada material del FBX su material compartido de la paleta. Devuelve true si hubo que reimportar.</summary>
        static bool RemapearMateriales(string rutaModelo, Dictionary<string, MaterialBlender> paleta)
        {
            if (!(AssetImporter.GetAtPath(rutaModelo) is ModelImporter importer))
                return false;

            var yaMapeados = new HashSet<string>(importer.GetExternalObjectMap()
                .Where(kv => kv.Key.type == typeof(Material) && kv.Value != null)
                .Select(kv => kv.Key.name));
            var embebidos = AssetDatabase.LoadAllAssetsAtPath(rutaModelo).OfType<Material>()
                .Where(m => !yaMapeados.Contains(m.name))
                .ToList();
            if (embebidos.Count == 0)
                return false;

            foreach (var embebido in embebidos)
            {
                var compartido = ObtenerMaterial(embebido.name, embebido, paleta);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embebido.name), compartido);
            }
            importer.SaveAndReimport();
            return true;
        }

        static Material ObtenerMaterial(string nombre, Material respaldo, Dictionary<string, MaterialBlender> paleta)
        {
            CrearCarpeta(CarpetaPaleta);
            var ruta = $"{CarpetaPaleta}/{NombreArchivo(nombre)}.mat";
            var existente = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (existente != null)
                return existente;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? respaldo.shader;
            var mat = new Material(shader) { name = nombre };

            Color color;
            float suavidad = 0.5f, metal = 0f;
            if (paleta.TryGetValue(nombre, out var mb) && mb.color_lineal != null && mb.color_lineal.Length >= 3)
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

            if (Emisivos.TryGetValue(nombre, out var emision))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emision);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (Translucidos.TryGetValue(nombre, out var alfa))
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

        // ---------- Prefabs ----------

        static void CrearPrefab(string rutaModelo)
        {
            var nombre = Path.GetFileNameWithoutExtension(rutaModelo);
            var sub = Path.GetDirectoryName(rutaModelo).Replace('\\', '/').Substring(CarpetaModelos.Length).TrimStart('/');
            var carpeta = string.IsNullOrEmpty(sub) ? CarpetaPrefabs : $"{CarpetaPrefabs}/{sub}";
            var ruta = $"{carpeta}/{nombre}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) != null)
                return;

            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(rutaModelo);
            if (modelo == null)
                return;
            CrearCarpeta(carpeta);

            var escena = EditorSceneManager.NewPreviewScene();
            try
            {
                var raiz = (GameObject)PrefabUtility.InstantiatePrefab(modelo, escena);
                raiz.name = nombre;

                if (Escenarios.ContainsKey(nombre))
                {
                    const StaticEditorFlags estatico = StaticEditorFlags.BatchingStatic
                        | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic
                        | StaticEditorFlags.ReflectionProbeStatic;
                    foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, estatico);
                }
                else if (ConColliderDeMalla.TryGetValue(nombre, out var sinCollider))
                {
                    foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>(true))
                        if (!sinCollider.Contains(mf.name))
                            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
                else
                {
                    // Objeto interactivo: un BoxCollider que envuelve todo el modelo para poder detectarlo/recogerlo.
                    var limites = LimitesLocales(raiz);
                    var caja = raiz.AddComponent<BoxCollider>();
                    caja.center = limites.center;
                    caja.size = limites.size;

                    if (nombre == "Linterna")
                    {
                        var luz = new GameObject("Luz");
                        luz.transform.SetParent(raiz.transform, false);
                        luz.transform.localPosition = limites.center;
                        var l = luz.AddComponent<Light>();
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

        static Bounds LimitesLocales(GameObject raiz)
        {
            var renders = raiz.GetComponentsInChildren<Renderer>();
            if (renders.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one * 0.1f);
            var b = renders[0].bounds;
            foreach (var r in renders)
                b.Encapsulate(r.bounds);
            // La raiz esta en el origen sin rotacion ni escala: limites del mundo = locales.
            return b;
        }

        // ---------- Escenas ----------

        static void CrearEscena(string nombreModelo, string nombreEscena)
        {
            var rutaEscena = $"{CarpetaEscenas}/{nombreEscena}.unity";
            var rutaPrefab = AssetDatabase.FindAssets($"t:Prefab {nombreModelo}", new[] { CarpetaPrefabs })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == nombreModelo);
            if (File.Exists(rutaEscena) || rutaPrefab == null)
                return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
            CrearCarpeta(CarpetaEscenas);

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
            var modo = haySinTitulo ? NewSceneMode.Single : NewSceneMode.Additive;
            var escena = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, modo);
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
                var b = LimitesLocales(inst);

                foreach (var go in escena.GetRootGameObjects())
                {
                    var cam = go.GetComponent<Camera>();
                    if (cam == null)
                        continue;
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
                    foreach (var luz in escena.GetRootGameObjects().SelectMany(g => g.GetComponents<Light>()))
                    {
                        if (luz.type != LightType.Directional)
                            continue;
                        luz.color = new Color(0.75f, 0.62f, 0.95f);
                        luz.intensity = 0.6f;
                    }
                }

                // Jugador en primera persona; la camara panoramica queda desactivada.
                CrearJugadorPrimeraPersona.Configurar(escena, nombreEscena);

                EditorSceneManager.SaveScene(escena, rutaEscena);
                Debug.Log($"[Goldy Pond] Escena creada: {rutaEscena}");
            }
            finally
            {
                if (modo == NewSceneMode.Additive)
                    EditorSceneManager.CloseScene(escena, true);
            }
        }

        // ---------- Utilidades ----------

        static void CrearCarpeta(string ruta)
        {
            if (AssetDatabase.IsValidFolder(ruta))
                return;
            var padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
            CrearCarpeta(padre);
            AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
        }

        static string NombreArchivo(string nombre)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');
            return nombre;
        }
    }
}
