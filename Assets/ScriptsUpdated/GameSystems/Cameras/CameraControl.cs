using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CameraControl : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float dragSpeed = 0.1f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 100f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 50f;

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] public Camera mainCamera;
    [SerializeField] private Camera minimapCamera;

    [Header("Minimap UI")]
    [SerializeField] private RawImage minimapImage;
    [SerializeField] private RenderTexture minimapRenderTexture;
    [SerializeField] private RectTransform cameraIcon;

    [Header("Compass UI")]
    [SerializeField] private RectTransform compassImage;
    [SerializeField] private Button resetButton;

    [Header("Flags")]
    public bool disableCamera = false;
    public bool minimapNeedsUpdate = true;

    [Header("Minimap Capture")]
    [SerializeField, Min(0.05f)] private float minimapCaptureInterval = 0.5f; // seconds
    private float _nextMinimapCaptureAt = 0f;

    [Header("Layer Visibility Toggles")]
    [SerializeField] private string cloudLayerName = "WeatherCloud";
    [SerializeField] private bool cloudsVisibleOnStart = true;
    [SerializeField] private bool alsoHideCloudsFromMinimap = false;

    private Button _cloudLayerButton;
    private int _cloudLayer = -1;
    private bool _cloudsVisible = true;

    // drag state
    private Vector3 _dragOriginViewport;
    private bool _isDraggingWorld = false;

    // minimap rotation state
    private bool _rotatingFromMinimap = false;
    private Vector3 _lastMousePosition;

    // UI gating state
    private bool _uiHoldMouse = false;
    private bool _uiHoldTouch = false;
    private bool _pointerOverUI = false;

    private bool _tutorialRestrictInput = false;
    private bool _allowTutorialWorldDrag = true;
    private bool _allowTutorialZoom = true;
    private bool _allowTutorialMinimapRotation = true;

    // minimap orbit-around-point override
    private bool    _hasOrbitTarget = false;
    private Vector3 _orbitTarget;

    private int _externalInputLockCount = 0;

    // cached rect
    private RectTransform _minimapRect;

    // save/restore pose
    private Vector3 _savedPosition;
    private Quaternion _savedRotation;
    private bool _hasSavedPose = false;

    public bool IsDragging() => _isDraggingWorld;

    public bool IsInputLocked => disableCamera || _externalInputLockCount > 0;

    private void Awake()
    {
        if (resetButton != null)
            resetButton.onClick.AddListener(ResetCameraRotation);

        if (minimapImage != null)
            _minimapRect = minimapImage.rectTransform;

        _cloudsVisible = cloudsVisibleOnStart;
        _cloudLayer = LayerMask.NameToLayer(cloudLayerName);

        if (_cloudLayer < 0)
        {
            //Debug.LogWarning($"[CameraControl] Layer '{cloudLayerName}' was not found.");
        }

        ApplyCloudLayerVisibility();
    }

    private void Start()
    {
        if (gridManager == null) return;

        if (minimapCamera != null && minimapImage != null && minimapRenderTexture != null)
        {
            minimapCamera.targetTexture = minimapRenderTexture;
            minimapImage.texture = minimapRenderTexture;
            AdjustMinimapCamera();
        }

        // one-time initial capture
        if (minimapNeedsUpdate) CaptureMinimap();

        SetCloudLayerVisible(cloudsVisibleOnStart);
    }

    private void Update()
    {
        UpdateUIBlockState();

        HandleMinimapRotation();

        if (!IsInputLocked)
        {
            HandleZoom();
            if (!IsCameraInputBlocked())
            {
                HandleMouseDrag();
            }
        }

        // clamp after movement
        if (gridManager != null)
            transform.position = ClampPositionToGridBounds(transform.position);

        // UI visuals
        if (cameraIcon != null) UpdateCameraIcon();
        if (compassImage != null && mainCamera != null)
            compassImage.localRotation = Quaternion.Euler(0f, 0f, -mainCamera.transform.eulerAngles.y);

        if (Time.unscaledTime >= _nextMinimapCaptureAt || minimapNeedsUpdate)
        {
            CaptureMinimap();
            _nextMinimapCaptureAt = Time.unscaledTime + minimapCaptureInterval;
        }
    }

    // ---------------- UI Blocking ----------------

    private void UpdateUIBlockState()
    {
        _pointerOverUI = false;

        if (EventSystem.current == null)
            return;

        // Touch path
        if (Input.touchCount > 0)
        {
            bool anyActiveTouch = false;
            bool anyTouchOverUI = false;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);

                if (t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled)
                    anyActiveTouch = true;

                // if any touch began on UI, hold until all touches end
                if (t.phase == TouchPhase.Began && EventSystem.current.IsPointerOverGameObject(t.fingerId))
                    _uiHoldTouch = true;

                if (EventSystem.current.IsPointerOverGameObject(t.fingerId))
                    anyTouchOverUI = true;
            }

            if (!anyActiveTouch)
                _uiHoldTouch = false;

            _pointerOverUI = _uiHoldTouch || anyTouchOverUI;
            return;
        }

        // Mouse path
        bool overMinimap = IsPointerOverMinimap();

        if (Input.GetMouseButtonDown(0))
        {
            if (!overMinimap && EventSystem.current.IsPointerOverGameObject())
                _uiHoldMouse = true;
        }

        if (Input.GetMouseButtonUp(0))
            _uiHoldMouse = false;

        _pointerOverUI = _uiHoldMouse || EventSystem.current.IsPointerOverGameObject();
    }

    private bool IsCameraInputBlocked()
    {
        if (_rotatingFromMinimap) return true;
        return _pointerOverUI;
    }

    private bool IsPointerOverMinimap()
    {
        if (_minimapRect == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(_minimapRect, Input.mousePosition, null);
    }

    // ---------------- Minimap Rotation ----------------

    private void HandleMinimapRotation()
    {
        if (mainCamera == null)
            return;

        if (_tutorialRestrictInput && !_allowTutorialMinimapRotation)
        {
            _rotatingFromMinimap = false;
            return;
        }

        if (Input.GetMouseButtonDown(0) && IsPointerOverMinimap())
        {
            _rotatingFromMinimap = true;
            _lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButton(0) && _rotatingFromMinimap)
        {
            Vector3 delta = Input.mousePosition - _lastMousePosition;
            float yaw = delta.x * rotationSpeed * Time.deltaTime;

            if (_hasOrbitTarget)
                transform.RotateAround(_orbitTarget, Vector3.up, yaw);
            else
                mainCamera.transform.Rotate(Vector3.up, yaw, Space.World);

            _lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
            _rotatingFromMinimap = false;
    }

    // ---------------- Movement ----------------

    private void HandleMouseDrag()
    {
        if (mainCamera == null)
            return;

        if (_tutorialRestrictInput && !_allowTutorialWorldDrag)
        {
            _isDraggingWorld = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            _dragOriginViewport = mainCamera.ScreenToViewportPoint(Input.mousePosition);
            _isDraggingWorld = false;
        }

        if (Input.GetMouseButton(0))
        {
            Vector3 current = mainCamera.ScreenToViewportPoint(Input.mousePosition);
            Vector3 diff = _dragOriginViewport - current;

            if (diff.sqrMagnitude > 0.000001f)
                _isDraggingWorld = true;

            Vector3 move = new Vector3(diff.x, 0f, diff.y) * dragSpeed;
            move = transform.TransformDirection(move);
            move.y = 0f;

            transform.position += move;
            _dragOriginViewport = current;
        }

        if (Input.GetMouseButtonUp(0))
            _isDraggingWorld = false;
    }

    // ---------------- Zoom ----------------

    private void HandleZoom()
    {
        if (_tutorialRestrictInput && !_allowTutorialZoom)
            return;

        if (Input.touchCount == 2)
        {
            Touch a = Input.GetTouch(0);
            Touch b = Input.GetTouch(1);

            Vector2 aPrev = a.position - a.deltaPosition;
            Vector2 bPrev = b.position - b.deltaPosition;

            float prevDist = (aPrev - bPrev).magnitude;
            float dist = (a.position - b.position).magnitude;

            float delta = dist - prevDist;
            AdjustZoom(delta * zoomSpeed * Time.deltaTime);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
            AdjustZoom(scroll * zoomSpeed * 10f);
    }

    private void AdjustZoom(float increment)
    {
        Vector3 p = transform.position;
        p.y = Mathf.Clamp(p.y - increment, minZoom, maxZoom);
        transform.position = p;
    }

    // ---------------- Minimap + UI ----------------

    private void AdjustMinimapCamera()
    {
        if (gridManager == null || minimapCamera == null) return;

        float gridCenterX = (gridManager.columns * gridManager.cellSize) * 0.5f - 1f;
        float gridCenterZ = (gridManager.rows * gridManager.cellSize) * 0.5f - 1f;

        minimapCamera.transform.position = new Vector3(gridCenterX, 50f, gridCenterZ);
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        float maxGridSize = Mathf.Max(gridManager.columns, gridManager.rows);
        minimapCamera.orthographicSize = (maxGridSize * gridManager.cellSize) * 0.5f;
    }

    private void UpdateCameraIcon()
    {
        if (gridManager == null || _minimapRect == null || mainCamera == null || cameraIcon == null)
            return;

        Vector3 camPos = mainCamera.transform.position;

        float gridW = gridManager.columns * gridManager.cellSize;
        float gridH = gridManager.rows * gridManager.cellSize;

        float nx = (gridW <= 0f) ? 0f : camPos.x / gridW;
        float ny = (gridH <= 0f) ? 0f : camPos.z / gridH;

        float x = nx * _minimapRect.rect.width - (_minimapRect.rect.width * 0.5f);
        float y = ny * _minimapRect.rect.height - (_minimapRect.rect.height * 0.5f);

        cameraIcon.anchoredPosition = new Vector2(x, y);
        cameraIcon.localRotation = Quaternion.Euler(0f, 0f, -mainCamera.transform.eulerAngles.y);
    }

    public void CaptureMinimap()
    {
        if (minimapCamera == null || minimapRenderTexture == null) return;

        minimapCamera.Render();
        minimapNeedsUpdate = false;
    }

    private Vector3 ClampPositionToGridBounds(Vector3 p)
    {
        if (gridManager == null) return p;

        float minX = -25f;
        float maxX = gridManager.columns * gridManager.cellSize + 25f;
        float minZ = -25f;
        float maxZ = gridManager.rows * gridManager.cellSize + 25f;

        p.x = Mathf.Clamp(p.x, minX, maxX);
        p.z = Mathf.Clamp(p.z, minZ, maxZ);
        return p;
    }

    // ---------------- Public helpers ----------------

    public void SaveCameraPose()
    {
        _savedPosition = transform.position;
        _savedRotation = transform.rotation;
        _hasSavedPose = true;
    }

    public void RestoreCameraPose(bool clampBounds = true, bool clampZoomValue = true)
    {
        if (!_hasSavedPose) return;

        Vector3 p = _savedPosition;

        if (clampZoomValue)
            p.y = Mathf.Clamp(p.y, minZoom, maxZoom);

        if (clampBounds)
            p = ClampPositionToGridBounds(p);

        transform.position = p;
        transform.rotation = _savedRotation;

        minimapNeedsUpdate = true;
        StartCoroutine(DeferredCapture());
    }

    public void ZoomToMaxHeight()
    {
        Vector3 p = transform.position;
        p.y = maxZoom;
        transform.position = ClampPositionToGridBounds(p);

        minimapNeedsUpdate = true;
        StartCoroutine(DeferredCapture());
    }

    public void ResetCameraRotation()
    {
        transform.rotation = Quaternion.Euler(45f, 0f, 0f);

        if (minimapCamera != null)
            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        if (compassImage != null)
            compassImage.localRotation = Quaternion.identity;
    }

    private IEnumerator DeferredCapture()
    {
        yield return null;
        CaptureMinimap();
    }

    public void FocusOnPoint(Vector3 point, Vector3 forward, float distance = 6f)
    {
        // flatten forward to XZ
        Vector3 dir = new Vector3(forward.x, 0f, forward.z);
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;
        else
            dir.Normalize();

        // place camera behind point, elevated
        Vector3 targetPos = point - dir * distance;
        targetPos.y = Mathf.Clamp(distance + 1f, minZoom, maxZoom);

        // clamp to grid bounds
        if (gridManager != null)
            targetPos = ClampPositionToGridBounds(targetPos);

        transform.position = targetPos;
        transform.LookAt(point);

        // refresh minimap visuals (defer 1 frame so UI/camera settle)
        minimapNeedsUpdate = true;
        StartCoroutine(DeferredCapture());
    }

    public void PushInputLock()
    {
        _externalInputLockCount++;
        ClearInputState();
    }

    public void PopInputLock()
    {
        _externalInputLockCount = Mathf.Max(0, _externalInputLockCount - 1);
        ClearInputState();
    }

    private void ClearInputState()
    {
        _isDraggingWorld = false;
        _rotatingFromMinimap = false;
        _uiHoldMouse = false;
        _uiHoldTouch = false;
        _pointerOverUI = false;
    }

    public void SetGridManager(GridManager newGridManager)
    {
        if (newGridManager == null)
            return;

        gridManager = newGridManager;
        transform.position = ClampPositionToGridBounds(transform.position);

        if (minimapCamera != null)
            AdjustMinimapCamera();

        if (cameraIcon != null && mainCamera != null && _minimapRect != null)
            UpdateCameraIcon();

        minimapNeedsUpdate = true;

        if (isActiveAndEnabled)
            StartCoroutine(DeferredCapture());
    }

    public void InstallRuntimeRefs(
    GridManager newGridManager = null,
    Camera newMainCamera = null,
    Camera newMinimapCamera = null,
    RawImage newMinimapImage = null,
    RenderTexture newMinimapRenderTexture = null,
    RectTransform newCameraIcon = null)
    {
        if (newGridManager != null)
            gridManager = newGridManager;

        if (newMainCamera != null)
            mainCamera = newMainCamera;

        if (newMinimapCamera != null)
            minimapCamera = newMinimapCamera;

        if (newMinimapImage != null)
            minimapImage = newMinimapImage;

        if (newMinimapRenderTexture != null)
            minimapRenderTexture = newMinimapRenderTexture;

        if (newCameraIcon != null)
            cameraIcon = newCameraIcon;

        if (minimapImage != null)
            _minimapRect = minimapImage.rectTransform;

        if (minimapImage != null && minimapRenderTexture != null)
            minimapImage.texture = minimapRenderTexture;

        if (minimapCamera != null && minimapRenderTexture != null)
            minimapCamera.targetTexture = minimapRenderTexture;

        if (gridManager != null)
        {
            transform.position = ClampPositionToGridBounds(transform.position);

            if (minimapCamera != null)
                AdjustMinimapCamera();
        }

        if (cameraIcon != null && mainCamera != null && _minimapRect != null)
            UpdateCameraIcon();

        minimapNeedsUpdate = true;

        if (isActiveAndEnabled)
            StartCoroutine(DeferredCapture());

        ApplyCloudLayerVisibility();
    }

    public CameraPoseSaveData SaveState()
    {
        CameraPoseSaveData data = new CameraPoseSaveData
        {
            rigPosition = transform.position,
            rigRotation = transform.rotation,
            hasSeparateMainCameraTransform = mainCamera != null && mainCamera.transform != transform,
            cloudsVisible = _cloudsVisible
        };

        if (data.hasSeparateMainCameraTransform)
        {
            data.mainCameraLocalPosition = mainCamera.transform.localPosition;
            data.mainCameraLocalRotation = mainCamera.transform.localRotation;
        }

        return data;
    }

    public void LoadState(CameraPoseSaveData data, bool clampBounds = true, bool clampZoomValue = true)
    {
        if (data == null)
            return;

        Vector3 p = data.rigPosition;

        if (clampZoomValue)
            p.y = Mathf.Clamp(p.y, minZoom, maxZoom);

        if (clampBounds)
            p = ClampPositionToGridBounds(p);

        transform.position = p;
        transform.rotation = data.rigRotation;

        if (data.hasSeparateMainCameraTransform && mainCamera != null && mainCamera.transform != transform)
        {
            mainCamera.transform.localPosition = data.mainCameraLocalPosition;
            mainCamera.transform.localRotation = data.mainCameraLocalRotation;
        }

        minimapNeedsUpdate = true;

        SetCloudLayerVisible(data.cloudsVisible);

        if (isActiveAndEnabled)
            StartCoroutine(DeferredCapture());
    }

    public bool IsRotatingFromMinimap() => _rotatingFromMinimap;

    public float GetCurrentYaw()
    {
        if (mainCamera != null)
            return mainCamera.transform.eulerAngles.y;

        return transform.eulerAngles.y;
    }

    public void SetTutorialInputRestrictions(
    bool restrictInput,
    bool allowWorldDrag,
    bool allowZoom,
    bool allowMinimapRotation)
    {
        _tutorialRestrictInput = restrictInput;
        _allowTutorialWorldDrag = allowWorldDrag;
        _allowTutorialZoom = allowZoom;
        _allowTutorialMinimapRotation = allowMinimapRotation;

        if (!_allowTutorialWorldDrag)
            _isDraggingWorld = false;

        if (!_allowTutorialMinimapRotation)
            _rotatingFromMinimap = false;
    }

    public void SetOrbitTarget(Vector3 point)
    {
        _orbitTarget    = point;
        _hasOrbitTarget = true;
    }

    public void ClearOrbitTarget()
    {
        _hasOrbitTarget = false;
    }

    public void ClearTutorialInputRestrictions()
    {
        _tutorialRestrictInput = false;
        _allowTutorialWorldDrag = true;
        _allowTutorialZoom = true;
        _allowTutorialMinimapRotation = true;

        _isDraggingWorld = false;
        _rotatingFromMinimap = false;
    }

    public void FocusTopDownOnPoint(Vector3 point, float height, float yaw = 0f, bool clampBounds = true)
    {
        Vector3 targetPos = new Vector3(point.x, Mathf.Clamp(height, minZoom, maxZoom), point.z);

        if (clampBounds && gridManager != null)
            targetPos = ClampPositionToGridBounds(targetPos);

        transform.position = targetPos;
        transform.rotation = Quaternion.Euler(90f, yaw, 0f);

        minimapNeedsUpdate = true;

        if (isActiveAndEnabled)
            StartCoroutine(DeferredCapture());
    }

    public void InstallCloudLayerToggleButton(Button button)
    {
        if (_cloudLayerButton != null)
            _cloudLayerButton.onClick.RemoveListener(ToggleCloudLayerVisibility);

        _cloudLayerButton = button;

        if (_cloudLayerButton != null)
            _cloudLayerButton.onClick.AddListener(ToggleCloudLayerVisibility);

        ApplyCloudLayerVisibility();
    }

    public void ToggleCloudLayerVisibility()
    {
        SetCloudLayerVisible(!_cloudsVisible);
    }

    public void SetCloudLayerVisible(bool visible)
    {
        _cloudsVisible = visible;
        ApplyCloudLayerVisibility();
    }

    private void ApplyCloudLayerVisibility()
    {
        if (_cloudLayer < 0)
            _cloudLayer = LayerMask.NameToLayer(cloudLayerName);

        if (_cloudLayer < 0)
            return;

        int layerMask = 1 << _cloudLayer;

        ApplyLayerVisibilityToCamera(mainCamera, layerMask, _cloudsVisible);

        if (alsoHideCloudsFromMinimap)
            ApplyLayerVisibilityToCamera(minimapCamera, layerMask, _cloudsVisible);
    }

    private void ApplyLayerVisibilityToCamera(Camera targetCamera, int layerMask, bool visible)
    {
        if (targetCamera == null)
            return;

        if (visible)
            targetCamera.cullingMask |= layerMask;
        else
            targetCamera.cullingMask &= ~layerMask;
    }
}
