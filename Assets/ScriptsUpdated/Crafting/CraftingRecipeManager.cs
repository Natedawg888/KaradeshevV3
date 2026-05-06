using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CraftingRecipeManager : MonoBehaviour
{
    public static CraftingRecipeManager Instance { get; private set; }

    [Tooltip("All crafting recipes in the game (drag ScriptableObject assets here).")]
    [SerializeField] private List<CraftingRecipe> allRecipes = new();

    private readonly Dictionary<string, CraftingRecipe> _byId =
        new Dictionary<string, CraftingRecipe>(StringComparer.Ordinal);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        RebuildCache();
    }

    private void RebuildCache()
    {
        _byId.Clear();
        for (int i = 0; i < allRecipes.Count; i++)
        {
            var r = allRecipes[i];
            if (r == null || string.IsNullOrWhiteSpace(r.craftingID)) continue;
            _byId[r.craftingID] = r; // last one wins if duplicates exist
        }
    }

    // -------- Query API (mirror TechnologyManager) --------
    public IReadOnlyList<CraftingRecipe> GetAll() => allRecipes;

    public CraftingRecipe GetByID(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        _byId.TryGetValue(id, out var r);
        return r;
    }

    public bool TryGet(string id, out CraftingRecipe recipe)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            recipe = null;
            return false;
        }
        return _byId.TryGetValue(id, out recipe);
    }
}