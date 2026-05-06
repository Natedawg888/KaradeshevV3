using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UnitCategoryIconManager : MonoBehaviour
{
    public static UnitCategoryIconManager Instance { get; private set; }

    [Tooltip("Icons indexed by MilitiaUnitCategory enum order (Land = 0, Sea = 1, Air = 2).")]
    public Sprite[] categoryIcons;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // If you want it to persist across scenes:
            // DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public Sprite GetIconForCategory(MilitiaUnitCategory category)
    {
        int index = (int)category;
        if (categoryIcons != null && index >= 0 && index < categoryIcons.Length)
        {
            return categoryIcons[index];
        }
        return null;
    }
}