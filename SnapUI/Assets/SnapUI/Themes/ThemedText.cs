using TMPro;
using UnityEngine;

public class ThemedText : BaseUIComponent
{
    public enum TextColor
    {
        Primary,
        Secondary,
        Accent
    }

    [SerializeField] private TextColor colorType = TextColor.Primary;

    private TMP_Text label;

    public override void Initialize()
    {
        label = GetComponent<TMP_Text>();
    }

    public override void RefreshUI()
    {
        if (label == null)
            label = GetComponent<TMP_Text>();
        if (label == null)
            return;

        var theme = ThemeManager.ActiveTheme;

        switch (colorType)
        {
            case TextColor.Primary:
            case TextColor.Secondary:
            case TextColor.Accent:
                label.color = theme.textColor;
                break;
        }

        if (theme.mainFont != null)
        {
            label.font = theme.mainFont;
        }
    }


}
