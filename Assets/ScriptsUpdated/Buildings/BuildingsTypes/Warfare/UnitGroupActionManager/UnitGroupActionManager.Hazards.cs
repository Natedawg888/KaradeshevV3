using UnityEngine;

public partial class UnitGroupActionManager
{
    // ---------------------------------------------------------------------
    //  HAZARDS WHEN SCOUTING UNDISCOVERED ENVIRONMENT TILES
    // ---------------------------------------------------------------------

    private bool TryComputeScoutHazardChances(
        TileControl destTile,
        TileUnitGroupData group,
        out EnvironmentControl envCtrl,
        out float hazardChance01,
        out float damageOutcomeChance01,
        out float fatalOutcomeChance01)
    {
        envCtrl = null;
        hazardChance01 = 0f;
        damageOutcomeChance01 = 0f;
        fatalOutcomeChance01 = 0f;

        if (destTile == null || group == null)
            return false;

        if (destTile.tileContentType != TileContentType.Environment)
            return false;

        envCtrl = destTile.GetComponentInChildren<EnvironmentControl>();
        if (envCtrl == null || envCtrl.IsDiscovered)
            return false;

        hazardChance01 = Mathf.Clamp01(envCtrl.DiscoveryFailureChance / 100f);
        if (hazardChance01 <= 0f)
            return false;

        float healthComponent = Mathf.Max(1f, group.maxHealth);
        float defenceComponent = 0f;

        float toughnessScore = (healthComponent + defenceComponent) * Mathf.Max(1, group.unitCount);

        float normalizedToughness = toughnessScore / (toughnessScore + 100f);
        float damageGivenHazard = Mathf.Clamp01(normalizedToughness);
        float fatalGivenHazard = 1f - damageGivenHazard;

        damageOutcomeChance01 = hazardChance01 * damageGivenHazard;
        fatalOutcomeChance01 = hazardChance01 * fatalGivenHazard;

        return true;
    }

    private bool TryApplyUndiscoveredTileHazardForScout(
        TileUnitGroupData group,
        TileUnitGroupControl owner,
        TileControl destTile)
    {
        if (group == null || owner == null || destTile == null)
            return false;

        EnvironmentControl envCtrl;
        float hazardChance01;
        float dmgOutcome01;
        float fatalOutcome01;

        if (!TryComputeScoutHazardChances(destTile, group, out envCtrl, out hazardChance01, out dmgOutcome01, out fatalOutcome01))
            return false;

        float roll = Random.value;
        if (roll > hazardChance01)
        {
            //Debug.Log(
                //$"[UnitGroupActionManager] Group {group.groupId} scouted undiscovered '{envCtrl.environmentName}' safely " +
                //$"(roll={roll:0.00} vs hazard={hazardChance01:0.00}).");
            return false;
        }

        float fatalGivenHazard = Mathf.Approximately(hazardChance01, 0f)
            ? 0f
            : Mathf.Clamp01(fatalOutcome01 / hazardChance01);

        bool isFatalEvent = Random.value < fatalGivenHazard;

        if (isFatalEvent)
        {
            int maxCasualties = Mathf.Max(1, group.unitCount);
            int casualties = Random.Range(1, maxCasualties + 1);

            ApplyFatalCasualtiesToGroupWhileScouting(group, owner, casualties, envCtrl, hazardChance01);
            return group.unitCount <= 0;
        }
        else
        {
            ApplyNonFatalDamageToGroupWhileScouting(group, envCtrl, hazardChance01);
            owner.RefreshMarker(group);
            return false;
        }
    }

    private void ApplyNonFatalDamageToGroupWhileScouting(
        TileUnitGroupData group,
        EnvironmentControl envCtrl,
        float hazardStrength01)
    {
        if (group == null) return;

        float minFrac = 0.10f;
        float maxFrac = 0.40f;
        float frac = Mathf.Lerp(minFrac, maxFrac, Mathf.Clamp01(hazardStrength01));

        int damage = Mathf.Max(1, Mathf.RoundToInt(group.maxHealth * frac));

        int oldHealth = group.currentHealth;
        group.currentHealth = Mathf.Max(1, group.currentHealth - damage);

        //Debug.Log(
            //$"[UnitGroupActionManager] Group {group.groupId} took {damage} damage " +
            //$"while SCOUTING in undiscovered '{envCtrl.environmentName}' " +
            //$"(health {oldHealth} → {group.currentHealth}/{group.maxHealth}).");
    }

    private void ApplyFatalCasualtiesToGroupWhileScouting(
        TileUnitGroupData group,
        TileUnitGroupControl owner,
        int casualties,
        EnvironmentControl envCtrl,
        float hazardStrength01)
    {
        if (group == null || owner == null) return;

        casualties = Mathf.Clamp(casualties, 1, group.unitCount);

        var popMgr = PlayersPopulationManager.Instance;

        if (!string.IsNullOrEmpty(group.populationReservationId) && popMgr != null)
        {
            int killFromReservation = casualties;

            if (group.reservedPopulation > 0)
                killFromReservation = Mathf.Min(killFromReservation, group.reservedPopulation);

            if (killFromReservation > 0)
            {
                popMgr.ApplyPenaltyFromReservation(group.populationReservationId, killFromReservation);
                group.reservedPopulation = Mathf.Max(0, group.reservedPopulation - killFromReservation);
            }
        }

        group.unitCount -= casualties;

        //Debug.Log(
            //$"[UnitGroupActionManager] Group {group.groupId} lost {casualties} " +
            //$"people while SCOUTING undiscovered '{envCtrl.environmentName}' " +
            //$"(units left: {group.unitCount}).");

        if (group.unitCount <= 0)
        {
            //Debug.Log(
                //$"[UnitGroupActionManager] Group {group.groupId} was wiped out by hazards while SCOUTING in '{envCtrl.environmentName}'.");

            if (!string.IsNullOrEmpty(group.populationReservationId) && popMgr != null && group.reservedPopulation > 0)
            {
                popMgr.ApplyPenaltyFromReservation(group.populationReservationId, group.reservedPopulation);
                group.reservedPopulation = 0;
            }

            if (!string.IsNullOrEmpty(group.populationReservationId) && popMgr != null)
            {
                popMgr.ReleaseReservation(group.populationReservationId);
                group.populationReservationId = null;
            }

            owner.RemoveGroupDueToFatalities(group);
        }
        else
        {
            owner.RefreshMarker(group);
        }
    }
}
