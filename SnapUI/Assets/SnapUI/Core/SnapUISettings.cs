using UnityEngine;

[CreateAssetMenu(fileName = "SnapUISettings", menuName = "SnapUI/Settings")]
public class SnapUISettings : ScriptableObject
{
    public static SnapUISettings Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<SnapUISettings>("SnapUISettings");

                if (instance == null)
                {
                    Debug.LogWarning("SnapUISettings not found in Resources folder. Creating a temporary instance.");
                    instance = ScriptableObject.CreateInstance<SnapUISettings>();
                }
            }
            return instance;
        }
    }

    private static SnapUISettings instance;

    [Header("Theme")]
    public UITheme defaultTheme;

    [Header("Fonts")]
    public Font defaultFont;

    [Header("Animations")]
    public float animationDuration = 0.25f;

    [Header("Options")]
    public bool enableShadows = true;
}
