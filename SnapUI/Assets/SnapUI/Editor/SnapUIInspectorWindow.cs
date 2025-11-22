using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class SnapUIInspectorWindow : EditorWindow
{
    // ------------------------------
    // Selection + Serialized Objects
    // ------------------------------

    private GameObject selectedGO;
    private RectTransform selectedRect;
    private SerializedObject soText;
    private SerializedObject soImage;
    private SerializedObject soThemedText;
    private SerializedObject soThemedImage;

    private TMP_Text tmpText;
    private Image uiImage;
    private ThemedText themedText;
    private ThemedImage themedImage;

    // Throttle timing
    private double lastApply;
    private const double applyInterval = 1.0 / 60.0; // 60hz

    private bool foldTheme = true;
    private bool foldStyle = true;
    private bool foldContent = true;
    private bool foldLayout = true;
    private bool foldDebug = false;

    [MenuItem("Window/SnapUI/SnapUI Inspector")]
    public static void ShowWindow()
    {
        var win = GetWindow<SnapUIInspectorWindow>("SnapUI Inspector");
        win.minSize = new Vector2(280, 320);
    }

    // ---------------------------------------------------------------------
    // Editor lifecycle
    // ---------------------------------------------------------------------

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        RefreshSelection();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        RefreshSelection();
        Repaint();
    }

    private void RefreshSelection()
    {
        selectedGO = Selection.activeGameObject;

        if (selectedGO == null)
        {
            selectedRect = null;
            soText = null;
            soImage = null;
            soThemedText = null;
            soThemedImage = null;
            return;
        }

        selectedRect = selectedGO.GetComponent<RectTransform>();
        tmpText = selectedGO.GetComponent<TMP_Text>();
        uiImage = selectedGO.GetComponent<Image>();
        themedText = selectedGO.GetComponent<ThemedText>();
        themedImage = selectedGO.GetComponent<ThemedImage>();

        // Build SerializedObjects
        soText = tmpText ? new SerializedObject(tmpText) : null;
        soImage = uiImage ? new SerializedObject(uiImage) : null;
        soThemedText = themedText ? new SerializedObject(themedText) : null;
        soThemedImage = themedImage ? new SerializedObject(themedImage) : null;
    }

    // ---------------------------------------------------------------------
    // GUI
    // ---------------------------------------------------------------------

    private void OnGUI()
    {
        DrawHeader();

        if (selectedGO == null)
        {
            EditorGUILayout.HelpBox("No UI element selected.", MessageType.Info);
            return;
        }

        EditorGUI.BeginDisabledGroup(selectedRect == null);

        DrawThemeSection();
        DrawStyleSection();
        DrawContentSection();
        DrawLayoutSection();
        DrawDebugSection();

        EditorGUI.EndDisabledGroup();
    }

    private void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (selectedGO == null)
            {
                GUILayout.Label("No UI element selected", EditorStyles.miniLabel);
                return;
            }

            GUIContent icon = EditorGUIUtility.ObjectContent(selectedGO, typeof(GameObject));
            GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(18));
            GUILayout.Label(selectedGO.name, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                EditorGUIUtility.PingObject(selectedGO);
            }
        }
    }

    // ---------------------------------------------------------------------
    // THEME
    // ---------------------------------------------------------------------

    private void DrawThemeSection()
    {
        foldTheme = EditorGUILayout.Foldout(foldTheme, "Theme");
        if (!foldTheme) return;

        EditorGUI.indentLevel++;

        UITheme activeTheme = ThemeManager.ActiveTheme;
        EditorGUILayout.ObjectField("Active Theme", activeTheme, typeof(UITheme), false);

        if (GUILayout.Button("Reapply Theme To Selection"))
        {
            ReapplyTheme(selectedGO);
        }

        if (GUILayout.Button("Reapply Theme To Canvas"))
        {
            Canvas c = selectedGO.GetComponentInParent<Canvas>();
            if (c) ReapplyTheme(c.gameObject);
        }

        EditorGUI.indentLevel--;
    }

    private void ReapplyTheme(GameObject root)
    {
        var comps = root.GetComponentsInChildren<BaseUIComponent>(true);
        foreach (var c in comps)
        {
            if (c == null) continue;
            c.OnThemeChanged(ThemeManager.ActiveTheme);
        }
    }

    // ---------------------------------------------------------------------
    // STYLE
    // ---------------------------------------------------------------------

    private void DrawStyleSection()
    {
        foldStyle = EditorGUILayout.BeginFoldoutHeaderGroup(foldStyle, "Style");
        if (!foldStyle)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUI.indentLevel++;

        DrawTMPStyle();
        EditorGUILayout.Space(4);
        DrawImageStyle();
        EditorGUILayout.Space(4);
        DrawThemedStyle();

        EditorGUI.indentLevel--;
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawTMPStyle()
    {
        if (soText == null) return;

        EditorGUILayout.LabelField("TMP Text", EditorStyles.boldLabel);

        soText.Update();

        EditorGUILayout.PropertyField(soText.FindProperty("m_text"), new GUIContent("Text"));
        EditorGUILayout.PropertyField(soText.FindProperty("m_fontSize"), new GUIContent("Font Size"));
        EditorGUILayout.PropertyField(soText.FindProperty("m_textAlignment"), new GUIContent("Alignment"));

        ApplyThrottled(soText);
    }

    private void DrawImageStyle()
    {
        if (soImage == null) return;

        EditorGUILayout.LabelField("Image", EditorStyles.boldLabel);

        soImage.Update();

        EditorGUILayout.PropertyField(soImage.FindProperty("m_Sprite"), new GUIContent("Sprite"));
        EditorGUILayout.PropertyField(soImage.FindProperty("m_Type"), new GUIContent("Type"));
        EditorGUILayout.PropertyField(soImage.FindProperty("m_Color"), new GUIContent("Color"));

        ApplyThrottled(soImage);
    }

    private void DrawThemedStyle()
    {
        if (soThemedText != null)
        {
            EditorGUILayout.LabelField("ThemedText", EditorStyles.boldLabel);

            soThemedText.Update();
            EditorGUILayout.PropertyField(soThemedText.FindProperty("colorType"));
            ApplyThrottled(soThemedText);
        }

        if (soThemedImage != null)
        {
            EditorGUILayout.LabelField("ThemedImage", EditorStyles.boldLabel);

            soThemedImage.Update();
            EditorGUILayout.PropertyField(soThemedImage.FindProperty("colorTarget"));
            ApplyThrottled(soThemedImage);
        }
    }

    // ---------------------------------------------------------------------
    // CONTENT SHORTCUTS
    // ---------------------------------------------------------------------

    private void DrawContentSection()
    {
        foldContent = EditorGUILayout.BeginFoldoutHeaderGroup(foldContent, "Content Shortcuts");
        if (!foldContent)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUI.indentLevel++;

        if (GUILayout.Button("Add Child Text"))
            CreateChildText();

        if (GUILayout.Button("Add Child Image"))
            CreateChildImage();

        if (GUILayout.Button("Add Child Panel"))
            CreateChildPanel();

        EditorGUI.indentLevel--;
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ----------------- Create Elements -----------------

    private void CreateChildText()
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ThemedText));
        go.transform.SetParent(selectedRect, false);

        TMP_Text t = go.GetComponent<TMP_Text>();
        t.text = "New Text";
        t.fontSize = 24;
        t.alignment = TextAlignmentOptions.Center;

        ThemedText tt = go.GetComponent<ThemedText>();
        tt.OnThemeChanged(ThemeManager.ActiveTheme);

        Selection.activeGameObject = go;
    }

    private void CreateChildImage()
    {
        GameObject go = new GameObject("Image", typeof(RectTransform), typeof(Image), typeof(ThemedImage));
        go.transform.SetParent(selectedRect, false);

        go.GetComponent<ThemedImage>().OnThemeChanged(ThemeManager.ActiveTheme);
        Selection.activeGameObject = go;
    }

    private void CreateChildPanel()
    {
        GameObject go = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(ThemedImage));
        go.transform.SetParent(selectedRect, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(260, 140);

        go.GetComponent<Image>().type = Image.Type.Sliced;

        ThemedImage ti = go.GetComponent<ThemedImage>();
        SetPrivateField(ti, "colorTarget", ThemedImage.ColorTarget.Background);
        ti.OnThemeChanged(ThemeManager.ActiveTheme);

        Selection.activeGameObject = go;
    }

    // ---------------------------------------------------------------------
    // LAYOUT
    // ---------------------------------------------------------------------

    private void DrawLayoutSection()
    {
        foldLayout = EditorGUILayout.BeginFoldoutHeaderGroup(foldLayout, "Layout");
        if (!foldLayout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        if (selectedRect == null)
        {
            EditorGUILayout.HelpBox("No RectTransform.", MessageType.Info);
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUI.indentLevel++;

        EditorGUILayout.LabelField("Anchors", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Center"))
                SetAnchor(new Vector2(0.5f, 0.5f));
            if (GUILayout.Button("Stretch H"))
                SetAnchorMinMaxX(0, 1);
            if (GUILayout.Button("Stretch V"))
                SetAnchorMinMaxY(0, 1);
            if (GUILayout.Button("Stretch All"))
                selectedRect.anchorMin = Vector2.zero;
        }

        EditorGUILayout.Space(3);

        EditorGUILayout.LabelField("Alignment", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Left"))
                AlignX(0f);
            if (GUILayout.Button("Center X"))
                AlignX(0.5f);
            if (GUILayout.Button("Right"))
                AlignX(1f);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Bottom"))
                AlignY(0f);
            if (GUILayout.Button("Center Y"))
                AlignY(0.5f);
            if (GUILayout.Button("Top"))
                AlignY(1f);
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void SetAnchor(Vector2 v)
    {
        Undo.RecordObject(selectedRect, "Anchor Change");
        selectedRect.anchorMin = v;
        selectedRect.anchorMax = v;
        EditorUtility.SetDirty(selectedRect);
    }

    private void SetAnchorMinMaxX(float min, float max)
    {
        Undo.RecordObject(selectedRect, "Anchor Change");
        selectedRect.anchorMin = new Vector2(min, selectedRect.anchorMin.y);
        selectedRect.anchorMax = new Vector2(max, selectedRect.anchorMax.y);
        EditorUtility.SetDirty(selectedRect);
    }

    private void SetAnchorMinMaxY(float min, float max)
    {
        Undo.RecordObject(selectedRect, "Anchor Change");
        selectedRect.anchorMin = new Vector2(selectedRect.anchorMin.x, min);
        selectedRect.anchorMax = new Vector2(selectedRect.anchorMax.x, max);
        EditorUtility.SetDirty(selectedRect);
    }

    private void AlignX(float normalized)
    {
        if (!selectedRect || !selectedRect.parent) return;
        Undo.RecordObject(selectedRect, "Align X");
        RectTransform parent = selectedRect.parent as RectTransform;
        float x = Mathf.Lerp(-parent.rect.width * 0.5f, parent.rect.width * 0.5f, normalized);
        selectedRect.anchoredPosition = new Vector2(x, selectedRect.anchoredPosition.y);
    }

    private void AlignY(float normalized)
    {
        if (!selectedRect || !selectedRect.parent) return;
        Undo.RecordObject(selectedRect, "Align Y");
        RectTransform parent = selectedRect.parent as RectTransform;
        float y = Mathf.Lerp(-parent.rect.height * 0.5f, parent.rect.height * 0.5f, normalized);
        selectedRect.anchoredPosition = new Vector2(selectedRect.anchoredPosition.x, y);
    }

    // ---------------------------------------------------------------------
    // DEBUG SECTION
    // ---------------------------------------------------------------------

    private void DrawDebugSection()
    {
        foldDebug = EditorGUILayout.BeginFoldoutHeaderGroup(foldDebug, "Debug");

        if (!foldDebug)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUI.indentLevel++;

        if (selectedRect)
        {
            EditorGUILayout.LabelField("Pos", selectedRect.anchoredPosition.ToString("F1"));
            EditorGUILayout.LabelField("Size", selectedRect.sizeDelta.ToString("F1"));
            EditorGUILayout.LabelField("AnchorMin", selectedRect.anchorMin.ToString("F2"));
            EditorGUILayout.LabelField("AnchorMax", selectedRect.anchorMax.ToString("F2"));
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ---------------------------------------------------------------------
    // UTILITIES
    // ---------------------------------------------------------------------

    private void ApplyThrottled(SerializedObject so)
    {
        if (!so.hasModifiedProperties)
            return;

        double now = EditorApplication.timeSinceStartup;
        if (now - lastApply >= applyInterval)
        {
            so.ApplyModifiedProperties();
            lastApply = now;
        }

        // Always repaint fast
        if (Event.current.type == EventType.Repaint)
            Repaint();
    }

    private void SetPrivateField(object instance, string field, object val)
    {
        var f = instance.GetType().GetField(field,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        f?.SetValue(instance, val);
    }
}
