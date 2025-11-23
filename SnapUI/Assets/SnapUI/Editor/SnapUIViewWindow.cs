using Mono.Cecil.Cil;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class SnapUIViewWindow : EditorWindow
{
    private float zoom = 1f;
    private Vector2 panOffset = Vector2.zero;

    private const float MinZoom = 0.01f;
    private const float MaxZoom = 4.0f;

    private Rect workspaceRect;
    private Rect lastPreviewRect;
    private RectTransform selectedRect;
    private bool isDragging;
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
    private bool cameraInitialized;

    private Vector2 deviceResolution = new Vector2(1920, 1080);
    private string[] devicePresets = new[]
    {
        "PC 1920x1080",
        "Phone 1080x1920",
        "Tablet 1536x2048",
        "Square 1024x1024"
    };
    private int selectedPreset;
    private int selectedCanvasIndex;

    private int[] gridSizes = { 0, 4, 8, 10, 16, 32, 64 };
    private string[] gridLabels =
    {
        "No Grid",
        "GridSize: 4 px",
        "GridSize: 8 px",
        "GridSize: 10 px",
        "GridSize: 16 px",
        "GridSize: 32 px",
        "GridSize: 64 px"
    };
    private int selectedGridIndex;

    private enum HandleType
    {
        None,
        ResizeTopLeft,
        ResizeTop,
        ResizeTopRight,
        ResizeRight,
        ResizeBottomRight,
        ResizeBottom,
        ResizeBottomLeft,
        ResizeLeft,
        Rotate
    }

    private struct HandleInfo
    {
        public Rect rect;
        public HandleType type;
    }

    private HandleInfo[] handles = new HandleInfo[9];
    private HandleType activeHandle = HandleType.None;
    private bool isResizing = false;

    private bool snapToUI = true;
    private float snapThreshold = 10f;

    private bool showGuideX;
    private bool showGuideY;
    private float guideX;
    private float guideY;

    private Vector2 userMotion;

    private RectTransform[] cachedSnapRects;
    private Canvas cachedSnapCanvas;

    private double lastAutoRepaint;
    private const double AutoRepaintInterval = 1.0 / 30.0;

    [MenuItem("Window/SnapUI/SnapUI View")]
    public static void ShowWindow()
    {
        var window = GetWindow<SnapUIViewWindow>("SnapUI View");
        window.minSize = new Vector2(400, 300);
    }

    private void OnEnable()
    {
        EnsurePreviewCameraExists();
        ScanForCanvases();
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

        cachedSnapRects = null;
        cachedSnapCanvas = null;
    }

    private void Update()
    {
        if (targetCanvas == null)
            return;

        double t = EditorApplication.timeSinceStartup;
        if (t - lastAutoRepaint > AutoRepaintInterval)
        {
            lastAutoRepaint = t;
            Repaint();
        }
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawWorkspace();
        HandleInput();
    }

    private void OnSelectionChange()
    {
        if (targetCanvas == null)
        {
            selectedRect = null;
            Repaint();
            return;
        }

        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            selectedRect = null;
            Repaint();
            return;
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null && rt.transform.IsChildOf(targetCanvas.transform))
        {
            selectedRect = rt;
            lastClickedRect = rt;
            showGuideX = false;
            showGuideY = false;
            Repaint();
        }
    }

    private void OnHierarchyChange()
    {
        cachedSnapRects = null;
        cachedSnapCanvas = null;
    }

    private void EnsurePreviewCameraExists()
    {
        if (cameraInitialized && previewCamera != null)
            return;

        var camObj = new GameObject("SnapUIPreviewCamera");
        camObj.hideFlags = HideFlags.HideAndDontSave;

        previewCamera = camObj.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
        previewCamera.orthographic = true;
        previewCamera.nearClipPlane = -50;
        previewCamera.farClipPlane = 50;
        previewCamera.cullingMask = LayerMask.GetMask("UI");

        cameraInitialized = true;
        SetupRenderTexture();
        ApplyPreviewHookIfNeeded();
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

    private void ApplyPreviewHookIfNeeded()
    {
        if (targetCanvas == null)
            return;

        if (previewHook == null)
        {
            previewHook = targetCanvas.GetComponent<CanvasPreviewHook>();
            if (previewHook == null)
                previewHook = targetCanvas.gameObject.AddComponent<CanvasPreviewHook>();
        }

        if (previewHook.previewCamera != previewCamera)
        {
            previewHook.previewCamera = previewCamera;
            previewHook.ApplyPreviewCamera();
        }
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
        ApplyPreviewHookIfNeeded();
    }

    private void ScanForCanvases()
    {
        sceneCanvases = FindObjectsOfType<Canvas>();

        if (sceneCanvases.Length > 0)
        {
            if (selectedCanvasIndex >= sceneCanvases.Length)
                selectedCanvasIndex = 0;

            SetTargetCanvas(sceneCanvases[selectedCanvasIndex]);
        }
        else
        {
            targetCanvas = null;
            previewHook = null;
        }
    }

    private void SetTargetCanvas(Canvas canvas)
    {
        if (canvas == null)
        {
            targetCanvas = null;
            previewHook = null;
            cachedSnapRects = null;
            cachedSnapCanvas = null;
            return;
        }

        targetCanvas = canvas;
        cachedSnapRects = null;
        cachedSnapCanvas = null;

        EnsurePreviewCameraExists();
        ApplyPreviewHookIfNeeded();
    }

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (sceneCanvases == null || sceneCanvases.Length == 0)
        {
            if (GUILayout.Button("Find Canvases", EditorStyles.toolbarButton, GUILayout.Width(120)))
                ScanForCanvases();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return;
        }

        string[] names = new string[sceneCanvases.Length];
        for (int i = 0; i < names.Length; i++)
            names[i] = sceneCanvases[i].name;

        int newIndex = EditorGUILayout.Popup(selectedCanvasIndex, names, GUILayout.Width(180));
        if (newIndex != selectedCanvasIndex)
        {
            selectedCanvasIndex = newIndex;
            SetTargetCanvas(sceneCanvases[newIndex]);
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
            Repaint();
        }

        GUILayout.EndHorizontal();
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

        if (selectedRect != null && targetCanvas != null)
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

            if (TryGetSelectionGuiRect(out Rect selectionGuiRect))
            {
                UpdateHandles(selectionGuiRect);
                DrawHandles();
            }
        }

        DrawSnapGuides();

        GUI.EndClip();
    }

    private bool TryGetSelectionGuiRect(out Rect guiRect)
    {
        guiRect = default;

        if (selectedRect == null || previewCamera == null || previewTexture == null)
            return false;

        Vector3[] corners = new Vector3[4];
        selectedRect.GetWorldCorners(corners);

        bool initialized = false;
        float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;

        for (int i = 0; i < 4; i++)
        {
            Vector3 screenPoint = previewCamera.WorldToScreenPoint(corners[i]);

            Vector2 guiPoint = new Vector2(
                lastPreviewRect.x + (screenPoint.x / previewTexture.width) * lastPreviewRect.width,
                lastPreviewRect.y + lastPreviewRect.height -
                (screenPoint.y / previewTexture.height) * lastPreviewRect.height
            );

            if (!initialized)
            {
                minX = maxX = guiPoint.x;
                minY = maxY = guiPoint.y;
                initialized = true;
            }
            else
            {
                if (guiPoint.x < minX) minX = guiPoint.x;
                if (guiPoint.x > maxX) maxX = guiPoint.x;
                if (guiPoint.y < minY) minY = guiPoint.y;
                if (guiPoint.y > maxY) maxY = guiPoint.y;
            }
        }

        if (!initialized)
            return false;

        guiRect = new Rect(minX, minY, maxX - minX, maxY - minY);
        return guiRect.width > 0.1f && guiRect.height > 0.1f;
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

            float guiX = lastPreviewRect.x + (s.x / previewTexture.width) * lastPreviewRect.width;

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

    private void HandleInput()
    {
        Event e = Event.current;

        if (e.type == EventType.ScrollWheel)
        {
            zoom = Mathf.Clamp(zoom - e.delta.y * 0.02f, MinZoom, MaxZoom);
            Repaint();
        }

        Vector2 mousePos = e.mousePosition;
        Vector2 localMouse = mousePos - workspaceRect.position;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            for (int i = 0; i < handles.Length; i++)
            {
                if (handles[i].type != HandleType.None && handles[i].rect.Contains(localMouse))
                {
                    activeHandle = handles[i].type;
                    isResizing = true;
                    isDragging = false;
                    lastMousePos = mousePos;
                    e.Use();
                    return;
                }
            }
        }

        if (isResizing && selectedRect != null)
        {
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                Vector2 delta = mousePos - lastMousePos;
                lastMousePos = mousePos;

                ResizeUIElement(delta);
                Repaint();
                return;
            }
        }

        if (e.type == EventType.MouseUp)
        {
            isResizing = false;
            activeHandle = HandleType.None;
            isDragging = false;
            showGuideX = false;
            showGuideY = false;
        }

        if (e.type == EventType.MouseDrag && e.button == 2)
        {
            panOffset += e.delta;
            Repaint();
        }

        if (!isResizing && e.type == EventType.MouseDown && e.button == 0)
        {
            TrySelectUIElement(mousePos);
            return;
        }

        if (isDragging && selectedRect != null)
        {
            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                Vector2 delta = mousePos - lastMousePos;
                lastMousePos = mousePos;
                DragUIElement(delta);
                Repaint();
            }
        }
    }

    private void DrawHandles()
    {
        Handles.BeginGUI();

        foreach (var h in handles)
        {
            if (h.type == HandleType.None)
                continue;

            Color c = (h.type == HandleType.Rotate)
                ? new Color(0.4f, 0.8f, 1f)
                : Color.white;

            EditorGUI.DrawRect(h.rect, c);
            GUI.Box(h.rect, GUIContent.none);
        }

        Handles.EndGUI();
    }

    private void ResizeUIElement(Vector2 mouseDelta)
    {
        if (selectedRect == null || previewTexture == null || targetCanvas == null)
            return;

        Vector2 previewDelta = new Vector2(
            mouseDelta.x / lastPreviewRect.width * previewTexture.width,
            -mouseDelta.y / lastPreviewRect.height * previewTexture.height
        );

        float scaleFactor = targetCanvas.scaleFactor;
        Vector2 canvasDelta = previewDelta / scaleFactor;

        Vector2 size = selectedRect.sizeDelta;
        Vector2 pos = selectedRect.anchoredPosition;

        switch (activeHandle)
        {
            case HandleType.ResizeRight:
                size.x += canvasDelta.x;
                break;

            case HandleType.ResizeLeft:
                size.x -= canvasDelta.x;
                pos.x += canvasDelta.x * 0.5f;
                break;

            case HandleType.ResizeTop:
                size.y += canvasDelta.y;
                break;

            case HandleType.ResizeBottom:
                size.y -= canvasDelta.y;
                pos.y += canvasDelta.y * 0.5f;
                break;

            case HandleType.ResizeTopLeft:
                size.x -= canvasDelta.x;
                size.y += canvasDelta.y;
                pos.x += canvasDelta.x * 0.5f;
                break;

            case HandleType.ResizeTopRight:
                size.x += canvasDelta.x;
                size.y += canvasDelta.y;
                break;

            case HandleType.ResizeBottomLeft:
                size.x -= canvasDelta.x;
                size.y -= canvasDelta.y;
                pos.x += canvasDelta.x * 0.5f;
                pos.y += canvasDelta.y * 0.5f;
                break;

            case HandleType.ResizeBottomRight:
                size.x += canvasDelta.x;
                size.y -= canvasDelta.y;
                pos.y += canvasDelta.y * 0.5f;
                break;

            case HandleType.Rotate:

                break;
        }

        selectedRect.sizeDelta = size;
        selectedRect.anchoredPosition = pos;

        EditorUtility.SetDirty(selectedRect);
    }

    private void UpdateHandles(Rect rect)
    {
        float size = 10f;

        float cx = rect.xMin + rect.width * 0.5f;
        float cy = rect.yMin + rect.height * 0.5f;

        float x0 = rect.xMin - size * 0.5f;
        float x1 = cx - size * 0.5f;
        float x2 = rect.xMax - size * 0.5f;

        float y0 = rect.yMin - size * 0.5f;
        float y1 = cy - size * 0.5f;
        float y2 = rect.yMax - size * 0.5f;

        handles[0].rect = new Rect(x0, y0, size, size);
        handles[0].type = HandleType.ResizeTopLeft;

        handles[1].rect = new Rect(x1, y0, size, size);
        handles[1].type = HandleType.ResizeTop;

        handles[2].rect = new Rect(x2, y0, size, size);
        handles[2].type = HandleType.ResizeTopRight;

        handles[3].rect = new Rect(x2, y1, size, size);
        handles[3].type = HandleType.ResizeRight;

        handles[4].rect = new Rect(x2, y2, size, size);
        handles[4].type = HandleType.ResizeBottomRight;

        handles[5].rect = new Rect(x1, y2, size, size);
        handles[5].type = HandleType.ResizeBottom;

        handles[6].rect = new Rect(x0, y2, size, size);
        handles[6].type = HandleType.ResizeBottomLeft;

        handles[7].rect = new Rect(x0, y1, size, size);
        handles[7].type = HandleType.ResizeLeft;

        handles[8].rect = new Rect(x1, y0 - 20f, size, size);
        handles[8].type = HandleType.Rotate;
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

    private void TrySelectUIElement(Vector2 mousePos)
    {
        if (previewCamera == null || targetCanvas == null)
            return;

        if (!TryGetPreviewScreenPoint(mousePos, out var previewScreen))
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

            Selection.activeObject = hit.gameObject;
        }
        else
        {
            selectedRect = null;
            lastClickedRect = null;
            isDragging = false;
        }

        showGuideX = false;
        showGuideY = false;

        Repaint();
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

    private RectTransform[] GetSnapTargets()
    {
        if (targetCanvas == null)
            return null;

        if (cachedSnapCanvas == targetCanvas && cachedSnapRects != null)
            return cachedSnapRects;

        cachedSnapRects = targetCanvas.GetComponentsInChildren<RectTransform>(false);
        cachedSnapCanvas = targetCanvas;
        return cachedSnapRects;
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

        RectTransform[] rects = GetSnapTargets();
        if (rects == null)
            return desiredPos;

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
            float softDelta = bestDeltaX;
            result.x += softDelta;

            showGuideX = true;
            guideX = testBounds.center.x + softDelta;
        }

        if (bestYDist <= snapThreshold && Mathf.Abs(userMotion.y) < detachDistance)
        {
            float softDeltaY = bestDeltaY;
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
}
