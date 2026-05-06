using System.Collections.Generic;

public interface IPairingService
{
    bool TryPickParentsForFamilies(IList<string> allowedFamilyIds,
                                   float minHealth, int minAgeTurns, int maxAgeTurns,
                                   out Individual mother, out Individual father);
    int CollectPairsForFamilies(IList<string> familyIds,
                                float minHealth, int minAgeTurns, int maxAgeTurns,
                                List<(Individual mother, Individual father)> outPairs,
                                int maxPairs);

    void CleanupInvalidPairs();
}