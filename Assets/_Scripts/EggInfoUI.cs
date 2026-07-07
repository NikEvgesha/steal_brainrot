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
    [SerializeField] private float _remoteVisibleDistance = 30f;
    [SerializeField] private float _remoteVisibleDistanceHysteresis = 4f;

    private EggStatus _status;
    private bool _remoteView;

    public void SetInfo(Egg egg)
    {
        if (egg == null)
            return;

        float elementMultiplier = G.Elements != null ? G.Elements.GetMultiplaer(egg.Data.DinamicData.ElementType) : 1f;
        if (_name != null)
            _name.text = LocalizationUtils.T("Item/" + egg.Name, egg.Name);
        if (_luck != null)
            _luck.text = FormatAmount(egg.Data.Luck) + "X";
        if (_price != null)
            _price.text = "$" + FormatAmount(egg.Data.Price * elementMultiplier);
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

        if (_buy != null)
            _buy.SetActive(false);
        if (_hutching != null)
            _hutching.SetActive(false);

        _status = status;
        switch (_status)
        {
            case EggStatus.Conveyer:
                if (_buy != null)
                    _buy.SetActive(true);
                break;
                
            case EggStatus.Maturing:
                if (!_remoteView && _hutching != null)
                    _hutching.SetActive(true);
                break;
            default:
                break;
        }
    }
    public void SetRemoteView(bool remote)
    {
        _remoteView = remote;
        if (_remoteView && _hutching != null)
            _hutching.SetActive(false);

        var distanceVisibility = WorldUiDistanceVisibility.Ensure(gameObject);
        if (distanceVisibility != null)
            distanceVisibility.Configure(remote, ResolveDistanceTarget(), _remoteVisibleDistance, _remoteVisibleDistanceHysteresis);
    }
    public void ShowTimeUI(int remainingSec, float progress01)
    {
        if (_remoteView)
        {
            if (_hutching != null)
                _hutching.SetActive(false);
            return;
        }

        // �����: ����� ��������� ����� X ��� Y �
        bool isReady = remainingSec <= 0;
        TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(remainingSec, 0));
        if (_hutching != null && !_hutching.activeSelf)
            _hutching.SetActive(true);
        if (_time != null)
        {
            _time.gameObject.SetActive(!isReady);
            _time.text = isReady
                ? string.Empty
                : LocalizationUtils.Format("UI/Egg/ReadyIn", "Ready in {0}", FormatTime(t));
        }

        // ��������: 0..1
        if (_progress != null)
            _progress.value = progress01;

        // �������� � �������: 34,1%
        float percent = progress01 * 100f;
        if (_percent != null)
            _percent.text = percent.ToString("0.0") + "%";

    }

    private static string FormatTime(TimeSpan t)
    {
        if (t.TotalHours >= 1d)
            return string.Format(CultureInfo.InvariantCulture, "{0:0}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds);

        return string.Format(CultureInfo.InvariantCulture, "{0:0}:{1:00}", (int)t.TotalMinutes, t.Seconds);
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

    private Transform ResolveDistanceTarget()
    {
        var egg = GetComponentInParent<Egg>();
        return egg != null ? egg.transform : transform;
    }

}
