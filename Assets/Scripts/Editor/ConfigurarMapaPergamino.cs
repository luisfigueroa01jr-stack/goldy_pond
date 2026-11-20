using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    /// <summary>
    ///  - Mapa: pone Assets/Imagenes/Mapa en la tabla del letrero del subterraneo (Tablero_Mapa, junto al elevador),
    ///    por los dos lados, como una lamina pegada (el modelo no tiene UV para usarla de textura).
    ///  - Pergamino: con el Pergamino seleccionado en la barra sale "(E) Ver mensaje" y mientras mantienes E
    ///    se ve Assets/Imagenes/Pergamino_William_Minerva (MensajesObjetos en los modelos de las dos escenas).
    /// Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Interaccion.
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigurarMapaPergamino
    {
        const string PrefabAldea = "Assets/Prefabs/Aldea/Goldy_Pond_Colores.prefab";
        const string PrefabSubterraneo = "Assets/Prefabs/Subterraneo/Subterraneo_Colores.prefab";
        const string ImagenMapa = "Assets/Imagenes/Mapa.png";
        const string ImagenPergamino = "Assets/Imagenes/Pergamino_William_Minerva.png";
        const string MaterialMapa = "Assets/Materials/Mapa.mat";

        static string Clave => $"GoldyPond.MapaPergamino.v1.{Application.dataPath}";

        static ConfigurarMapaPergamino()
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

        [MenuItem("Goldy Pond/Interaccion/Mapa del subterraneo y mensaje del pergamino")]
        public static void Aplicar()
        {
            var mapa = ImportarComoSprite(ImagenMapa);
            var pergamino = ImportarComoSprite(ImagenPergamino);

            PonerMensajes(PrefabAldea, pergamino);
            PonerMensajes(PrefabSubterraneo, pergamino);
            PonerMapa(mapa);
        }

        static void PonerMensajes(string rutaPrefab, Sprite pergamino)
        {
            if (pergamino == null || AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab) == null) return;
            var raiz = PrefabUtility.LoadPrefabContents(rutaPrefab);
            try
            {
                var m = raiz.GetComponent<MensajesObjetos>();
                if (m == null) m = raiz.AddComponent<MensajesObjetos>();
                var lista = m.mensajes.Where(x => x != null && x.objeto != "Pergamino").ToList();
                lista.Add(new MensajesObjetos.Mensaje { objeto = "Pergamino", imagen = pergamino });
                m.mensajes = lista.ToArray();
                PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
            Debug.Log($"[Goldy Pond] Mensaje del Pergamino listo en {System.IO.Path.GetFileNameWithoutExtension(rutaPrefab)}.");
        }

        static void PonerMapa(Sprite mapa)
        {
            if (mapa == null || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSubterraneo) == null) return;
            var material = CrearMaterial(mapa.texture);

            var raiz = PrefabUtility.LoadPrefabContents(PrefabSubterraneo);
            try
            {
                var tabla = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Tablero_Mapa"));
                var mf = tabla != null ? tabla.GetComponent<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null) { Debug.LogWarning("[Goldy Pond] No encontre Tablero_Mapa en el Subterraneo."); return; }

                var malla = mf.sharedMesh;
                var puntos = malla.vertices.Select(v => tabla.TransformPoint(v)).ToArray();
                var normal = CaraMasGrande(tabla, malla);
                var centro = puntos.Aggregate(Vector3.zero, (s, p) => s + p) / puntos.Length;

                // Ejes de la cara: lo ancho (horizontal) y lo alto (lo que queda).
                var ancho = Vector3.Cross(Vector3.up, normal);
                if (ancho.sqrMagnitude < 0.01f) ancho = Vector3.Cross(Vector3.forward, normal);
                ancho.Normalize();
                var alto = Vector3.Cross(normal, ancho).normalized;
                float Medida(Vector3 eje) => puntos.Max(p => Vector3.Dot(p - centro, eje)) - puntos.Min(p => Vector3.Dot(p - centro, eje));
                float w = Medida(ancho), h = Medida(alto), grosor = Medida(normal);

                // La imagen cabe completa en la tabla, con un pequeno margen.
                float aspecto = mapa.rect.width / mapa.rect.height;
                float anchoImg = w * 0.94f, altoImg = anchoImg / aspecto;
                if (altoImg > h * 0.94f) { altoImg = h * 0.94f; anchoImg = altoImg * aspecto; }

                var padre = tabla.parent != null ? tabla.parent : raiz.transform;
                var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                foreach (var (nombre, lado) in new[] { ("Mapa (frente)", 1f), ("Mapa (atras)", -1f) })
                {
                    var n = normal * lado;
                    var t = padre.Find(nombre);
                    if (t == null)
                    {
                        t = new GameObject(nombre, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                        t.SetParent(padre, false);
                    }
                    t.GetComponent<MeshFilter>().sharedMesh = quad;
                    var r = t.GetComponent<MeshRenderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    // El Quad se ve desde su -Z: se voltea para que mire hacia afuera de la tabla.
                    // Con "alto" como arriba, el mapa queda derecho visto desde cualquiera de los dos lados.
                    t.SetPositionAndRotation(centro + n * (grosor * 0.5f + 0.004f), Quaternion.LookRotation(-n, alto));
                    var esc = padre.lossyScale;
                    t.localScale = new Vector3(anchoImg / esc.x, altoImg / esc.y, 1f / esc.z);
                }

                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabSubterraneo);
                Debug.Log($"[Goldy Pond] Mapa puesto en la tabla del subterraneo ({anchoImg:0.00} x {altoImg:0.00} m, por los dos lados).");
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        /// <summary>Normal (en mundo) de la cara mas grande de la malla.</summary>
        static Vector3 CaraMasGrande(Transform t, Mesh malla)
        {
            var v = malla.vertices;
            var tris = malla.triangles;
            var areas = new Dictionary<Vector3Int, (Vector3 n, float a)>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = t.TransformPoint(v[tris[i]]);
                var c = Vector3.Cross(t.TransformPoint(v[tris[i + 1]]) - a, t.TransformPoint(v[tris[i + 2]]) - a);
                float area = c.magnitude * 0.5f;
                if (area < 1e-8f) continue;
                var n = c.normalized;
                var clave = Vector3Int.RoundToInt(n * 10f);
                var opuesta = Vector3Int.RoundToInt(-n * 10f);
                if (areas.ContainsKey(opuesta)) { clave = opuesta; n = -n; }
                areas.TryGetValue(clave, out var acc);
                areas[clave] = (n, acc.a + area);
            }
            return areas.Count == 0 ? t.forward : areas.Values.OrderByDescending(x => x.a).First().n.normalized;
        }

        static Material CrearMaterial(Texture2D textura)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialMapa);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent Cutout");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MaterialMapa);
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

        static Sprite ImportarComoSprite(string ruta)
        {
            var importer = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (importer == null) { Debug.LogWarning($"[Goldy Pond] No existe {ruta}"); return null; }
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
