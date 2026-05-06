using System;
using System.Collections.Generic;

public class FamilyRepository
{
    private readonly List<Family> _families;
    private readonly Dictionary<string, Family> _byId;
    private readonly System.Random _rng;

    public FamilyRepository(System.Random rng)
    {
        _rng = rng ?? new System.Random();
        _families = new List<Family>(256);
        _byId = new Dictionary<string, Family>(StringComparer.Ordinal);
    }

    public IReadOnlyList<Family> All => _families;
    public int Count => _families.Count;

    public void Clear()
    {
        _families.Clear();
        _byId.Clear();
    }

    public Family GetById(string familyId)
    {
        if (string.IsNullOrEmpty(familyId))
            return null;

        Family fam;
        return _byId.TryGetValue(familyId, out fam) ? fam : null;
    }

    public void RebuildIndexes()
    {
        _byId.Clear();

        for (int i = 0; i < _families.Count; i++)
        {
            var fam = _families[i];
            if (fam == null || string.IsNullOrWhiteSpace(fam.FamilyId))
                continue;

            _byId[fam.FamilyId] = fam;
        }
    }

    public Family CreateFamily(string partnerAId, string partnerBId, string familyName)
    {
        return CreateFamily(
            partnerAId,
            partnerBId,
            string.IsNullOrEmpty(familyName) ? "Family" : familyName,
            parentFamilyId: null,
            lineageRootId: null);
    }

    public Family CreateFamily(
        string partnerAId,
        string partnerBId,
        string familyName,
        string parentFamilyId,
        string lineageRootId)
    {
        var fam = new Family(
            partnerAId,
            partnerBId,
            string.IsNullOrEmpty(familyName) ? "Family" : familyName);

        fam.ParentFamilyId = parentFamilyId;

        if (!string.IsNullOrEmpty(lineageRootId))
            fam.LineageRootId = lineageRootId;
        else
            fam.LineageRootId = LineageUtils.NewGene(32, _rng);

        _families.Add(fam);

        if (!string.IsNullOrEmpty(fam.FamilyId))
            _byId[fam.FamilyId] = fam;

        return fam;
    }

    public Family CreateChildFamilyFrom(Family parent, string partnerAId, string partnerBId, string familyName)
    {
        if (parent == null)
            return CreateFamily(partnerAId, partnerBId, familyName);

        return CreateFamily(
            partnerAId,
            partnerBId,
            string.IsNullOrEmpty(familyName) ? parent.FamilyName : familyName,
            parentFamilyId: parent.FamilyId,
            lineageRootId: string.IsNullOrEmpty(parent.LineageRootId) ? parent.FamilyId : parent.LineageRootId);
    }

    public void RemoveAt(int idx)
    {
        if (idx < 0 || idx >= _families.Count)
            return;

        var fam = _families[idx];
        _families.RemoveAt(idx);

        if (fam != null && !string.IsNullOrEmpty(fam.FamilyId))
            _byId.Remove(fam.FamilyId);
    }

    public void Remove(Family f)
    {
        if (f == null)
            return;

        _families.Remove(f);

        if (!string.IsNullOrEmpty(f.FamilyId))
            _byId.Remove(f.FamilyId);
    }
}