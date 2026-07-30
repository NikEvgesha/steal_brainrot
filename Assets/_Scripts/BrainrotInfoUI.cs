using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BrainrotInfoUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _income;
    [SerializeField] private TMP_Text _accumulationIncome;
    [SerializeField] private TMP_Text _offlineIncome;
    [SerializeField] private float _remoteVisibleDistance = 30f;
    [SerializeField] private float _remoteVisibleDistanceHysteresis = 4f;
    private bool _remoteView;
    private void Start()
    {
        if (_offlineIncome != null)
            _offlineIncome.gameObject.SetActive(false);
    }

    public void SetInfo(BrainrotTypeData data, BrainrotDinamicData dinamicData)
    {
        //_name.text = data.Name;
        if (_accumulationIncome != null)
            _accumulationIncome.text = CurrencyText.Coins("0");
        if (_income != null)
            _income.text = CurrencyText.Coins(FormatAmount(dinamicData.ResultIncome)) + "/s";
    }
    public void SetInfo(double income)
    {
        //_name.text = data.Name;
        if (_accumulationIncome != null)
            _accumulationIncome.text = CurrencyText.Coins("0");
        if (_income != null)
            _income.text = CurrencyText.Coins(FormatAmount(income)) + "/s";
    }


    public void UpdateIncome(double income)
    {
        if (_accumulationIncome != null)
            _accumulationIncome.text = CurrencyText.Coins(FormatAmount(income));
    }
    public void UpdateOfflineIncome(double income)
    {
        if (_offlineIncome != null)
            _offlineIncome.gameObject.SetActive(false);
    }

    public void SetRemoteView(bool remote)
    {
        _remoteView = remote;
        if (_accumulationIncome != null)
            _accumulationIncome.gameObject.SetActive(!remote);
        if (_offlineIncome != null)
            _offlineIncome.gameObject.SetActive(false);

        var distanceVisibility = WorldUiDistanceVisibility.Ensure(gameObject);
        if (distanceVisibility != null)
            distanceVisibility.Configure(remote, ResolveDistanceTarget(), _remoteVisibleDistance, _remoteVisibleDistanceHysteresis);
    }

    private static string FormatAmount(double amount)
    {
        if (G.Currency != null)
            return G.Currency.ToString(amount);

        return Math.Round(amount).ToString("0", CultureInfo.InvariantCulture);
    }

    private Transform ResolveDistanceTarget()
    {
        var brainrot = GetComponentInParent<Brainrot>();
        return brainrot != null ? brainrot.transform : transform;
    }
}
