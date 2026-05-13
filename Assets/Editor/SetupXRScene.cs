#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

public static class SetupXRScene
{
    [MenuItem("Tools/VR/Setup XR Origin In Active Scene")]
    public static void SetupInActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("No active loaded scene found.");
            return;
        }

        var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
        if (origin == null)
        {
            var go = new GameObject("XR Origin");
            Undo.RegisterCreatedObjectUndo(go, "Create XR Origin");
            origin = go.AddComponent<XROrigin>();

            var offset = new GameObject("Camera Offset");
            Undo.RegisterCreatedObjectUndo(offset, "Create Camera Offset");
            offset.transform.SetParent(go.transform, false);
            origin.CameraFloorOffsetObject = offset;

            var camObj = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(camObj, "Create XR Camera");
            camObj.tag = "MainCamera";
            camObj.transform.SetParent(offset.transform, false);
            camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            origin.Camera = camObj.GetComponent<Camera>();
        }

        EnsureController(origin, "Left Controller", true);
        EnsureController(origin, "Right Controller", false);
        EnsureInstaller(origin);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("XR scene setup complete. Save scene to persist XR Origin/controller wiring.");
    }

    private static void EnsureController(XROrigin origin, string controllerName, bool left)
    {
        var existing = origin.transform.Find("Camera Offset/" + controllerName);
        GameObject controllerObj;
        if (existing == null)
        {
            controllerObj = new GameObject(controllerName);
            Undo.RegisterCreatedObjectUndo(controllerObj, "Create " + controllerName);
            controllerObj.transform.SetParent(origin.CameraFloorOffsetObject.transform, false);
        }
        else
        {
            controllerObj = existing.gameObject;
        }

        var provider = controllerObj.GetComponent<XRControllerPoseProvider>();
        if (provider == null)
        {
            provider = Undo.AddComponent<XRControllerPoseProvider>(controllerObj);
        }

        if (left)
        {
            controllerObj.name = "Left Controller";
        }
        else
        {
            controllerObj.name = "Right Controller";
        }
    }

    private static void EnsureInstaller(XROrigin origin)
    {
        var installer = Object.FindAnyObjectByType<XRRuntimeRigInstaller>(FindObjectsInactive.Include);
        if (installer == null)
        {
            installer = Undo.AddComponent<XRRuntimeRigInstaller>(origin.gameObject);
        }

        var left = origin.transform.Find("Camera Offset/Left Controller")?.GetComponent<XRControllerPoseProvider>();
        var right = origin.transform.Find("Camera Offset/Right Controller")?.GetComponent<XRControllerPoseProvider>();

        var so = new SerializedObject(installer);
        so.FindProperty("sceneXrOrigin").objectReferenceValue = origin;
        so.FindProperty("leftControllerPoseProvider").objectReferenceValue = left;
        so.FindProperty("rightControllerPoseProvider").objectReferenceValue = right;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
