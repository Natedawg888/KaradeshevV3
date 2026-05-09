using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopStatsPieView : PopStatsSubviewBase
{
    [Header("Pie")]
    public RectTransform genderPieRoot;
    public Image maleSliceImage;      // TOP (front) wedge
    public Image femaleSliceImage;    // BOTTOM (background full circle)
    public TMP_Text maleCountText;
    public TMP_Text femaleCountText;

    public override void RefreshNow()
    {
        if (!maleSliceImage || !femaleSliceImage) return;

        // --- counts drive the pie (accurate split) ---
        int maleCount = 0, femaleCount = 0;
        if (populationManager && populationManager.AllPopulations != null)
        {
            var pops = populationManager.AllPopulations;
            for (int i = 0; i < pops.Count; i++)
            {
                var p = pops[i];
                if (p == null) continue;
                if      (p.gender == Gender.Male)   maleCount   += p.count;
                else if (p.gender == Gender.Female) femaleCount += p.count;
            }
        }
        else if (stats != null)
        {
            var g = stats.GetGenderRatios();
            var hist = stats.History;
            int total = (hist != null && hist.Count > 0) ? hist[hist.Count - 1].total : 0;
            maleCount   = Mathf.RoundToInt(total * Mathf.Clamp01(g.male));
            femaleCount = Mathf.Max(0, total - maleCount);
        }
        int totalCount = maleCount + femaleCount;

        if (maleCountText)   maleCountText.text   = maleCount.ToString();
        if (femaleCountText) femaleCountText.text = femaleCount.ToString();

        // --- draw: female = full circle background; male = wedge overlay ---
        ConfigureFullCircle(femaleSliceImage); // full background

        float maleFill = totalCount > 0 ? (float)maleCount / totalCount : 0.5f;
        ConfigureRadialWedge(maleSliceImage, maleFill); // wedge from default origin

        // make sure the male slice is above the female in hierarchy (front)
        // (or use separate canvases/sorting if you prefer)
    }

    private static void ConfigureFullCircle(Image img)
    {
        if (!img) return;
        img.type = Image.Type.Filled;                 // can be Simple if sprite is a full circle
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = 2;                           // any origin is fine when fill=1
        img.fillClockwise = true;
        img.fillAmount = 1f;                          // full circle
        img.rectTransform.localEulerAngles = Vector3.zero; // no rotation
    }

    private static void ConfigureRadialWedge(Image img, float fill01)
    {
        if (!img) return;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = 2;                           // consistent start (e.g., bottom)
        img.fillClockwise = true;
        img.fillAmount = Mathf.Clamp01(fill01);       // 0..1 of the circle
        img.rectTransform.localEulerAngles = Vector3.zero; // no rotation
    }
}