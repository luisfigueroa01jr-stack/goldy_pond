using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoldyPond.EditorTools
{
    /// <summary>
    /// Hace recogibles la Llave, el Engranaje, la Linterna y el Pergamino:
    /// genera su icono (foto del modelo con fondo transparente) en Assets/Resources/Iconos
    /// y les agrega ObjetoRecogible en su prefab, asi todas las copias de las escenas lo tienen.
    /// Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Interfaz.
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigurarObjetosRecogibles
    {
        static readonly string[] Objetos = { "Llave", "Engranaje", "Linterna", "Pergamino" };
        const string CarpetaPrefabs = "Assets/Prefabs/Objetos";
        const string CarpetaIconos = "Assets/Resources/Iconos";
        const int TamanoIcono = 256;

        static string Clave => $"GoldyPond.Recogibles.v1.{Application.dataPath}";

        static ConfigurarObjetosRecogibles()
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
            Aplicar(false);
        }

        [MenuItem("Goldy Pond/Interfaz/Hacer recogibles los objetos (y regenerar iconos)")]
        static void Menu() => Aplicar(true);

        public static void Aplicar(bool regenerarIconos)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(CarpetaIconos)) AssetDatabase.CreateFolder("Assets/Resources", "Iconos");

            int listos = 0;
            foreach (var nombre in Objetos)
            {
                var rutaPrefab = $"{CarpetaPrefabs}/{nombre}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
                if (prefab == null) { Debug.LogWarning($"[Goldy Pond] No existe {rutaPrefab}"); continue; }

                var rutaIcono = $"{CarpetaIconos}/{nombre}.png";
                if (regenerarIconos || !File.Exists(rutaIcono))
                    GenerarIcono(prefab, rutaIcono);
                var icono = ImportarComoSprite(rutaIcono);

                var raiz = PrefabUtility.LoadPrefabContents(rutaPrefab);
                try
                {
                    var r = raiz.GetComponent<ObjetoRecogible>();
                    if (r == null)
                    {
                        r = raiz.AddComponent<ObjetoRecogible>();
                        r.nombre = nombre;
                    }
                    if (r.icono == null || regenerarIconos) r.icono = icono;
                    PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
                    listos++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(raiz);
                }
            }
            Debug.Log($"[Goldy Pond] {listos} objetos recogibles con E (iconos en {CarpetaIconos}).");
        }

        static Sprite ImportarComoSprite(string ruta)
        {
            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(ruta) is TextureImporter imp && imp.textureType != TextureImporterType.Sprite)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        }

        /// <summary>
        /// Foto del prefab en una escena aparte. Se toma dos veces (fondo negro y blanco)
        /// para sacar la transparencia aunque el pipeline no guarde el canal alfa.
        /// </summary>
        static void GenerarIcono(GameObject prefab, string ruta)
        {
            var escena = EditorSceneManager.NewPreviewScene();
            RenderTexture rt = null;
            try
            {
                var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
                foreach (var l in obj.GetComponentsInChildren<Light>(true)) l.enabled = false;

                var renders = obj.GetComponentsInChildren<Renderer>();
                if (renders.Length == 0) return;
                var b = renders[0].bounds;
                foreach (var r in renders) b.Encapsulate(r.bounds);

                Luz(escena, Quaternion.Euler(40f, -35f, 0f), 1.3f, Color.white);
                Luz(escena, Quaternion.Euler(10f, 150f, 0f), 0.6f, new Color(1f, 0.9f, 0.95f));
                Luz(escena, Quaternion.Euler(-60f, 0f, 0f), 0.35f, Color.white);

                var camGO = new GameObject("Camara icono");
                SceneManager.MoveGameObjectToScene(camGO, escena);
                var cam = camGO.AddComponent<Camera>();
                cam.scene = escena;
                cam.cameraType = CameraType.Preview;
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.allowHDR = false;
                cam.allowMSAA = true;
                // Un poco desde arriba y de lado, como icono de inventario.
                camGO.transform.rotation = Quaternion.Euler(18f, -28f, 0f);
                float radio = b.extents.magnitude;
                camGO.transform.position = b.center - camGO.transform.forward * (radio * 4f);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = radio * 10f;
                cam.orthographicSize = TamanoVisible(b, camGO.transform) * 0.56f;

                rt = new RenderTexture(TamanoIcono, TamanoIcono, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
                cam.targetTexture = rt;

                var negro = Tomar(cam, rt, Color.black);
                var blanco = Tomar(cam, rt, Color.white);

                var final = new Texture2D(TamanoIcono, TamanoIcono, TextureFormat.RGBA32, false);
                var px = new Color[negro.Length];
                for (int i = 0; i < px.Length; i++)
                {
                    var n = negro[i];
                    var w = blanco[i];
                    float a = Mathf.Clamp01(1f - ((w.r - n.r) + (w.g - n.g) + (w.b - n.b)) / 3f);
                    px[i] = a < 0.004f ? Color.clear : new Color(n.r / a, n.g / a, n.b / a, a);
                }
                final.SetPixels(px);
                final.Apply();
                File.WriteAllBytes(ruta, final.EncodeToPNG());
                Object.DestroyImmediate(final);
                cam.targetTexture = null;
            }
            finally
            {
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                EditorSceneManager.ClosePreviewScene(escena);
            }
        }

        static void Luz(Scene escena, Quaternion rot, float intensidad, Color color)
        {
            var go = new GameObject("Luz icono");
            SceneManager.MoveGameObjectToScene(go, escena);
            go.transform.rotation = rot;
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensidad;
            l.color = color;
            l.shadows = LightShadows.None;
        }

        static Color[] Tomar(Camera cam, RenderTexture rt, Color fondo)
        {
            cam.backgroundColor = fondo;
            cam.Render();
            var anterior = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = anterior;
            var px = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return px;
        }

        /// <summary>Mitad mas grande (alto o ancho) de la caja vista desde la camara.</summary>
        static float TamanoVisible(Bounds b, Transform cam)
        {
            float maxX = 0f, maxY = 0f;
            for (int i = 0; i < 8; i++)
            {
                var esquina = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var local = cam.InverseTransformPoint(esquina) - cam.InverseTransformPoint(b.center);
                maxX = Mathf.Max(maxX, Mathf.Abs(local.x));
                maxY = Mathf.Max(maxY, Mathf.Abs(local.y));
            }
            return Mathf.Max(maxX, maxY) * 2f;
        }
    }
}
