using UnityEngine;

public class VRModeCoordinator : MonoBehaviour
{
    [SerializeField] private XRRuntimeRigInstaller rigInstaller;
    [SerializeField] private XRControllerPoseProvider rightController;
    [SerializeField] private Transform hmdTransform;
    [SerializeField] private Canvas gameUiCanvas;
    [SerializeField] private Vector3 uiOffset = new Vector3(0f, -0.15f, 1.1f);

    private void Start()
    {
        Screen.fullScreen = true;
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;

        if (UWCharacter.Instance != null)
        {
            UWCharacter.Instance.MouseLookEnabled = false;
            if (UWCharacter.Instance.XAxis != null) { UWCharacter.Instance.XAxis.enabled = false; }
            if (UWCharacter.Instance.YAxis != null) { UWCharacter.Instance.YAxis.enabled = false; }
        }

        if (hmdTransform == null)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                hmdTransform = cam.transform;
            }
        }

        if (rightController == null && rigInstaller != null)
        {
            rightController = rigInstaller.GetRightControllerProvider();
        }

        if (gameUiCanvas == null)
        {
            var gameUi = GameObject.Find("GameUI");
            if (gameUi != null)
            {
                gameUiCanvas = gameUi.GetComponentInChildren<Canvas>(true);
            }
        }

        if (gameUiCanvas != null)
        {
            gameUiCanvas.renderMode = RenderMode.WorldSpace;
        }
    }

    private void LateUpdate()
    {
        if (gameUiCanvas != null && hmdTransform != null)
        {
            var anchor = hmdTransform.TransformPoint(uiOffset);
            gameUiCanvas.transform.position = anchor;
            gameUiCanvas.transform.rotation = Quaternion.LookRotation(gameUiCanvas.transform.position - hmdTransform.position);
        }

        if (rightController != null && rightController.IsSecondaryUsePressed() && gameUiCanvas != null)
        {
            gameUiCanvas.gameObject.SetActive(!gameUiCanvas.gameObject.activeSelf);
        }
    }
}
