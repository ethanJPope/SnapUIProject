using UnityEditor;
using UnityEngine;

public class SnapUIViewWindow : EditorWindow
{
    private Vector2 scrollPos;
    private float zoom = 1.0f;
    private Vector2 panOffset = Vector2.zero;

    private const float MinZoom = 0.3f;
    private const float MaxZoom = 2.0f;

    [MenuItem("Window/SnapUI/SnapUI View")]
    public static void ShowWindow()
    {
        var window = GetWindow<SnapUIViewWindow>("SnapUI View");
        window.minSize = new Vector2(400, 300);
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawWorkspace();
        HandleInput();
    }

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Reset View", EditorStyles.toolbarButton))
        {
            zoom = 1.0f;
            panOffset = Vector2.zero;
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("Zoom: " + zoom.ToString("0.00"));

        GUILayout.EndHorizontal();
    }

    private void DrawWorkspace()
    {
        Rect workspaceRect = new Rect(0, 20, position.width, position.height - 20);
        GUI.BeginGroup(workspaceRect);

        Matrix4x4 oldMatrix = GUI.matrix;

        GUI.matrix = Matrix4x4.TRS(panOffset, Quaternion.identity, new Vector3(zoom, zoom, 1));
        Rect previewRect = new Rect(100, 100, 800, 450);

        EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.17f));
        GUI.Label(new Rect(120, 120, 500, 30), "SnapUI Preview Area", EditorStyles.boldLabel);

        GUI.matrix = oldMatrix;

        GUI.EndClip();
    }

    private void HandleInput()
    {
        Event e = Event.current;

        if (e.type == EventType.ScrollWheel)
        {
            float zoomDelta = -e.delta.y * 0.02f;
            zoom = Mathf.Clamp(zoom + zoomDelta, MinZoom, MaxZoom);
            Repaint();
        }

        if (e.type == EventType.MouseDrag && e.button == 2)
        {
            panOffset += e.delta;
            Repaint();
        }
    }
}
