using System.Collections;
using TMPro;
using UnityEngine;

public class RemoteFriendBoard : MonoBehaviour
{
    [SerializeField] private InteractionPanel interactionPanel;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string friendLabel = "Друг";
    [SerializeField] private string addLabel = "Добавить в друзья";
    [SerializeField] private string sentLabel = "Запрос отправлен";
    [SerializeField] private string offlineLabel = "Оффлайн";

    private FriendsApi api;
    private bool playerInArea;
    private bool isFriend;
    private bool isOnline;
    private string friendCode;
    private string displayName;
    private bool requestInFlight;

    private void Awake()
    {
        if (api == null) api = G.Backend.FriendsApi;
        if (interactionPanel == null) interactionPanel = GetComponentInChildren<InteractionPanel>(true);
    }

    private void OnEnable()
    {
        if (interactionPanel != null)
            interactionPanel.InteractionComplete.AddListener(OnInteract);
    }

    private void OnDisable()
    {
        if (interactionPanel != null)
            interactionPanel.InteractionComplete.RemoveListener(OnInteract);
    }

    public void SetRemote(string code, string name, bool friend, bool online)
    {
        friendCode = code;
        displayName = name;
        isFriend = friend;
        isOnline = online;

        if (nameText != null)
            nameText.text = string.IsNullOrEmpty(displayName) ? "Player" : displayName;

        gameObject.SetActive(online);
        UpdatePanel();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInArea = true;
        UpdatePanel();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInArea = false;
        if (interactionPanel != null) interactionPanel.gameObject.SetActive(false);
    }

    private void UpdatePanel()
    {
        if (interactionPanel == null) return;
        if (!playerInArea)
        {
            interactionPanel.gameObject.SetActive(false);
            return;
        }

        if (!isOnline)
        {
            interactionPanel.gameObject.SetActive(false);
            if (statusText != null) statusText.text = offlineLabel;
            return;
        }

        if (statusText != null)
            statusText.text = isFriend ? friendLabel : addLabel;

        interactionPanel.gameObject.SetActive(!isFriend);
        if (!isFriend)
            interactionPanel.SetInfo(addLabel);
    }

    private void OnInteract()
    {
        if (requestInFlight) return;
        if (isFriend || !isOnline) return;
        if (string.IsNullOrWhiteSpace(friendCode)) return;

        StartCoroutine(SendRequest());
    }

    private IEnumerator SendRequest()
    {
        requestInFlight = true;
        bool ok = false;
        yield return api.SendFriendRequest(friendCode, success => ok = success);
        if (ok)
        {
            if (statusText != null) statusText.text = sentLabel;
            if (interactionPanel != null) interactionPanel.gameObject.SetActive(false);
        }
        requestInFlight = false;
    }
}
