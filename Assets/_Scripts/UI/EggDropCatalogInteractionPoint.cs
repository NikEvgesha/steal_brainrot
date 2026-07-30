using System;
using UnityEngine;

public class EggDropCatalogInteractionPoint : MonoBehaviour
{
    [Header("Scene Refs")]
    [SerializeField] private InteractionPanel interactionPanel;
    [SerializeField] private EggDropCatalogUI catalogUi;
    [SerializeField] private bool autoDiscoverReferences = true;

    [Header("Behavior")]
    [SerializeField] private bool hideInteractionWhileCatalogOpen = true;
    [SerializeField] private bool closeCatalogOnExit = false;
    [SerializeField] private bool refreshCatalogOnOpen = true;

    [Header("Localization")]
    [SerializeField] private string interactionLocalizationKey = "UI/EggCatalog/Open";
    [SerializeField] private string interactionTextFallback = "Egg chances";

    private bool _playerInside;

    private void Awake()
    {
        AutoSetupReferences();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        AutoSetupReferences();
    }

    private void OnEnable()
    {
        if (interactionPanel != null)
            interactionPanel.InteractionComplete.AddListener(OnInteractionRequested);
        RefreshInteractionState();
    }

    private void OnDisable()
    {
        if (interactionPanel != null)
            interactionPanel.InteractionComplete.RemoveListener(OnInteractionRequested);
        HideInteraction();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        _playerInside = true;
        RefreshInteractionState();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        _playerInside = false;
        HideInteraction();
        if (closeCatalogOnExit && catalogUi != null)
            catalogUi.Close();
    }

    public void CloseCatalog()
    {
        if (catalogUi == null)
            return;

        catalogUi.Close();
        RefreshInteractionState();
    }

    private void OnInteractionRequested()
    {
        if (!_playerInside || catalogUi == null)
            return;

        if (refreshCatalogOnOpen)
            catalogUi.Refresh();
        catalogUi.Open();
        RefreshInteractionState();
    }

    private void RefreshInteractionState()
    {
        if (interactionPanel == null)
            return;

        var catalogIsOpen = catalogUi != null && catalogUi.IsOpen;
        var show = _playerInside && (!hideInteractionWhileCatalogOpen || !catalogIsOpen);
        interactionPanel.gameObject.SetActive(show);
        if (show)
            interactionPanel.SetInfoLocalized(interactionLocalizationKey, interactionTextFallback);
    }

    private void HideInteraction()
    {
        if (interactionPanel != null)
            interactionPanel.gameObject.SetActive(false);
    }

    [ContextMenu("EggCatalog/Auto Setup References")]
    private void AutoSetupReferences()
    {
        if (!autoDiscoverReferences)
            return;

        if (interactionPanel == null)
            interactionPanel = GetComponentInChildren<InteractionPanel>(true);

        if (catalogUi == null)
            catalogUi = GetComponentInChildren<EggDropCatalogUI>(true);

        if (catalogUi == null)
        {
            var root = transform.root;
            if (root != null)
                catalogUi = root.GetComponentInChildren<EggDropCatalogUI>(true);
        }
    }

    private static string L(string key, string fallback)
    {
        if (LocalizationManager.Instance != null && LocalizationManager.Instance.LocalizationData != null)
        {
            var translated = LocalizationManager.Instance.LocalizationData.GetTranslation(key);
            if (!string.IsNullOrWhiteSpace(translated) && !string.Equals(translated, key, StringComparison.Ordinal))
                return translated;
        }

        return fallback;
    }
}
