using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;   // <-- add this
#endif

[CreateAssetMenu(menuName = "UI/Stage Theme", fileName = "StageTheme_")]
public class StageTheme : ScriptableObject
{
    [Header("Which stage this theme applies to")]
    public Stage stage;


    [Header("Typography")]
    public TMP_FontAsset tmpFont;
    public Color fontColor = Color.white;

    [Header("Banners")]
    public Sprite topBannerSprite;
    public Sprite bottomBannerSprite;

    [Header("Borders")]
    public Sprite horizontalBorderSprite;
    public Sprite squareBorderSprite;

    [Header("Title Cards")]
    public Sprite titleCardSprite;

    [Header("Population — Icon Sprite")]           // 👈 NEW
    public Sprite populationIcon;

    [Header("Panels — Sprite for themed panels")]
    public Sprite panelSprite;                 // used for a list of UI panels

    // StageTheme.cs  (ADD)
    [Header("Profile / Avatars (per stage)")]
    public List<Sprite> stageAvatars; // per-stage avatar set

    [Tooltip("If the current avatar name isn't found when switching stages, use this one.")]
    public string fallbackAvatarName;

    [Header("Randomise Button — Base Sprite")]
    public Sprite randomiseBtnSprite;

    [Header("Avatar Menu — Item Prefab")]
    public GameObject avatarItemPrefab;

    [Header("Avatar Scroll View — Layout (optional overrides)")]
    public bool overrideAvatarScrollPos = false;
    public Vector2 avatarScrollAnchoredPos;   // anchoredPosition of the Scroll View

    public bool overrideAvatarScrollHeight = false;
    public float avatarScrollHeight = 600f;   // sizeDelta.y of the Scroll View

    [Header("Inventory Button — Base Sprite")]
    public Sprite inventoryBtnSprite;

    [Header("Experience Tracker — Prefab")]
    public GameObject xpTrackerPrefab;

    [Header("Experience Tracker — Layout")]
    public bool overrideXPTrackerPosition = false;
    public Vector2 xpTrackerAnchoredPos = Vector2.zero;

    public bool overrideXPTrackerSize = false;
    public Vector2 xpTrackerSize = new Vector2(160f, 160f);

    [Header("Misc Buttons — Base Sprite")]
    public Sprite miscButtonSprite;
    public bool overrideMiscButtonTextColor = false;
    public Color miscButtonTextColor = Color.white;

    [Header("Info Buttons — Base Sprite")]
    public Sprite infoButtonSprite;

    [Header("Inventory Text 1 (Filters)")]
    public bool overrideInvText1Position = false;
    public Vector2 invText1AnchoredPos;

    public bool overrideInvText1RectSize = false;
    public Vector2 invText1RectSize;

    public bool overrideInvText1FontSize = false;
    public float invText1FontSize = 32f;

    [Header("Inventory Text 2 (Totals)")]
    public bool overrideInvText2Position = false;
    public Vector2 invText2AnchoredPos;

    public bool overrideInvText2RectSize = false;
    public Vector2 invText2RectSize;

    public bool overrideInvText2FontSize = false;
    public float invText2FontSize = 28f;

    [Header("Inventory Scroll View — Layout (optional overrides)")]
    public bool overrideInventoryScrollPos = false;
    public Vector2 inventoryScrollAnchoredPos;

    public bool overrideInventoryScrollSize = false;   // width & height in one go
    public Vector2 inventoryScrollSize = new Vector2(600f, 500f);

    [Header("Inventory Grid — Layout (optional overrides)")]
    public bool overrideInventoryGridColumns = false;
    public int inventoryGridColumns = 4;   // default number of columns
    public bool overrideInventoryGridSpacing = false;
    public Vector2 inventoryGridSpacing = new Vector2(10, 10);

    [Header("Inventory — Item Prefab")]
    public GameObject inventoryItemPrefab;

    [Header("Undiscovery Panel — Text 1")]
    public bool overrideUndiscText1Position = false;
    public Vector2 undiscText1AnchoredPos;

    public bool overrideUndiscText1RectSize = false;
    public Vector2 undiscText1RectSize;

    public bool overrideUndiscText1FontSize = false;
    public float undiscText1FontSize = 32f;

    // (optional) if you want per-theme color as well:
    public bool overrideUndiscText1Color = false;
    public Color undiscText1Color = Color.white;

