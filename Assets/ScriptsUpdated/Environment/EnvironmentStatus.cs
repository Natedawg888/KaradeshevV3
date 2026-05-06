using System;
using System.Collections.Generic;
using UnityEngine;

public class EnvironmentStatus : MonoBehaviour
{

    [Header("Renderer Exclusions")]
    [Tooltip("Child object names ignored by discovery material swapping. Use this for Fire, Smoke, UI markers, etc.")]
    [SerializeField]
    private string[] excludedRendererRootNames =
    {
        "Fire",
    };

    [Header("Discovery")]
    [SerializeField] private bool isDiscovered = false;
    public bool isDiscoverable = true;
    public bool IsDiscovered => isDiscovered;

    [Header("Materials")]
    [Tooltip("Material to show when the environment is not yet discovered.")]
    public Material undiscoveredMaterial;

    // Original shared materials per renderer
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new();

    // Current in-progress material state per renderer
    private readonly Dictionary<Renderer, Material[]> currentMaterials = new();

    // Queue of (renderer, slotIndex) to reveal over time
    private readonly Queue<(Renderer renderer, int slotIndex)> revealQueue = new();

    private bool partialInitialized = false;
    private int requiredDiscoveryTurns = 1; // set when discovery starts

    public event Action OnDiscovered;
    public event Action OnReset;

    private void Awake()
    {
        CacheOriginalMaterials();
        InitializeUndiscoveredVisuals();
    }

    private void CacheOriginalMaterials()
    {
        originalMaterials.Clear();
        currentMaterials.Clear();

        foreach (var renderer in GetComponentsInChildren<Renderer>(includeInactive: true))
        {
            if (renderer == null)
                continue;

            if (ShouldExcludeRenderer(renderer))
                continue;

            var shared = renderer.sharedMaterials;
            originalMaterials[renderer] = shared;
            currentMaterials[renderer] = CreateUndiscoveredArray(shared.Length);
        }
    }

    private bool ShouldExcludeRenderer(Renderer renderer)
    {
        if (renderer == null)
            return true;

        if (excludedRendererRootNames == null || excludedRendererRootNames.Length == 0)
            return false;

        Transform current = renderer.transform;

        while (current != null && current != transform)
        {
            for (int i = 0; i < excludedRendererRootNames.Length; i++)
            {
                string excludedName = excludedRendererRootNames[i];

                if (string.IsNullOrWhiteSpace(excludedName))
                    continue;

                if (string.Equals(current.name, excludedName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void InitializeUndiscoveredVisuals()
    {
        if (isDiscovered)
            RestoreOriginal();
        else
            ApplyUndiscovered();
    }

    private void ApplyUndiscovered()
    {
        partialInitialized = false;
        revealQueue.Clear();

        foreach (var renderer in originalMaterials.Keys)
        {
            int len = originalMaterials[renderer]?.Length ?? 1;
            currentMaterials[renderer] = CreateUndiscoveredArray(len);
        }

        ApplyCurrentToRenderers();
    }

    private void ApplyCurrentToRenderers()
    {
        foreach (var kv in currentMaterials)
        {
            var renderer = kv.Key;
            if (renderer == null) continue;
            renderer.materials = kv.Value;
        }
    }

    private Material[] CreateUndiscoveredArray(int length)
    {
        var arr = new Material[Mathf.Max(1, length)];
        for (int i = 0; i < arr.Length; i++)
            arr[i] = undiscoveredMaterial;
        return arr;
    }

    private void BuildRevealQueue()
    {
        revealQueue.Clear();
        foreach (var kv in originalMaterials)
        {
            var renderer = kv.Key;
            var slots = kv.Value.Length;
            for (int i = 0; i < slots; i++)
                revealQueue.Enqueue((renderer, i));

            // start from fully undiscovered for this renderer
            currentMaterials[renderer] = CreateUndiscoveredArray(slots);
        }

        ShuffleQueue(revealQueue);
        ApplyCurrentToRenderers();
    }

    private void ShuffleQueue<T>(Queue<T> queue)
    {
        var list = new List<T>(queue);
        for (int i = 0; i < list.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
        queue.Clear();
        foreach (var item in list)
            queue.Enqueue(item);
    }

    /// <summary>Called when discovery begins; prepares partial reveal over the given number of turns.</summary>
    public void StartPartialReveal(int discoveryTurns)
    {
        if (isDiscovered) return;
        requiredDiscoveryTurns = Mathf.Max(1, discoveryTurns);
        partialInitialized = true;
        BuildRevealQueue();
    }

    /// <summary>Advance one tick of partial reveal. Should be driven externally (e.g., from PlayerDiscoveryManager.OnTurnEnded).</summary>
    public void AdvancePartialReveal()
    {
        if (isDiscovered || !partialInitialized || requiredDiscoveryTurns <= 0 || undiscoveredMaterial == null)
            return;

        int totalSlots = 0;
        foreach (var kv in originalMaterials)
            totalSlots += kv.Value.Length;

        int perTurn = Mathf.CeilToInt((float)totalSlots / requiredDiscoveryTurns);
        int revealedThisTick = 0;

        while (revealedThisTick < perTurn && revealQueue.Count > 0)
        {
            var (renderer, slotIndex) = revealQueue.Dequeue();
            if (currentMaterials.TryGetValue(renderer, out var currentArray) &&
                originalMaterials.TryGetValue(renderer, out var origArray) &&
                slotIndex >= 0 && slotIndex < currentArray.Length)
            {
                currentArray[slotIndex] = origArray[slotIndex];
                currentMaterials[renderer] = currentArray;
            }
            revealedThisTick++;
        }

        ApplyCurrentToRenderers();
    }

    /// <summary>Reset ongoing partial reveal back to undiscovered.</summary>
    public void ResetReveal()
    {
        if (isDiscovered)
            isDiscovered = false;

        partialInitialized = false;
        revealQueue.Clear();
        ApplyUndiscovered();
        OnReset?.Invoke();
    }

    private void RestoreOriginal()
    {
        foreach (var kv in originalMaterials)
        {
            var renderer = kv.Key;
            var original = kv.Value;
            if (renderer == null) continue;
            if (original != null && original.Length > 0)
                renderer.materials = original;
        }
    }

    /// <summary>Finish discovery instantly.</summary>
    public void CompleteDiscovery()
    {
        if (isDiscovered) return;
        isDiscovered = true;
        partialInitialized = false;
        revealQueue.Clear();
        RestoreOriginal();
        OnDiscovered?.Invoke();
    }

    /// <summary>Explicit setter.</summary>
    public void SetDiscovered(bool discovered)
    {
        if (isDiscovered == discovered) return;
        if (discovered) CompleteDiscovery();
        else ResetReveal();
    }
}