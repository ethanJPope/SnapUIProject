using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering.Universal;

public class ThemedImage : BaseUIComponent
{
    public enum ColorTarget
    {
        Primary,
        Secondary,
        Background,
        Accent
    }

    [SerializeField] private ColorTarget colorTarget = ColorTarget.Primary;

    private Image img;

    public override void Initialize()
    {
        img = GetComponent<Image>();
    }

    public override void RefreshUI()
    {
        var theme = ThemeManager.ActiveTheme;

        switch (colorTarget)
        {
            case ColorTarget.Primary:
                img.color = theme.primaryColor;
                break;
            case ColorTarget.Secondary:
                img.color = theme.secondaryColor;
                break;
            case ColorTarget.Background:
                img.color = theme.backgroundColor;
                break;
            case ColorTarget.Accent:
                img.color = theme.accentColor;
                break;
        }
    }
}
