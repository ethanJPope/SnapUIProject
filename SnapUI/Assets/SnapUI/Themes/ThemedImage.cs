using UnityEngine;
using UnityEngine.UI;

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
        if (img == null)
        {
            img = GetComponent<Image>();
        }

        if (img == null)
        {
            return;
        }

        UITheme theme = ThemeManager.ActiveTheme;
        if (theme == null)
        {
            return;
        }

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
