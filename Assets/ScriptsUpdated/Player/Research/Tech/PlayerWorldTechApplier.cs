using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerWorldTechApplier : MonoBehaviour
{
    public static PlayerWorldTechApplier Instance { get; private set; }

    private Dictionary<string, List<WorldTechEffectSO>> _effectsByTech;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        BuildCache();
    }

    private void BuildCache()
    {
        _effectsByTech = new Dictionary<string, List<WorldTechEffectSO>>();
        var all = Resources.LoadAll<WorldTechEffectSO>("") ?? new WorldTechEffectSO[0];

        foreach (var so in all)
        {
            if (!so || string.IsNullOrWhiteSpace(so.techID))
                continue;

            if (!_effectsByTech.TryGetValue(so.techID, out var list))
            {
                list = new List<WorldTechEffectSO>();
                _effectsByTech[so.techID] = list;
            }

            list.Add(so);
        }

        int total = _effectsByTech.Sum(kv => kv.Value?.Count ?? 0);
        //Debug.Log($"[WorldTechApplier] BuildCache loaded WorldTechEffectSO techKeys={_effectsByTech.Count}, totalEffects={total}");
    }

    public void ApplyFor(string techID)
    {
        if (string.IsNullOrWhiteSpace(techID))
            return;

        if (_effectsByTech == null)
            BuildCache();

        if (!_effectsByTech.TryGetValue(techID, out var list) || list == null || list.Count == 0)
            return;

        var knownResMgr = PlayerKnownResourcesManager.Instance;
        var knownBuildMgr = PlayerKnownBuildingsManager.Instance;
        var knownTechMgr = PlayerKnownTechnologyManager.Instance;
        var knownCraftMgr = PlayerKnownCraftingManager.Instance;
        var knownProdMgr = PlayerKnownProductionManager.Instance;
        var knownUnitMgr = PlayerKnownUnitsManager.Instance;
        var knownSpiritMgr = PlayerKnownSpiritsManager.Instance;
        var knownRitualMgr = PlayerKnownRitualsManager.Instance;

        var addResSet = new HashSet<ResourceDefinition>();
        var removeResSet = new HashSet<ResourceDefinition>();

        var addBldSet = new HashSet<string>(System.StringComparer.Ordinal);
        var removeBldSet = new HashSet<string>(System.StringComparer.Ordinal);

        var addTechSet = new HashSet<string>(System.StringComparer.Ordinal);
        var removeTechSet = new HashSet<string>(System.StringComparer.Ordinal);

        var addCraftSet = new HashSet<string>(System.StringComparer.Ordinal);
        var removeCraftSet = new HashSet<string>(System.StringComparer.Ordinal);

        var addProdSet = new HashSet<string>(System.StringComparer.Ordinal);
        var removeProdSet = new HashSet<string>(System.StringComparer.Ordinal);

        var addUnitSet = new HashSet<MilitiaUnit>();
        var removeUnitSet = new HashSet<MilitiaUnit>();

        var addSpiritSet = new HashSet<SpiritDefinitionSO>();
        var removeSpiritSet = new HashSet<SpiritDefinitionSO>();

        var addRitualSet = new HashSet<ReligionRitualDefinitionSO>();
        var removeRitualSet = new HashSet<ReligionRitualDefinitionSO>();

        foreach (var so in list)
        {
            if (!so)
                continue;

            if (so.addKnownResources != null)
                foreach (var def in so.addKnownResources)
                    if (def) addResSet.Add(def);

            if (so.removeKnownResources != null)
                foreach (var def in so.removeKnownResources)
                    if (def) removeResSet.Add(def);

            if (so.addKnownBuildingIDs != null)
                foreach (var id in so.addKnownBuildingIDs)
                    if (!string.IsNullOrWhiteSpace(id)) addBldSet.Add(id.Trim());

            if (so.removeKnownBuildingIDs != null)
                foreach (var id in so.removeKnownBuildingIDs)
                    if (!string.IsNullOrWhiteSpace(id)) removeBldSet.Add(id.Trim());

            if (so.addKnownTechnologyIDs != null)
                foreach (var id in so.addKnownTechnologyIDs)
                    if (!string.IsNullOrWhiteSpace(id)) addTechSet.Add(id.Trim());

            if (so.removeKnownTechnologyIDs != null)
                foreach (var id in so.removeKnownTechnologyIDs)
                    if (!string.IsNullOrWhiteSpace(id)) removeTechSet.Add(id.Trim());

            if (so.addKnownCraftingIDs != null)
                foreach (var id in so.addKnownCraftingIDs)
                    if (!string.IsNullOrWhiteSpace(id)) addCraftSet.Add(id.Trim());

            if (so.removeKnownCraftingIDs != null)
                foreach (var id in so.removeKnownCraftingIDs)
                    if (!string.IsNullOrWhiteSpace(id)) removeCraftSet.Add(id.Trim());

            if (so.addKnownProductionIDs != null)
                foreach (var id in so.addKnownProductionIDs)
                    if (!string.IsNullOrWhiteSpace(id)) addProdSet.Add(id.Trim());

            if (so.removeKnownProductionIDs != null)
                foreach (var id in so.removeKnownProductionIDs)
                    if (!string.IsNullOrWhiteSpace(id)) removeProdSet.Add(id.Trim());

            if (so.addKnownUnits != null)
                foreach (var u in so.addKnownUnits)
                    if (u) addUnitSet.Add(u);

            if (so.removeKnownUnits != null)
                foreach (var u in so.removeKnownUnits)
                    if (u) removeUnitSet.Add(u);

            if (so.addKnownSpirits != null)
                foreach (var spirit in so.addKnownSpirits)
                    if (spirit) addSpiritSet.Add(spirit);

            if (so.removeKnownSpirits != null)
                foreach (var spirit in so.removeKnownSpirits)
                    if (spirit) removeSpiritSet.Add(spirit);

            if (so.addKnownRituals != null)
                foreach (var ritual in so.addKnownRituals)
                    if (ritual) addRitualSet.Add(ritual);

            if (so.removeKnownRituals != null)
                foreach (var ritual in so.removeKnownRituals)
                    if (ritual) removeRitualSet.Add(ritual);
        }

        // Apply path: remove beats add.
        if (removeResSet.Count > 0)
            addResSet.RemoveWhere(d => d != null && removeResSet.Contains(d));

        if (removeBldSet.Count > 0)
            addBldSet.ExceptWith(removeBldSet);

        if (removeTechSet.Count > 0)
            addTechSet.ExceptWith(removeTechSet);

        if (removeCraftSet.Count > 0)
            addCraftSet.ExceptWith(removeCraftSet);

        if (removeProdSet.Count > 0)
            addProdSet.ExceptWith(removeProdSet);

        if (removeUnitSet.Count > 0)
            addUnitSet.RemoveWhere(u => u != null && removeUnitSet.Contains(u));

        if (removeSpiritSet.Count > 0)
            addSpiritSet.RemoveWhere(s => s != null && removeSpiritSet.Contains(s));

        if (removeRitualSet.Count > 0)
            addRitualSet.RemoveWhere(r => r != null && removeRitualSet.Contains(r));

        if (knownResMgr != null)
        {
            if (addResSet.Count > 0) knownResMgr.LearnMany(addResSet);
            if (removeResSet.Count > 0) knownResMgr.ForgetMany(removeResSet);
        }

        if (knownBuildMgr != null)
        {
            if (addBldSet.Count > 0) knownBuildMgr.LearnMany(addBldSet);
            if (removeBldSet.Count > 0) knownBuildMgr.ForgetMany(removeBldSet);
        }

        if (knownTechMgr != null)
        {
            if (addTechSet.Count > 0) knownTechMgr.LearnMany(addTechSet);
            if (removeTechSet.Count > 0) knownTechMgr.ForgetMany(removeTechSet, revokeIfResearched: true);
        }

        if (knownCraftMgr != null)
        {
            if (addCraftSet.Count > 0) knownCraftMgr.LearnMany(addCraftSet);
            if (removeCraftSet.Count > 0) knownCraftMgr.ForgetMany(removeCraftSet);
        }

        if (knownProdMgr != null)
        {
            if (addProdSet.Count > 0) knownProdMgr.LearnMany(addProdSet);
            if (removeProdSet.Count > 0) knownProdMgr.ForgetMany(removeProdSet);
        }

        if (knownUnitMgr != null)
        {
            if (addUnitSet.Count > 0) knownUnitMgr.LearnMany(addUnitSet);
            if (removeUnitSet.Count > 0) knownUnitMgr.ForgetMany(removeUnitSet);
        }

        if (knownSpiritMgr != null)
        {
            if (addSpiritSet.Count > 0) knownSpiritMgr.LearnMany(addSpiritSet);
            if (removeSpiritSet.Count > 0) knownSpiritMgr.ForgetMany(removeSpiritSet);
        }

        if (knownRitualMgr != null)
        {
            foreach (var ritual in removeRitualSet)
                knownRitualMgr.ForgetRitual(ritual);

            foreach (var ritual in addRitualSet)
                knownRitualMgr.LearnRitual(ritual);
        }
    }

    public void RemoveFor(string techID)
    {
        if (string.IsNullOrWhiteSpace(techID))
            return;

        if (_effectsByTech == null)
            BuildCache();

        if (!_effectsByTech.TryGetValue(techID, out var list) || list == null || list.Count == 0)
            return;

        var knownResMgr = PlayerKnownResourcesManager.Instance;
        var knownBuildMgr = PlayerKnownBuildingsManager.Instance;
        var knownTechMgr = PlayerKnownTechnologyManager.Instance;
        var knownCraftMgr = PlayerKnownCraftingManager.Instance;
        var knownProdMgr = PlayerKnownProductionManager.Instance;
        var knownUnitMgr = PlayerKnownUnitsManager.Instance;
        var knownSpiritMgr = PlayerKnownSpiritsManager.Instance;
        var knownRitualMgr = PlayerKnownRitualsManager.Instance;

        // Undo path:
        // tech added X => undo forget X
        // tech removed Y => undo learn Y
        var addResSet = new HashSet<ResourceDefinition>();
        var removeResSet = new HashSet<ResourceDefinition>();

        var addBldSet = new HashSet<string>(System.StringComparer.Ordinal);
        var removeBldSet = new HashSet<string>(System.StringComparer.Ordinal);

        var addTechSet = new HashSet<string>(System.StringComparer.Ordinal);
        var removeTechSet = new HashSet<string>(System.StringComparer.Ordinal);

        var addCraftSet = new HashSet<string>(System.StringComparer.Ordinal);
        var removeCraftSet = new HashSet<string>(System.StringComparer.Ordinal);

        var addProdSet = new HashSet<string>(System.StringComparer.Ordinal);
        var removeProdSet = new HashSet<string>(System.StringComparer.Ordinal);

        var addUnitSet = new HashSet<MilitiaUnit>();
        var removeUnitSet = new HashSet<MilitiaUnit>();

        var addSpiritSet = new HashSet<SpiritDefinitionSO>();
        var removeSpiritSet = new HashSet<SpiritDefinitionSO>();

        var addRitualSet = new HashSet<ReligionRitualDefinitionSO>();
        var removeRitualSet = new HashSet<ReligionRitualDefinitionSO>();

        foreach (var so in list)
        {
            if (!so)
                continue;

            if (so.addKnownResources != null)
                foreach (var d in so.addKnownResources)
                    if (d) removeResSet.Add(d);

            if (so.removeKnownResources != null)
                foreach (var d in so.removeKnownResources)
                    if (d) addResSet.Add(d);

            if (so.addKnownBuildingIDs != null)
                foreach (var id in so.addKnownBuildingIDs)
                    if (!string.IsNullOrWhiteSpace(id)) removeBldSet.Add(id.Trim());

            if (so.removeKnownBuildingIDs != null)
                foreach (var id in so.removeKnownBuildingIDs)
                    if (!string.IsNullOrWhiteSpace(id)) addBldSet.Add(id.Trim());

            if (so.addKnownTechnologyIDs != null)
                foreach (var id in so.addKnownTechnologyIDs)
                    if (!string.IsNullOrWhiteSpace(id)) removeTechSet.Add(id.Trim());

            if (so.removeKnownTechnologyIDs != null)
                foreach (var id in so.removeKnownTechnologyIDs)
                    if (!string.IsNullOrWhiteSpace(id)) addTechSet.Add(id.Trim());

            if (so.addKnownCraftingIDs != null)
                foreach (var id in so.addKnownCraftingIDs)
                    if (!string.IsNullOrWhiteSpace(id)) removeCraftSet.Add(id.Trim());

            if (so.removeKnownCraftingIDs != null)
                foreach (var id in so.removeKnownCraftingIDs)
                    if (!string.IsNullOrWhiteSpace(id)) addCraftSet.Add(id.Trim());

            if (so.addKnownProductionIDs != null)
                foreach (var id in so.addKnownProductionIDs)
                    if (!string.IsNullOrWhiteSpace(id)) removeProdSet.Add(id.Trim());

            if (so.removeKnownProductionIDs != null)
                foreach (var id in so.removeKnownProductionIDs)
                    if (!string.IsNullOrWhiteSpace(id)) addProdSet.Add(id.Trim());

            if (so.addKnownUnits != null)
                foreach (var u in so.addKnownUnits)
                    if (u) removeUnitSet.Add(u);

            if (so.removeKnownUnits != null)
                foreach (var u in so.removeKnownUnits)
                    if (u) addUnitSet.Add(u);

            if (so.addKnownSpirits != null)
                foreach (var spirit in so.addKnownSpirits)
                    if (spirit) removeSpiritSet.Add(spirit);

            if (so.removeKnownSpirits != null)
                foreach (var spirit in so.removeKnownSpirits)
                    if (spirit) addSpiritSet.Add(spirit);

            if (so.addKnownRituals != null)
                foreach (var ritual in so.addKnownRituals)
                    if (ritual) removeRitualSet.Add(ritual);

            if (so.removeKnownRituals != null)
                foreach (var ritual in so.removeKnownRituals)
                    if (ritual) addRitualSet.Add(ritual);
        }

        // Undo path: re-learn beats forget, because we're reversing the applied result.
        if (addResSet.Count > 0)
            removeResSet.RemoveWhere(d => d != null && addResSet.Contains(d));

        if (addBldSet.Count > 0)
            removeBldSet.ExceptWith(addBldSet);

        if (addTechSet.Count > 0)
            removeTechSet.ExceptWith(addTechSet);

        if (addCraftSet.Count > 0)
            removeCraftSet.ExceptWith(addCraftSet);

        if (addProdSet.Count > 0)
            removeProdSet.ExceptWith(addProdSet);

        if (addUnitSet.Count > 0)
            removeUnitSet.RemoveWhere(u => u != null && addUnitSet.Contains(u));

        if (addSpiritSet.Count > 0)
            removeSpiritSet.RemoveWhere(s => s != null && addSpiritSet.Contains(s));

        if (addRitualSet.Count > 0)
            removeRitualSet.RemoveWhere(r => r != null && addRitualSet.Contains(r));

        if (knownResMgr != null)
        {
            if (removeResSet.Count > 0) knownResMgr.ForgetMany(removeResSet);
            if (addResSet.Count > 0) knownResMgr.LearnMany(addResSet);
        }

        if (knownBuildMgr != null)
        {
            if (removeBldSet.Count > 0) knownBuildMgr.ForgetMany(removeBldSet);
            if (addBldSet.Count > 0) knownBuildMgr.LearnMany(addBldSet);
        }

        if (knownTechMgr != null)
        {
            if (removeTechSet.Count > 0) knownTechMgr.ForgetMany(removeTechSet, revokeIfResearched: true);
            if (addTechSet.Count > 0) knownTechMgr.LearnMany(addTechSet);
        }

        if (knownCraftMgr != null)
        {
            if (removeCraftSet.Count > 0) knownCraftMgr.ForgetMany(removeCraftSet);
            if (addCraftSet.Count > 0) knownCraftMgr.LearnMany(addCraftSet);
        }

        if (knownProdMgr != null)
        {
            if (removeProdSet.Count > 0) knownProdMgr.ForgetMany(removeProdSet);
            if (addProdSet.Count > 0) knownProdMgr.LearnMany(addProdSet);
        }
        else
        {
            //Debug.LogError("[WorldTechApplier] PlayerKnownProductionManager.Instance is NULL (cannot undo production plans).");
        }

        if (knownUnitMgr != null)
        {
            if (removeUnitSet.Count > 0) knownUnitMgr.ForgetMany(removeUnitSet);
            if (addUnitSet.Count > 0) knownUnitMgr.LearnMany(addUnitSet);
        }

        if (knownSpiritMgr != null)
        {
            if (removeSpiritSet.Count > 0) knownSpiritMgr.ForgetMany(removeSpiritSet);
            if (addSpiritSet.Count > 0) knownSpiritMgr.LearnMany(addSpiritSet);
        }

        if (knownRitualMgr != null)
        {
            foreach (var ritual in removeRitualSet)
                knownRitualMgr.ForgetRitual(ritual);

            foreach (var ritual in addRitualSet)
                knownRitualMgr.LearnRitual(ritual);
        }
    }
}
