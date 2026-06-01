using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuShortcutHotkeys : MonoBehaviour
{
    [SerializeField] private Button inventoryButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private KeyCode inventoryKey = KeyCode.Tab;
    [SerializeField] private KeyCode shopKey = KeyCode.B;

    private void Update()
    {
        if (ShouldIgnoreInput())
            return;

        TryInvoke(inventoryKey, inventoryButton);
        TryInvoke(shopKey, shopButton);
    }

    private static void TryInvoke(KeyCode key, Button button)
    {
        if (key == KeyCode.None || button == null || !button.interactable || !button.gameObject.activeInHierarchy)
            return;

        if (Input.GetKeyDown(key))
            button.onClick.Invoke();
    }

    private static bool ShouldIgnoreInput()
    {
        if (G.Control != null && G.Control.UseTouchControl)
            return true;

        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null)
            return false;

        return selected.GetComponentInParent<TMP_InputField>() != null || selected.GetComponentInParent<InputField>() != null;
    }
}
