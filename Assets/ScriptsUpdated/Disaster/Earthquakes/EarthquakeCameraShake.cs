using System.Collections;
using UnityEngine;

public class EarthquakeCameraShake : MonoBehaviour
{
    [Header("References")]
    public EarthquakeSimulationSystem simulationSystem;

    [Tooltip("Leave empty. The script will find the camera automatically.")]
    public Transform cameraTransform;

    [Tooltip("Optional. If your camera has a parent rig, assign that instead. Otherwise leave empty.")]
    public Transform cameraShakeRoot;

    [Header("Find Camera")]
    public bool useCameraMainFirst = true;
    public bool includeInactiveCameras = true;

    [Header("Magnitude Input Range")]
    public float minMagnitude = 2.5f;
    public float maxMagnitude = 8.5f;

    [Header("Shake Duration")]
    public float minDuration = 0.25f;
    public float maxDuration = 2.25f;

    [Header("Position Shake")]
    public float minOffset = 0.025f;
    public float maxOffset = 0.35f;

    [Header("Rotation Shake")]
    public float minRotation = 0.05f;
    public float maxRotation = 1.8f;

    [Header("Smoothing")]
    public AnimationCurve shakeFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);
    public float shakeFrequency = 28f;

    [Header("Debug")]
    public bool debugLogging = true;

    private Coroutine shakeRoutine;

    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    private Transform ShakeTarget
    {
        get
        {
            if (cameraShakeRoot != null)
                return cameraShakeRoot;

            return cameraTransform;
        }
    }

    private void Awake()
    {
        if (simulationSystem == null)
            simulationSystem = FindObjectOfType<EarthquakeSimulationSystem>();

        FindCamera();
    }

    private void OnEnable()
    {
        if (simulationSystem == null)
            simulationSystem = FindObjectOfType<EarthquakeSimulationSystem>();

        if (simulationSystem != null)
            simulationSystem.OnEarthquake += HandleEarthquake;
    }

    private void OnDisable()
    {
        if (simulationSystem != null)
            simulationSystem.OnEarthquake -= HandleEarthquake;

        StopAndRestore();
    }

    private void FindCamera()
    {
        if (cameraTransform != null)
            return;

        Camera found = null;

        if (useCameraMainFirst && Camera.main != null)
            found = Camera.main;

        if (found == null)
        {
#if UNITY_2023_1_OR_NEWER
            Camera[] cameras = FindObjectsByType<Camera>(
                includeInactiveCameras ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            if (cameras != null && cameras.Length > 0)
                found = cameras[0];
#else
            found = FindObjectOfType<Camera>(includeInactiveCameras);
#endif
        }

        if (found != null)
        {
            cameraTransform = found.transform;

            if (debugLogging)
                Debug.Log($"EarthquakeCameraShake: Found camera '{found.name}'.");
        }
        else
        {
            if (debugLogging)
                Debug.LogWarning("EarthquakeCameraShake: Could not find a Camera in the scene.");
        }
    }

    private void HandleEarthquake(EarthquakeEventData data)
    {
        FindCamera();

        Transform target = ShakeTarget;

        if (target == null)
        {
            if (debugLogging)
                Debug.LogWarning("EarthquakeCameraShake: No camera target found.");

            return;
        }

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(Shake(data.magnitude));
    }

    private IEnumerator Shake(float magnitude)
    {
        Transform target = ShakeTarget;

        if (target == null)
            yield break;

        originalLocalPosition = target.localPosition;
        originalLocalRotation = target.localRotation;

        float t = Mathf.InverseLerp(minMagnitude, maxMagnitude, magnitude);

        float duration = Mathf.Lerp(minDuration, maxDuration, t);
        float offset = Mathf.Lerp(minOffset, maxOffset, t);
        float rotation = Mathf.Lerp(minRotation, maxRotation, t);

        if (debugLogging)
        {
            Debug.Log(
                $"EarthquakeCameraShake: Shaking '{target.name}', " +
                $"magnitude={magnitude:0.0}, duration={duration:0.00}"
            );
        }

        float elapsed = 0f;

        float seedX = Random.Range(0f, 100f);
        float seedY = Random.Range(0f, 100f);
        float seedR = Random.Range(0f, 100f);

        while (elapsed < duration)
        {
            if (target == null)
                yield break;

            elapsed += Time.deltaTime;

            float p = Mathf.Clamp01(elapsed / duration);
            float strength = shakeFalloff.Evaluate(p);

            float time = elapsed * shakeFrequency;

            float x = (Mathf.PerlinNoise(seedX, time) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(seedY, time) - 0.5f) * 2f;
            float r = (Mathf.PerlinNoise(seedR, time) - 0.5f) * 2f;

            Vector3 shakeOffset = new Vector3(
                x * offset,
                y * offset,
                0f
            ) * strength;

            Quaternion shakeRot = Quaternion.Euler(
                0f,
                0f,
                r * rotation * strength
            );

            target.localPosition = originalLocalPosition + shakeOffset;
            target.localRotation = originalLocalRotation * shakeRot;

            yield return null;
        }

        RestoreTarget();
        shakeRoutine = null;
    }

    private void StopAndRestore()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        RestoreTarget();
    }

    private void RestoreTarget()
    {
        Transform target = ShakeTarget;

        if (target == null)
            return;

        target.localPosition = originalLocalPosition;
        target.localRotation = originalLocalRotation;
    }
}