using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GoldyPond.EditorTools
{
    /// <summary>
    /// Configura lo que se usa con E dentro de los modelos de Blender (en los prefabs, asi las escenas lo heredan):
    ///  - Puerta en las 7 Puerta_Principal de las casas de la Aldea
    ///  - Puerta en la reja doble (Puerta_Reja_Izquierda/Derecha) del Subterraneo, que se abren juntas
    ///  - Puerta del molino (Object.021 dentro de "Molino"): cerrada, se abre con la Llave en la mano
    ///  - Libro en Libro_de_las_Reglas: con E muestra Assets/Imagenes/Reglas del juego en rojo
    ///  - Mecanismo del segundo piso del molino: le falta el engranaje; al ponerlo abre la trampilla
    ///  - Trampilla de la planta baja del molino: queda cerrada (acostada sobre su marco)
    /// Se aplica solo una vez; despues se puede repetir desde Goldy Pond > Interaccion.
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigurarPuertas
    {
        const string PrefabAldea = "Assets/Prefabs/Aldea/Goldy_Pond_Colores.prefab";
        const string PrefabSubterraneo = "Assets/Prefabs/Subterraneo/Subterraneo_Colores.prefab";
        const string ImagenReglas = "Assets/Imagenes/Reglas del juego en rojo.png";
        static readonly string[] IconosUI = { "Assets/Resources/UI/Ojo.png", "Assets/Resources/UI/Cerrar.png" };

        // v3: puerta del molino abre hacia afuera, mecanismo del engranaje y trampilla.
        static string Clave => $"GoldyPond.Puertas.v4.{Application.dataPath}";

        static ConfigurarPuertas()
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

        [MenuItem("Goldy Pond/Interaccion/Configurar puertas, molino, libro y mecanismo (E)")]
        public static void Aplicar()
        {
            foreach (var ruta in IconosUI) ImportarComoSprite(ruta, 512);
            var reglas = ImportarComoSprite(ImagenReglas, 2048);

            int n = Configurar(PrefabAldea, "Puerta_Principal", "puerta", false);
            n += Configurar(PrefabSubterraneo, "Puerta_Reja", "reja", true);
            Debug.Log($"[Goldy Pond] {n} puertas se abren y cierran con E.");

            ConfigurarMolinoYLibro(reglas);
        }

        static int Configurar(string rutaPrefab, string prefijo, string nombre, bool abrirJuntas)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab) == null)
            {
                Debug.LogWarning($"[Goldy Pond] No existe {rutaPrefab}");
                return 0;
            }

            var raiz = PrefabUtility.LoadPrefabContents(rutaPrefab);
            try
            {
                var puertas = new List<Puerta>();
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith(prefijo)) continue;
                    var p = AgregarPuerta(t, nombre);
                    puertas.Add(p);
                }

                if (abrirJuntas && puertas.Count > 1)
                    foreach (var p in puertas)
                        p.companeras = puertas.FindAll(o => o != p).ToArray();

                if (puertas.Count == 0)
                    Debug.LogWarning($"[Goldy Pond] No encontre objetos que empiecen con '{prefijo}' en {rutaPrefab}");
                else
                    PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
                return puertas.Count;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }

        static void ConfigurarMolinoYLibro(Sprite reglas)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAldea) == null) return;
            var raiz = PrefabUtility.LoadPrefabContents(PrefabAldea);
            try
            {
                Transform puertaMolino = null, libro = null, pisoMolino = null, tablero = null, ranura = null, tapa = null, marco = null;
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    bool enMolino = t.parent != null && t.parent.name == "Molino";
                    if (enMolino && t.name.StartsWith("Piso_Planta_Baja")) pisoMolino = t;
                    if (enMolino && t.name.StartsWith("Mecanismo_Tablero")) tablero = t;
                    if (enMolino && t.name.StartsWith("Ranura_Pieza_Faltante")) ranura = t;
                    if (enMolino && t.name.StartsWith("Trampilla_Tapa")) tapa = t;
                    if (enMolino && t.name.StartsWith("Trampilla_Marco")) marco = t;
                    // En Blender la puerta del molino se llama Object.021 (madera cafe oscuro, 2.76 m de alto).
                    if (t.name == "Object.021" && t.parent != null && t.parent.name == "Molino") puertaMolino = t;
                    // El nodo del libro que tiene adentro el atril, las paginas y la tapa.
                    if (t.name == "Libro_de_las_Reglas" && t.GetComponentInChildren<MeshFilter>(true) != null) libro = t;
                }

                if (puertaMolino != null)
                {
                    bool nueva = puertaMolino.GetComponent<Puerta>() == null;
                    var p = AgregarPuerta(puertaMolino, "puerta del molino");
                    if (nueva)
                    {
                        p.calcularBisagra = true;      // su origen esta al centro, no en la bisagra
                        p.objetoNecesario = "Llave";
                        p.enLaMano = true;
                        p.mensajeCerrada = "La puerta está cerrada. Necesitas una llave";
                        p.anguloApertura = 95f;
                        p.duracion = 1.3f;              // es grande y pesada: abre mas despacio
                        p.distanciaCerradura = 2.6f;
                    }
                    // Por dentro la mesa del pergamino queda justo enfrente: abre hacia afuera (lejos del piso del molino).
                    p.abrirLejosDe = pisoMolino;
                    p.anguloApertura = 80f;
                    Debug.Log("[Goldy Pond] Puerta del molino: cerrada, se abre con la Llave en la mano, hacia afuera.");
                }
                else Debug.LogWarning("[Goldy Pond] No encontre la puerta del molino (Object.021 dentro de Molino).");

                if (libro != null)
                {
                    var l = libro.GetComponent<Libro>();
                    if (l == null) l = libro.gameObject.AddComponent<Libro>();
                    if (reglas != null) l.imagen = reglas;
                    if (libro.GetComponentInChildren<Collider>(true) == null)
                        libro.gameObject.AddComponent<BoxCollider>();
                    Debug.Log($"[Goldy Pond] Libro de las reglas listo (imagen: {(l.imagen != null ? l.imagen.name : "FALTA")}).");
                }
                else Debug.LogWarning("[Goldy Pond] No encontre Libro_de_las_Reglas.");

                Trampilla trampilla = null;
                if (tapa != null && marco != null)
                {
                    trampilla = tapa.GetComponent<Trampilla>();
                    if (trampilla == null)
                    {
                        trampilla = tapa.gameObject.AddComponent<Trampilla>();
                        if (CerrarTapa(tapa, marco, trampilla)) Debug.Log("[Goldy Pond] Trampilla del molino cerrada.");
                        else Debug.LogWarning("[Goldy Pond] No pude calcular como cerrar la trampilla.");
                    }
                }
                else Debug.LogWarning("[Goldy Pond] No encontre Trampilla_Tapa / Trampilla_Marco en el molino.");

                if (tablero != null)
                {
                    var m = tablero.GetComponent<Mecanismo>();
                    if (m == null) m = tablero.gameObject.AddComponent<Mecanismo>();
                    m.ranura = ranura;
                    m.trampilla = trampilla;
                    if (ranura != null) m.frenteLocal = FrenteDeLaPieza(ranura, pisoMolino != null ? pisoMolino.position : tablero.position);
                    // Caja invisible (trigger) un poco mas grande que el tablero, para que el rayo de la camara lo detecte facil.
                    if (!tablero.GetComponents<BoxCollider>().Any(c => c.isTrigger))
                    {
                        var caja = tablero.gameObject.AddComponent<BoxCollider>();
                        caja.isTrigger = true;
                        RecogiblesAutomaticos.AjustarCaja(caja);
                    }
                    Debug.Log($"[Goldy Pond] Mecanismo del molino listo (ranura: {(ranura != null ? "si" : "FALTA")}, trampilla: {(trampilla != null ? "si" : "FALTA")}).");
                }
                else Debug.LogWarning("[Goldy Pond] No encontre Mecanismo_Tablero en el molino.");

                PrefabUtility.SaveAsPrefabAsset(raiz, PrefabAldea);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }

        /// <summary>
        /// La tapa viene levantada en el modelo. Guarda esa pose como "abierta" y la gira sobre su bisagra
        /// (el borde de abajo) hasta que queda acostada tapando el marco.
        /// </summary>
        static bool CerrarTapa(Transform tapa, Transform marco, Trampilla trampilla)
        {
            var mf = tapa.GetComponent<MeshFilter>();
            var rMarco = marco.GetComponent<Renderer>();
            if (mf == null || mf.sharedMesh == null || rMarco == null) return false;

            var puntos = mf.sharedMesh.vertices.Select(v => tapa.TransformPoint(v)).ToArray();
            float minY = puntos.Min(p => p.y);
            var bajos = puntos.Where(p => p.y < minY + 0.03f).ToArray();

            // La bisagra: los dos puntos de abajo mas separados.
            Vector3 a = bajos[0], b = bajos[0];
            float mejor = -1f;
            foreach (var p in bajos)
                foreach (var q in bajos)
                {
                    float d = (p - q).sqrMagnitude;
                    if (d > mejor) { mejor = d; a = p; b = q; }
                }
            if (mejor < 0.01f) return false;
            var eje = (b - a).normalized;
            var pivote = (a + b) * 0.5f;

            var centro = puntos.Aggregate(Vector3.zero, (s, p) => s + p) / puntos.Length;
            var brazo = centro - pivote;
            brazo -= eje * Vector3.Dot(brazo, eje);
            var objetivo = rMarco.bounds.center - pivote;
            objetivo.y = 0f;
            objetivo -= eje * Vector3.Dot(objetivo, eje);
            objetivo.y = 0f;
            if (brazo.sqrMagnitude < 1e-6f || objetivo.sqrMagnitude < 1e-6f) return false;

            trampilla.posicionAbierta = tapa.localPosition;
            trampilla.rotacionAbierta = tapa.localRotation;

            var giro = Quaternion.FromToRotation(brazo.normalized, objetivo.normalized);
            tapa.SetPositionAndRotation(pivote + giro * (tapa.position - pivote), giro * tapa.rotation);

            // Que quede apoyada encima del marco.
            float nuevoMin = mf.sharedMesh.vertices.Min(v => tapa.TransformPoint(v).y);
            tapa.position += Vector3.up * (rMarco.bounds.max.y - 0.01f - nuevoMin);
            return true;
        }

        /// <summary>
        /// La direccion de la cara mas grande de la pieza (sumando el area de sus triangulos),
        /// del lado que mira hacia "haciaDonde". Se devuelve en coordenadas de la pieza.
        /// </summary>
        static Vector3 FrenteDeLaPieza(Transform pieza, Vector3 haciaDonde)
        {
            var mf = pieza.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return Vector3.zero;
            var malla = mf.sharedMesh;
            var v = malla.vertices;
            var tris = malla.triangles;
            var areas = new Dictionary<Vector3Int, (Vector3 suma, float area)>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                var c = Vector3.Cross(v[tris[i + 1]] - v[tris[i]], v[tris[i + 2]] - v[tris[i]]);
                float area = c.magnitude * 0.5f;
                if (area < 1e-8f) continue;
                var n = c.normalized;
                // Junta las normales opuestas (las dos caras de la misma tabla) en el mismo grupo.
                var clave = Vector3Int.RoundToInt(n * 10f);
                var opuesta = Vector3Int.RoundToInt(-n * 10f);
                if (areas.ContainsKey(opuesta)) { clave = opuesta; n = -n; }
                areas.TryGetValue(clave, out var acc);
                areas[clave] = (acc.suma + n * area, acc.area + area);
            }
            if (areas.Count == 0) return Vector3.zero;
            var mejor = areas.Values.OrderByDescending(a => a.area).First().suma.normalized;
            var enMundo = pieza.TransformDirection(mejor);
            if (Vector3.Dot(enMundo, haciaDonde - pieza.position) < 0f) mejor = -mejor;
            return mejor;
        }

        static Puerta AgregarPuerta(Transform t, string nombre)
        {
            var p = t.GetComponent<Puerta>();
            if (p == null)
            {
                p = t.gameObject.AddComponent<Puerta>();
                p.nombre = nombre;
            }
            // Sin collider el rayo de la camara no la detecta.
            if (t.GetComponentInChildren<Collider>() == null)
                t.gameObject.AddComponent<BoxCollider>();
            return p;
        }

        /// <summary>Se asegura de que el PNG se importe como Sprite (para usarlo en la interfaz) y lo devuelve.</summary>
        static Sprite ImportarComoSprite(string ruta, int tamanoMaximo)
        {
            var importer = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Goldy Pond] No existe {ruta}");
                return null;
            }
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = tamanoMaximo;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        }
    }
}
