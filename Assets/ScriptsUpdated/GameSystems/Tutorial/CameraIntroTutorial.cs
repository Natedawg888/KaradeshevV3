using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CameraIntroTutorial : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraControl cameraControl;

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private GameObject darkOverlay;
    [SerializeField] private GameObject darkOverlayWithHole;
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button skipButton;

    [Header("Settings")]
    [SerializeField] private bool resumeTurnTimerWhenFinished = true;
    [SerializeField] private float pinchDeltaThreshold = 20f;
    [SerializeField] private float minimapRotateYawThreshold = 20f;

    [SerializeField] private EnvironmentTileTutorial environmentTileTutorial;

    private bool _running;
    private bool _waitingForDrag;
    private bool _waitingForDragRelease;
    private bool _waitingForZoom;
    private bool _waitingForRotate;
    private bool _cameraLockedByTutorial;
    private bool _completedThisGame;
    private bool _zoomedIn;
    private bool _zoomedOut;
    private bool _startedMinimapRotate;
    private float _minimapRotateStartYaw;

    private bool _hasStarterWorldPoint;
    private Vector3 _starterWorldPoint;

    private bool _hasStarterTarget;
    private GameObject _starterTarget;

    public bool IsRunning => _running;
    public bool HasCompletedTutorial => _completedThisGame;

    public bool ShouldRunTutorial()
    {
        return !_running && !_completedThisGame;
    }

    private void Awake()
    {
        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>();

        _completedThisGame = false;

        BindButtons();
        SetRootVisible(false);
        SetBlockingMode(false);
        SetDarkOverlayState(false, false);
    }

    public void InstallRuntimeRefs(CameraControl newCameraControl = null, EnvironmentTileTutorial newEnvironmentTileTutorial = null)
    {
        if (newCameraControl != null)
            cameraControl = newCameraControl;

        if (newEnvironmentTileTutorial != null)
            environmentTileTutorial = newEnvironmentTileTutorial;

        BindButtons();
    }

    private void BindButtons()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinuePressed);
            continueButton.onClick.AddListener(OnContinuePressed);
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(OnSkipPressed);
            skipButton.onClick.AddListener(OnSkipPressed);
        }
    }

    public void BeginTutorial()
    {
        if (!ShouldRunTutorial())
        {
            if (resumeTurnTimerWhenFinished)
                TurnSystem.Instance?.ResumeTurnTimer();

            return;
        }

        _running = true;
        _waitingForDrag = false;
        _waitingForDragRelease = false;
        _waitingForZoom = false;
        _waitingForRotate = false;
        _zoomedIn = false;
        _zoomedOut = false;
        _startedMinimapRotate = false;
        _minimapRotateStartYaw = 0f;

        TurnSystem.Instance?.PauseTurnTimer();

        if (cameraControl != null)
        {
            cameraControl.PushInputLock();
            _cameraLockedByTutorial = true;
        }

        SetRootVisible(true);
        SetBlockingMode(true);
        SetDarkOverlayState(true, false);

        if (continueButton != null)
            continueButton.gameObject.SetActive(true);

        if (skipButton != null)
            skipButton.gameObject.SetActive(false);

        if (messagePanel != null)
            messagePanel.SetActive(true);

        SetMessage("Welcome. Let’s quickly go over the camera controls.");
    }

    private void Update()
    {
        if (!_running)
            return;

        if (_waitingForDrag)
        {
            if (cameraControl != null && cameraControl.IsDragging())
            {
                _waitingForDrag = false;
                _waitingForDragRelease = true;
            }

            return;
        }

        if (_waitingForDragRelease)
        {
            bool mouseReleased = !Input.GetMouseButton(0);
            bool noTouches = Input.touchCount == 0;

            if (cameraControl != null && !cameraControl.IsDragging() && mouseReleased && noTouches)
            {
                _waitingForDragRelease = false;
                _waitingForZoom = true;
                _zoomedIn = false;
                _zoomedOut = false;

                ApplyZoomStepInputRules();
                SetMessage("Pinch and squeeze to zoom in and out.");
            }

            return;
        }

        if (_waitingForZoom)
        {
            int zoomDirection = GetZoomDirectionThisFrame();

            if (zoomDirection > 0)
                _zoomedIn = true;
            else if (zoomDirection < 0)
                _zoomedOut = true;

            if ((_zoomedIn && !_zoomedOut) || (!_zoomedIn && _zoomedOut))
                SetMessage("Now zoom the other way.");

            if (_zoomedIn && _zoomedOut)
                BeginRotateStep();

            return;
        }

        if (_waitingForRotate)
        {
            if (cameraControl == null)
                return;

            if (cameraControl.IsRotatingFromMinimap())
            {
                float currentYaw = cameraControl.GetCurrentYaw();

                if (!_startedMinimapRotate)
                {
                    _startedMinimapRotate = true;
                    _minimapRotateStartYaw = currentYaw;
                }

                float yawDelta = Mathf.Abs(Mathf.DeltaAngle(_minimapRotateStartYaw, currentYaw));
                if (yawDelta >= minimapRotateYawThreshold)
                    CompleteTutorial();
            }

            return;
        }
    }

    private void OnContinuePressed()
    {
        if (!_running)
            return;

        if (_cameraLockedByTutorial && cameraControl != null)
        {
            cameraControl.PopInputLock();
            _cameraLockedByTutorial = false;
        }

        SetDarkOverlayState(false, false);

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);

        SetBlockingMode(false);

        _waitingForDrag = true;
        ApplyDragStepInputRules();
        SetMessage("Click and drag to move the camera.");
    }

    private void OnSkipPressed()
    {
        SkipTutorial();
    }

    private void BeginRotateStep()
    {
        _waitingForZoom = false;
        _waitingForRotate = true;
        _startedMinimapRotate = false;
        _minimapRotateStartYaw = 0f;

        ApplyRotateStepInputRules();
        SetDarkOverlayState(false, true);
        SetMessage("Drag on the minimap to rotate the camera.");
    }

    public void SkipTutorial()
    {
        _waitingForDrag = false;
        _waitingForDragRelease = false;
        _waitingForZoom = false;
        _waitingForRotate = false;
        _running = false;
        _completedThisGame = true;
        _zoomedIn = false;
        _zoomedOut = false;
        _startedMinimapRotate = false;
        _minimapRotateStartYaw = 0f;

        if (_cameraLockedByTutorial && cameraControl != null)
        {
            cameraControl.PopInputLock();
            _cameraLockedByTutorial = false;
        }

        ClearTutorialInputRules();
        SetDarkOverlayState(false, false);
        SetBlockingMode(false);
        SetRootVisible(false);

        BeginNextTutorialOrResume();
    }

    private int GetZoomDirectionThisFrame()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0.0001f)
            return 1;

        if (scroll < -0.0001f)
            return -1;

        if (Input.touchCount == 2)
        {
            Touch a = Input.GetTouch(0);
            Touch b = Input.GetTouch(1);

            Vector2 aPrev = a.position - a.deltaPosition;
            Vector2 bPrev = b.position - b.deltaPosition;

            float prevDist = Vector2.Distance(aPrev, bPrev);
            float currentDist = Vector2.Distance(a.position, b.position);
            float delta = currentDist - prevDist;

            if (delta >= pinchDeltaThreshold)
                return 1;

            if (delta <= -pinchDeltaThreshold)
                return -1;
        }

        return 0;
    }

    private void CompleteTutorial()
    {
        _waitingForDrag = false;
        _waitingForDragRelease = false;
        _waitingForZoom = false;
        _waitingForRotate = false;
        _running = false;
        _completedThisGame = true;
        _zoomedIn = false;
        _zoomedOut = false;
        _startedMinimapRotate = false;
        _minimapRotateStartYaw = 0f;

        if (_cameraLockedByTutorial && cameraControl != null)
        {
            cameraControl.PopInputLock();
            _cameraLockedByTutorial = false;
        }

        ClearTutorialInputRules();
        SetDarkOverlayState(false, false);
        SetBlockingMode(false);
        SetRootVisible(false);

        BeginNextTutorialOrResume();
    }

    public void ResetTutorialForNewGame()
    {
        _running = false;
        _waitingForDrag = false;
        _waitingForDragRelease = false;
        _waitingForZoom = false;
        _waitingForRotate = false;
        _cameraLockedByTutorial = false;
        _completedThisGame = false;
        _zoomedIn = false;
        _zoomedOut = false;
        _startedMinimapRotate = false;
        _minimapRotateStartYaw = 0f;
        _hasStarterWorldPoint = false;
        _starterWorldPoint = Vector3.zero;
        _hasStarterTarget = false;
        _starterTarget = null;

        ClearTutorialInputRules();
        SetDarkOverlayState(false, false);
        SetBlockingMode(false);
        SetRootVisible(false);
    }

    private void SetMessage(string value)
    {
        if (messageText != null)
            messageText.text = value;
    }

    private void SetRootVisible(bool visible)
    {
        if (root != null)
            root.SetActive(visible);
    }

    private void SetBlockingMode(bool blocking)
    {
        if (rootCanvasGroup == null)
            return;

        rootCanvasGroup.blocksRaycasts = blocking;
        rootCanvasGroup.interactable = blocking;
    }

    private void SetDarkOverlayState(bool showNormalOverlay, bool showOverlayWithHole)
    {
        if (darkOverlay != null)
            darkOverlay.SetActive(showNormalOverlay);

        if (darkOverlayWithHole != null)
            darkOverlayWithHole.SetActive(showOverlayWithHole);
    }

    private void ApplyDragStepInputRules()
    {
        if (cameraControl == null)
            return;

        cameraControl.SetTutorialInputRestrictions(
            restrictInput: true,
            allowWorldDrag: true,
            allowZoom: false,
            allowMinimapRotation: false
        );
    }

    private void ApplyZoomStepInputRules()
    {
        if (cameraControl == null)
            return;

        cameraControl.SetTutorialInputRestrictions(
            restrictInput: true,
            allowWorldDrag: false,
            allowZoom: true,
            allowMinimapRotation: false
        );
    }

    private void ApplyRotateStepInputRules()
    {
        if (cameraControl == null)
            return;

        cameraControl.SetTutorialInputRestrictions(
            restrictInput: true,
            allowWorldDrag: false,
            allowZoom: false,
            allowMinimapRotation: true
        );
    }

    private void ClearTutorialInputRules()
    {
        if (cameraControl == null)
            return;

        cameraControl.ClearTutorialInputRestrictions();
    }

    public void SetStarterWorldPoint(Vector3 worldPoint)
    {
        _starterWorldPoint = worldPoint;
        _hasStarterWorldPoint = true;

        if (environmentTileTutorial != null)
            environmentTileTutorial.SetStarterWorldPoint(worldPoint);
    }

    public void SetStarterTarget(GameObject starterTarget)
    {
        _starterTarget = starterTarget;
        _hasStarterTarget = starterTarget != null;

        if (environmentTileTutorial != null)
            environmentTileTutorial.SetStarterTarget(starterTarget);
    }

    private void BeginNextTutorialOrResume()
    {
        if (environmentTileTutorial != null && _hasStarterWorldPoint && _hasStarterTarget)
        {
            environmentTileTutorial.SetStarterWorldPoint(_starterWorldPoint);
            environmentTileTutorial.SetStarterTarget(_starterTarget);

            if (environmentTileTutorial.ShouldRunTutorial())
            {
                environmentTileTutorial.BeginTutorial();
                return;
            }
        }

        if (resumeTurnTimerWhenFinished)
        {
            TurnSystem.Instance?.ResumeTurnTimer();
            TileInteraction.SetSelectionEnabled(false);
            TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);
        }
    }
}