using System.Collections.Generic;

[System.Serializable]
public class Family
{
    public string FamilyName;
    
    public string FamilyId;
    public string PartnerAId;
    public string PartnerBId;
    public List<string> ChildrenIds = new();

    // NEW — lineage helpers
    public string ParentFamilyId;  // direct parent family (who this branch emancipated from)
    public string LineageRootId;   // stable root of the line (first family in the line)

    public Family(string aId, string bId = null, string familyName = null)
    {
        FamilyId   = System.Guid.NewGuid().ToString();
        PartnerAId = aId;
        PartnerBId = bId;
        FamilyName = string.IsNullOrEmpty(familyName) ? GenerateDefaultName() : familyName;

        // Lineage defaults: start a new root if none is set later by the caller
        ParentFamilyId = null;
        LineageRootId  = FamilyId;
    }

    public bool HasTwoAdults => !string.IsNullOrEmpty(PartnerAId) && !string.IsNullOrEmpty(PartnerBId);

    private static int _seq = 1;
    private static string GenerateDefaultName() => $"Family {_seq++}";
}