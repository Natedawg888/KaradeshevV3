using UnityEngine;

[CreateAssetMenu(
    fileName = "PathogenCauseDefinition",
    menuName = "Kardashev/Disease/Pathogen Cause Definition")]
public class PathogenCauseDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public PathogenCauseType causeType;
    public string displayName;

    [TextArea(2, 5)]
    public string description;

    [Header("UI")]
    public Sprite causeIcon;
    public Color uiTint = Color.white;

    [TextArea(2, 4)]
    public string tooltipText;
}