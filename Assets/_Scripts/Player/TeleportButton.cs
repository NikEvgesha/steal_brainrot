using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class TeleportButton : MonoBehaviour
{
    [SerializeField] private ScenePoint _destination;
    private Button _button;

    [HideInInspector]
    public UnityEvent<ScenePoint> teleportButtonClicked;

    private void Awake()
    {
        _button = GetComponent<Button>();
        BlockyUITheme.StyleTopNavigationButton(_button);
        _button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        teleportButtonClicked?.Invoke(_destination);
    }
}
