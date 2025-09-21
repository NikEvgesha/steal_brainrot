using UnityEngine;
using UnityEngine.Events;


[System.Serializable]
public struct EggData
{
    public string Name;
    //public Rarity Rarity;
    public float Price;
}
public class Egg : MonoBehaviour
{
    [SerializeField] private EggData _data;
    [SerializeField] private EggInfoUI _infoUI;
    [SerializeField] private InteractionPanel _buyPanel;

    private EggStatus _status;
    private FieldCell _currentCell;

    public EggData Data { get { return _data; } }
    public UnityEvent<Egg> EggPurchased;


    private void Start()
    {
        _infoUI.SetInfo(Data);
        _status = EggStatus.Conveyer;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _buyPanel.gameObject.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _buyPanel.gameObject.SetActive(false);
        }
    }


    public void TryBuy()
    {
        if (CurrencyManager.Instance.CheckEnoughCurrency(CurrencyType.Coins, _data.Price))
        {
            CurrencyManager.Instance.RemoveCurrency(CurrencyType.Coins, _data.Price);
            transform.SetParent(PlayerManager.Instance.transform);
            transform.localPosition = Vector3.up;
            EggPurchased.Invoke(this);
            _status = EggStatus.Purchased;
            Destroy(_infoUI.gameObject);
            Destroy(_buyPanel.gameObject);
        }
        else
        {
            // Show currency shop
        }
    }

}
