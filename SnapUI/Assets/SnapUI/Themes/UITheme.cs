using UnityEngine;
using TMPro;
[CreateAssetMenu(fileName = "UITheme", menuName = "SnapUI/UI Theme")]
public class UITheme : ScriptableObject
{
    [Header("Colors")]
    public Color primaryColor = Color.white;
    public Color secondaryColor = Color.gray;
    public Color backgroundColor = new Color(0.5f, 0.5f, 0.5f);
    public Color textColor = Color.white;
    public Color accentColor = new Color(0.2f, 0.5f, 1f);

    [Header("Styling")]
    public float borderRadius = 8f;
    public float shadowStrength = 0.4f;

    [Header("Fonts")]
    public TMP_FontAsset  mainFont;
}
