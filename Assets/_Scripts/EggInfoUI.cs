using System;
using System.Globalization;
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

    public void SetInfo(Egg egg)
    {
        _name.text = egg.Name;
        _luck.text = FormatAmount(egg.Data.Luck) + "X";
        _price.text = "$" + FormatAmount(egg.Data.Price * G.Elements.GetMultiplaer(egg.Data.DinamicData.ElementType));
    }

    private static string FormatAmount(double amount)
    {
        if (G.Currency != null)
            return G.Currency.ToString(amount);

        if (double.IsNaN(amount) || double.IsInfinity(amount))
            return "0";

        return Math.Round(Math.Max(0d, amount)).ToString("0", CultureInfo.InvariantCulture);
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
        // �����: ����� ��������� ����� X ��� Y �
        TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(remainingSec, 0));
        _time.text = $"���� ��������� ����� {FormatRus(t)}";

        // ��������: 0..1
        _progress.value = progress01;

        // �������� � �������: 34,1%
        float percent = progress01 * 100f;
        _percent.text = percent.ToString("0.0") + "%";

    }
    private string FormatRus(TimeSpan t)
    {
        // ���������: ���������� ��� + ��� (����� ��������� �� �����/����)
        int m = (int)t.TotalMinutes;
        int s = t.Seconds;

        if (t.TotalHours >= 1)
        {
            int h = (int)t.TotalHours;
            m = t.Minutes;
            return $"{h} {Plural(h, "���", "����", "�����")} {m} {Plural(m, "������", "������", "�����")}";
        }

        if (m > 0)
            return $"{m} {Plural(m, "������", "������", "�����")} {s} {Plural(s, "�������", "�������", "������")}";

        return $"{s} {Plural(s, "�������", "�������", "������")}";
    }
    private string Plural(int n, string form1, string form2, string form5)
    {
        // ������� ��������� 1,2-4,5-0
        n = Mathf.Abs(n) % 100;
        int n1 = n % 10;
        if (n > 10 && n < 20) return form5;
        if (n1 > 1 && n1 < 5) return form2;
        if (n1 == 1) return form1;
        return form5;
    }

}