    [Header("Undiscovery Panel — Text 2")]
    public bool overrideUndiscText2Position = false;
    public Vector2 undiscText2AnchoredPos;

    public bool overrideUndiscText2RectSize = false;
    public Vector2 undiscText2RectSize;

    public bool overrideUndiscText2FontSize = false;
    public float undiscText2FontSize = 28f;

    // (optional) per-theme color
    public bool overrideUndiscText2Color = false;
    public Color undiscText2Color = Color.white;

    [Header("Undiscovery Information Panel — Layout")]
    public bool overrideUndiscPanelSize = false;
    public Vector2 undiscPanelSize = new Vector2(520f, 360f);

    [Header("Information Object — Layout (optional overrides)")]
    public bool overrideInfoObjectPosition = false;
    public Vector2 infoObjectAnchoredPos;

    [Header("Discovered Panel — Layout")]
    public bool overrideDiscoveredPanelPosition = false;
    public Vector2 discoveredPanelAnchoredPos = Vector2.zero;

    [Header("Discovered Panel — Border Sprite")]
    public Sprite discoveredPanelBorderSprite;

    [Header("Discovered Panel — Border Layout")]
    public bool overrideDiscoveredBorderSize = false;
    public Vector2 discoveredBorderSize = new Vector2(600f, 400f);

    [Header("Discovered Panel — Child Layout")]
    public bool overrideDiscoveredChildPosition = false;        // local position relative to panel
    public Vector2 discoveredChildAnchoredPos = Vector2.zero;

    [Header("Discovered Panel — Child 2 Layout")]
    public bool overrideDiscoveredChild2Position = false;        // local position relative to panel
    public Vector2 discoveredChild2AnchoredPos = Vector2.zero;
    
    [Header("Name Edit Buttons — Sprites")]
    public Sprite nameEditButtonSprite;    // for the "Edit Name" button(s)
    public Sprite nameCancelButtonSprite;  // for the "Cancel Edit" button(s)

    [Header("Discovered Panel — Text 1")]
    public bool overrideDiscText1Position = false;
    public Vector2 discText1AnchoredPos;

    public bool overrideDiscText1RectSize = false;
    public Vector2 discText1RectSize;

    public bool overrideDiscText1FontSize = false;
    public float discText1FontSize = 32f;

    // (optional) if you want per-theme color as well:
    public bool overrideDiscText1Color = false;
    public Color discText1Color = Color.white;

    [Header("Discovery Sliders — Layout")]
    public bool overrideSlidersPosition = false;
    public Vector2 slidersAnchoredPos = Vector2.zero;     // applied to each slider

    public bool overrideSlidersSize = false;
    public Vector2 slidersSize = new Vector2(300f, 20f);  // width x height for each

    [Header("Gathering Info Panel — Layout")]
    public bool overrideGatheringInfoPanelSize = false;
    public Vector2 gatheringInfoPanelSize = new Vector2(560f, 360f);

    [Header("Gathering Info Object — Layout (optional overrides)")]
    public bool overrideGatheringInfoObjectPosition = false;
    public Vector2 gatheringInfoObjectAnchoredPos;


    [Header("Survey Info Panel — Layout")]
    public bool overrideSurveyInfoPanelSize = false;
    public Vector2 surveyInfoPanelSize = new Vector2(560f, 360f);

    [Header("Survey Info Object — Layout (optional overrides)")]
    public bool overrideSurveyInfoObjectPosition = false;
    public Vector2 surveyInfoObjectAnchoredPos;

    [Header("Survey Panel — Layout")]
    public bool overrideSurveyPanelSize = false;
    public Vector2 surveyPanelSize = new Vector2(560f, 360f);

    [Header("Survey Object — Layout (optional overrides)")]
    public bool overrideSurveyObjectPosition = false;
    public Vector2 surveyObjectAnchoredPos;

    [Header("Resource Entry Prefab")]
    public GameObject resourceEntryPrefab;

    [Header("Collected Goods Panel — Layout")]
    public bool overrideCollectGoodsSize = false;
    public Vector2 collectGoodsPanelSize = new Vector2(520f, 360f);

    [Header("Collected Goods Object — Layout (optional overrides)")]
    public bool overrideCollectGoodsObjectPosition = false;
    public Vector2 collectGoodsObjectAnchoredPos;

