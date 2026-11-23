using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    // - Mapa: pone Assets/Imagenes/Mapa en la tabla del letrero del subterraneo (Tablero_Mapa, junto al elevador),
    // por los dos lados, como una lamina pegada (el modelo no tiene UV para usarla de textura).
    // - Pergamino: con el Pergamino seleccionado en la barra sale "(E) Ver mensaje" y mientras mantienes E
    // se ve Assets/Imagenes/Pergamino_William_Minerva (MensajesObjetos en los modelos de las dos escenas).
    // Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Interaccion.
    [InitializeOnLoad]
    public static class ConfigurarMapaPergamino
    {
        private const string PREFAB_ALDEA = "Assets/Prefabs/Aldea/Goldy_Pond_Colores.prefab";
        private const string PREFAB_SUBTERRANEO = "Assets/Prefabs/Subterraneo/Subterraneo_Colores.prefab";
        private const string IMAGEN_MAPA = "Assets/Imagenes/Mapa.png";
        private const string IMAGEN_PERGAMINO = "Assets/Imagenes/Pergamino_William_Minerva.png";
        private const string MATERIAL_MAPA = "Assets/Materials/Mapa.mat";

        private static string Clave => $"GoldyPond.MapaPergamino.v1.{Application.dataPath}";

        static ConfigurarMapaPergamino()
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

        [MenuItem("Goldy Pond/Interaccion/Mapa del subterraneo y mensaje del pergamino")]
        public static void aplicar()
        {
            Sprite mapa = importarComoSprite(IMAGEN_MAPA);
            Sprite pergamino = importarComoSprite(IMAGEN_PERGAMINO);

            ponerMensajes(PREFAB_ALDEA, pergamino);
            ponerMensajes(PREFAB_SUBTERRANEO, pergamino);
            ponerMapa(mapa);
        }

        private static void ponerMensajes(string rutaPrefab, Sprite pergamino)
        {
            if (pergamino == null || AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab) == null)
            {
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

                List<MensajesObjetos.Mensaje> lista = m.mensajes.Where(x => x != null && x.objeto != "Pergamino").ToList();
                lista.Add(new MensajesObjetos.Mensaje { objeto = "Pergamino", imagen = pergamino });
                m.mensajes = lista.ToArray();
                PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }

            Debug.Log($"[Goldy Pond] Mensaje del Pergamino listo en {System.IO.Path.GetFileNameWithoutExtension(rutaPrefab)}.");
        }

        private static void ponerMapa(Sprite mapa)
        {
            if (mapa == null || AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_SUBTERRANEO) == null)
            {
                return;
            }

            Material material = crearMaterial(mapa.texture);

            GameObject raiz = PrefabUtility.LoadPrefabContents(PREFAB_SUBTERRANEO);
            try
            {
                Transform tabla = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Tablero_Mapa"));
                MeshFilter mf = tabla != null ? tabla.GetComponent<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null)
                {
                    Debug.LogWarning("[Goldy Pond] No encontre Tablero_Mapa en el Subterraneo.");
                    return;
                }

                Mesh malla = mf.sharedMesh;
                Vector3[] puntos = malla.vertices.Select(v => tabla.TransformPoint(v)).ToArray();
                Vector3 normal = caraMasGrande(tabla, malla);
                Vector3 centro = puntos.Aggregate(Vector3.zero, (s, p) => s + p) / puntos.Length;

                // Ejes de la cara: lo ancho (horizontal) y lo alto (lo que queda).
                Vector3 ancho = Vector3.Cross(Vector3.up, normal);
                if (ancho.sqrMagnitude < 0.01f)
                {
                    ancho = Vector3.Cross(Vector3.forward, normal);
                }

                ancho.Normalize();
                Vector3 alto = Vector3.Cross(normal, ancho).normalized;
                float medida(Vector3 eje)
                {
                    return puntos.Max(p => Vector3.Dot(p - centro, eje)) - puntos.Min(p => Vector3.Dot(p - centro, eje));
                }

                float w = medida(ancho), h = medida(alto), grosor = medida(normal);

                // La imagen cabe completa en la tabla, con un pequeno margen.
                float aspecto = mapa.rect.width / mapa.rect.height;
                float anchoImg = w * 0.94f, altoImg = anchoImg / aspecto;
                if (altoImg > h * 0.94f)
                {
                    altoImg = h * 0.94f;
                    anchoImg = altoImg * aspecto;
                }

                Transform padre = tabla.parent != null ? tabla.parent : raiz.transform;
                Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                foreach (var (nombre, lado) in new[] { ("Mapa (frente)", 1f), ("Mapa (atras)", -1f) })
                {
                    Vector3 n = normal * lado;
                    Transform t = padre.Find(nombre);
                    if (t == null)
                    {
                        t = new GameObject(nombre, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                        t.SetParent(padre, false);
                    }

                    t.GetComponent<MeshFilter>().sharedMesh = quad;
                    MeshRenderer r = t.GetComponent<MeshRenderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    // El Quad se ve desde su -Z: se voltea para que mire hacia afuera de la tabla.
                    // Con "alto" como arriba, el mapa queda derecho visto desde cualquiera de los dos lados.
                    t.SetPositionAndRotation(centro + n * (grosor * 0.5f + 0.004f), Quaternion.LookRotation(-n, alto));
                    Vector3 esc = padre.lossyScale;
                    t.localScale = new Vector3(anchoImg / esc.x, altoImg / esc.y, 1f / esc.z);
                }

                PrefabUtility.SaveAsPrefabAsset(raiz, PREFAB_SUBTERRANEO);
                Debug.Log($"[Goldy Pond] Mapa puesto en la tabla del subterraneo ({anchoImg:0.00} x {altoImg:0.00} m, por los dos lados).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }

        // Normal (en mundo) de la cara mas grande de la malla.
        private static Vector3 caraMasGrande(Transform t, Mesh malla)
        {
            Vector3[] v = malla.vertices;
            int[] tris = malla.triangles;
            Dictionary<Vector3Int, (Vector3 n, float a)> areas = new Dictionary<Vector3Int, (Vector3 n, float a)>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = t.TransformPoint(v[tris[i]]);
                Vector3 c = Vector3.Cross(t.TransformPoint(v[tris[i + 1]]) - a, t.TransformPoint(v[tris[i + 2]]) - a);
                float area = c.magnitude * 0.5f;
                if (area < 1e-8f)
                {
                    continue;
                }

                Vector3 n = c.normalized;
                Vector3Int clave = Vector3Int.RoundToInt(n * 10f);
                Vector3Int opuesta = Vector3Int.RoundToInt(-n * 10f);
                if (areas.ContainsKey(opuesta))
                {
                    clave = opuesta;
                    n = -n;
                }

                areas.TryGetValue(clave, out var acc);
                areas[clave] = (n, acc.a + area);
            }

            return areas.Count == 0 ? t.forward : areas.Values.OrderByDescending(x => x.a).First().n.normalized;
        }

        private static Material crearMaterial(Texture2D textura)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_MAPA);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent Cutout");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MATERIAL_MAPA);
            }

            mat.SetTexture("_BaseMap", textura);
            mat.SetTexture("_MainTex", textura);
            mat.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.92f)); // un poquito apagado para que no "brille" en la cueva
            // Lo transparente del PNG no se dibuja: ahi se ve la tabla.
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cull", 2f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Sprite importarComoSprite(string ruta)
        {
            TextureImporter importer = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Goldy Pond] No existe {ruta}");
                return null;
            }

            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || importer.maxTextureSize < 2048)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true; // el mapa se ve de lejos en 3D
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        }
    }
}
