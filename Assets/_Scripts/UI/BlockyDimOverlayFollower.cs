using UnityEngine;
using UnityEngine.UI;

public class BlockyDimOverlayFollower : MonoBehaviour
{
    [SerializeField] private GameObject target;

    private Image _image;

    public void Init(GameObject targetRoot)
    {
        target = targetRoot;
        _image = GetComponent<Image>();
        UpdateVisibility();
    }

    private void Awake()
    {
        _image = GetComponent<Image>();
    }

    private void LateUpdate()
    {
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (_image == null)
            _image = GetComponent<Image>();

        if (_image != null)
            _image.enabled = target != null && target.activeInHierarchy;
    }
}
