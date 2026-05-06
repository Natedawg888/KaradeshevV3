using System;
using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class EnvironmentFireState : MonoBehaviour
{
    [Header("Fire Rules")]
    [SerializeField] private bool canCatchFire = true;

    [Tooltip("Dryness starts here when ignited if current dryness is lower.")]
    [Range(0f, 1f)][SerializeField] private float drynessWhenIgnited = 0.80f;

    [Tooltip("How quickly the tile dries out when not being rained on.")]
    [Range(0f, 1f)][SerializeField] private float drynessRecoveryPerStep = 0.08f;

    [Tooltip("How strongly rain reduces dryness each step.")]
    [Range(0f, 1f)][SerializeField] private float rainDrynessReductionPerStep = 0.35f;

    [Header("Fire Visuals")]
    [SerializeField] private GameObject[] fireVisualObjects;
    [SerializeField] private bool autoFindFireChildByName = true;
    [SerializeField] private string fireChildName = "Fire";

    public bool CanCatchFire => canCatchFire;
    public bool IsOnFire { get; private set; }
    public int BurnTurnsRemaining { get; private set; }
    public int BaseBurnTurns { get; private set; }
    public float CurrentDryness01 { get; private set; } = 0.5f;

    public event Action<EnvironmentFireState> OnIgnited;
    public event Action<EnvironmentFireState> OnExtinguished;

    private readonly Dictionary<Renderer, Material[]> originalFireMaterials = new();

    private void Awake()
    {
        CacheFireVisualsIfNeeded();
        CacheOriginalFireMaterials();
        RefreshVisuals();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            CacheFireVisualsIfNeeded();

        RefreshVisuals();
    }

    public void SetCanCatchFire(bool value)
    {
        canCatchFire = value;

        if (!canCatchFire && IsOnFire)
            Extinguish();
    }

    public void RefreshDrynessFromWeather(float rain01)
    {
        rain01 = Mathf.Clamp01(rain01);

        if (rain01 > 0f)
        {
            CurrentDryness01 = Mathf.Clamp01(
                CurrentDryness01 - rain01 * rainDrynessReductionPerStep);
        }
        else
        {
            CurrentDryness01 = Mathf.Clamp01(
                CurrentDryness01 + drynessRecoveryPerStep);
        }
    }

    public bool TryIgnite(float chance01, int burnTurns)
    {
        if (!canCatchFire)
            return false;

        if (IsOnFire)
            return false;

        chance01 = Mathf.Clamp01(chance01);
        burnTurns = Mathf.Max(0, burnTurns);

        if (chance01 <= 0f || burnTurns <= 0)
            return false;

        if (UnityEngine.Random.value > chance01)
            return false;

        IsOnFire = true;
        BaseBurnTurns = burnTurns;
        BurnTurnsRemaining = burnTurns;
        CurrentDryness01 = Mathf.Max(CurrentDryness01, drynessWhenIgnited);

        RefreshVisuals();
        OnIgnited?.Invoke(this);

        return true;
    }

    private void CacheOriginalFireMaterials()
    {
        originalFireMaterials.Clear();

        if (fireVisualObjects == null)
            return;

        for (int i = 0; i < fireVisualObjects.Length; i++)
        {
            GameObject fireObj = fireVisualObjects[i];
            if (fireObj == null)
                continue;

            Renderer[] renderers = fireObj.GetComponentsInChildren<Renderer>(includeInactive: true);

            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null)
                    continue;

                originalFireMaterials[renderer] = renderer.sharedMaterials;
            }
        }
    }

    public bool AdvanceBurnStep(float rain01, float extinguishChanceAtFullRain)
    {
        if (!IsOnFire)
            return false;

        rain01 = Mathf.Clamp01(rain01);
        RefreshDrynessFromWeather(rain01);

        float extinguishChance = Mathf.Clamp01(extinguishChanceAtFullRain * rain01);
        if (extinguishChance > 0f && UnityEngine.Random.value < extinguishChance)
        {
            Extinguish();
            return false;
        }

        BurnTurnsRemaining--;

        if (BurnTurnsRemaining <= 0)
        {
            Extinguish();
            return false;
        }

        RefreshVisuals();
        return true;
    }

    public void Extinguish()
    {
        if (!IsOnFire)
            return;

        IsOnFire = false;
        BurnTurnsRemaining = 0;

        RefreshVisuals();
        OnExtinguished?.Invoke(this);
    }

    private void CacheFireVisualsIfNeeded()
    {
        if (!autoFindFireChildByName)
            return;

        if (fireVisualObjects != null && fireVisualObjects.Length > 0)
            return;

        Transform child = FindChildRecursive(transform, fireChildName);
        if (child != null)
            fireVisualObjects = new[] { child.gameObject };
    }

    private Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindChildRecursive(child, targetName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void RefreshVisuals()
    {
        if (fireVisualObjects == null)
            return;

        if (IsOnFire)
            RestoreOriginalFireMaterials();

        for (int i = 0; i < fireVisualObjects.Length; i++)
        {
            if (fireVisualObjects[i] != null)
                fireVisualObjects[i].SetActive(IsOnFire);
        }
    }

    private void RestoreOriginalFireMaterials()
    {
        foreach (var kvp in originalFireMaterials)
        {
            Renderer renderer = kvp.Key;
            Material[] materials = kvp.Value;

            if (renderer == null || materials == null || materials.Length == 0)
                continue;

            renderer.sharedMaterials = materials;
        }
    }
}