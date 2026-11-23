using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    // Villanos de Camila (Modelos/Boss, Villano1-3) exportados desde Blender a
    // Assets/Personajes/Villanos/[Nombre]/[Nombre].fbx con sus texturas en Texturas/.
    // Crea un material URP Lit por cada material del .blend (con su textura) y un prefab
    // en Assets/Prefabs/Villanos con collider de capsula.
    // Menu: Goldy Pond/Villanos/Crear materiales y prefabs
    public class ImportarVillanos : AssetPostprocessor
    {
        private const string CARPETA_VILLANOS = "Assets/Personajes/Villanos";
        private const string CARPETA_PREFABS = "Assets/Prefabs/Villanos";

        [System.Serializable]
        private class Par { public string material; public string textura; }

        private class InfoVillano
        {
            public string modelo;
            public float altura;
            public bool tiene_esqueleto;
            public List<Par> materiales = new List<Par>();
        }

        private void onPreprocessModel()
        {
            if (!assetPath.StartsWith(CARPETA_VILLANOS + "/") || !(assetImporter is ModelImporter mi))
            {
                return;
            }

            if (!mi.importSettingsMissing)
            {
                return;
            }

            InfoVillano info = leerInfo(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
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

        private void onPreprocessTexture()
        {
            if (!assetPath.StartsWith(CARPETA_VILLANOS + "/") || !(assetImporter is TextureImporter ti))
            {
                return;
            }

            if (!ti.importSettingsMissing)
            {
                return;
            }

            ti.maxTextureSize = 2048;
            ti.sRGBTexture = true;
        }

        [MenuItem("Goldy Pond/Villanos/Crear materiales y prefabs")]
        public static void crear()
        {
            if (!AssetDatabase.IsValidFolder(CARPETA_VILLANOS))
            {
                Debug.LogWarning("[Goldy Pond] No existe " + CARPETA_VILLANOS);
                return;
            }

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (string carpeta in AssetDatabase.GetSubFolders(CARPETA_VILLANOS))
            {
                InfoVillano info = leerInfo(carpeta);
                if (info == null)
                {
                    continue;
                }

                string rutaFbx = $"{carpeta}/{info.modelo}.fbx";
                ModelImporter importer = AssetImporter.GetAtPath(rutaFbx) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogWarning("[Goldy Pond] Falta " + rutaFbx);
                    continue;
                }

                // Materiales
                string carpetaMats = $"{carpeta}/Materiales";
                if (!AssetDatabase.IsValidFolder(carpetaMats))
                {
                    AssetDatabase.CreateFolder(carpeta, "Materiales");
                }

                foreach (Par par in info.materiales)
                {
                    string rutaMat = $"{carpetaMats}/{par.material}.mat";
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
                    if (mat == null)
                    {
                        mat = new Material(lit) { name = par.material };
                        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{carpeta}/Texturas/{par.textura}");
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
                if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                {
                    AssetDatabase.CreateFolder("Assets", "Prefabs");
                }

                if (!AssetDatabase.IsValidFolder(CARPETA_PREFABS))
                {
                    AssetDatabase.CreateFolder("Assets/Prefabs", "Villanos");
                }

                string rutaPrefab = $"{CARPETA_PREFABS}/{info.modelo}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab) != null)
                {
                    Debug.Log($"[Goldy Pond] Villano {info.modelo}: el prefab ya existe, se respeta.");
                    continue;
                }

                GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(rutaFbx);
                GameObject raiz = new GameObject(info.modelo);
                GameObject instancia = (GameObject)PrefabUtility.InstantiatePrefab(modelo, raiz.transform);
                instancia.name = "Modelo";
                // En Blender miran hacia -Y, que en Unity queda hacia -Z: se giran para que miren hacia +Z (adelante).
                instancia.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * instancia.transform.localRotation;
                Renderer[] renders = raiz.GetComponentsInChildren<Renderer>();
                Bounds b = renders[0].bounds;
                foreach (Renderer r in renders)
                {
                    b.Encapsulate(r.bounds);
                }

                CapsuleCollider capsula = raiz.AddComponent<CapsuleCollider>();
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

        // -executeMethod GoldyPond.EditorTools.ImportarVillanos.crearBatch
        public static void crearBatch()
        {
            AssetDatabase.Refresh();
            crear();
        }

        private static InfoVillano leerInfo(string carpeta)
        {
            string nombre = Path.GetFileName(carpeta);
            string ruta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, carpeta, nombre + "_materiales.json");
            if (!File.Exists(ruta))
            {
                return null;
            }

            // El json trae "materiales" como objeto {material: textura}; JsonUtility no lee diccionarios.
            string texto = File.ReadAllText(ruta);
            InfoVillano info = new InfoVillano { modelo = nombre };
            info.tiene_esqueleto = texto.Contains("\"tiene_esqueleto\": true");
            int ini = texto.IndexOf("\"materiales\"");
            int abre = texto.IndexOf('{', ini);
            int cierra = texto.IndexOf('}', abre);
            string cuerpo = texto.Substring(abre + 1, cierra - abre - 1);
            List<string> partes = cuerpo.Split('"').Where((s, i) => i % 2 == 1).ToList();
            for (int i = 0; i + 1 < partes.Count; i += 2)
            {
                info.materiales.Add(new Par { material = partes[i], textura = partes[i + 1] });
            }

            return info;
        }
    }
}
