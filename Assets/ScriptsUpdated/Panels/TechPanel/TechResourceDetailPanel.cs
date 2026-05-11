using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Detail panel shown when the player clicks a resource entry.
/// Displays name, icon, description, and sources:
///   - envIconsRoot / tileIconsRoot filled with deduplicated spawn-location entries
///   - sourcesText for crafting / production sources
/// spawnerSourcesRoot is hidden when the resource has no world-spawn sources.
/// </summary>
public class TechResourceDetailPanel : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text descriptionText;

    [Header("Spawn Location Icons")]
    public GameObject spawnerSourcesRoot;
    public Transform envIconsRoot;
    public Transform tileIconsRoot;
    public EnvironmentTypeEntry envEntryPrefab;
    public TileTypeEntry tileEntryPrefab;
    public EnvironmentIconLibrary iconLibrary;
    [Tooltip("Shown when the resource has no world-spawn sources at all.")]
    public TMP_Text noSpawnersText;

    [Header("Other Sources")]
    public TMP_Text sourcesText;

    private readonly List<GameObject> _spawnedEntries = new();

    private void Awake()
    {
        if (closeButton) closeButton.onClick.AddListener(Hide);
        if (root) root.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowFor(ResourceDefinition resource)
    {
        if (resource == null) { Hide(); return; }

        ClearEntries();

        if (root) root.SetActive(true);
        gameObject.SetActive(true);

        if (nameText)        nameText.text        = resource.resourceName ?? resource.resourceID;
        if (descriptionText) descriptionText.text = resource.description  ?? string.Empty;
        if (icon)
        {
            icon.sprite  = resource.resourceIcon;
            icon.enabled = resource.resourceIcon != null;
        }

        var sources = ResourceSourceCache.GetSources(resource);

        PopulateSpawnEntries(sources);

        if (sourcesText != null)
            sourcesText.text = BuildNonSpawnerText(sources);
    }

    public void Hide()
    {
        ClearEntries();
        if (root) root.SetActive(false);
    }

    // ── Spawn entry fill ──────────────────────────────────────────────────────

    private void PopulateSpawnEntries(ResourceSourceCache.ResourceSources sources)
    {
        // Only base-environment spawners (not event-triggered) drive the icon area
        bool hasAnySpawners   = sources != null && sources.spawnerSources.Count > 0;
        bool hasBaseSpawners  = false;
        if (hasAnySpawners)
            foreach (var ss in sources.spawnerSources)
                if (!ss.isExternal) { hasBaseSpawners = true; break; }

        if (noSpawnersText != null)
            noSpawnersText.text = hasAnySpawners ? string.Empty : "No Spawners";

        if (spawnerSourcesRoot != null)
            spawnerSourcesRoot.SetActive(hasBaseSpawners);

        if (!hasBaseSpawners) return;

        var envTypes  = new HashSet<EnvironmentType>();
        var tileTypes = new HashSet<EnvironmentTileType>();

        foreach (var ss in sources.spawnerSources)
        {
            if (ss.isExternal) continue;

            if (ss.environmentTypes != null)
                foreach (var e in ss.environmentTypes) envTypes.Add(e);

            if (ss.tileTypes != null)
                foreach (var t in ss.tileTypes) tileTypes.Add(t);
        }

        if (envIconsRoot != null && envEntryPrefab != null)
        {
            foreach (var envType in envTypes)
            {
                var entry = Instantiate(envEntryPrefab, envIconsRoot);
                entry.Bind(envType, iconLibrary != null ? iconLibrary.GetEnvIcon(envType) : null);
                _spawnedEntries.Add(entry.gameObject);
            }
        }

        if (tileIconsRoot != null && tileEntryPrefab != null)
        {
            foreach (var tileType in tileTypes)
            {
                var entry = Instantiate(tileEntryPrefab, tileIconsRoot);
                entry.Bind(tileType, iconLibrary != null ? iconLibrary.GetTileIcon(tileType) : null);
                _spawnedEntries.Add(entry.gameObject);
            }
        }
    }

    private void ClearEntries()
    {
        for (int i = _spawnedEntries.Count - 1; i >= 0; i--)
        {
            if (_spawnedEntries[i] != null)
                Destroy(_spawnedEntries[i]);
        }
        _spawnedEntries.Clear();
    }

    // ── Source text ───────────────────────────────────────────────────────────

    private static string BuildNonSpawnerText(ResourceSourceCache.ResourceSources sources)
    {
        if (sources == null) return string.Empty;

        var sb = new StringBuilder();

        // External tile-event sources (animal remains, burnt, flooding, etc.)
        var externalLabels = new HashSet<string>();
        foreach (var ss in sources.spawnerSources)
            if (ss.isExternal && !string.IsNullOrEmpty(ss.externalSourceLabel))
                externalLabels.Add(ss.externalSourceLabel);

        if (externalLabels.Count > 0)
        {
            sb.AppendLine("<b>Found from:</b>");
            foreach (var label in externalLabels)
                sb.AppendLine($"  • {label}");
        }

        if (sources.craftingRecipes.Count > 0)
        {
            sb.AppendLine("<b>Crafting:</b>");
            foreach (var r in sources.craftingRecipes)
                sb.AppendLine($"  • {r.craftingName ?? r.craftingID}");
        }

        if (sources.productionPlans.Count > 0)
        {
            sb.AppendLine("<b>Production:</b>");
            foreach (var p in sources.productionPlans)
                sb.AppendLine($"  • {p.planName ?? p.productionID}");
        }

        return sb.ToString().TrimEnd();
    }
}
