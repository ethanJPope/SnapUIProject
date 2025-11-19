using UnityEngine;

[ExecuteAlways]
public class CanvasPreviewHook : MonoBehaviour
{
    public Camera previewCamera;

    [HideInInspector]
    public Camera originalCamera;

    private Canvas canvas;

    private void OnEnable()
    {
        canvas = GetComponent<Canvas>();

        if (canvas == null)
        {
            return;
        }

        originalCamera = canvas.worldCamera;
    }
    private void Update()
    {
        if (canvas != null && previewCamera != null && canvas.worldCamera != previewCamera)
        {
            ApplyPreviewCamera();
        }
    }


    public void ApplyPreviewCamera()
    { 
        if (canvas == null || previewCamera == null)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = previewCamera;
        canvas.planeDistance = 0.1f;
        canvas.sortingOrder = 5000;
    }

    public void RestoreOriginal()
    {
        if (canvas == null)
            return;

        canvas.worldCamera = originalCamera;
    }
}
