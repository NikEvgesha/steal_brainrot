using System;
using UnityEngine;
using UnityEngine.UI;

public class EggInfoUI : MonoBehaviour
{
    [SerializeField] private GameObject _buy;
    [SerializeField] private Text _name;
    [SerializeField] private Text _luck; 
    [SerializeField] private Text _price;
    [SerializeField] private GameObject _hutching;
    [SerializeField] private Text _time;
    [SerializeField] private Slider _progress;
    [SerializeField] private Text _percent;

    private EggStatus _status;

    public void SetInfo(EggData data)
    {
        _name.text = data.Name;
        _luck.text = data.Luck+"X"+ " Удача" ;
        _price.text = "$"+ (data.Price * ElementTypeMultiplaer.Init.GetMultiplaer(data.DinamicData.Type)).ToString();
    }
    public void SetStatus(EggStatus status)
    {

        _buy.SetActive(false);
        _hutching.SetActive(false);

        _status = status;
        switch (_status)
        {
            case EggStatus.Conveyer:
                _buy.SetActive(true);
                break;
            case EggStatus.Maturing:
                _hutching.SetActive(true);
                break;
            default:
                break;
        }
    }
    public void ShowTimeUI(int remainingSec, float progress01)
    {
        // Текст: «Яйцо вылупится через X мин Y с»
        TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(remainingSec, 0));
        _time.text = $"Яйцо вылупится через {FormatRus(t)}";

        // Прогресс: 0..1
        _progress.value = progress01;

        // Проценты с запятой: 34,1%
        float percent = progress01 * 100f;
        _percent.text = percent.ToString("0.0") + "%";

    }
    private string FormatRus(TimeSpan t)
    {
        // Упрощённо: показываем мин + сек (можно расширить до часов/дней)
        int m = (int)t.TotalMinutes;
        int s = t.Seconds;

        if (t.TotalHours >= 1)
        {
            int h = (int)t.TotalHours;
            m = t.Minutes;
            return $"{h} {Plural(h, "час", "часа", "часов")} {m} {Plural(m, "минуту", "минуты", "минут")}";
        }

        if (m > 0)
            return $"{m} {Plural(m, "минуту", "минуты", "минут")} {s} {Plural(s, "секунду", "секунды", "секунд")}";

        return $"{s} {Plural(s, "секунду", "секунды", "секунд")}";
    }
    private string Plural(int n, string form1, string form2, string form5)
    {
        // Русское склонение 1,2-4,5-0
        n = Mathf.Abs(n) % 100;
        int n1 = n % 10;
        if (n > 10 && n < 20) return form5;
        if (n1 > 1 && n1 < 5) return form2;
        if (n1 == 1) return form1;
        return form5;
    }

}
