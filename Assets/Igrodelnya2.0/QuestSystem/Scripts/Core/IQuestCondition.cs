public interface IQuestCondition
{
    void Initialize();              // подписаться на события
    bool IsSatisfied { get; }       // выполнено ли условие
    float GetProgressNormalized();  // прогресс 0..1
    void Dispose();                 // отписаться от событий
}
