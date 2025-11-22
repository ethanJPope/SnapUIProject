using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SnapUIHierarchyWindow : EditorWindow
{
    private Canvas[] sceneCanvases;
    private int selectedCanvasIndex = 0;
    private Vector2 scrollPos;

    private Dictionary<int, bool> foldoutStates = new Dictionary<int, bool>();

    [MenuItem("Window/SnapUI/SnapUI Hierarchy")]
    public static void ShowWindow()
    {
        var window = GetWindow<SnapUIHierarchyWindow>("SnapUI Hierarchy");
        window.minSize = new Vector2(250, 200);
    }

    private void OnEnable()
    {
        RefreshCanvases();
        Selection.selectionChanged += OnExternalSelectionChanged;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnExternalSelectionChanged;
    }

    private void OnHierarchyChange()
    {
        RefreshCanvases();
        Repaint();
    }

    private void OnExternalSelectionChanged()
    {
        Repaint();
    }

    private void RefreshCanvases()
    {
        sceneCanvases = FindObjectsOfType<Canvas>();
        if (sceneCanvases == null || sceneCanvases.Length == 0)
        {
            selectedCanvasIndex = 0;
        }
        if (selectedCanvasIndex >= sceneCanvases.Length)
        {
            selectedCanvasIndex = 0;
        }
        Repaint();
    }

    private void OnGUI()
    {
        DrawToopbar();
        EditorGUILayout.Space();

        if (sceneCanvases == null || sceneCanvases.Length == 0)
        {
            EditorGUILayout.HelpBox("No Canvas objects found in the scene.", MessageType.Info);
            if (GUILayout.Button("Refresh"))
            {
                RefreshCanvases();
            }
            return;
        }

        Canvas targetCanvas = sceneCanvases[selectedCanvasIndex];
        if (targetCanvas == null)
        {
            EditorGUILayout.HelpBox("Selected Canvas is null. Please refresh.", MessageType.Warning);
            if (GUILayout.Button("Refresh"))
            {
                RefreshCanvases();
            }
            return;
        }
        
        using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPos))
        {
            scrollPos = scroll.scrollPosition;
            RectTransform rootRect = targetCanvas.GetComponent<RectTransform>();
            DrawRectTransformNode(rootRect, 0, true);
        }
    }

    private void DrawToopbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            string[] names = new string[sceneCanvases.Length];
            for (int i = 0; i < sceneCanvases.Length; i++)
            {
                names[i] = sceneCanvases[i].name;
            }
            int newIndex = EditorGUILayout.Popup(selectedCanvasIndex, names, EditorStyles.toolbarPopup);
            if (newIndex != selectedCanvasIndex)
            {
                selectedCanvasIndex = newIndex;
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                RefreshCanvases();
            }
        }
    }

    private void DrawRectTransformNode(RectTransform rect, int indent, bool isRoot = false)
    {
        if (rect == null)
        {
            return;
        }

        int id = rect.GetInstanceID();
        bool hasChildren = rect.childCount > 0;

        if (!foldoutStates.ContainsKey(id))
        {
            foldoutStates[id] = false;
        }

        bool isSelected = Selection.activeGameObject == rect.gameObject;

        Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

        if (Event.current.type == EventType.Repaint && isSelected)
        {
            Color highlightColor = new Color(0.24f, 0.48f, 0.90f, 0.35f);
            EditorGUI.DrawRect(rowRect, highlightColor);
        }

        float indentWidth = 16f;
        rowRect.x += indent * indentWidth;
        rowRect.width -= indent * indentWidth;

        if (hasChildren)
        {
            Rect foldoutRect = new Rect(rowRect.x, rowRect.y, 14f, rowRect.height);
            foldoutStates[id] = EditorGUI.Foldout(foldoutRect, foldoutStates[id], GUIContent.none);
            rowRect.x += 14f;
            rowRect.width -= 14f;
        }

        GUIContent labelContent = EditorGUIUtility.ObjectContent(rect.gameObject, typeof(GameObject));
        labelContent.text = rect.gameObject.name;

        if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
        {
            Selection.activeGameObject = rect.gameObject;
        }

        EditorGUI.LabelField(rowRect, labelContent, isRoot ? EditorStyles.boldLabel : EditorStyles.label);

        if (hasChildren && foldoutStates[id])
        {
            for (int i = 0; i < rect.childCount; i++)
            {
                RectTransform childRect = rect.GetChild(i) as RectTransform;
                if (childRect != null)
                {
                    DrawRectTransformNode(childRect, indent + 1);
                }
            }
        }
    }
}

