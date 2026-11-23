using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Menu: Goldy Pond > Crear Animator de caminar.
// Selecciona el .fbx del personaje (exportado de Blender con sus acciones)
// y crea un Animator Controller con un Blend Tree Quieto / Caminar / Correr
// controlado por el float "Velocidad" (0, 1, 2).
// Busca los clips por nombre: idle/quieto, walk/caminar, run/correr.
public static class CrearAnimatorCaminar
{
    [MenuItem("Goldy Pond/Crear Animator de caminar")]
    private static void crear()
    {
        string rutaFbx = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(rutaFbx) || !rutaFbx.ToLower().EndsWith(".fbx"))
        {
            EditorUtility.DisplayDialog("Crear Animator", "Selecciona primero el archivo .fbx del personaje en la ventana Project.", "OK");
            return;
        }

        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(rutaFbx)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToArray();

        AnimationClip buscar(params string[] claves)
        {
            return clips.FirstOrDefault(c => claves.Any(k => c.name.ToLower().Contains(k)));
        }

        AnimationClip quieto = buscar("idle", "quieto", "reposo");
        AnimationClip caminar = buscar("walk", "caminar", "camina");
        AnimationClip correr = buscar("run", "correr", "corre");

        if (caminar == null)
        {
            string nombres = clips.Length == 0 ? "(ninguna)" : string.Join(", ", clips.Select(c => c.name));
            EditorUtility.DisplayDialog("Crear Animator",
                "No encontre una animacion de caminar (nombre con 'walk' o 'caminar').\n\nAnimaciones en el FBX: " + nombres, "OK");
            return;
        }

        string rutaCtrl = System.IO.Path.ChangeExtension(rutaFbx, null) + "_Animator.controller";
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(rutaCtrl);
        ctrl.AddParameter("Velocidad", AnimatorControllerParameterType.Float);

        AnimatorState estado = ctrl.CreateBlendTreeInController("Locomocion", out BlendTree arbol);
        arbol.blendParameter = "Velocidad";
        arbol.useAutomaticThresholds = false;
        if (quieto != null)
        {
            arbol.AddChild(quieto, 0f);
        }

        arbol.AddChild(caminar, 1f);
        if (correr != null)
        {
            arbol.AddChild(correr, 2f);
        }

        ctrl.layers[0].stateMachine.defaultState = estado;

        AssetDatabase.SaveAssets();
        Selection.activeObject = ctrl;
        EditorGUIUtility.PingObject(ctrl);
        Debug.Log($"[Goldy Pond] Animator creado en {rutaCtrl} (quieto: {quieto?.name ?? "-"}, caminar: {caminar.name}, correr: {correr?.name ?? "-"}). " +
                  "Recuerda marcar Loop Time en esas animaciones (pestana Animation del FBX).");
    }
}
