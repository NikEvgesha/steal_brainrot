using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ArrowLine : MonoBehaviour
{
    [Tooltip("Скорость смещения текстуры (эффект движения)")]
    public float textureScrollSpeed = 1f;
    [Tooltip("Ширина одной стрелки (в единицах мира)")]
    public float arrowWidth = 1f;

    private LineRenderer lr;
    private Material lineMaterial;
    private bool _isActive = false;

    private Transform _startTransform;
    private Transform _endTransform;
    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        if (lr == null)
        {
            Debug.LogError("LineRenderer не найден!");
            return;
        }
        // Создаём копию материала, чтобы не изменять sharedMaterial
        lr.material = new Material(lr.material);
        lineMaterial = lr.material;
        ActiveArrowLine(false);
    }

    private void Update()
    {
        if (!_isActive)
            return;
        AnimationArrow();
    }
    private void AnimationArrow()
    {
        if (_startTransform && _endTransform)
        {
            // Обновляем позиции начала и конца линии
            lr.SetPosition(0, _startTransform.position);
            lr.SetPosition(1, _endTransform.position);

            // Вычисляем длину линии и задаём tiling так, чтобы стрелки равномерно распределялись
            float distance = Vector3.Distance(_startTransform.position, _endTransform.position);
            lineMaterial.mainTextureScale = new Vector2(distance / arrowWidth, 1);
        } 
        else
        {
            ActiveArrowLine(false);
            return;
        }

        // Обновляем смещение текстуры для создания эффекта движения
        Vector2 offset = lineMaterial.mainTextureOffset;
        // Используем операцию остатка, чтобы значение оставалось в диапазоне [0, 1]
        offset.x = (offset.x - textureScrollSpeed * Time.deltaTime) % 1f;
        lineMaterial.mainTextureOffset = offset;
    }
    private void SetNewTarget(Transform start,Transform end)
    {
        _startTransform = start;
        _endTransform = end;
    }
    public void StartArrowLine(Transform start, Transform end)
    {
        ActiveArrowLine(start && end);
        SetNewTarget(start, end);
    }
    public void ActiveArrowLine(bool isActive)
    {
        _isActive = isActive;
        lr.enabled = _isActive;
    }
}
