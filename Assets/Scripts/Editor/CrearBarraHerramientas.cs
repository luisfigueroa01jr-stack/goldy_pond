using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GoldyPond.EditorTools
{
    // Pone la barra de herramientas (HUD con 5 casillas) abajo de la pantalla en Aldea, Subterraneo y Laberinto.
    // Se aplica sola una vez por escena; despues se puede repetir desde el menu Goldy Pond > Interfaz.
    [InitializeOnLoad]
    public static class CrearBarraHerramientas
    {
        private const string CARPETA_SPRITES = "Assets/UI/Barra de herramientas";
        private static readonly string[] Escenas = { "Aldea", "Subterraneo", "Laberinto" };

        // (nombre, borde 9-slice izquierda/abajo/derecha/arriba)
        private static readonly (string archivo, Vector4 borde)[] Sprites =
        {
            ("Marco", new Vector4(48, 48, 48, 48)),
            ("Casilla", new Vector4(40, 40, 40, 40)),
            ("CasillaSeleccionada", new Vector4(44, 44, 44, 44)),
            ("Laurel", Vector4.zero),
        };

        private static string clave(string escena)
        {
            return $"GoldyPond.BarraHerramientas.{Application.dataPath}.{escena}";
        }

        static CrearBarraHerramientas()
        {
            EditorApplication.delayCall += aplicarPendientes;
        }

        private static void aplicarPendientes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += aplicarPendientes;
                return;
            }

            foreach (string e in Escenas)
            {
                if (!EditorPrefs.GetBool(clave(e), false))
                {
                    aplicarAEscena(e);
                }
            }
        }

        [MenuItem("Goldy Pond/Interfaz/Agregar barra de herramientas a Aldea y Subterraneo")]
        private static void menuAmbas()
        {
            foreach (string e in Escenas)
            {
                aplicarAEscena(e);
            }
        }

        [MenuItem("Goldy Pond/Interfaz/Agregar barra de herramientas a la escena abierta")]
        private static void menuEscenaAbierta()
        {
            Scene escena = SceneManager.GetActiveScene();
            if (configurar(escena))
            {
                EditorSceneManager.MarkSceneDirty(escena);
            }
        }

        [MenuItem("Goldy Pond/Interfaz/Reconstruir barra seleccionada")]
        private static void menuReconstruir()
        {
            BarraHerramientas barra = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<BarraHerramientas>()
                : Object.FindFirstObjectByType<BarraHerramientas>();
            if (barra == null)
            {
                Debug.LogWarning("[Goldy Pond] No hay ninguna BarraHerramientas en la escena.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(barra.gameObject, "Reconstruir barra");
            asignarSprites(barra);
            barra.construir();
            EditorSceneManager.MarkSceneDirty(barra.gameObject.scene);
        }

        private static void aplicarAEscena(string nombre)
        {
            string ruta = $"Assets/Scenes/{nombre}.unity";
            if (!System.IO.File.Exists(ruta))
            {
                return;
            }

            Scene escena = SceneManager.GetSceneByPath(ruta);
            bool yaAbierta = escena.IsValid() && escena.isLoaded;
            if (!yaAbierta)
            {
                bool haySinTitulo = Enumerable.Range(0, SceneManager.sceneCount)
                    .Any(i => string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path));
                if (haySinTitulo)
                {
                    return; // se reintenta en la proxima recarga
                }

                escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
            }

            bool estabaSucia = escena.isDirty;
            bool cambio = configurar(escena);
            EditorPrefs.SetBool(clave(nombre), true);

            if (cambio)
            {
                if (yaAbierta && estabaSucia)
                {
                    EditorSceneManager.MarkSceneDirty(escena);
                    Debug.Log($"[Goldy Pond] Barra de herramientas agregada a {nombre}. Guarda la escena con Ctrl+S.");
                }
                else
                {
                    EditorSceneManager.SaveScene(escena);
                    Debug.Log($"[Goldy Pond] Barra de herramientas agregada y guardada en {ruta}.");
                }
            }

            if (!yaAbierta)
            {
                EditorSceneManager.CloseScene(escena, true);
            }
        }

        // Crea el Canvas "HUD" con la barra. Devuelve false si la escena ya tenia una barra.
        public static bool configurar(Scene escena)
        {
            GameObject[] raices = escena.GetRootGameObjects();
            if (raices.Any(r => r.GetComponentInChildren<BarraHerramientas>(true) != null))
            {
                return false;
            }

            prepararSprites();

            GameObject hud = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(hud, escena);
            hud.layer = LayerMask.NameToLayer("UI");
            Canvas canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler escalador = hud.GetComponent<CanvasScaler>();
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1920, 1080);
            escalador.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            escalador.matchWidthOrHeight = 0.5f;

            GameObject go = new GameObject("Barra de herramientas", typeof(RectTransform));
            go.layer = hud.layer;
            go.transform.SetParent(hud.transform, false);
            BarraHerramientas barra = go.AddComponent<BarraHerramientas>();
            asignarSprites(barra);
            barra.construir();
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = hud.layer;
            }

            Undo.RegisterCreatedObjectUndo(hud, "Agregar barra de herramientas");
            return true;
        }

        private static void asignarSprites(BarraHerramientas barra)
        {
            barra.spriteMarco = cargar("Marco");
            barra.spriteCasilla = cargar("Casilla");
            barra.spriteCasillaSeleccionada = cargar("CasillaSeleccionada");
            barra.spriteLaurel = cargar("Laurel");
            EditorUtility.SetDirty(barra);
        }

        private static Sprite cargar(string archivo)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{CARPETA_SPRITES}/{archivo}.png");
        }

        // Importa los PNG como Sprite (UI) con sus bordes 9-slice. Solo los cambia si no son Sprite todavia.
        private static void prepararSprites()
        {
            foreach (var (archivo, borde) in Sprites)
            {
                string ruta = $"{CARPETA_SPRITES}/{archivo}.png";
                if (!(AssetImporter.GetAtPath(ruta) is TextureImporter imp))
                {
                    Debug.LogWarning($"[Goldy Pond] Falta {ruta}");
                    continue;
                }

                if (imp.textureType == TextureImporterType.Sprite && cargar(archivo) != null)
                {
                    continue;
                }

                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                TextureImporterSettings ajustes = new TextureImporterSettings();
                imp.ReadTextureSettings(ajustes);
                ajustes.spriteBorder = borde;
                ajustes.spriteMeshType = SpriteMeshType.FullRect;
                imp.SetTextureSettings(ajustes);
                imp.SaveAndReimport();
            }
        }
    }
}
