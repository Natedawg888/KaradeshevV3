using UnityEngine;

[DisallowMultipleComponent]
public class VolcanoEnvironmentVisuals : MonoBehaviour
{
    [Header("Visual Roots")]
    [Tooltip("Normal mountain mesh/form. ON in Mountain state.")]
    public GameObject mountainRoot;

    [Tooltip("Dormant caldera/base volcano mesh. ON in Dormant and Erupting states.")]
    public GameObject dormantCalderaRoot;

    [Tooltip("Lava overlay/root. ON only while Erupting.")]
    public GameObject eruptingLavaRoot;

    [Tooltip("Smoke overlay/root. ON only while Erupting.")]
    public GameObject eruptingSmokeRoot;

    [Header("Optional Behavior")]
    [Tooltip("If true, mountainRoot stays ON during Dormant/Erupting. Useful if your caldera is only an overlay.")]
    public bool keepMountainRootOnForVolcanoStates = false;

    [Tooltip("If true, dormantCalderaRoot stays ON in Mountain too. Usually false.")]
    public bool keepCalderaRootOnForMountain = false;

    [Header("Optional Lights")]
    [Tooltip("If left empty, lights are auto-found from children, including inactive children.")]
    public Light[] eruptionLights;

    [Header("Debug")]
    public bool autoFindLightsIfEmpty = true;
    public bool debugLogging = false;

    private void Awake()
    {
        CacheLightsIfNeeded();
        ApplyCurrentStateSafe();
    }

    private void OnEnable()
    {
        CacheLightsIfNeeded();
        ApplyCurrentStateSafe();
    }

    private void CacheLightsIfNeeded()
    {
        if (!autoFindLightsIfEmpty)
            return;

        if (eruptionLights != null && eruptionLights.Length > 0)
            return;

        eruptionLights = GetComponentsInChildren<Light>(true);

        if (debugLogging)
            //Debug.Log($"[VolcanoEnvironmentVisuals] Auto-found {eruptionLights.Length} lights on {name}");
    }

    public void SetState(VolcanoActivityState state)
    {
        CacheLightsIfNeeded();

        bool mountain = state == VolcanoActivityState.Mountain;
        bool dormant = state == VolcanoActivityState.Dormant;
        bool erupting = state == VolcanoActivityState.Erupting;
        bool visibleVolcano = dormant || erupting;

        if (mountainRoot != null)
            mountainRoot.SetActive(mountain || (visibleVolcano && keepMountainRootOnForVolcanoStates));

        if (dormantCalderaRoot != null)
            dormantCalderaRoot.SetActive(visibleVolcano || (mountain && keepCalderaRootOnForMountain));

        if (eruptingLavaRoot != null)
            eruptingLavaRoot.SetActive(erupting);

        if (eruptingSmokeRoot != null)
            eruptingSmokeRoot.SetActive(erupting);

        SetLightsEnabled(erupting);

        if (debugLogging)
            //Debug.Log($"[VolcanoEnvironmentVisuals] {name} SetState -> {state}");
    }

    private void SetLightsEnabled(bool enabledState)
    {
        if (eruptionLights == null)
            return;

        for (int i = 0; i < eruptionLights.Length; i++)
        {
            Light light = eruptionLights[i];
            if (light == null)
                continue;

            if (light.gameObject.activeSelf != enabledState)
                light.gameObject.SetActive(enabledState);

            light.enabled = enabledState;
        }
    }

    private void ApplyCurrentStateSafe()
    {
        VolcanoTileState state = GetComponentInParent<VolcanoTileState>();
        if (state != null)
            SetState(state.ActivityState);
        else
            SetState(VolcanoActivityState.Mountain);
    }
}
