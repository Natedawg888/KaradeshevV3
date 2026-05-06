using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TaskFailedPanelControl : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("UI")]
    public TMP_Text titleText;
    public TMP_Text storyText;
    public TMP_Text populationLostText;
    public Button closeButton;

    public event Action OnClose;

    private bool _isOpen = false;

    private void Awake()
    {
        if (root == null) root = gameObject;

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        // start hidden
        root.SetActive(false);
        _isOpen = false;
    }

    public void Show(EnvironmentControl env, TaskFailureData data)
    {
        if (root == null) root = gameObject;

        transform.SetAsLastSibling();

        _isOpen = true;
        root.SetActive(true);

        string title = data.type == TaskFailureType.Discovery
            ? "Discovery Failed"
            : "Gathering Failed";

        if (titleText != null) titleText.text = title;
        if (storyText != null) storyText.text = data.story ?? "";

        if (populationLostText != null)
        {
            populationLostText.text = (data.populationLost > 0)
                ? $"Population Lost: {data.populationLost}"
                : "Population Lost: None";
        }
    }

    public void Hide()
    {
        if (root == null) root = gameObject;

        bool wasOpen = _isOpen;
        _isOpen = false;

        root.SetActive(false);

        if (wasOpen)
            OnClose?.Invoke();
    }
}
