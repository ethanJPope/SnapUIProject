using UnityEngine;

[CreateAssetMenu(fileName = "UIElementTemplate", menuName = "SnapUI/UI Element Template")]
public class UIElementTemplate : ScriptableObject
{
    public string displayName;
    public GameObject prefab;

    private void OnValidate()
    {
        if(string.IsNullOrEmpty(displayName) && prefab != null)
        {
            displayName = prefab.name;
        }
    }
}
