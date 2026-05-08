using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [SerializeField] private TMP_Text fpsText;

    [Header("Smoothing")]
    [Tooltip("How quickly the displayed FPS updates. Higher = smoother but slower to react.")]
    [SerializeField, Range(0.05f, 1f)] private float updateInterval = 0.25f;

    [Tooltip("Optional: show frame time (ms) as well.")]
    [SerializeField] private bool showMs = true;

    private float _timer;
    private int _frames;
    private float _accumulatedUnscaledTime;

    private void Reset()
    {
        // Try auto-find on same object
        fpsText = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (fpsText == null)
            fpsText = GetComponent<TMP_Text>();

        if (fpsText == null)
            //Debug.LogWarning($"{nameof(FPSCounter)}: No TMP_Text assigned/found. Assign one in the inspector.");
    }

    private void Update()
    {
        // Use unscaled time so the FPS counter still reads correctly even if Time.timeScale changes.
        float dt = Time.unscaledDeltaTime;

        _timer += dt;
        _frames++;
        _accumulatedUnscaledTime += dt;

        if (_timer < updateInterval) return;

        float avgDelta = (_frames > 0) ? (_accumulatedUnscaledTime / _frames) : 0f;
        float fps = (avgDelta > 0f) ? (1f / avgDelta) : 0f;
        float ms = avgDelta * 1000f;

        if (fpsText != null)
        {
            if (showMs)
                fpsText.text = $"{fps:0} FPS  ({ms:0.0} ms)";
            else
                fpsText.text = $"{fps:0} FPS";
        }

        _timer = 0f;
        _frames = 0;
        _accumulatedUnscaledTime = 0f;
    }
}
