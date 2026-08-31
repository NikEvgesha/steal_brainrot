using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AdsOverlayView : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform countdownPanel;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private TextMeshProUGUI countdownRewardText;
    [SerializeField] private Button timedRewardX2Button;
    [SerializeField] private TextMeshProUGUI timedRewardX2ButtonText;
    [SerializeField] private RectTransform rewardTextRect;
    [SerializeField] private TextMeshProUGUI rewardText;

    public Canvas Canvas => canvas != null ? canvas : GetComponent<Canvas>();
    public RectTransform CountdownPanel => countdownPanel;
    public TextMeshProUGUI CountdownText => countdownText;
    public TextMeshProUGUI CountdownRewardText => countdownRewardText;
    public Button TimedRewardX2Button => timedRewardX2Button;
    public TextMeshProUGUI TimedRewardX2ButtonText => timedRewardX2ButtonText;
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

        if (timedRewardX2Button == null)
            timedRewardX2Button = FindChildByName(transform, "TimedRewardX2Button")?.GetComponent<Button>();

        if (timedRewardX2ButtonText == null && timedRewardX2Button != null)
            timedRewardX2ButtonText = FindChildByName(timedRewardX2Button.transform, "Label")?.GetComponent<TextMeshProUGUI>();

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
