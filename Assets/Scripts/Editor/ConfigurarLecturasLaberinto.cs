using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    /// <summary>
    /// Lo que se lee en el laberinto:
    ///  - La Hoja: al recogerla y traerla en la mano, mantener E muestra Assets/Imagenes/mensaje oculto.
    ///  - El Libro del inicio: "(E) Leer" muestra Assets/Imagenes/libro del laberinto.
    /// Importa las dos imagenes como Sprite y las registra en MensajesObjetos del laberinto
    /// y tambien en la Aldea y el Subterraneo (por si te llevas la hoja, o si el laberinto esta metido en la Aldea).
    /// Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Laberinto.
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigurarLecturasLaberinto
    {
        const string ImagenHoja = "Assets/Imagenes/mensaje oculto.png";
        const string ImagenLibro = "Assets/Imagenes/libro del laberinto.png";
        const string PrefabLaberinto = "Assets/Prefabs/laberinto/laberinto_colores.prefab";
        static readonly string[] OtrosPrefabs = { "Assets/Prefabs/Aldea/Goldy_Pond_Colores.prefab", "Assets/Prefabs/Subterraneo/Subterraneo_Colores.prefab" };
        static string Clave => $"GoldyPond.LecturasLaberinto.v2.{Application.dataPath}";

        static ConfigurarLecturasLaberinto()
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

        [MenuItem("Goldy Pond/Laberinto/Hoja y libro (mensajes)")]
        public static void Aplicar()
        {
            var hoja = ImportarComoSprite(ImagenHoja);
            var libro = ImportarComoSprite(ImagenLibro);
            if (hoja == null || libro == null) { Debug.LogWarning("[Goldy Pond] Faltan las imagenes \"mensaje oculto\" o \"libro del laberinto\" en Assets/Imagenes."); return; }

            Poner(PrefabLaberinto, ("Hoja", hoja), ("Libro del laberinto", libro));
            foreach (var p in OtrosPrefabs) Poner(p, ("Hoja", hoja), ("Libro del laberinto", libro)); // el laberinto tambien puede estar metido en la Aldea
            Debug.Log("[Goldy Pond] Hoja (mensaje oculto) y libro del laberinto listos.");
        }

        static void Poner(string rutaPrefab, params (string objeto, Sprite imagen)[] mensajes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab) == null) { Debug.LogWarning($"[Goldy Pond] No existe {rutaPrefab}"); return; }
            var raiz = PrefabUtility.LoadPrefabContents(rutaPrefab);
            try
            {
                var m = raiz.GetComponent<MensajesObjetos>();
                if (m == null) m = raiz.AddComponent<MensajesObjetos>();
                var lista = m.mensajes.Where(x => x != null && !mensajes.Any(n => n.objeto == x.objeto)).ToList();
                foreach (var (objeto, imagen) in mensajes)
                    lista.Add(new MensajesObjetos.Mensaje { objeto = objeto, imagen = imagen });
                m.mensajes = lista.ToArray();
                PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        static Sprite ImportarComoSprite(string ruta)
        {
            var importer = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || importer.maxTextureSize < 2048)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        }
    }
}
