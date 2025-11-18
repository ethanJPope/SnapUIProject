using UnityEngine;

public abstract class BaseUIComponent : MonoBehaviour
{
    private bool initialized = false;

    protected virtual void Awake()
    {
        RegisterWithThemeManager();
    }

    protected virtual void OnEnable()
    {
        if (!initialized)
        {
            Initialize();
            initialized = true;
        }

        RefreshUI();
    }

    protected virtual void OnDestroy()
    {
        ThemeManager.UnregisterComponent(this);
    }

    public virtual void Initialize() { }

    public virtual void RefreshUI() { }

    public virtual void OnThemeChanged(UITheme theme)
    {
        RefreshUI();
    }

    private void RegisterWithThemeManager()
    {
        ThemeManager.RegisterComponent(this);
    }
}
