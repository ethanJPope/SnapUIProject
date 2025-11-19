using UnityEngine;

[ExecuteAlways]
public class CanvasPreviewHook : MonoBehaviour
{
    [Tooltip("Camera used to render the canvas inside the SnapUI View.")]
    public Camera previewCamera;

    [HideInInspector]
    public Camera originalCamera;

    private Canvas canvas;

    private void OnEnable()
    {
        canvas = GetComponent<Canvas>();

        if (canvas == null)
        {
            Debug.LogError("SnapUI: CanvasPreviewHook must be placed on a Canvas object.");
            return;
        }

        // Store the original camera so we can restore later
        originalCamera = canvas.worldCamera;
    }

    /// <summary>
    /// Forces the canvas to render using the preview camera.
    /// Called by SnapUIViewWindow every repaint.
    /// </summary>
    public void ApplyPreviewCamera()
    {
        if (canvas == null || previewCamera == null)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = previewCamera;
        canvas.planeDistance = 1;
    }

    /// <summary>
    /// Called when the SnapUI window closes, restoring the original camera.
    /// </summary>
    public void RestoreOriginal()
    {
        if (canvas == null)
            return;

        canvas.worldCamera = originalCamera;
    }
}
