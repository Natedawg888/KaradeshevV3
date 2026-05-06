// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIColorRegistry : MonoBehaviour
// {
//     public static AIColorRegistry Instance;

//     [Header("AI Base Materials")]
//     public Material baseUndiscoveredMaterial;
//     public Material baseGlowingUndiscoveredMaterial;

//     private List<Color> registeredColors = new List<Color>();

//     private void Awake()
//     {
//         if (Instance == null)
//         {
//             Instance = this;
//             DontDestroyOnLoad(gameObject); // ✅ Keep AIColorRegistry persistent across scenes
//         }
//         else
//         {
//             Destroy(gameObject);
//         }
//     }

//     /// ✅ **Registers AI Color and Ensures It’s Unique**
//     public Color GetUniqueAIColor()
//     {
//         Color newColor;
//         int attempts = 0;
//         do
//         {
//             newColor = GenerateLightColor();
//             attempts++;
//         }
//         while (IsColorTooSimilar(newColor) && attempts < 10); // Max attempts to avoid infinite loops

//         registeredColors.Add(newColor);
//         return newColor;
//     }

//     /// **🔹 Checks if Color is Too Similar to Existing Colors**
//     private bool IsColorTooSimilar(Color newColor)
//     {
//         foreach (Color existingColor in registeredColors)
//         {
//             if (AreColorsSimilar(existingColor, newColor))
//                 return true;
//         }
//         return false;
//     }

//     /// **🔹 Determines if Two Colors Are Too Similar**
//     private bool AreColorsSimilar(Color c1, Color c2)
//     {
//         float threshold = 0.15f; // Adjust similarity threshold as needed
//         float diff = Mathf.Abs(c1.r - c2.r) + Mathf.Abs(c1.g - c2.g) + Mathf.Abs(c1.b - c2.b);
//         return diff < threshold; // If difference is too small, colors are "too similar"
//     }

//     /// ✅ **Generates a Light AI Color (High Brightness)**
//     private Color GenerateLightColor()
//     {
//         float hue = UnityEngine.Random.Range(0f, 1f);      // Any hue (0 - 1)
//         float saturation = UnityEngine.Random.Range(0.4f, 0.6f);  // Moderate saturation (not too intense)
//         float brightness = UnityEngine.Random.Range(0.7f, 1f);     // ✅ High brightness

//         return Color.HSVToRGB(hue, saturation, brightness);
//     }

//     /// **🔹 Retrieves AI Base Undiscovered Material**
//     public Material GetBaseUndiscoveredMaterial() => baseUndiscoveredMaterial;
    
//     /// **🔹 Retrieves AI Base Glowing Undiscovered Material**
//     public Material GetBaseGlowingUndiscoveredMaterial() => baseGlowingUndiscoveredMaterial;
// }