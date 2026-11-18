using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoldyPond.EditorTools
{
    /// <summary>
    /// Pone un jugador en primera persona (JugadorPrimeraPersona + camara a la altura de los ojos)
    /// en las escenas Aldea y Subterraneo, y desactiva la camara panoramica.
    /// Se aplica sola una vez por escena; despues se puede repetir desde el menu Goldy Pond.
    /// </summary>
    [InitializeOnLoad]
    public static class CrearJugadorPrimeraPersona
    {
        static readonly string[] Escenas = { "Aldea", "Subterraneo" };

        static string Clave(string escena) => $"GoldyPond.JugadorFPS.{Application.dataPath}.{escena}";

        static CrearJugadorPrimeraPersona()
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

        [MenuItem("Goldy Pond/Jugador en primera persona/Agregar a Aldea y Subterraneo")]
        static void MenuAmbas()
        {
            foreach (var e in Escenas)
                AplicarAEscena(e);
        }

        [MenuItem("Goldy Pond/Jugador en primera persona/Agregar a la escena abierta")]
        static void MenuEscenaAbierta()
        {
            var escena = SceneManager.GetActiveScene();
            if (Configurar(escena, escena.name))
                EditorSceneManager.MarkSceneDirty(escena);
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
                    return; // Unity no deja abrir escenas aditivas con una sin titulo; se reintenta en la proxima recarga.
                escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
            }

            bool estabaSucia = escena.isDirty;
            bool cambio = Configurar(escena, nombre);
            EditorPrefs.SetBool(Clave(nombre), true);

            if (cambio)
            {
                if (yaAbierta && estabaSucia)
                {
                    EditorSceneManager.MarkSceneDirty(escena);
                    Debug.Log($"[Goldy Pond] Jugador en primera persona agregado a {nombre}. Guarda la escena con Ctrl+S.");
                }
                else
                {
                    EditorSceneManager.SaveScene(escena);
                    Debug.Log($"[Goldy Pond] Jugador en primera persona agregado y guardado en {ruta}.");
                }
            }
            if (!yaAbierta)
                EditorSceneManager.CloseScene(escena, true);
        }

        /// <summary>Agrega el jugador a la escena. Devuelve false si ya tenia uno.</summary>
        public static bool Configurar(Scene escena, string nombre)
        {
            var raices = escena.GetRootGameObjects();
            if (raices.Any(r => r.GetComponentInChildren<JugadorPrimeraPersona>(true) != null))
                return false;

            // Camara panoramica: se desactiva (no se borra) por si se quiere usar para cinematicas.
            Color? fondo = null;
            foreach (var cam in raices.SelectMany(r => r.GetComponentsInChildren<Camera>(true)))
            {
                if (cam.clearFlags == CameraClearFlags.SolidColor) fondo = cam.backgroundColor;
                cam.gameObject.tag = "Untagged";
                cam.gameObject.name = "Camara panoramica (desactivada)";
                cam.gameObject.SetActive(false);
            }

            Physics.SyncTransforms();
            ObtenerInicio(raices, nombre, out var posicion, out var mirarHacia);

            var jugador = new GameObject("Jugador");
            SceneManager.MoveGameObjectToScene(jugador, escena);
            var cc = jugador.AddComponent<CharacterController>();
            cc.height = 1.2f;
            cc.radius = 0.2f;
            cc.center = new Vector3(0f, 0.6f, 0f);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;
            jugador.AddComponent<JugadorPrimeraPersona>();
            jugador.transform.position = posicion;
            var dir = Vector3.ProjectOnPlane(mirarHacia - posicion, Vector3.up);
            if (dir.sqrMagnitude > 0.01f)
                jugador.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            var ojos = new GameObject("Camara");
            ojos.tag = "MainCamera";
            ojos.transform.SetParent(jugador.transform, false);
            ojos.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var camara = ojos.AddComponent<Camera>();
            camara.nearClipPlane = 0.05f;
            camara.farClipPlane = 500f;
            camara.fieldOfView = 70f;
            if (fondo.HasValue)
            {
                camara.clearFlags = CameraClearFlags.SolidColor;
                camara.backgroundColor = fondo.Value;
            }
            ojos.AddComponent<AudioListener>();

            Undo.RegisterCreatedObjectUndo(jugador, "Agregar jugador en primera persona");
            return true;
        }

        static void ObtenerInicio(GameObject[] raices, string nombre, out Vector3 posicion, out Vector3 mirarHacia)
        {
            Vector3 xz;
            mirarHacia = Vector3.zero;
            if (nombre == "Aldea")
            {
                // Justo adentro del arco de la entrada, mirando hacia la plaza.
                var arco = raices.SelectMany(r => r.GetComponentsInChildren<Renderer>(true))
                                 .FirstOrDefault(r => r.name.StartsWith("Arco_Entrada"));
                var puerta = arco != null ? arco.bounds.center : new Vector3(0f, 0f, -43f);
                var haciaCentro = Vector3.ProjectOnPlane(-puerta, Vector3.up).normalized;
                if (haciaCentro == Vector3.zero) haciaCentro = Vector3.forward;
                xz = puerta + haciaCentro * 6f;
            }
            else if (nombre == "Subterraneo")
            {
                // Inicio del puente, mirando al elevador.
                xz = new Vector3(0f, 0f, -11f);
                mirarHacia = new Vector3(0f, 0f, 0.3f);
            }
            else
            {
                var vista = SceneView.lastActiveSceneView;
                xz = vista != null ? vista.pivot : Vector3.zero;
                mirarHacia = xz + Vector3.forward;
            }

            // Altura del suelo: esfera del ancho del jugador hacia abajo desde poco arriba
            // (no desde el cielo: la cueva tiene techo; y un rayo fino se cuela entre las tablas del puente).
            float suelo = 0f;
            var golpes = Physics.SphereCastAll(new Vector3(xz.x, 3f, xz.z), 0.3f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore)
                                .Where(g => g.distance > 0f).ToArray();
            if (golpes.Length > 0)
                suelo = golpes.OrderBy(g => g.distance).First().point.y;
            posicion = new Vector3(xz.x, suelo + 0.05f, xz.z);
            mirarHacia.y = posicion.y;
        }
    }
}
