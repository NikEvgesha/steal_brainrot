using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FriendRequestRowView : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text codeText;
    public Button acceptButton;
    public Button declineButton;

    public void Bind(FriendsApi.FriendRequestItem item, Action onAccept, Action onDecline)
    {
        nameText.text = string.IsNullOrEmpty(item.displayName) ? "Player" : item.displayName;
        codeText.text = item.friendCode;

        acceptButton.onClick.RemoveAllListeners();
        declineButton.onClick.RemoveAllListeners();

        acceptButton.onClick.AddListener(() => onAccept?.Invoke());
        declineButton.onClick.AddListener(() => onDecline?.Invoke());
    }
}
