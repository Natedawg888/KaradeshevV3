using System;
using UnityEngine;

[Serializable]
public class MeleeTargetEntry
{
    public MeleeTargetType type;

    public string displayName;
    public Sprite icon;
    public int count;

    // animals
    public float aggression;
    public float flightiness;
    public float strength;
    public int animalGroupId = -1;

    // units
    public string unitGroupId;
    public float movementSpeed;
    public int power, defense, agility, accuracy, range, stealth;

    // ✅ NEW: unit health
    public int currentHealth;
    public int maxHealth;
}