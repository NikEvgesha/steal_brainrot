using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FriendRowView : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text codeText;
    public TMP_Text onlineText;
    public TMP_Text offlineText;
    public Button viewButton;
    public Button removeButton;

    public void Bind(FriendsApi.FriendItem item, Action onRemove, Action onView)
    {
        nameText.text = string.IsNullOrEmpty(item.displayName) ? LocalizationUtils.T("UI/Common/Player", "Player") : item.displayName;
        codeText.text = item.friendCode;

        onlineText.gameObject.SetActive(item.isOnline);
        offlineText.gameObject.SetActive(!item.isOnline);
        viewButton.gameObject.SetActive(item.isOnline);
        //onlineText.text = item.isOnline ? "Online" : "Offline";

        viewButton.onClick.RemoveAllListeners();
        removeButton.onClick.RemoveAllListeners();

        viewButton.onClick.AddListener(() => onView?.Invoke());
        removeButton.onClick.AddListener(() => onRemove?.Invoke());
    }
}
