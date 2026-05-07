using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildingFireState : MonoBehaviour
{
    [Header("Fire Rules")]
    [SerializeField] private bool canCatchFire = true;

    [Header("Extinguish Cost")]
    [Tooltip("Resources the player must spend to manually extinguish this building.")]
    public List<ResourceCost> extinguishCost = new();

    [Header("Fire Visuals")]
    [SerializeField] private GameObject[] fireVisualObjects;
    [SerializeField] private bool autoFindFireChildByName = true;
    [SerializeField] private string fireChildName = "Fire";

    public bool CanCatchFire => canCatchFire;
    public bool IsOnFire { get; private set; }
    public int BurnTurnsRemaining { get; private set; }
    public int BaseBurnTurns { get; private set; }

    public event Action<BuildingFireState> OnIgnited;
    public event Action<BuildingFireState, int> OnFireDamageStep;
    public event Action<BuildingFireState> OnExtinguished;

    private void Awake()
    {
        CacheFireVisualsIfNeeded();
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

        RefreshVisuals();
        OnIgnited?.Invoke(this);
        PostFireNotification();

        return true;
    }

    public bool AdvanceBurnStep(int damageThisStep, float extinguishChance01)
    {
        if (!IsOnFire)
            return false;

        if (damageThisStep > 0)
            OnFireDamageStep?.Invoke(this, damageThisStep);

        extinguishChance01 = Mathf.Clamp01(extinguishChance01);
        if (extinguishChance01 > 0f && UnityEngine.Random.value < extinguishChance01)
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

        for (int i = 0; i < fireVisualObjects.Length; i++)
        {
            if (fireVisualObjects[i] != null)
                fireVisualObjects[i].SetActive(IsOnFire);
        }
    }

    private void PostFireNotification()
    {
        if (NotificationManager.Instance == null) return;

        var building = GetComponent<BuildingControl>();
        string buildingName = building != null && !string.IsNullOrWhiteSpace(building.buildingName)
            ? building.buildingName
            : gameObject.name;

        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftBuilding(NotificationType.BuildingOnFire, buildingName);
        else
        {
            title   = "Building on Fire!";
            message = $"{buildingName} is on fire!";
        }

        NotificationManager.Instance.AddNotification(NotificationType.BuildingOnFire, title, message, transform.position);
    }
}