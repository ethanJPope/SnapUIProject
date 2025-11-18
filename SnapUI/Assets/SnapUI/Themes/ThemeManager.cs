using System.Collections.Generic;
using UnityEngine;

public class ThemeManager : MonoBehaviour
{
    private static UITheme activeTheme;
    private static List<BaseUIComponent> registeredComponents = new List<BaseUIComponent>();

    public static UITheme ActiveTheme
    {
        get
        {
            if (activeTheme == null)
            {
                var settings = SnapUISettings.Instance;
                if (settings != null && settings.defaultTheme != null)
                {
                    activeTheme = settings.defaultTheme;
                }
                else
                {
                    Debug.LogWarning("ThemeManager had no active theme. Creating a temporary fallback theme.");
                    activeTheme = ScriptableObject.CreateInstance<UITheme>();
                }
            }
            return activeTheme;
        }
    }

    public static void SetTheme(UITheme theme)
    {
        activeTheme = theme;
        NotifyAllComponents();
    }

    public static void RegisterComponent(BaseUIComponent component)
    {
        if (component == null) return;

        if (!registeredComponents.Contains(component))
        {
            registeredComponents.Add(component);
            component.OnThemeChanged(ActiveTheme);
        }
    }

    public static void UnregisterComponent(BaseUIComponent component)
    {
        if (component == null) return;

        if (registeredComponents.Contains(component))
        {
            registeredComponents.Remove(component);
        }
    }

    private static void NotifyAllComponents()
    {
        foreach (var component in registeredComponents)
        {
            component.OnThemeChanged(activeTheme);
        }
    }

}
