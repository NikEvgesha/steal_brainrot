using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))] // Автоматически требует компонент Button
public class UISound : MonoBehaviour
{
    private Button _button; // Ссылка на кнопку

    void Awake()
    {
        // Получаем компонент кнопки с того же объекта
        _button = GetComponent<Button>();


        // Добавляем обработчик нажатия кнопки
        _button.onClick.AddListener(PlaySound);
    }

    // Метод для воспроизведения звука
    private void PlaySound()
    {
        G.Sound.PlayUIClick();
    }

    // Очистка слушателя при уничтожении объекта
    void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(PlaySound);
        }
    }
}