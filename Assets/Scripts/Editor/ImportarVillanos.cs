using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    /// <summary>
    /// Villanos de Camila (Modelos/Boss, Villano1-3) exportados desde Blender a
    /// Assets/Personajes/Villanos/[Nombre]/[Nombre].fbx con sus texturas en Texturas/.
    /// Crea un material URP Lit por cada material del .blend (con su textura) y un prefab
    /// en Assets/Prefabs/Villanos con collider de capsula.
    /// Menu: Goldy Pond/Villanos/Crear materiales y prefabs
    /// </summary>
    public class ImportarVillanos : AssetPostprocessor
    {
        const string CarpetaVillanos = "Assets/Personajes/Villanos";
        const string CarpetaPrefabs = "Assets/Prefabs/Villanos";

        [System.Serializable]
        class Par { public string material; public string textura; }

        class InfoVillano
        {
            public string modelo;
            public float altura;
            public bool tiene_esqueleto;
            public List<Par> materiales = new List<Par>();
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(CarpetaVillanos + "/") || !(assetImporter is ModelImporter mi))
                return;
            if (!mi.importSettingsMissing)
                return;
            var info = LeerInfo(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.importAnimation = false;
            mi.animationType = info != null && info.tiene_esqueleto
                ? ModelImporterAnimationType.Generic
                : ModelImporterAnimationType.None;
            mi.importNormals = ModelImporterNormals.Import;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.addCollider = false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(CarpetaVillanos + "/") || !(assetImporter is TextureImporter ti))
                return;
            if (!ti.importSettingsMissing)
                return;
            ti.maxTextureSize = 2048;
            ti.sRGBTexture = true;
        }

        [MenuItem("Goldy Pond/Villanos/Crear materiales y prefabs")]
        public static void Crear()
        {
            if (!AssetDatabase.IsValidFolder(CarpetaVillanos))
            {
                Debug.LogWarning("[Goldy Pond] No existe " + CarpetaVillanos);
                return;
            }
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var carpeta in AssetDatabase.GetSubFolders(CarpetaVillanos))
            {
                var info = LeerInfo(carpeta);
                if (info == null) continue;
                var rutaFbx = $"{carpeta}/{info.modelo}.fbx";
                var importer = AssetImporter.GetAtPath(rutaFbx) as ModelImporter;
                if (importer == null) { Debug.LogWarning("[Goldy Pond] Falta " + rutaFbx); continue; }

                // Materiales
                var carpetaMats = $"{carpeta}/Materiales";
                if (!AssetDatabase.IsValidFolder(carpetaMats))
                    AssetDatabase.CreateFolder(carpeta, "Materiales");
                foreach (var par in info.materiales)
                {
                    var rutaMat = $"{carpetaMats}/{par.material}.mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
                    if (mat == null)
                    {
                        mat = new Material(lit) { name = par.material };
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{carpeta}/Texturas/{par.textura}");
                        mat.SetTexture("_BaseMap", tex);
                        mat.SetTexture("_MainTex", tex);
                        mat.SetColor("_BaseColor", Color.white);
                        mat.SetFloat("_Smoothness", 0.15f);
                        AssetDatabase.CreateAsset(mat, rutaMat);
                    }
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), par.material), mat);
                }
                importer.SaveAndReimport();

                // Prefab: raiz vacia + modelo + capsula del tamano del villano
                if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
                if (!AssetDatabase.IsValidFolder(CarpetaPrefabs)) AssetDatabase.CreateFolder("Assets/Prefabs", "Villanos");
                var rutaPrefab = $"{CarpetaPrefabs}/{info.modelo}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab) != null)
                {
                    Debug.Log($"[Goldy Pond] Villano {info.modelo}: el prefab ya existe, se respeta.");
                    continue;
                }
                var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(rutaFbx);
                var raiz = new GameObject(info.modelo);
                var instancia = (GameObject)PrefabUtility.InstantiatePrefab(modelo, raiz.transform);
                instancia.name = "Modelo";
                // En Blender miran hacia -Y, que en Unity queda hacia -Z: se giran para que miren hacia +Z (adelante).
                instancia.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * instancia.transform.localRotation;
                var renders = raiz.GetComponentsInChildren<Renderer>();
                var b = renders[0].bounds;
                foreach (var r in renders) b.Encapsulate(r.bounds);
                var capsula = raiz.AddComponent<CapsuleCollider>();
                capsula.height = b.size.y;
                capsula.radius = Mathf.Min(Mathf.Max(b.size.x, b.size.z) * 0.3f, b.size.y * 0.5f);
                capsula.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
                Object.DestroyImmediate(raiz);
                Debug.Log($"[Goldy Pond] Villano {info.modelo}: prefab creado, alto {b.size.y:0.00} m, " +
                          $"ancho {b.size.x:0.00} x {b.size.z:0.00} m, {info.materiales.Count} materiales.");
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>-executeMethod GoldyPond.EditorTools.ImportarVillanos.CrearBatch</summary>
        public static void CrearBatch()
        {
            AssetDatabase.Refresh();
            Crear();
        }

        static InfoVillano LeerInfo(string carpeta)
        {
            var nombre = Path.GetFileName(carpeta);
            var ruta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, carpeta, nombre + "_materiales.json");
            if (!File.Exists(ruta)) return null;
            // El json trae "materiales" como objeto {material: textura}; JsonUtility no lee diccionarios.
            var texto = File.ReadAllText(ruta);
            var info = new InfoVillano { modelo = nombre };
            info.tiene_esqueleto = texto.Contains("\"tiene_esqueleto\": true");
            var ini = texto.IndexOf("\"materiales\"");
            var abre = texto.IndexOf('{', ini);
            var cierra = texto.IndexOf('}', abre);
            var cuerpo = texto.Substring(abre + 1, cierra - abre - 1);
            var partes = cuerpo.Split('"').Where((s, i) => i % 2 == 1).ToList();
            for (int i = 0; i + 1 < partes.Count; i += 2)
                info.materiales.Add(new Par { material = partes[i], textura = partes[i + 1] });
            return info;
        }
    }
}
