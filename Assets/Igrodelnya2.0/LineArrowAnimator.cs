using UnityEngine;

public class LineArrowAnimator : MonoBehaviour
{
    public float speed = 1.0f; // скорость анимации
    private LineRenderer lr;

    void Start()
    {
        lr = GetComponent<LineRenderer>();
    }

    void Update()
    {
        // Получаем материал, назначенный LineRenderer
        Material mat = lr.material;
        // Изменяем смещение по оси X для создания эффекта движения
        Vector2 offset = mat.mainTextureOffset;
        offset.x -= Time.deltaTime * speed;
        mat.mainTextureOffset = offset;
    }
}
