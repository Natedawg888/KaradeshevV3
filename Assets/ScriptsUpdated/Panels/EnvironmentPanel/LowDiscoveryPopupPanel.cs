// LowDiscoveryPopupPanel.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LowDiscoveryPopupPanel : MonoBehaviour
{
    [Header("UI")]
    public GameObject root;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public Button closeButton;

    private void Awake()
    {
        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
        Hide();
    }

    public void Show(string title, string body)
    {
        if (titleText) titleText.text = title ?? "Discovery Blocked";
        if (bodyText)  bodyText.text  = body  ?? "Your people are too wary to attempt this right now.";
        if (root) root.SetActive(true);
        else gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
        else gameObject.SetActive(false);
    }
}
