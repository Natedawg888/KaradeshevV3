#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds the NotificationRow and NotificationPanel prefabs.
/// Tools → Notifications → Create Notification Prefabs
/// </summary>
public static class NotificationPrefabBuilder
{
    private const string SaveFolder = "Assets/Resources/Notifications";

    [MenuItem("Tools/Notifications/Create Notification Prefabs")]
    public static void CreateAll()
    {
        EnsureFolder();
        var rowPrefab   = CreateRowPrefab();
        var panelPrefab = CreatePanelPrefab(rowPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[NotificationPrefabBuilder] Created prefabs in " + SaveFolder);
        Selection.activeObject = panelPrefab;
    }

    // ------------------------------------------------------------------
    // Row prefab
    // ------------------------------------------------------------------

    private static GameObject CreateRowPrefab()
    {
        // Root
        var root = new GameObject("NotificationRow");
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(400, 80);
        root.AddComponent<CanvasRenderer>();

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        // Delete button (left stripe — colour shows read/unread state)
        var indicator = CreateChild(root, "DeleteButton");
        var indicatorRect = indicator.GetComponent<RectTransform>();
        indicatorRect.anchorMin = new Vector2(0, 0);
        indicatorRect.anchorMax = new Vector2(0, 1);
        indicatorRect.offsetMin = Vector2.zero;
        indicatorRect.offsetMax = new Vector2(18, 0);
        var indicatorImg = indicator.AddComponent<Image>();
        indicatorImg.color = new Color(0.2f, 0.8f, 1f, 1f);
        var deleteBtn = indicator.AddComponent<Button>();
        deleteBtn.targetGraphic = indicatorImg;
        var deleteLabelGO = CreateChild(indicator, "X");
        FullStretch(deleteLabelGO.GetComponent<RectTransform>());
        var deleteLabel = deleteLabelGO.AddComponent<TextMeshProUGUI>();
        deleteLabel.text      = "✕";
        deleteLabel.fontSize  = 11;
        deleteLabel.color     = Color.white;
        deleteLabel.alignment = TextAlignmentOptions.Center;

        // Type icon (left of text area)
        var iconGO = CreateChild(root, "TypeIcon");
        var iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0, 0.5f);
        iconRect.anchorMax = new Vector2(0, 0.5f);
        iconRect.pivot     = new Vector2(0, 0.5f);
        iconRect.anchoredPosition = new Vector2(10, 0);
        iconRect.sizeDelta = new Vector2(32, 32);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.color = Color.white;
        iconImg.preserveAspect = true;

        // Title (offset right of icon)
        var titleGO = CreateChild(root, "Title");
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.5f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.offsetMin = new Vector2(50, 0);
        titleRect.offsetMax = new Vector2(-8, 0);
        var title = titleGO.AddComponent<TextMeshProUGUI>();
        title.text      = "Title";
        title.fontSize  = 14;
        title.fontStyle = FontStyles.Bold;
        title.color     = Color.white;
        title.alignment = TextAlignmentOptions.BottomLeft;

        // Message
        var msgGO = CreateChild(root, "Message");
        var msgRect = msgGO.GetComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0, 0);
        msgRect.anchorMax = new Vector2(0.65f, 0.5f);
        msgRect.offsetMin = new Vector2(50, 4);
        msgRect.offsetMax = new Vector2(0, 0);
        var msg = msgGO.AddComponent<TextMeshProUGUI>();
        msg.text      = "Message";
        msg.fontSize  = 12;
        msg.color     = new Color(0.8f, 0.8f, 0.8f, 1f);
        msg.alignment = TextAlignmentOptions.TopLeft;

        // Turn label
        var turnGO = CreateChild(root, "TurnLabel");
        var turnRect = turnGO.GetComponent<RectTransform>();
        turnRect.anchorMin = new Vector2(0.75f, 0);
        turnRect.anchorMax = new Vector2(1, 0.5f);
        turnRect.offsetMin = new Vector2(0, 4);
        turnRect.offsetMax = new Vector2(-8, 0);
        var turn = turnGO.AddComponent<TextMeshProUGUI>();
        turn.text      = "Turn 0";
        turn.fontSize  = 11;
        turn.color     = new Color(0.6f, 0.6f, 0.6f, 1f);
        turn.alignment = TextAlignmentOptions.BottomRight;

