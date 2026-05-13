#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

public static class SetupXRScene
{
    private const string ActionsAssetPath = "Assets/XRI/Actions/UnderworldXRActions.inputactions";

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
            if (camObj.GetComponent<XRHmdViewPoseProvider>() == null)
            {
                camObj.AddComponent<XRHmdViewPoseProvider>();
            }
        }

        EnsureController(origin, "Left Controller");
        EnsureController(origin, "Right Controller");
        EnsureInstaller(origin);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("XR scene setup complete. Save scene to persist XR Origin/controller wiring.");
    }

    [MenuItem("Tools/VR/Wire Default XR Controller Actions")]
    public static void WireDefaultControllerActions()
    {
        var asset = LoadOrCreateActionsAsset();
        var leftProvider = FindControllerProvider("Left Controller");
        var rightProvider = FindControllerProvider("Right Controller");

        if (leftProvider == null || rightProvider == null)
        {
            Debug.LogError("Could not find Left Controller or Right Controller with XRControllerPoseProvider. Run scene setup first.");
            return;
        }

        AssignProviderActions(leftProvider, asset, "LeftTrigger", "LeftGrip", "LeftThumbstick", "LeftSecondary");
        AssignProviderActions(rightProvider, asset, "RightTrigger", "RightGrip", "RightThumbstick", "RightSecondary");

        EditorUtility.SetDirty(leftProvider);
        EditorUtility.SetDirty(rightProvider);
        Debug.Log("Default XR controller actions wired to XRControllerPoseProvider components.");
    }

    private static void AssignProviderActions(XRControllerPoseProvider provider, InputActionAsset asset, string trigger, string grip, string stick, string secondary)
    {
        var so = new SerializedObject(provider);
        SetActionProperty(so.FindProperty("triggerAction"), asset, trigger);
        SetActionProperty(so.FindProperty("gripAction"), asset, grip);
        SetActionProperty(so.FindProperty("thumbstickAction"), asset, stick);
        SetActionProperty(so.FindProperty("secondaryUseAction"), asset, secondary);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetActionProperty(SerializedProperty property, InputActionAsset asset, string actionName)
    {
        var action = asset.FindAction(actionName, throwIfNotFound: true);
        property.FindPropertyRelative("m_UseReference").boolValue = false;
        property.FindPropertyRelative("m_Action").stringValue = action.ToString();
    }

    private static XRControllerPoseProvider FindControllerProvider(string controllerName)
    {
        var controller = GameObject.Find(controllerName);
        return controller != null ? controller.GetComponent<XRControllerPoseProvider>() : null;
    }

    private static InputActionAsset LoadOrCreateActionsAsset()
    {
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsAssetPath);
        if (asset != null)
        {
            return asset;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ActionsAssetPath));
        asset = ScriptableObject.CreateInstance<InputActionAsset>();
        var map = new InputActionMap("XRControllers");

        var leftTrigger = map.AddAction("LeftTrigger", InputActionType.Button, "<XRController>{LeftHand}/triggerPressed");
        var rightTrigger = map.AddAction("RightTrigger", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");
        var leftGrip = map.AddAction("LeftGrip", InputActionType.Value, "<XRController>{LeftHand}/grip");
        var rightGrip = map.AddAction("RightGrip", InputActionType.Value, "<XRController>{RightHand}/grip");
        var leftThumb = map.AddAction("LeftThumbstick", InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis");
        var rightThumb = map.AddAction("RightThumbstick", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
        var leftSecondary = map.AddAction("LeftSecondary", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
        var rightSecondary = map.AddAction("RightSecondary", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");

        _ = leftTrigger; _ = rightTrigger; _ = leftGrip; _ = rightGrip; _ = leftThumb; _ = rightThumb; _ = leftSecondary; _ = rightSecondary;

        asset.AddActionMap(map);
        AssetDatabase.CreateAsset(asset, ActionsAssetPath);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static void EnsureController(XROrigin origin, string controllerName)
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
            _ = Undo.AddComponent<XRControllerPoseProvider>(controllerObj);
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
        var hmd = origin.Camera != null ? origin.Camera.GetComponent<XRHmdViewPoseProvider>() : null;
        if (hmd == null && origin.Camera != null)
        {
            hmd = Undo.AddComponent<XRHmdViewPoseProvider>(origin.Camera.gameObject);
        }

        so.FindProperty("rightControllerPoseProvider").objectReferenceValue = right;
        so.FindProperty("hmdViewPoseProvider").objectReferenceValue = hmd;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
