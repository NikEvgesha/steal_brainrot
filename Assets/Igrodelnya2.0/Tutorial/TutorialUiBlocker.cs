using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialUiBlocker : MonoBehaviour
{
    private static readonly string[] BlockedNameTokens =
    {
        "inventory", "shop", "roulette", "settings", "friend", "quest",
        "playtime", "reward", "album", "specialoffer"
    };

    private readonly Dictionary<Button, bool> _originalStates = new();
    private TutorialView _tutorialView;
    private bool _albumAllowed;
    private float _nextRefresh;

    public void Begin(TutorialView tutorialView)
    {
        _tutorialView = tutorialView;
        Refresh(force: true);
    }

    public void SetAlbumAllowed(bool allowed)
    {
        if (_albumAllowed == allowed)
            return;

        _albumAllowed = allowed;
        Restore();
        Refresh(force: true);
    }

    private void Update()
    {
        Refresh(force: false);
    }

    public void End()
    {
        Restore();
        _tutorialView = null;
    }

    private void OnDisable()
    {
        Restore();
    }

    private void OnDestroy()
    {
        Restore();
    }

    private void Refresh(bool force)
    {
        if (!force && Time.unscaledTime < _nextRefresh)
            return;

        _nextRefresh = Time.unscaledTime + 0.75f;
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || _originalStates.ContainsKey(button) || !ShouldBlock(button))
                continue;

            _originalStates[button] = button.interactable;
            button.interactable = false;
        }
    }

    private bool ShouldBlock(Button button)
    {
        if (_tutorialView != null && button.transform.IsChildOf(_tutorialView.transform))
            return false;
        if (button.GetComponentInParent<InteractionPanel>(true) != null)
            return false;
        if (button.GetComponentInParent<ControlUI>(true) != null)
            return false;

        string hierarchyName = BuildHierarchyName(button.transform).ToLowerInvariant();
        bool albumButton = hierarchyName.Contains("album") || button.GetComponentInParent<AlbumScreenController>(true) != null;
        if (_albumAllowed && (albumButton || hierarchyName.Contains("reward")))
            return false;

        for (int i = 0; i < BlockedNameTokens.Length; i++)
        {
            if (hierarchyName.Contains(BlockedNameTokens[i]))
                return true;
        }

        return false;
    }

    private static string BuildHierarchyName(Transform current)
    {
        string result = string.Empty;
        int depth = 0;
        while (current != null && depth++ < 6)
        {
            result += "/" + current.name;
            current = current.parent;
        }

        return result;
    }

    private void Restore()
    {
        foreach (KeyValuePair<Button, bool> pair in _originalStates)
        {
            if (pair.Key != null)
                pair.Key.interactable = pair.Value;
        }

        _originalStates.Clear();
    }
}
