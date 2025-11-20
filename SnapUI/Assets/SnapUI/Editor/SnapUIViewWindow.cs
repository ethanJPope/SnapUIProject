using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class SnapUIViewWindow : EditorWindow
{
    private float zoom = 1f;
    private Vector2 panOffset = Vector2.zero;

    private const float MinZoom = 0.01f;
    private const float MaxZoom = 4.0f;

    private Rect workspaceRect;
    private Rect lastPreviewRect;
    private RectTransform selectedRect;
    private bool isDragging = false;
    private Vector2 lastMousePos;

    private Color selectedColor = Color.yellow;
    private Color hierarchyColor = new Color(0.7f, 0.7f, 0.7f);

    private RectTransform lastClickedRect;
    private double lastClickTime;
    private const double clickInterval = 0.25;

    private Canvas[] sceneCanvases;
    private Canvas targetCanvas;
    private CanvasPreviewHook previewHook;

    private RenderTexture previewTexture;
    private Camera previewCamera;

    private bool cameraInitialized = false;

    private int[] gridSizes = new int[] { 0, 4, 8, 10, 16, 32, 64 };
    private string[] gridLabels = new string[] { "No Grid", "GridSize: 4 px", "GridSize: 8 px", "GridSize: 10 px", "GridSize: 16 px", "GridSize: 32 px", "GridSize: 64 px" };
    private int selectedGridIndex = 0;

    private bool snapToUI = true;
    private float snapThreshold = 10f;

    private bool showGuideX = false;
    private bool showGuideY = false;
    private float guideX = 0f;
    private float guideY = 0f;

    private Vector2 userMotion;

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

        if (previewHook != null)
            previewHook.ApplyPreviewCamera();
    }

    private void OnDisable()
    {
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

    private void EnsurePreviewCameraExists()
    {
        if (cameraInitialized)
            return;

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
            return;

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
    }

    private void RebuildPreviewSystem()
    {
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
    }

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
        }
    }

    private void SetTargetCanvas(Canvas canvas)
    {
        if (canvas == null)
            return;

        Selection.activeObject = canvas.gameObject;
        targetCanvas = canvas;

        previewHook = targetCanvas.GetComponent<CanvasPreviewHook>();
        if (previewHook == null)
            previewHook = targetCanvas.gameObject.AddComponent<CanvasPreviewHook>();

        previewHook.previewCamera = previewCamera;
        previewHook.ApplyPreviewCamera();
    }


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

        int newGrid = EditorGUILayout.Popup(selectedGridIndex, gridLabels, GUILayout.Width(130));
        if (newGrid != selectedGridIndex)
        {
            selectedGridIndex = newGrid;
            Repaint();
        }

        bool newSnap = GUILayout.Toggle(
            snapToUI,
            snapToUI ? "Smart Align: On" : "Smart Align: Off",
            EditorStyles.toolbarButton
        );
        if (newSnap != snapToUI)
            snapToUI = newSnap;

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Rebuild Preview Camera", EditorStyles.toolbarButton, GUILayout.Width(170)))
            RebuildPreviewSystem();

        if (GUILayout.Button("Reset View", EditorStyles.toolbarButton))
        {
            zoom = 1f;
            panOffset = Vector2.zero;
        }

        GUILayout.EndHorizontal();
    }



    private void DrawWorkspace()
    {
        workspaceRect = new Rect(0, 20, position.width, position.height - 20);
        GUI.BeginClip(workspaceRect);

        if (previewTexture != null)
        {
            float aspect = deviceResolution.x / deviceResolution.y;
            float w = deviceResolution.x * zoom * 0.25f;
            float h = w / aspect;

            lastPreviewRect = new Rect(
                panOffset.x + (workspaceRect.width / 2f - w / 2f),
                panOffset.y + (workspaceRect.height / 2f - h / 2f),
                w,
                h
            );

            GUI.DrawTexture(lastPreviewRect, previewTexture, ScaleMode.StretchToFill);

            if (gridSizes[selectedGridIndex] > 0)
                DrawGrid(lastPreviewRect, gridSizes[selectedGridIndex]);
        }

        if (selectedRect != null)
        {
            DrawOutline(selectedRect, selectedColor);

            Transform parent = selectedRect.parent;
            if (parent != null && parent != targetCanvas.transform)
            {
                RectTransform parentRect = parent as RectTransform;
                if (parentRect != null)
                    DrawOutline(parentRect, hierarchyColor);

                foreach (Transform s in parent)
                {
                    RectTransform sibling = s as RectTransform;
                    if (sibling != null && sibling != selectedRect)
                        DrawOutline(sibling, hierarchyColor);
                }
            }
        }

        DrawSnapGuides();

        GUI.EndClip();
    }


    private void DrawSnapGuides()
    {
        if (!showGuideX && !showGuideY)
            return;

        if (previewCamera == null || previewTexture == null || selectedRect == null)
            return;

        Handles.BeginGUI();
        Handles.color = new Color(1f, 0.2f, 0.2f, 0.9f);

        if (showGuideX)
        {
            Vector3 w = selectedRect.parent.TransformPoint(new Vector3(guideX, 0f, 0f));
            Vector3 s = previewCamera.WorldToScreenPoint(w);

            float guiX =
                lastPreviewRect.x + (s.x / previewTexture.width) * lastPreviewRect.width;

            Handles.DrawLine(
                new Vector3(guiX, lastPreviewRect.y),
                new Vector3(guiX, lastPreviewRect.y + lastPreviewRect.height)
            );
        }

        if (showGuideY)
        {
            Vector3 w = selectedRect.parent.TransformPoint(new Vector3(0f, guideY, 0f));
            Vector3 s = previewCamera.WorldToScreenPoint(w);

            float guiY = lastPreviewRect.y +
                        lastPreviewRect.height -
                        (s.y / previewTexture.height) * lastPreviewRect.height;

            Handles.DrawLine(
                new Vector3(lastPreviewRect.x, guiY),
                new Vector3(lastPreviewRect.x + lastPreviewRect.width, guiY)
            );
        }

        Handles.EndGUI();
    }



    private void DrawGrid(Rect rect, int gridSize)
    {
        if (gridSize <= 0)
            return;

        Handles.BeginGUI();
        Handles.color = new Color(0f, 0f, 0f, 0.1f);

        float cellW = (gridSize / deviceResolution.x) * rect.width;
        float cellH = (gridSize / deviceResolution.y) * rect.height;

        for (float x = rect.x; x <= rect.x + rect.width; x += cellW)
            Handles.DrawLine(new Vector3(x, rect.yMin), new Vector3(x, rect.yMax));

        for (float y = rect.y; y <= rect.y + rect.height; y += cellH)
            Handles.DrawLine(new Vector3(rect.xMin, y), new Vector3(rect.xMax, y));

        Handles.EndGUI();
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

        if (e.type == EventType.MouseDown && e.button == 0)
            TrySelectUIElement(e.mousePosition);

        if (e.type == EventType.MouseUp)
        {
            isDragging = false;
            showGuideX = false;
            showGuideY = false;
        }

        if (isDragging && selectedRect != null)
        {
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                Vector2 delta = e.mousePosition - lastMousePos;
                lastMousePos = e.mousePosition;
                DragUIElement(delta);
                Repaint();
            }

            if (e.type == EventType.MouseUp)
                isDragging = false;
        }
    }



    private bool TryGetPreviewScreenPoint(Vector2 mousePos, out Vector2 previewScreen)
    {
        previewScreen = Vector2.zero;

        if (previewCamera == null || previewTexture == null)
            return false;

        if (!workspaceRect.Contains(mousePos))
            return false;

        Vector2 localMouse = mousePos - new Vector2(workspaceRect.x, workspaceRect.y);

        if (!lastPreviewRect.Contains(localMouse))
            return false;

        float normX = (localMouse.x - lastPreviewRect.x) / lastPreviewRect.width;
        float normY = (localMouse.y - lastPreviewRect.y) / lastPreviewRect.height;

        previewScreen = new Vector2(
            normX * previewTexture.width,
            (1f - normY) * previewTexture.height
        );

        return true;
    }


    private void DragUIElement(Vector2 mouseDelta)
    {
        if (selectedRect == null || targetCanvas == null || previewTexture == null)
            return;

        if (lastPreviewRect.width <= 0f || lastPreviewRect.height <= 0f)
            return;

        Vector2 previewDelta = new Vector2(
            mouseDelta.x / lastPreviewRect.width * previewTexture.width,
            -mouseDelta.y / lastPreviewRect.height * previewTexture.height
        );

        float scaleFactor = targetCanvas.scaleFactor;
        Vector2 canvasDelta = previewDelta / scaleFactor;

        userMotion = canvasDelta;

        Vector2 newPos = selectedRect.anchoredPosition + canvasDelta;

        int gridSize = gridSizes[selectedGridIndex];
        if (gridSize > 0)
        {
            newPos.x = Mathf.Round(newPos.x / gridSize) * gridSize;
            newPos.y = Mathf.Round(newPos.y / gridSize) * gridSize;
        }

        newPos = ApplyUISnapping(newPos, selectedRect);

        selectedRect.anchoredPosition = newPos;
        EditorUtility.SetDirty(selectedRect);
    }



    private Vector2 ApplyUISnapping(Vector2 desiredPos, RectTransform moving)
    {
        if (!snapToUI || targetCanvas == null || moving == null)
            return desiredPos;

        RectTransform parent = moving.parent as RectTransform;
        if (parent == null)
            return desiredPos;

        if (userMotion.magnitude > snapThreshold * 0.8f)
        {
            showGuideX = false;
            showGuideY = false;
            return desiredPos;
        }

        float detachDistance = snapThreshold * 1.5f;

        Vector2 originalPos = moving.anchoredPosition;

        moving.anchoredPosition = desiredPos;
        Bounds testBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, moving);
        moving.anchoredPosition = originalPos;

        float mLeft = testBounds.min.x;
        float mRight = testBounds.max.x;
        float mMidX = testBounds.center.x;

        float mBottom = testBounds.min.y;
        float mTop = testBounds.max.y;
        float mMidY = testBounds.center.y;

        float bestXDist = snapThreshold + 1f;
        float bestYDist = snapThreshold + 1f;
        float bestDeltaX = 0f;
        float bestDeltaY = 0f;

        RectTransform[] rects = targetCanvas.GetComponentsInChildren<RectTransform>(false);

        foreach (RectTransform r in rects)
        {
            if (r == null || r == moving)
                continue;

            Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, r);

            float oLeft = b.min.x;
            float oRight = b.max.x;
            float oMidX = b.center.x;

            float oBottom = b.min.y;
            float oTop = b.max.y;
            float oMidY = b.center.y;

            TrySnapAxis(mLeft, oLeft, ref bestXDist, ref bestDeltaX);
            TrySnapAxis(mRight, oRight, ref bestXDist, ref bestDeltaX);
            TrySnapAxis(mMidX, oMidX, ref bestXDist, ref bestDeltaX);
            TrySnapAxis(mLeft, oRight, ref bestXDist, ref bestDeltaX);
            TrySnapAxis(mRight, oLeft, ref bestXDist, ref bestDeltaX);

            TrySnapAxis(mBottom, oBottom, ref bestYDist, ref bestDeltaY);
            TrySnapAxis(mTop, oTop, ref bestYDist, ref bestDeltaY);
            TrySnapAxis(mMidY, oMidY, ref bestYDist, ref bestDeltaY);
            TrySnapAxis(mBottom, oTop, ref bestYDist, ref bestDeltaY);
            TrySnapAxis(mTop, oBottom, ref bestYDist, ref bestDeltaY);
        }

        Vector2 result = desiredPos;

        showGuideX = false;
        showGuideY = false;

    if (bestXDist <= snapThreshold && Mathf.Abs(userMotion.x) < detachDistance)
    {
        float strength = 1f - (bestXDist / snapThreshold);
        strength = Mathf.Clamp01(strength);

        float softDelta = bestDeltaX * strength;

        result.x += softDelta;

        showGuideX = true;
        guideX = testBounds.center.x + softDelta;
    }



        if (bestYDist <= snapThreshold && Mathf.Abs(userMotion.y) < detachDistance)
        {
            float strength = 1f - (bestYDist / snapThreshold);
            strength = Mathf.Clamp01(strength);

            float softDeltaY = bestDeltaY * strength;
            result.y += softDeltaY;
            showGuideY = true;
            guideY = testBounds.center.y + softDeltaY;
        }

        return result;
    }


    private void TrySnapAxis(float from, float to, ref float bestDist, ref float bestDelta)
    {
        float dist = Mathf.Abs(from - to);
        if (dist < bestDist && dist <= snapThreshold)
        {
            bestDist = dist;
            bestDelta = to - from;
        }
    }


    private void DrawOutline(RectTransform rect, Color col)
    {
        if (previewCamera == null || previewTexture == null)
            return;

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        Vector2[] pts = new Vector2[4];

        for (int i = 0; i < 4; i++)
        {
            Vector3 screenPoint = previewCamera.WorldToScreenPoint(corners[i]);

            Vector2 guiPoint = new Vector2(
                lastPreviewRect.x + (screenPoint.x / previewTexture.width) * lastPreviewRect.width,
                lastPreviewRect.y + lastPreviewRect.height -
                (screenPoint.y / previewTexture.height) * lastPreviewRect.height
            );

            pts[i] = guiPoint;
        }

        Handles.BeginGUI();
        Handles.color = col;
        Handles.DrawLine(pts[0], pts[1]);
        Handles.DrawLine(pts[1], pts[2]);
        Handles.DrawLine(pts[2], pts[3]);
        Handles.DrawLine(pts[3], pts[0]);
        Handles.EndGUI();
    }


    private void TrySelectUIElement(Vector2 mousePos)
    {
        if (previewCamera == null || targetCanvas == null)
            return;

        Vector2 previewScreen;
        if (!TryGetPreviewScreenPoint(mousePos, out previewScreen))
            return;

        double t = EditorApplication.timeSinceStartup;

        if (t - lastClickTime < clickInterval && lastClickedRect != null)
        { 
            Transform p = lastClickedRect.parent;

            if (p != null && p != targetCanvas.transform)
            {
                selectedRect = p as RectTransform;
                lastClickedRect = selectedRect;
                isDragging = false;
            }

            lastClickTime = t;
            Repaint(); 
            return;
        }

        lastClickTime = t;

        RectTransform[] rects = targetCanvas.GetComponentsInChildren<RectTransform>(true);
        RectTransform hit = null;

        foreach (RectTransform r in rects)
        {
            if (r == targetCanvas.transform)
                continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(r, previewScreen, previewCamera))
                hit = r;
        }

        if (hit != null)
        {
            selectedRect = hit;
            lastClickedRect = hit;
            isDragging = true;
            lastMousePos = mousePos;
        }
        else
        {
            selectedRect = null;
            lastClickedRect = null;
            isDragging = false;
        }

        Repaint();
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
