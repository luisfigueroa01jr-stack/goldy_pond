using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoldyPond.EditorTools
{
    // Hace recogibles la Llave, el Engranaje, la Linterna y el Pergamino:
    // genera su icono (foto del modelo con fondo transparente) en Assets/Resources/Iconos
    // y les agrega ObjetoRecogible en su prefab, asi todas las copias de las escenas lo tienen.
    // Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Interfaz.
    [InitializeOnLoad]
    public static class ConfigurarObjetosRecogibles
    {
        private static readonly string[] Objetos = { "Llave", "Engranaje", "Linterna", "Pergamino" };
        private const string CARPETA_PREFABS = "Assets/Prefabs/Objetos";
        private const string CARPETA_ICONOS = "Assets/Resources/Iconos";
        private const int TAMANO_ICONO = 256;

        private static string Clave => $"GoldyPond.Recogibles.v1.{Application.dataPath}";

        static ConfigurarObjetosRecogibles()
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
            aplicar(false);
        }

        [MenuItem("Goldy Pond/Interfaz/Hacer recogibles los objetos (y regenerar iconos)")]
        private static void menu()
        {
            aplicar(true);
        }

        public static void aplicar(bool regenerarIconos)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(CARPETA_ICONOS))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Iconos");
            }

            int listos = 0;
            foreach (string nombre in Objetos)
            {
                string rutaPrefab = $"{CARPETA_PREFABS}/{nombre}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
                if (prefab == null)
                {
                    Debug.LogWarning($"[Goldy Pond] No existe {rutaPrefab}");
                    continue;
                }

                string rutaIcono = $"{CARPETA_ICONOS}/{nombre}.png";
                if (regenerarIconos || !File.Exists(rutaIcono))
                {
                    generarIcono(prefab, rutaIcono);
                }

                Sprite icono = importarComoSprite(rutaIcono);

                GameObject raiz = PrefabUtility.LoadPrefabContents(rutaPrefab);
                try
                {
                    ObjetoRecogible r = raiz.GetComponent<ObjetoRecogible>();
                    if (r == null)
                    {
                        r = raiz.AddComponent<ObjetoRecogible>();
                        r.nombre = nombre;
                    }

                    if (r.icono == null || regenerarIconos)
                    {
                        r.icono = icono;
                    }

                    PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
                    listos++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(raiz);
                }
            }

            Debug.Log($"[Goldy Pond] {listos} objetos recogibles con E (iconos en {CARPETA_ICONOS}).");
        }

        private static Sprite importarComoSprite(string ruta)
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

        // Foto del prefab en una escena aparte. Se toma dos veces (fondo negro y blanco)
        // para sacar la transparencia aunque el pipeline no guarde el canal alfa.
        private static void generarIcono(GameObject prefab, string ruta)
        {
            Scene escena = EditorSceneManager.NewPreviewScene();
            RenderTexture rt = null;
            try
            {
                GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
                foreach (Light l in obj.GetComponentsInChildren<Light>(true))
                {
                    l.enabled = false;
                }

                Renderer[] renders = obj.GetComponentsInChildren<Renderer>();
                if (renders.Length == 0)
                {
                    return;
                }

                Bounds b = renders[0].bounds;
                foreach (Renderer r in renders)
                {
                    b.Encapsulate(r.bounds);
                }

                luz(escena, Quaternion.Euler(40f, -35f, 0f), 1.3f, Color.white);
                luz(escena, Quaternion.Euler(10f, 150f, 0f), 0.6f, new Color(1f, 0.9f, 0.95f));
                luz(escena, Quaternion.Euler(-60f, 0f, 0f), 0.35f, Color.white);

                GameObject camGO = new GameObject("Camara icono");
                SceneManager.MoveGameObjectToScene(camGO, escena);
                Camera cam = camGO.AddComponent<Camera>();
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
                cam.orthographicSize = tamanoVisible(b, camGO.transform) * 0.56f;

                rt = new RenderTexture(TAMANO_ICONO, TAMANO_ICONO, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
                cam.targetTexture = rt;

                Color[] negro = tomar(cam, rt, Color.black);
                Color[] blanco = tomar(cam, rt, Color.white);

                Texture2D final = new Texture2D(TAMANO_ICONO, TAMANO_ICONO, TextureFormat.RGBA32, false);
                Color[] px = new Color[negro.Length];
                for (int i = 0; i < px.Length; i++)
                {
                    Color n = negro[i];
                    Color w = blanco[i];
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
                if (rt != null)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }

                EditorSceneManager.ClosePreviewScene(escena);
            }
        }

        private static void luz(
            Scene escena,
            Quaternion rot,
            float intensidad,
            Color color
        )
        {
            GameObject go = new GameObject("Luz icono");
            SceneManager.MoveGameObjectToScene(go, escena);
            go.transform.rotation = rot;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensidad;
            l.color = color;
            l.shadows = LightShadows.None;
        }

        private static Color[] tomar(Camera cam, RenderTexture rt, Color fondo)
        {
            cam.backgroundColor = fondo;
            cam.Render();
            RenderTexture anterior = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = anterior;
            Color[] px = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return px;
        }

        // Mitad mas grande (alto o ancho) de la caja vista desde la camara.
        private static float tamanoVisible(Bounds b, Transform cam)
        {
            float maxX = 0f, maxY = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 esquina = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = cam.InverseTransformPoint(esquina) - cam.InverseTransformPoint(b.center);
                maxX = Mathf.Max(maxX, Mathf.Abs(local.x));
                maxY = Mathf.Max(maxY, Mathf.Abs(local.y));
            }

            return Mathf.Max(maxX, maxY) * 2f;
        }
    }
}