        // Go To button (bottom-right of row)
        var goToGO = CreateChild(root, "GoToButton");
        var goToRect = goToGO.GetComponent<RectTransform>();
        goToRect.anchorMin = new Vector2(1, 0);
        goToRect.anchorMax = new Vector2(1, 1);
        goToRect.offsetMin = new Vector2(-72, 8);
        goToRect.offsetMax = new Vector2(-8, -8);
        var goToImg = goToGO.AddComponent<Image>();
        goToImg.color = new Color(0.15f, 0.45f, 0.75f, 1f);
        var goToBtn = goToGO.AddComponent<Button>();
        goToBtn.targetGraphic = goToImg;
        var goToText = CreateChild(goToGO, "Text");
        FullStretch(goToText.GetComponent<RectTransform>());
        var goToTMP = goToText.AddComponent<TextMeshProUGUI>();
        goToTMP.text      = "Go To";
        goToTMP.fontSize  = 11;
        goToTMP.color     = Color.white;
        goToTMP.alignment = TextAlignmentOptions.Center;

        // Wire NotificationRowUI
        var rowUI = root.AddComponent<NotificationRowUI>();
        SetSerializedFields(rowUI, title, msg, turn, deleteBtn, iconImg, goToBtn);

        // Save prefab
        string path   = SaveFolder + "/NotificationRow.prefab";
        var prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefabAsset;
    }

    // ------------------------------------------------------------------
    // Panel prefab
    // ------------------------------------------------------------------

    private static GameObject CreatePanelPrefab(GameObject rowPrefab)
    {
        // Root panel
        var root = new GameObject("NotificationPanel");
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(420, 500);
        root.AddComponent<CanvasRenderer>();
        var rootBg = root.AddComponent<Image>();
        rootBg.color = new Color(0.1f, 0.1f, 0.1f, 0.97f);

        // Header
        var header = CreateChild(root, "Header");
        var headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.offsetMin = new Vector2(0, -50);
        headerRect.offsetMax = Vector2.zero;
        var headerImg = header.AddComponent<Image>();
        headerImg.color = new Color(0.08f, 0.08f, 0.08f, 1f);

        var headerLabel = CreateChild(header, "HeaderLabel");
        var hlRect = headerLabel.GetComponent<RectTransform>();
        hlRect.anchorMin = Vector2.zero;
        hlRect.anchorMax = Vector2.one;
        hlRect.offsetMin = new Vector2(12, 0);
        hlRect.offsetMax = new Vector2(-100, 0);
        var hl = headerLabel.AddComponent<TextMeshProUGUI>();
        hl.text      = "Notifications";
        hl.fontSize  = 16;
        hl.fontStyle = FontStyles.Bold;
        hl.color     = Color.white;
        hl.alignment = TextAlignmentOptions.MidlineLeft;

        // Clear All button
        var clearBtn = CreateChild(header, "ClearAllButton");
        var clearRect = clearBtn.GetComponent<RectTransform>();
        clearRect.anchorMin = new Vector2(1, 0);
        clearRect.anchorMax = new Vector2(1, 1);
        clearRect.offsetMin = new Vector2(-90, 8);
        clearRect.offsetMax = new Vector2(-8, -8);
        var clearImg = clearBtn.AddComponent<Image>();
        clearImg.color = new Color(0.6f, 0.2f, 0.2f, 1f);
        var clearButton = clearBtn.AddComponent<Button>();
        clearButton.targetGraphic = clearImg;
        var clearText = CreateChild(clearBtn, "Text");
        FullStretch(clearText.GetComponent<RectTransform>());
        var ct = clearText.AddComponent<TextMeshProUGUI>();
        ct.text      = "Clear All";
        ct.fontSize  = 12;
        ct.color     = Color.white;
        ct.alignment = TextAlignmentOptions.Center;

        // Scroll view
        var scrollGO = CreateChild(root, "ScrollView");
        var scrollRect2 = scrollGO.GetComponent<RectTransform>();
        scrollRect2.anchorMin = new Vector2(0, 0);
        scrollRect2.anchorMax = new Vector2(1, 1);
        scrollRect2.offsetMin = new Vector2(0, 40);
        scrollRect2.offsetMax = new Vector2(0, -50);
        scrollGO.AddComponent<Image>().color = Color.clear;
        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        // Viewport
        var viewport = CreateChild(scrollGO, "Viewport");
        FullStretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<Image>().color = Color.clear;
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        scroll.viewport = viewport.GetComponent<RectTransform>();

        // Content
        var content = CreateChild(viewport, "Content");
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot     = new Vector2(0.5f, 1);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing           = 4;
        vlg.padding           = new RectOffset(8, 8, 8, 8);
        vlg.childAlignment    = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRect;

        // Empty label
        var emptyGO = CreateChild(content, "EmptyLabel");
        var emptyRect = emptyGO.GetComponent<RectTransform>();
        emptyRect.sizeDelta = new Vector2(400, 60);
        var emptyTMP = emptyGO.AddComponent<TextMeshProUGUI>();
        emptyTMP.text      = "No notifications";
        emptyTMP.fontSize  = 14;
        emptyTMP.color     = new Color(0.5f, 0.5f, 0.5f, 1f);
        emptyTMP.alignment = TextAlignmentOptions.Center;
        var emptyCsf = emptyGO.AddComponent<ContentSizeFitter>();
        emptyCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Close button (bottom)
        var closeBtn = CreateChild(root, "CloseButton");
        var closeBtnRect = closeBtn.GetComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(0, 0);
        closeBtnRect.anchorMax = new Vector2(1, 0);
        closeBtnRect.offsetMin = new Vector2(8, 6);
        closeBtnRect.offsetMax = new Vector2(-8, 36);
        var closeImg = closeBtn.AddComponent<Image>();
        closeImg.color = new Color(0.25f, 0.25f, 0.25f, 1f);
        var closeBtnComponent = closeBtn.AddComponent<Button>();
        closeBtnComponent.targetGraphic = closeImg;
        var closeText = CreateChild(closeBtn, "Text");
        FullStretch(closeText.GetComponent<RectTransform>());
        var closeTMP = closeText.AddComponent<TextMeshProUGUI>();
        closeTMP.text      = "Close";
        closeTMP.fontSize  = 13;
        closeTMP.color     = Color.white;
        closeTMP.alignment = TextAlignmentOptions.Center;

        // Wire NotificationPanelUI
        var panelUI = root.AddComponent<NotificationPanelUI>();
        SetPanelFields(panelUI, root, rowPrefab?.GetComponent<NotificationRowUI>(),
            contentRect.gameObject, emptyGO, clearButton, closeBtnComponent);

        // Save prefab
        string path = SaveFolder + "/NotificationPanel.prefab";
        var prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefabAsset;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static GameObject CreateChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void FullStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(SaveFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "Notifications");
    }

    private static void SetSerializedFields(NotificationRowUI rowUI,
        TextMeshProUGUI title, TextMeshProUGUI msg, TextMeshProUGUI turn,
        Button deleteBtn, Image icon, Button goToBtn)
    {
        var so = new SerializedObject(rowUI);
        so.FindProperty("titleText").objectReferenceValue    = title;
        so.FindProperty("messageText").objectReferenceValue  = msg;
        so.FindProperty("turnText").objectReferenceValue     = turn;
        so.FindProperty("deleteButton").objectReferenceValue = deleteBtn;
        so.FindProperty("typeIcon").objectReferenceValue     = icon;
        so.FindProperty("goToButton").objectReferenceValue   = goToBtn;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPanelFields(NotificationPanelUI panelUI, GameObject panelRoot,
        NotificationRowUI rowPrefab, GameObject container, GameObject emptyLabel,
        Button clearBtn, Button closeBtn)
    {
        var so = new SerializedObject(panelUI);
        so.FindProperty("panelRoot").objectReferenceValue      = panelRoot;
        so.FindProperty("rowPrefab").objectReferenceValue      = rowPrefab;
        so.FindProperty("rowContainer").objectReferenceValue   = container.transform;
        so.FindProperty("emptyLabel").objectReferenceValue     = emptyLabel;
        so.FindProperty("clearAllButton").objectReferenceValue = clearBtn;
        so.FindProperty("closeButton").objectReferenceValue    = closeBtn;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
