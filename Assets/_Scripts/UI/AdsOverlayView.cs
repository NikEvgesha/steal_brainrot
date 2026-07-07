using TMPro;
using UnityEngine;

public class AdsOverlayView : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform countdownPanel;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private TextMeshProUGUI countdownRewardText;
    [SerializeField] private RectTransform rewardTextRect;
    [SerializeField] private TextMeshProUGUI rewardText;

    public Canvas Canvas => canvas != null ? canvas : GetComponent<Canvas>();
    public RectTransform CountdownPanel => countdownPanel;
    public TextMeshProUGUI CountdownText => countdownText;
    public TextMeshProUGUI CountdownRewardText => countdownRewardText;
    public RectTransform RewardTextRect => rewardTextRect != null ? rewardTextRect : rewardText != null ? rewardText.rectTransform : null;
    public TextMeshProUGUI RewardText => rewardText;

#if UNITY_EDITOR
    private void Reset()
    {
        AutoBind();
    }

    private void OnValidate()
    {
        AutoBind();
    }

    [ContextMenu("Auto Bind")]
    private void AutoBind()
    {
        if (canvas == null)
            canvas = GetComponent<Canvas>();

        if (countdownPanel == null)
            countdownPanel = FindChildByName(transform, "CountdownPanel") as RectTransform;

        if (countdownText == null)
            countdownText = FindChildByName(transform, "CountdownText")?.GetComponent<TextMeshProUGUI>();

        if (countdownRewardText == null)
            countdownRewardText = FindChildByName(transform, "CountdownRewardText")?.GetComponent<TextMeshProUGUI>();

        if (rewardText == null)
            rewardText = FindChildByName(transform, "InterstitialRewardText")?.GetComponent<TextMeshProUGUI>();

        if (rewardTextRect == null && rewardText != null)
            rewardTextRect = rewardText.rectTransform;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
#endif
}
