using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PersistentLoadingUI : MonoBehaviour
{
    public static PersistentLoadingUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Image loadingFillImage;

    [Header("Loop Animation")]
    [SerializeField, Min(0.2f)] private float loadingLoopSeconds = 1.2f;
    [SerializeField] private bool pingPongLoop = true;
    [SerializeField] private Image.FillMethod fillMethod = Image.FillMethod.Radial360;
    [SerializeField] private bool fillClockwise = true;
    [SerializeField, Range(0, 3)] private int fillOrigin = 0;

    private Coroutine _loadingLoopCoroutine;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (rootCanvas == null)
            rootCanvas = root.GetComponent<Canvas>();

        if (Instance != null && Instance != this)
        {
            Destroy(root);
            return;
        }

        Instance = this;

        // Persist the whole loader root exactly as-is.
        DontDestroyOnLoad(root);

        ConfigureLoadingImage();
        HideImmediate();
    }

    public void Show()
    {
        if (root != null)
            root.SetActive(true);

        if (rootCanvas != null)
            rootCanvas.enabled = true;
    }

    public void HideImmediate()
    {
        StopLoop(false);

        if (rootCanvas != null)
            rootCanvas.enabled = false;

        if (root != null)
            root.SetActive(false);
    }

    public void StartLoop()
    {
        Show();
        ConfigureLoadingImage();
        StopLoop(false);

        if (loadingFillImage != null)
            _loadingLoopCoroutine = StartCoroutine(AnimateLoadingLoop());
        else
            Debug.LogWarning("[PersistentLoadingUI] loadingFillImage is not assigned.", this);
    }

    public void StopLoop(bool fillComplete)
    {
        if (_loadingLoopCoroutine != null)
        {
            StopCoroutine(_loadingLoopCoroutine);
            _loadingLoopCoroutine = null;
        }

        if (loadingFillImage != null)
            loadingFillImage.fillAmount = fillComplete ? 1f : 0f;
    }

    private void ConfigureLoadingImage()
    {
        if (loadingFillImage == null)
            return;

        loadingFillImage.type = Image.Type.Filled;
        loadingFillImage.fillMethod = fillMethod;
        loadingFillImage.fillClockwise = fillClockwise;
        loadingFillImage.fillOrigin = fillOrigin;
        loadingFillImage.fillAmount = 0f;
    }

    private IEnumerator AnimateLoadingLoop()
    {
        if (loadingFillImage == null)
            yield break;

        float cycle = Mathf.Max(0.2f, loadingLoopSeconds);
        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;

            if (pingPongLoop)
                loadingFillImage.fillAmount = Mathf.PingPong(elapsed / cycle, 1f);
            else
                loadingFillImage.fillAmount = Mathf.Repeat(elapsed, cycle) / cycle;

            yield return null;
        }
    }
}