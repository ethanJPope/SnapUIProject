using UnityEngine;
using UnityEngine.UI;

public class ThemeSwitcher : MonoBehaviour
{
    public Button lightButton;
    public Button darkButton;
    public UITheme lightTheme;
    public UITheme darkTheme;

    void Start()
    {
        lightButton.onClick.AddListener(() => ThemeManager.SetTheme(lightTheme));

        darkButton.onClick.AddListener(() => ThemeManager.SetTheme(darkTheme));
    }

}
