using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ImmigrantOfferPanel : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("The actual UI root GameObject to show/hide (can be a parent or another object).")]
    [SerializeField] private GameObject panelRoot;

    [Header("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text queueText;

    [SerializeField] private Button acceptButton;
    [SerializeField] private Button declineButton;

    public CameraControl cameraControl;

    private Action _onAccept;
    private Action _onDecline;

    public void Show(ImmigrantOfferManager.ImmigrantOffer offer, Action onAccept, Action onDecline, int remainingAfterThis)
    {
        _onAccept = onAccept;
        _onDecline = onDecline;

        if (acceptButton != null)
        {
            acceptButton.onClick.RemoveAllListeners();
            acceptButton.onClick.AddListener(() => _onAccept?.Invoke());
        }

        if (declineButton != null)
        {
            declineButton.onClick.RemoveAllListeners();
            declineButton.onClick.AddListener(() => _onDecline?.Invoke());
        }

        cameraControl.PushInputLock();

        string envName = (offer.env != null && !string.IsNullOrEmpty(offer.env.environmentName))
            ? offer.env.environmentName
            : (string.IsNullOrEmpty(offer.envNameFallback) ? "this area" : offer.envNameFallback);

        string kind = offer.kind == EnvironmentTaskKind.Discovery ? "Discovery" : "Gathering";

        if (titleText != null) titleText.text = "Immigrants Arrive";

        if (bodyText != null)
        {
            bodyText.text = offer.isNewFamily
                ? $"{kind} succeeded near {envName}.\nA new family wants to join: {offer.adults} adults, {offer.children} children.\nAccept them?"
                : $"{kind} succeeded near {envName}.\n{offer.individuals} people want to join.\nAccept them?";
        }

        if (queueText != null)
        {
            queueText.text = remainingAfterThis > 0
                ? $"{remainingAfterThis} more in queue"
                : "No more in queue";
        }

        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        cameraControl.PopInputLock();

        _onAccept = null;
        _onDecline = null;
        panelRoot.SetActive(false);
    }
}