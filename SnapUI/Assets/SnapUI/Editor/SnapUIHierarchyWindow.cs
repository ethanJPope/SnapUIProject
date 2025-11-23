using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SnapUIHierarchyWindow : EditorWindow
{
    private Canvas[] sceneCanvases;
    private int selectedCanvasIndex;
    private Vector2 scrollPos;

    private Dictionary<int, bool> foldoutStates = new Dictionary<int, bool>();

    private Transform dragInsertParent;
    private int dragInsertIndex = -1;
    private Rect dragInsertRect;

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
        if (sceneCanvases != null && selectedCanvasIndex >= sceneCanvases.Length)
        {
            selectedCanvasIndex = 0;
        }
        Repaint();
    }

    private void OnGUI()
    {
        Event e = Event.current;

        if (e.type == EventType.DragExited)
        {
            dragInsertParent = null;
            dragInsertIndex = -1;
            Repaint();
        }

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

            if (Event.current.type == EventType.Repaint && dragInsertParent != null && dragInsertIndex >= 0)
            {
                Color c = new Color(0.25f, 0.7f, 1f, 0.9f);
                EditorGUI.DrawRect(dragInsertRect, c);
            }
        }
    }

    private void DrawToopbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (sceneCanvases == null || sceneCanvases.Length == 0)
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    RefreshCanvases();
                }
                return;
            }

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

        Rect contentRect = rowRect;

        if (hasChildren)
        {
            Rect foldoutRect = new Rect(contentRect.x, contentRect.y, 14f, contentRect.height);
            foldoutStates[id] = EditorGUI.Foldout(foldoutRect, foldoutStates[id], GUIContent.none);
            contentRect.x += 14f;
            contentRect.width -= 14f;
        }

        Event e = Event.current;

        if (e.type == EventType.MouseDown && e.button == 0 && rowRect.Contains(e.mousePosition))
        {
            Selection.activeGameObject = rect.gameObject;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new Object[] { rect.gameObject };
            DragAndDrop.StartDrag(rect.gameObject.name);
            e.Use();
        }

        if (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
        {
            GameObject dragGO = null;
            if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
            {
                dragGO = DragAndDrop.objectReferences[0] as GameObject;
            }

            RectTransform dragRT = dragGO != null ? dragGO.GetComponent<RectTransform>() : null;

            if (dragRT != null && dragRT != rect && rowRect.Contains(e.mousePosition))
            {
                bool top = e.mousePosition.y < rowRect.y + rowRect.height * 0.25f;
                bool bottom = e.mousePosition.y > rowRect.y + rowRect.height * 0.75f;
                bool middle = !top && !bottom;

                Transform targetParent = null;
                int targetIndex = 0;
                bool valid = true;

                if (middle)
                {
                    targetParent = rect;
                    targetIndex = rect.childCount;
                }
                else
                {
                    targetParent = rect.parent;
                    if (targetParent == null)
                    {
                        valid = false;
                    }
                    else
                    {
                        targetIndex = rect.GetSiblingIndex() + (bottom ? 1 : 0);
                    }
                }

                if (valid && (targetParent == dragRT || IsDescendant(dragRT, targetParent)))
                {
                    valid = false;
                }

                if (valid)
                {
                    dragInsertParent = targetParent;
                    dragInsertIndex = targetIndex;

                    float y;
                    if (middle)
                    {
                        y = rowRect.center.y;
                    }
                    else if (bottom)
                    {
                        y = rowRect.yMax - 1f;
                    }
                    else
                    {
                        y = rowRect.y;
                    }

                    dragInsertRect = new Rect(rowRect.x, y, rowRect.width, 2f);

                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;

                    if (e.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        Undo.RecordObject(dragRT, "Reparent UI Element");

                        if (dragRT.parent != targetParent)
                        {
                            dragRT.SetParent(targetParent, false);
                        }

                        int maxIndex = targetParent != null ? targetParent.childCount - 1 : 0;
                        int clampedIndex = Mathf.Clamp(dragInsertIndex, 0, maxIndex);
                        dragRT.SetSiblingIndex(clampedIndex);

                        dragInsertParent = null;
                        dragInsertIndex = -1;

                        EditorApplication.RepaintHierarchyWindow();
                        Repaint();

                        e.Use();
                    }
                    else
                    {
                        Repaint();
                        e.Use();
                    }
                }
                else
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                }
            }
        }

        GUIContent labelContent = EditorGUIUtility.ObjectContent(rect.gameObject, typeof(GameObject));
        labelContent.text = rect.gameObject.name;

        if (GUI.Button(contentRect, GUIContent.none, GUIStyle.none))
        {
            Selection.activeGameObject = rect.gameObject;
        }

        EditorGUI.LabelField(contentRect, labelContent, isRoot ? EditorStyles.boldLabel : EditorStyles.label);

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

    private bool IsDescendant(Transform parent, Transform possibleDescendant)
    {
        if (parent == null || possibleDescendant == null) return false;
        Transform t = possibleDescendant;
        while (t != null)
        {
            if (t == parent) return true;
            t = t.parent;
        }
        return false;
    }
}
