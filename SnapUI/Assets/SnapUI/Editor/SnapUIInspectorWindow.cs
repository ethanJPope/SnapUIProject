using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using UnityEditor.VersionControl;

public class SnapUIInspectorWindow : EditorWindow
{
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

    private double lastApply;
    private const double applyInterval = 1.0 / 60.0;

    private bool foldTheme = true;
    private bool foldStyle = true;
    private bool foldContent = true;
    private bool foldLayout = true;
    private bool foldDebug = false;

    private int editThemeIndex;
    private SerializedObject soEditTheme;

    private int templateIndex;

    [MenuItem("Window/SnapUI/SnapUI Inspector")]
    public static void ShowWindow()
    {
        var win = GetWindow<SnapUIInspectorWindow>("SnapUI Inspector");
        win.minSize = new Vector2(280, 320);
    }

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
            tmpText = null;
            uiImage = null;
            themedText = null;
            themedImage = null;
            return;
        }

        selectedRect = selectedGO.GetComponent<RectTransform>();
        tmpText = selectedGO.GetComponent<TMP_Text>();
        uiImage = selectedGO.GetComponent<Image>();
        themedText = selectedGO.GetComponent<ThemedText>();
        themedImage = selectedGO.GetComponent<ThemedImage>();

        soText = tmpText != null ? new SerializedObject(tmpText) : null;
        soImage = uiImage != null ? new SerializedObject(uiImage) : null;
        soThemedText = themedText != null ? new SerializedObject(themedText) : null;
        soThemedImage = themedImage != null ? new SerializedObject(themedImage) : null;
    }

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

    private UIElementTemplate[] GetAllTemplates()
    {
        string[] guids = AssetDatabase.FindAssets("t:UIElementTemplate");
        var list = new System.Collections.Generic.List<UIElementTemplate>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UIElementTemplate template = AssetDatabase.LoadAssetAtPath<UIElementTemplate>(path);
            if (template != null)
            {
                list.Add(template);
            }
        }

        return list.ToArray();
    }

    private UITheme[] GetAllThemes()
    {
        string[] guids = AssetDatabase.FindAssets("t:UITheme");
        var list = new System.Collections.Generic.List<UITheme>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(path);
            if (theme != null)
            {
                list.Add(theme);
            }
        }

        return list.ToArray();
    }

    private void DrawThemeSection()
    {
        foldTheme = EditorGUILayout.Foldout(foldTheme, "Theme");
        if (!foldTheme) return;

        EditorGUI.indentLevel++;

        UITheme activeTheme = ThemeManager.ActiveTheme;
        EditorGUILayout.ObjectField("Active Theme (runtime)", activeTheme, typeof(UITheme), false);

        UITheme[] allThemes = GetAllThemes();

        if (allThemes.Length > 0)
        {
            string[] names = new string[allThemes.Length];
            for (int i = 0; i < allThemes.Length; i++)
            {
                names[i] = allThemes[i].name;
            }

            SnapUISettings settings = SnapUISettings.Instance;
            UITheme defaultTheme = settings != null ? settings.defaultTheme : null;

            int currentIndex = 0;

            if (defaultTheme != null)
            {
                for (int i = 0; i < allThemes.Length; i++)
                {
                    if (allThemes[i] == defaultTheme)
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }
            else if (activeTheme != null)
            {
                for (int i = 0; i < allThemes.Length; i++)
                {
                    if (allThemes[i] == activeTheme)
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }

            int newIndex = EditorGUILayout.Popup("Default Theme", currentIndex, names);

            if (newIndex != currentIndex)
            {
                UITheme newTheme = allThemes[newIndex];

                ThemeManager.SetTheme(newTheme);

                if (settings != null)
                {
                    Undo.RecordObject(settings, "Change Default UI Theme");
                    settings.defaultTheme = newTheme;
                    EditorUtility.SetDirty(settings);
                }

                Canvas c = selectedGO != null ? selectedGO.GetComponentInParent<Canvas>() : null;
                if (c != null)
                {
                    ReapplyTheme(c.gameObject);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Theme Editor", EditorStyles.boldLabel);

            if (allThemes.Length > 0)
            {
                if (editThemeIndex < 0 || editThemeIndex >= allThemes.Length)
                {
                    editThemeIndex = 0;
                }

                editThemeIndex = EditorGUILayout.Popup("Theme", editThemeIndex, names);
                UITheme editTheme = allThemes[editThemeIndex];

                if (editTheme != null)
                {
                    if (soEditTheme == null || soEditTheme.targetObject != editTheme)
                    {
                        soEditTheme = new SerializedObject(editTheme);
                    }

                    soEditTheme.Update();

                    string newName = EditorGUILayout.TextField("Name", editTheme.name);
                    if (!string.IsNullOrEmpty(newName) && newName != editTheme.name)
                    {
                        string assetPath = AssetDatabase.GetAssetPath(editTheme);
                        AssetDatabase.RenameAsset(assetPath, newName);
                        AssetDatabase.SaveAssets();
                    }

                    EditorGUILayout.PropertyField(soEditTheme.FindProperty("primaryColor"));
                    EditorGUILayout.PropertyField(soEditTheme.FindProperty("secondaryColor"));
                    EditorGUILayout.PropertyField(soEditTheme.FindProperty("backgroundColor"));
                    EditorGUILayout.PropertyField(soEditTheme.FindProperty("textColor"));
                    EditorGUILayout.PropertyField(soEditTheme.FindProperty("accentColor"));
                    EditorGUILayout.PropertyField(soEditTheme.FindProperty("borderRadius"));
                    EditorGUILayout.PropertyField(soEditTheme.FindProperty("shadowStrength"));
                    EditorGUILayout.PropertyField(soEditTheme.FindProperty("mainFont"));

                    if (soEditTheme.ApplyModifiedProperties())
                    {
                        ThemeManager.SetTheme(editTheme);
                        Canvas c = selectedGO != null ? selectedGO.GetComponentInParent<Canvas>() : null;
                        if (c != null)
                        {
                            ReapplyTheme(c.gameObject);
                        }
                    }
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Create New Theme"))
            {
                CreateNewThemeAsset();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No UITheme assets found in the project.", MessageType.Info);

            if (GUILayout.Button("Create New Theme"))
            {
                CreateNewThemeAsset();
            }
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Reapply Theme To Selection"))
        {
            if (selectedGO != null)
            {
                ReapplyTheme(selectedGO);
            }
        }

        if (GUILayout.Button("Reapply Theme To Canvas"))
        {
            Canvas c = selectedGO != null ? selectedGO.GetComponentInParent<Canvas>() : null;
            if (c != null)
            {
                ReapplyTheme(c.gameObject);
            }
        }

        EditorGUI.indentLevel--;
    }

    private void CreateNewThemeAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create UI Theme",
            "NewUITheme",
            "asset",
            "Choose a location for the new UITheme asset");

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        UITheme newTheme = ScriptableObject.CreateInstance<UITheme>();
        AssetDatabase.CreateAsset(newTheme, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ThemeManager.SetTheme(newTheme);

        SnapUISettings settings = SnapUISettings.Instance;
        if (settings != null)
        {
            Undo.RecordObject(settings, "Set Default UI Theme");
            settings.defaultTheme = newTheme;
            EditorUtility.SetDirty(settings);
        }

        Canvas c = selectedGO != null ? selectedGO.GetComponentInParent<Canvas>() : null;
        if (c != null)
        {
            ReapplyTheme(c.gameObject);
        }
    }


    private void ReapplyTheme(GameObject root)
    {
        BaseUIComponent[] comps = root.GetComponentsInChildren<BaseUIComponent>(true);
        for (int i = 0; i < comps.Length; i++)
        {
            BaseUIComponent c = comps[i];
            if (c == null) continue;
            c.OnThemeChanged(ThemeManager.ActiveTheme);
        }
    }

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

        SerializedProperty alignProp = soText.FindProperty("m_textAlignment");
        if (alignProp != null)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(alignProp, new GUIContent("Alignment"));
            if (EditorGUI.EndChangeCheck())
            {
                soText.ApplyModifiedProperties();
                if (tmpText != null)
                {
                    Undo.RecordObject(tmpText, "Change Text Alignment");
                    tmpText.alignment = (TextAlignmentOptions)alignProp.intValue;
                    EditorUtility.SetDirty(tmpText);
                    tmpText.ForceMeshUpdate();
                }
            }
        }

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
        {
            CreateChildText();
        }

        if (GUILayout.Button("Add Child Image"))
        {
            CreateChildImage();
        }

        if (GUILayout.Button("Add Child Panel"))
        {
            CreateChildPanel();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Templates", EditorStyles.boldLabel);

        UIElementTemplate[] templates = GetAllTemplates();

        if (templates.Length > 0)
        {
            string[] names = new string[templates.Length];
            for (int i = 0; i < templates.Length; i++)
            {
                string label = templates[i].displayName;
                if (string.IsNullOrEmpty(label))
                {
                    label = templates[i].name;
                }
                names[i] = label;
            }

            if (templateIndex < 0 || templateIndex >= templates.Length)
            {
                templateIndex = 0;
            }

            templateIndex = EditorGUILayout.Popup("Template", templateIndex, names);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create From Template"))
            {
                UIElementTemplate t = templates[templateIndex];
                CreateFromTemplate(t);
            }

            if (GUILayout.Button("Save Selection As Template"))
            {
                SaveSelectionAsTemplate();
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("No UIElementTemplate assets found", MessageType.Info);
            if (GUILayout.Button("Save Selection As Template"))
            {
                SaveSelectionAsTemplate();
            }
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void CreateFromTemplate(UIElementTemplate template)
    {
        if (template == null) return;
        if (template.prefab == null) return;
        if (selectedRect == null) return;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(template.prefab);
        if (instance == null) return;

        Undo.RegisterCreatedObjectUndo(instance, "Create From Template");

        Transform t = instance.transform;
        t.SetParent(selectedRect, false);
        t.SetAsLastSibling();

        RectTransform rt = t as RectTransform;
        if (rt != null)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        instance.hideFlags = HideFlags.None;
        instance.SetActive(true);

        Selection.activeGameObject = instance;
        EditorGUIUtility.PingObject(instance);
        EditorApplication.RepaintHierarchyWindow();

        Canvas c = selectedGO != null ? selectedGO.GetComponentInParent<Canvas>() : null;
        if (c != null)
        {
            ReapplyTheme(c.gameObject);
        }
    }



    private void SaveSelectionAsTemplate()
    {
        if (selectedGO == null)
        {
            return;
        }

        string prefabPath = EditorUtility.SaveFilePanelInProject(
            "Save Element Template",
            selectedGO.name + "_Template",
            "prefab",
            "Choose a location to save the UI Element Template");

        if (string.IsNullOrEmpty(prefabPath))
        {
            return;
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(selectedGO, prefabPath, InteractionMode.UserAction);
        if (prefab == null)
        {
            return;
        }

        string folder = System.IO.Path.GetDirectoryName(prefabPath);
        string prefabName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
        string templatePath = System.IO.Path.Combine(folder, prefabName + "_Asset.asset");
        templatePath = templatePath.Replace("\\", "/");

        UIElementTemplate template = ScriptableObject.CreateInstance<UIElementTemplate>();
        template.displayName = selectedGO.name;
        template.prefab = prefab;

        AssetDatabase.CreateAsset(template, templatePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

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

        ThemedImage themed = go.GetComponent<ThemedImage>();
        themed.OnThemeChanged(ThemeManager.ActiveTheme);

        Selection.activeGameObject = go;
    }

    private void CreateChildPanel()
    {
        GameObject go = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(ThemedImage));
        go.transform.SetParent(selectedRect, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(260, 140);

        Image img = go.GetComponent<Image>();
        img.type = Image.Type.Sliced;

        ThemedImage themed = go.GetComponent<ThemedImage>();
        SetPrivateField(themed, "colorTarget", ThemedImage.ColorTarget.Background);
        themed.OnThemeChanged(ThemeManager.ActiveTheme);

        Selection.activeGameObject = go;
    }

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
            {
                SetAnchor(new Vector2(0.5f, 0.5f));
            }
            if (GUILayout.Button("Stretch H"))
            {
                SetAnchorMinMaxX(0f, 1f);
            }
            if (GUILayout.Button("Stretch V"))
            {
                SetAnchorMinMaxY(0f, 1f);
            }
            if (GUILayout.Button("Stretch All"))
            {
                selectedRect.anchorMin = Vector2.zero;
            }
        }

        EditorGUILayout.Space(3);

        EditorGUILayout.LabelField("Alignment", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Left"))
            {
                AlignX(0f);
            }
            if (GUILayout.Button("Center X"))
            {
                AlignX(0.5f);
            }
            if (GUILayout.Button("Right"))
            {
                AlignX(1f);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Bottom"))
            {
                AlignY(0f);
            }
            if (GUILayout.Button("Center Y"))
            {
                AlignY(0.5f);
            }
            if (GUILayout.Button("Top"))
            {
                AlignY(1f);
            }
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
        if (selectedRect == null) return;
        if (selectedRect.parent == null) return;

        Undo.RecordObject(selectedRect, "Align X");

        RectTransform parent = selectedRect.parent as RectTransform;
        float x = Mathf.Lerp(-parent.rect.width * 0.5f, parent.rect.width * 0.5f, normalized);
        selectedRect.anchoredPosition = new Vector2(x, selectedRect.anchoredPosition.y);
    }

    private void AlignY(float normalized)
    {
        if (selectedRect == null) return;
        if (selectedRect.parent == null) return;

        Undo.RecordObject(selectedRect, "Align Y");

        RectTransform parent = selectedRect.parent as RectTransform;
        float y = Mathf.Lerp(-parent.rect.height * 0.5f, parent.rect.height * 0.5f, normalized);
        selectedRect.anchoredPosition = new Vector2(selectedRect.anchoredPosition.x, y);
    }

    private void DrawDebugSection()
    {
        foldDebug = EditorGUILayout.BeginFoldoutHeaderGroup(foldDebug, "Debug");
        if (!foldDebug)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUI.indentLevel++;

        if (selectedRect != null)
        {
            EditorGUILayout.LabelField("Pos", selectedRect.anchoredPosition.ToString("F1"));
            EditorGUILayout.LabelField("Size", selectedRect.sizeDelta.ToString("F1"));
            EditorGUILayout.LabelField("AnchorMin", selectedRect.anchorMin.ToString("F2"));
            EditorGUILayout.LabelField("AnchorMax", selectedRect.anchorMax.ToString("F2"));
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void ApplyThrottled(SerializedObject so)
    {
        if (!so.hasModifiedProperties)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now - lastApply >= applyInterval)
        {
            so.ApplyModifiedProperties();
            lastApply = now;
        }

        if (Event.current.type == EventType.Repaint)
        {
            Repaint();
        }
    }

    private void SetPrivateField(object instance, string field, object val)
    {
        var f = instance.GetType().GetField(field,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (f != null)
        {
            f.SetValue(instance, val);
        }
    }
}
