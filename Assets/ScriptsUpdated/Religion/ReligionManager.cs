using System.Collections.Generic;
using UnityEngine;

public class ReligionManager : MonoBehaviour
{
    public static ReligionManager Instance { get; private set; }

    [Header("Available Spirits In This Game")]
    [SerializeField] private List<SpiritDefinitionSO> allSpirits = new List<SpiritDefinitionSO>();

    private readonly Dictionary<string, SpiritDefinitionSO> _byId =
        new Dictionary<string, SpiritDefinitionSO>(System.StringComparer.Ordinal);

    public IReadOnlyList<SpiritDefinitionSO> AllSpirits => allSpirits;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RebuildLookup();
    }

    public void RebuildLookup()
    {
        _byId.Clear();

        for (int i = 0; i < allSpirits.Count; i++)
        {
            SpiritDefinitionSO spirit = allSpirits[i];
            if (spirit == null || string.IsNullOrWhiteSpace(spirit.spiritID))
                continue;

            string id = spirit.spiritID.Trim();
            if (!_byId.ContainsKey(id))
                _byId.Add(id, spirit);
        }
    }

    public SpiritDefinitionSO GetSpiritById(string spiritId)
    {
        if (string.IsNullOrWhiteSpace(spiritId))
            return null;

        _byId.TryGetValue(spiritId.Trim(), out SpiritDefinitionSO result);
        return result;
    }

    public List<SpiritDefinitionSO> GetSpiritsForBeliefSystem(BeliefSystemType beliefSystem)
    {
        List<SpiritDefinitionSO> results = new List<SpiritDefinitionSO>();

        for (int i = 0; i < allSpirits.Count; i++)
        {
            SpiritDefinitionSO spirit = allSpirits[i];
            if (spirit == null)
                continue;

            if (spirit.beliefSystem == beliefSystem)
                results.Add(spirit);
        }

        return results;
    }
}