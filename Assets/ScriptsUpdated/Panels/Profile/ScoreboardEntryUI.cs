using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreboardEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text civNameText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Image profileImage;

    public void SetEntry(int rank, ScoreboardEntry entry, Sprite avatar)
    {
        if (rankText       != null) rankText.text       = rank.ToString();
        if (playerNameText != null) playerNameText.text = entry?.playerName       ?? "---";
        if (civNameText    != null) civNameText.text    = entry?.civilizationName ?? "---";
        if (scoreText      != null) scoreText.text      = entry?.score.ToString() ?? "0";
        if (profileImage   != null) profileImage.sprite = avatar;
    }

    public void SetEmpty(int rank)
    {
        if (rankText       != null) rankText.text       = rank.ToString();
        if (playerNameText != null) playerNameText.text = "---";
        if (civNameText    != null) civNameText.text    = "---";
        if (scoreText      != null) scoreText.text      = "0";
        if (profileImage   != null) profileImage.sprite = null;
    }
}
