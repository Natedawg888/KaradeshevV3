using System.Text;
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

    // Reused every interval — no per-update heap alloc
    private readonly StringBuilder _sb = new StringBuilder(32);

    private void Reset()
    {
        fpsText = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (fpsText == null)
            fpsText = GetComponent<TMP_Text>();

        if (fpsText == null) {}
            //Debug.LogWarning($"{nameof(FPSCounter)}: No TMP_Text assigned/found. Assign one in the inspector.");
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _timer += dt;
        _frames++;
        _accumulatedUnscaledTime += dt;

        if (_timer < updateInterval) return;

        float avgDelta = (_frames > 0) ? (_accumulatedUnscaledTime / _frames) : 0f;
        float fps = (avgDelta > 0f) ? (1f / avgDelta) : 0f;
        float ms  = avgDelta * 1000f;

        if (fpsText != null)
        {
            // Build string with no heap alloc: Append(int/char) + SetText(StringBuilder)
            int fpsI    = Mathf.RoundToInt(fps);
            int msWhole = (int)ms;
            int msFrac  = Mathf.Clamp((int)((ms - msWhole) * 10f), 0, 9);

            _sb.Clear();
            _sb.Append(fpsI).Append(" FPS");
            if (showMs)
                _sb.Append("  (").Append(msWhole).Append('.').Append(msFrac).Append(" ms)");

            fpsText.SetText(_sb);
        }

        _timer = 0f;
        _frames = 0;
        _accumulatedUnscaledTime = 0f;
    }
}
