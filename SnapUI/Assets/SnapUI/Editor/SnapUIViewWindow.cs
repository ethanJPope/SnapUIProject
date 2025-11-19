using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class SnapUIViewWindow : EditorWindow
{
    private float zoom = 1f;
    private Vector2 panOffset = Vector2.zero;

    private const float MinZoom = 0.3f;
    private const float MaxZoom = 2.0f;

    private Canvas[] sceneCanvases;
    private Canvas targetCanvas;
    private CanvasPreviewHook previewHook;

    private RenderTexture previewTexture;
    private Camera previewCamera;

    private bool cameraInitialized = false;

    private Vector2 deviceResolution = new Vector2(1920, 1080);

    private string[] devicePresets = new string[]
    {
        "PC 1920x1080",
        "Phone 1080x1920",
        "Tablet 1536x2048",
        "Square 1024x1024"
    };

    private int selectedPreset = 0;
    private int selectedCanvasIndex = 0;

    [MenuItem("Window/SnapUI/SnapUI View")]
    public static void ShowWindow()
    {
        var window = GetWindow<SnapUIViewWindow>("SnapUI View");
        window.minSize = new Vector2(400, 300);
    }

    private void OnGUI()
    {
        EnsurePreviewCameraExists();
        ScanForCanvases();
        DrawToolbar();
        DrawWorkspace();
        HandleInput();

        // Always enforce camera on canvas (Unity may override it)
        if (previewHook != null)
            previewHook.ApplyPreviewCamera();
    }

    private void OnDisable()
    {
        Debug.Log("SnapUI: Window closed. Cleaning preview camera and texture.");

        if (previewHook != null)
            previewHook.previewCamera = null;

        if (previewCamera != null)
            DestroyImmediate(previewCamera.gameObject);

        if (previewTexture != null)
        {
            previewTexture.Release();
            DestroyImmediate(previewTexture);
        }

        previewCamera = null;
        previewTexture = null;
        cameraInitialized = false;
    }

    // ----------------------------------------------------------------------
    // CAMERA SETUP
    // ----------------------------------------------------------------------

    private void EnsurePreviewCameraExists()
    {
        if (cameraInitialized)
            return;

        Debug.Log("SnapUI: Creating Preview Camera.");

        GameObject camObj = new GameObject("SnapUIPreviewCamera");
        camObj.hideFlags = HideFlags.None;

        previewCamera = camObj.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
        previewCamera.orthographic = true;
        previewCamera.nearClipPlane = -50;
        previewCamera.farClipPlane = 50;
        previewCamera.cullingMask = LayerMask.GetMask("UI");

        cameraInitialized = true;
        SetupRenderTexture();
    }

    private void SetupRenderTexture()
    {
        if (previewCamera == null)
        {
            Debug.LogWarning("SnapUI: SetupRenderTexture called early; creating camera now.");
            EnsurePreviewCameraExists();
        }

        if (previewCamera == null)
        {
            Debug.LogError("SnapUI: previewCamera STILL NULL after creation attempt.");
            return;
        }

        if (previewTexture != null)
        {
            previewTexture.Release();
            DestroyImmediate(previewTexture);
        }

        previewTexture = new RenderTexture(
            Mathf.RoundToInt(deviceResolution.x),
            Mathf.RoundToInt(deviceResolution.y),
            24,
            RenderTextureFormat.ARGB32
        );

        previewTexture.Create();
        previewCamera.targetTexture = previewTexture;

        Debug.Log("SnapUI: RenderTexture assigned to preview camera.");
    }

    // ----------------------------------------------------------------------
    // REBUILD BUTTON LOGIC
    // ----------------------------------------------------------------------

    private void RebuildPreviewSystem()
    {
        Debug.Log("SnapUI: Rebuilding Preview System...");

        if (previewCamera != null)
        {
            DestroyImmediate(previewCamera.gameObject);
            previewCamera = null;
        }

        if (previewTexture != null)
        {
            previewTexture.Release();
            DestroyImmediate(previewTexture);
            previewTexture = null;
        }

        cameraInitialized = false;
        EnsurePreviewCameraExists();

        if (previewHook != null)
        {
            previewHook.previewCamera = previewCamera;
            previewHook.ApplyPreviewCamera();
        }

        Debug.Log("SnapUI: Preview system rebuilt successfully.");
    }

    // ----------------------------------------------------------------------
    // CANVAS SETUP
    // ----------------------------------------------------------------------

    private void ScanForCanvases()
    {
        if (sceneCanvases == null)
        {
            sceneCanvases = FindObjectsOfType<Canvas>();

            if (sceneCanvases.Length > 0)
            {
                selectedCanvasIndex = 0;
                SetTargetCanvas(sceneCanvases[selectedCanvasIndex]);
            }
            else
            {
                Debug.LogWarning("SnapUI: No canvases found.");
            }
        }
    }

    private void SetTargetCanvas(Canvas canvas)
    {
        Debug.Log("SnapUI: Selecting canvas: " + canvas);

        if (canvas == null) return;

        Selection.activeObject = canvas.gameObject;
        targetCanvas = canvas;

        EditorApplication.delayCall += () =>
        {
            if (targetCanvas == null) return;

            previewHook = targetCanvas.GetComponent<CanvasPreviewHook>();
            if (previewHook == null)
            {
                previewHook = targetCanvas.gameObject.AddComponent<CanvasPreviewHook>();
                Debug.Log("SnapUI: Added CanvasPreviewHook.");
            }

            previewHook.previewCamera = previewCamera;
            previewHook.ApplyPreviewCamera();
        };
    }

    // ----------------------------------------------------------------------
    // GUI
    // ----------------------------------------------------------------------

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (sceneCanvases != null)
        {
            string[] names = new string[sceneCanvases.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = sceneCanvases[i].name;

            int newIndex = EditorGUILayout.Popup(selectedCanvasIndex, names, GUILayout.Width(180));
            if (newIndex != selectedCanvasIndex)
            {
                selectedCanvasIndex = newIndex;
                SetTargetCanvas(sceneCanvases[newIndex]);
            }
        }

        GUILayout.Space(10);

        int newPreset = EditorGUILayout.Popup(selectedPreset, devicePresets, GUILayout.Width(180));
        if (newPreset != selectedPreset)
        {
            selectedPreset = newPreset;
            ApplyDevicePreset();
            SetupRenderTexture();
        }

        GUILayout.FlexibleSpace();

        // ⭐ Rebuild Preview Camera Button
        if (GUILayout.Button("Rebuild Preview Camera", EditorStyles.toolbarButton, GUILayout.Width(170)))
        {
            RebuildPreviewSystem();
        }

        // Reset View Button
        if (GUILayout.Button("Reset View", EditorStyles.toolbarButton))
        {
            zoom = 1f;
            panOffset = Vector2.zero;
        }

        GUILayout.EndHorizontal();
    }

    private void DrawWorkspace()
    {
        Rect r = new Rect(0, 20, position.width, position.height - 20);
        GUI.BeginClip(r);

        if (previewTexture != null)
        {
            float aspect = deviceResolution.x / deviceResolution.y;
            float w = deviceResolution.x * zoom * 0.25f;
            float h = w / aspect;

            Rect previewRect = new Rect(
                panOffset.x + (r.width / 2 - w / 2),
                panOffset.y + (r.height / 2 - h / 2),
                w, h
            );

            GUI.DrawTexture(previewRect, previewTexture, ScaleMode.StretchToFill);
        }

        GUI.EndClip();
    }

    private void HandleInput()
    {
        Event e = Event.current;

        if (e.type == EventType.ScrollWheel)
        {
            zoom = Mathf.Clamp(zoom - e.delta.y * 0.02f, MinZoom, MaxZoom);
            Repaint();
        }

        if (e.type == EventType.MouseDrag && e.button == 2)
        {
            panOffset += e.delta;
            Repaint();
        }
    }

    private void ApplyDevicePreset()
    {
        switch (selectedPreset)
        {
            case 0: deviceResolution = new Vector2(1920, 1080); break;
            case 1: deviceResolution = new Vector2(1080, 1920); break;
            case 2: deviceResolution = new Vector2(1536, 2048); break;
            case 3: deviceResolution = new Vector2(1024, 1024); break;
        }
    }
}