    [Header("Collected Goods Object 2 — Layout (optional overrides)")]
    public bool overrideCollectGoods2ObjectPosition = false;
    public Vector2 collectGoods2ObjectAnchoredPos;

    [Header("Collected Goods Panel — Text 1")]
    public bool overrideCollectGoodsText1Position = false;
    public Vector2 collectGoodsText1AnchoredPos;

    public bool overrideCollectGoodsText1RectSize = false;
    public Vector2 collectGoodsText1RectSize;

    public bool overrideCollectGoodsText1FontSize = false;
    public float collectGoodsText1FontSize = 32f;

    // (optional) if you want per-theme color as well:
    public bool overrideCollectGoodsText1Color = false;
    public Color collectGoodsText1Color = Color.white;

    [Header("Collected Goods — Item Prefab")]
    public GameObject collectedItemEntryPrefab;

    [Header("Season — Icon Sprites")]
    public Sprite springIcon;
    public Sprite summerIcon;
    public Sprite autumnIcon;
    public Sprite winterIcon;

    [Header("Season — Fill Sprites")]
    public Sprite springFill;
    public Sprite summerFill;
    public Sprite autumnFill;
    public Sprite winterFill;

    [Header("Turn Phases — Icon Sprites")]
    public Sprite phaseDayIcon;
    public Sprite phaseDuskIcon;
    public Sprite phaseNightIcon;
    public Sprite phaseDawnIcon;

    [Header("Turn Phases — Fill Sprite")]
    public Sprite phaseFill;

    [Header("Population Pie — Sprites")]
    public Sprite malePieSprite;
    public Sprite femalePieSprite;

    [Header("Population Text 1")]
    public bool overridePopText1Position = false;
    public Vector2 popText1AnchoredPos;

    public bool overridePopText1RectSize = false;
    public Vector2 popText1RectSize;

    public bool overridePopText1FontSize = false;
    public float popText1FontSize = 32f;

    [Header("Bars — Fill/Base Sprite")]
    public Sprite barSprite;

    [Header("Age Group — Icon Sprites")]
    public Sprite childrenIcon;
    public Sprite teensIcon;
    public Sprite adultsIcon;
    public Sprite eldersIcon;

    [Header("Lines — Color")]
    public Color lineColor = Color.white;

    // (room to grow later: colors, fonts, sfx, etc.)
    
    #if UNITY_EDITOR
    // ---------- Editor-only: drag the exact folder here ----------
    [Header("Editor Only — Avatar Folder")]
    [Tooltip("Drag the stage's avatar folder here. All sprites inside (incl. subfolders & sliced sprites) will be used.")]
    public DefaultAsset avatarsFolder;

    [Tooltip("Auto-refresh 'stageAvatars' from the folder when you edit this asset.")]
    public bool autoRefreshFromFolder = true;

    [ContextMenu("Refresh Avatars From Folder")]
    public void Editor_RefreshAvatarsFromFolder()
    {
        var sprites = Editor_FindSpritesInFolder(avatarsFolder);
        if (sprites == null) return;
        stageAvatars = sprites;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private void OnValidate()
    {
        if (!autoRefreshFromFolder) return;
        var sprites = Editor_FindSpritesInFolder(avatarsFolder);
        if (sprites == null) return;
        stageAvatars = sprites;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private static List<Sprite> Editor_FindSpritesInFolder(DefaultAsset folder)
    {
        if (!folder) return null;

        var path = UnityEditor.AssetDatabase.GetAssetPath(folder);
        if (string.IsNullOrEmpty(path) || !UnityEditor.AssetDatabase.IsValidFolder(path))
        {
            //Debug.LogWarning($"[StageTheme] Invalid folder: {path}");
            return new List<Sprite>();
        }

        // Search recursively for Texture2D/Sprite assets under this folder
        var guids = UnityEditor.AssetDatabase.FindAssets("t:Texture2D t:Sprite", new[] { path });

        var results = new List<Sprite>(guids.Length * 2);
        var seen = new HashSet<Sprite>();

        foreach (var guid in guids)
        {
            var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath); // gets sliced sprites too
            if (assets == null) continue;

            foreach (var a in assets)
            {
                if (a is Sprite s && !seen.Contains(s))
                {
                    seen.Add(s);
                    results.Add(s);
                }
            }
        }

        // Stable order (by name). Change to path if you prefer deterministic by folder order.
        results.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        return results;
    }
#endif
}
