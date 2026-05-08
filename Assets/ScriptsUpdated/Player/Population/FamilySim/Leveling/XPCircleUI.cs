using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class XPCircleUI : MonoBehaviour
{
    [Header("Refs")]
    public PlayerLevel player;   // auto-assigned if left null
    public Image circleFill;     // Image.type = Filled, FillMethod = Radial360
    public TMP_Text levelText;   // optional: "Lv 5"

    private void Awake()
    {
        TryAutoAssignPlayer();
    }

    private void OnEnable()
    {
        if (player == null)
            TryAutoAssignPlayer();

        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    public void SetPlayer(PlayerLevel newPlayer, bool refreshNow = true)
    {
        if (player == newPlayer)
        {
            if (refreshNow)
                Refresh();
            return;
        }

        Unsubscribe();
        player = newPlayer;

        if (isActiveAndEnabled)
            Subscribe();

        if (refreshNow)
            Refresh();
    }

    private void TryAutoAssignPlayer()
    {
        if (player != null)
            return;

        player = PlayerLevel.Instance != null
            ? PlayerLevel.Instance
            : FindObjectOfType<PlayerLevel>();

        if (player == null)
            //Debug.LogWarning("[XPCircleUI] No PlayerLevel found in scene.");
    }

    private void Subscribe()
    {
        if (player == null)
            return;

        player.OnXPChanged -= HandleXPChanged;
        player.OnLevelUp -= HandleLevelUp;

        player.OnXPChanged += HandleXPChanged;
        player.OnLevelUp += HandleLevelUp;
    }

    private void Unsubscribe()
    {
        if (player == null)
            return;

        player.OnXPChanged -= HandleXPChanged;
        player.OnLevelUp -= HandleLevelUp;
    }

    private void HandleXPChanged(int currentXP, int xpToNext) => Refresh();
    private void HandleLevelUp(int newLevel) => Refresh();

    public void Refresh()
    {
        if (player == null)
            return;

        if (circleFill != null)
            circleFill.fillAmount = player.Progress01;

        if (levelText != null)
            levelText.text = $"{player.currentLevel}";
    }
}
