using System;
using System.Collections.Generic;
using System.Linq;

public class DeathReconciliationService : IDeathReconciliationService
{
    public void Reconcile(IndividualRepository indRepo, FamilyRepository famRepo, PlayersPopulationManager popMgr, GeneralPopulationManager general)
    {
        if (indRepo == null || popMgr == null || general == null) return;

        // 1) Count alive per aggregate group
        var aliveByGroup = new Dictionary<Guid, int>(128);
        var all = indRepo.All;
        for (int i = 0; i < all.Count; i++)
        {
            var p = all[i];
            if (!p.IsAlive) continue;
            if (!aliveByGroup.TryGetValue(p.AggregatedGroupGuid, out int c)) c = 0;
            aliveByGroup[p.AggregatedGroupGuid] = c + 1;
        }

        // 2) Cull excess to aggregate targets
        var groups = popMgr.AllPopulations;
        for (int g = 0; g < groups.Count; g++)
        {
            var pg = groups[g];
            if (aliveByGroup.TryGetValue(pg.GroupID, out var _))
            {
                indRepo.CullExcessToTarget(pg.GroupID, pg.count);
            }
        }

        // 3) Remap invalid groups or kill
        var valid = new HashSet<Guid>(groups.Select(gg => gg.GroupID));
        for (int i = 0; i < all.Count; i++)
        {
            var p = all[i];
            if (!p.IsAlive) continue;
            if (valid.Contains(p.AggregatedGroupGuid)) continue;

            PopulationGroup replacement = null;
            for (int j = 0; j < groups.Count; j++)
            {
                var gg = groups[j];
                if (gg.ageGroup == p.AggregatedAgeGroup && gg.gender == p.Gender && gg.count > 0)
                { replacement = gg; break; }
            }

            if (replacement != null) p.AggregatedGroupGuid = replacement.GroupID;
            else indRepo.Kill(p);
        }
    }
}