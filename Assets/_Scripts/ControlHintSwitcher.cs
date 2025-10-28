using UnityEngine;

public class ControlHintSwitcher : MonoBehaviour
{
    [SerializeField] private GameObject _desktopHint;
    [SerializeField] private GameObject _mobileHint;


    private void Start()
    {
        bool isMobile = G.Control.UseTouchControl;
        if (_desktopHint != null)
        {
            _desktopHint.SetActive(!isMobile);
        }
        if (_mobileHint != null)
        {
            _mobileHint.SetActive(isMobile);
        }
    }
}
