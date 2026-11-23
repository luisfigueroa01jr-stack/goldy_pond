using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    // Lo que se lee en el laberinto:
    // - La Hoja: al recogerla y traerla en la mano, mantener E muestra Assets/Imagenes/mensaje oculto.
    // - El Libro del inicio: "(E) Leer" muestra Assets/Imagenes/libro del laberinto.
    // Importa las dos imagenes como Sprite y las registra en MensajesObjetos del laberinto
    // y tambien en la Aldea y el Subterraneo (por si te llevas la hoja, o si el laberinto esta metido en la Aldea).
    // Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Laberinto.
    [InitializeOnLoad]
    public static class ConfigurarLecturasLaberinto
    {
        private const string IMAGEN_HOJA = "Assets/Imagenes/mensaje oculto.png";
        private const string IMAGEN_LIBRO = "Assets/Imagenes/libro del laberinto.png";
        private const string PREFAB_LABERINTO = "Assets/Prefabs/laberinto/laberinto_colores.prefab";
        private static readonly string[] OtrosPrefabs = { "Assets/Prefabs/Aldea/Goldy_Pond_Colores.prefab", "Assets/Prefabs/Subterraneo/Subterraneo_Colores.prefab" };
        private static string Clave => $"GoldyPond.LecturasLaberinto.v2.{Application.dataPath}";

        static ConfigurarLecturasLaberinto()
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

        [MenuItem("Goldy Pond/Laberinto/Hoja y libro (mensajes)")]
        public static void aplicar()
        {
            Sprite hoja = importarComoSprite(IMAGEN_HOJA);
            Sprite libro = importarComoSprite(IMAGEN_LIBRO);
            if (hoja == null || libro == null)
            {
                Debug.LogWarning("[Goldy Pond] Faltan las imagenes \"mensaje oculto\" o \"libro del laberinto\" en Assets/Imagenes.");
                return;
            }

            poner(PREFAB_LABERINTO, ("Hoja", hoja), ("Libro del laberinto", libro));
            foreach (string p in OtrosPrefabs)
            {
                poner(p, ("Hoja", hoja), ("Libro del laberinto", libro)); // el laberinto tambien puede estar metido en la Aldea
            }

            Debug.Log("[Goldy Pond] Hoja (mensaje oculto) y libro del laberinto listos.");
        }

        private static void poner(
            string rutaPrefab,
            params (string objeto, Sprite imagen)[] mensajes
        )
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab) == null)
            {
                Debug.LogWarning($"[Goldy Pond] No existe {rutaPrefab}");
                return;
            }

            GameObject raiz = PrefabUtility.LoadPrefabContents(rutaPrefab);
            try
            {
                MensajesObjetos m = raiz.GetComponent<MensajesObjetos>();
                if (m == null)
                {
                    m = raiz.AddComponent<MensajesObjetos>();
                }

                System.Collections.Generic.List<MensajesObjetos.Mensaje> lista = m.mensajes.Where(x => x != null && !mensajes.Any(n => n.objeto == x.objeto)).ToList();
                foreach (var (objeto, imagen) in mensajes)
                    lista.Add(new MensajesObjetos.Mensaje { objeto = objeto, imagen = imagen });
                m.mensajes = lista.ToArray();
                PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }

        private static Sprite importarComoSprite(string ruta)
        {
            TextureImporter importer = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (importer == null)
            {
                return null;
            }

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
