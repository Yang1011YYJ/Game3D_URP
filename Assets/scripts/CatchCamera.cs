using UnityEngine;

public class CatchCamera : MonoBehaviour
{
    public Canvas canvas;

    Camera currentAssignedCam;

    void Awake()
    {
        if (canvas == null)
            canvas = GetComponent<Canvas>();

        TryAssignCamera();
    }

    void LateUpdate()
    {
        TryAssignCamera();
    }

    void TryAssignCamera()
    {
        if (canvas == null) return;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return;

        Camera activeCam = GetActiveCamera();
        if (activeCam == null) return;

        // ✅ 只有「真的換了」才設
        if (currentAssignedCam != activeCam)
        {
            currentAssignedCam = activeCam;
            canvas.worldCamera = activeCam;
            // Debug.Log($"[CatchCamera] Canvas 改用 Camera: {activeCam.name}");
        }
    }

    Camera GetActiveCamera()
    {
        Camera[] cams = Camera.allCameras;

        Camera best = null;
        float bestDepth = float.MinValue;

        foreach (var cam in cams)
        {
            if (!cam.enabled) continue;
            if (!cam.gameObject.activeInHierarchy) continue;

            if (cam.depth > bestDepth)
            {
                bestDepth = cam.depth;
                best = cam;
            }
        }

        return best;
    }
}
