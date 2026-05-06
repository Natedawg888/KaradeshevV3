using UnityEngine;
using UnityEngine.UI;

public class BuildingStatusIcon : MonoBehaviour
{
    [Header("Bindings")]
    public BuildingStatus source;
    public Image statusImage;
    public GameObject iconHolderObject;

    [Header("Sprites")]
    public Sprite damagedIcon;
    public Sprite destroyedIcon;

    [Header("Display")]
    public bool hideWhenNormal = true;
    public bool hideWhenMissingSprite = true;


    private void Awake()
    {
        if (!source) source = GetComponentInParent<BuildingStatus>();
        if (!statusImage) statusImage = GetComponentInChildren<Image>(true);

        SetVisible(false);
    }

    private void OnEnable()
    {
        if (source) source.OnStateChanged += HandleStateChanged;

        // If icon is under normalRoot, reparent under the building root so it won't be disabled
        if (source && source.normalRoot && transform.IsChildOf(source.normalRoot.transform))
        {
            transform.SetParent(source.transform, true); // keep world position
        }

        HandleStateChanged(source ? source.CurrentState : BuildingState.Normal);
    }

    private void OnDisable()
    {
        if (source) source.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(BuildingState s)
    {
        if (!statusImage)
        {
            SetVisible(false);
            return;
        }

        // choose sprite by state
        Sprite next = null;
        switch (s)
        {
            case BuildingState.Normal:
                next = null; // no icon in normal
                break;
            case BuildingState.Damaged:
                next = damagedIcon;
                break;
            case BuildingState.Destroyed:
                next = destroyedIcon;
                break;
        }

        statusImage.sprite = next;

        // visibility rules
        bool shouldHide =
            (hideWhenNormal && s == BuildingState.Normal) ||
            (hideWhenMissingSprite && next == null);

        SetVisible(!shouldHide);
    }

    private void SetVisible(bool on)
    {
        if (iconHolderObject)
        {
            iconHolderObject.SetActive(on);
        }
    }

    public void RefreshNow()
    {
        HandleStateChanged(source ? source.CurrentState : BuildingState.Normal);
    }
}
