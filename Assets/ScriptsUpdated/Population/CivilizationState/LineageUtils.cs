using System;
using System.Text;

public static class LineageUtils
{
    // 0-9 A-Z gene alphabet (base36)
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string NewGene(int length, Random rng)
    {
        if (length <= 0) length = 32;
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(Alphabet[rng.Next(Alphabet.Length)]);
        return sb.ToString();
    }

    public static string MergeForChild(string motherRoot, string fatherRoot, Gender childGender, Random rng)
    {
        // Normalise lengths
        int L = Math.Max(SafeLen(motherRoot), SafeLen(fatherRoot));
        if (L <= 0) L = 32;
        motherRoot = PadOrRepeat(motherRoot, L, rng);
        fatherRoot = PadOrRepeat(fatherRoot, L, rng);

        // Rule:
        //  - Sons: father dominates every 5th char, mother every 3rd char.
        //  - Daughters: mother every 5th char, father every 3rd char.
        //  - Conflicts (i%15==0): choose one randomly.
        //  - Other positions: random parent (50/50).
        //  - Small mutation chance to keep variability.
        bool male = (childGender == Gender.Male);

        var res = new StringBuilder(L);
        for (int i = 0; i < L; i++)
        {
            bool p3 = (i % 5) == 0;
            bool p5 = (i % 3) == 0;

            char m = motherRoot[i];
            char f = fatherRoot[i];
            char pick;

            if (p3 && p5)
            {
                // both hit: random tie-break
                pick = (rng.NextDouble() < 0.5) ? (male ? f : m) : (male ? m : f);
            }
            else if (p3)
            {
                pick = male ? f : m;
            }
            else if (p5)
            {
                pick = male ? m : f;
            }
            else
            {
                pick = (rng.NextDouble() < 0.5) ? m : f;
            }

            // tiny mutation rate (e.g., 1.5%)
            if (rng.NextDouble() < 0.015)
                pick = Alphabet[rng.Next(Alphabet.Length)];

            res.Append(pick);
        }
        return res.ToString();
    }

    public static double HammingSimilarity(string a, string b)
    {
        int L = Math.Max(SafeLen(a), SafeLen(b));
        if (L == 0) return 1.0;
        a = PadOrRepeat(a, L, null);
        b = PadOrRepeat(b, L, null);

        int same = 0;
        for (int i = 0; i < L; i++)
            if (a[i] == b[i]) same++;
        return (double)same / L;
    }

    // Returns true when two individuals share enough genetic similarity to block pairing.
    // threshold=0 disables the check entirely; threshold=0.5 blocks siblings/close relatives.
    public static bool IsTooSimilarForPairing(string a, string b, double threshold)
    {
        if (threshold <= 0.0) return false;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        return HammingSimilarity(a, b) >= threshold;
    }

    private static int SafeLen(string s) => string.IsNullOrEmpty(s) ? 0 : s.Length;

    private static string PadOrRepeat(string s, int L, Random rng)
    {
        if (string.IsNullOrEmpty(s))
            return NewGene(L, rng ?? new Random());
        if (s.Length == L) return s;
        if (s.Length > L)  return s.Substring(0, L);

        // repeat, then trim
        var sb = new StringBuilder(L);
        while (sb.Length < L) sb.Append(s);
        if (sb.Length > L) sb.Length = L;
        return sb.ToString();
    }
}
