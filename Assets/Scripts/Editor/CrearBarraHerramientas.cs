using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GoldyPond.EditorTools
{
    /// <summary>
    /// Pone la barra de herramientas (HUD con 5 casillas) abajo de la pantalla en Aldea, Subterraneo y Laberinto.
    /// Se aplica sola una vez por escena; despues se puede repetir desde el menu Goldy Pond > Interfaz.
    /// </summary>
    [InitializeOnLoad]
    public static class CrearBarraHerramientas
    {
        const string CarpetaSprites = "Assets/UI/Barra de herramientas";
        static readonly string[] Escenas = { "Aldea", "Subterraneo", "Laberinto" };

        // (nombre, borde 9-slice izquierda/abajo/derecha/arriba)
        static readonly (string archivo, Vector4 borde)[] Sprites =
        {
            ("Marco", new Vector4(48, 48, 48, 48)),
            ("Casilla", new Vector4(40, 40, 40, 40)),
            ("CasillaSeleccionada", new Vector4(44, 44, 44, 44)),
            ("Laurel", Vector4.zero),
        };

        static string Clave(string escena) => $"GoldyPond.BarraHerramientas.{Application.dataPath}.{escena}";

        static CrearBarraHerramientas()
        {
            EditorApplication.delayCall += AplicarPendientes;
        }

        static void AplicarPendientes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AplicarPendientes;
                return;
            }
            foreach (var e in Escenas)
                if (!EditorPrefs.GetBool(Clave(e), false))
                    AplicarAEscena(e);
        }

        [MenuItem("Goldy Pond/Interfaz/Agregar barra de herramientas a Aldea y Subterraneo")]
        static void MenuAmbas()
        {
            foreach (var e in Escenas)
                AplicarAEscena(e);
        }

        [MenuItem("Goldy Pond/Interfaz/Agregar barra de herramientas a la escena abierta")]
        static void MenuEscenaAbierta()
        {
            var escena = SceneManager.GetActiveScene();
            if (Configurar(escena))
                EditorSceneManager.MarkSceneDirty(escena);
        }

        [MenuItem("Goldy Pond/Interfaz/Reconstruir barra seleccionada")]
        static void MenuReconstruir()
        {
            var barra = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<BarraHerramientas>()
                : Object.FindFirstObjectByType<BarraHerramientas>();
            if (barra == null) { Debug.LogWarning("[Goldy Pond] No hay ninguna BarraHerramientas en la escena."); return; }
            Undo.RegisterFullObjectHierarchyUndo(barra.gameObject, "Reconstruir barra");
            AsignarSprites(barra);
            barra.Construir();
            EditorSceneManager.MarkSceneDirty(barra.gameObject.scene);
        }

        static void AplicarAEscena(string nombre)
        {
            var ruta = $"Assets/Scenes/{nombre}.unity";
            if (!System.IO.File.Exists(ruta))
                return;

            var escena = SceneManager.GetSceneByPath(ruta);
            bool yaAbierta = escena.IsValid() && escena.isLoaded;
            if (!yaAbierta)
            {
                bool haySinTitulo = Enumerable.Range(0, SceneManager.sceneCount)
                    .Any(i => string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path));
                if (haySinTitulo)
                    return; // se reintenta en la proxima recarga
                escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
            }

            bool estabaSucia = escena.isDirty;
            bool cambio = Configurar(escena);
            EditorPrefs.SetBool(Clave(nombre), true);

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
                EditorSceneManager.CloseScene(escena, true);
        }

        /// <summary>Crea el Canvas "HUD" con la barra. Devuelve false si la escena ya tenia una barra.</summary>
        public static bool Configurar(Scene escena)
        {
            var raices = escena.GetRootGameObjects();
            if (raices.Any(r => r.GetComponentInChildren<BarraHerramientas>(true) != null))
                return false;

            PrepararSprites();

            var hud = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(hud, escena);
            hud.layer = LayerMask.NameToLayer("UI");
            var canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var escalador = hud.GetComponent<CanvasScaler>();
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1920, 1080);
            escalador.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            escalador.matchWidthOrHeight = 0.5f;

            var go = new GameObject("Barra de herramientas", typeof(RectTransform));
            go.layer = hud.layer;
            go.transform.SetParent(hud.transform, false);
            var barra = go.AddComponent<BarraHerramientas>();
            AsignarSprites(barra);
            barra.Construir();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = hud.layer;

            Undo.RegisterCreatedObjectUndo(hud, "Agregar barra de herramientas");
            return true;
        }

        static void AsignarSprites(BarraHerramientas barra)
        {
            barra.spriteMarco = Cargar("Marco");
            barra.spriteCasilla = Cargar("Casilla");
            barra.spriteCasillaSeleccionada = Cargar("CasillaSeleccionada");
            barra.spriteLaurel = Cargar("Laurel");
            EditorUtility.SetDirty(barra);
        }

        static Sprite Cargar(string archivo) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{CarpetaSprites}/{archivo}.png");

        /// <summary>Importa los PNG como Sprite (UI) con sus bordes 9-slice. Solo los cambia si no son Sprite todavia.</summary>
        static void PrepararSprites()
        {
            foreach (var (archivo, borde) in Sprites)
            {
                var ruta = $"{CarpetaSprites}/{archivo}.png";
                if (!(AssetImporter.GetAtPath(ruta) is TextureImporter imp))
                {
                    Debug.LogWarning($"[Goldy Pond] Falta {ruta}");
                    continue;
                }
                if (imp.textureType == TextureImporterType.Sprite && Cargar(archivo) != null)
                    continue;

                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                var ajustes = new TextureImporterSettings();
                imp.ReadTextureSettings(ajustes);
                ajustes.spriteBorder = borde;
                ajustes.spriteMeshType = SpriteMeshType.FullRect;
                imp.SetTextureSettings(ajustes);
                imp.SaveAndReimport();
            }
        }
    }
}
