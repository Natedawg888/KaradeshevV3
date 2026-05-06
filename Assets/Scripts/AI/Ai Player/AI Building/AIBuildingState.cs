// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIBuildingState : MonoBehaviour
// {
//     [Header("Building Discovery Settings")]
//     public bool isDiscovered = false;

//     public Material undiscoveredAIMaterial;
//     public Material glowingUndiscoveredAIMaterial;

//     private Renderer buildingRenderer;
//     private Material[] originalMaterials;

//     private void Awake()
//     {
//         buildingRenderer = GetComponent<Renderer>();

//         if (buildingRenderer == null)
//         {
//             Debug.LogError($"[AIBuildingState] Renderer is NULL for {gameObject.name}!");
//             return;
//         }

//         originalMaterials = buildingRenderer.materials; // Store original materials
//     }

//     public void UpdateBuildingMaterialForAI(AIPlayer aiPlayer)
//     {
//         if (buildingRenderer == null)
//         {
//             Debug.LogError($"[AIBuildingState] Renderer is NULL for {gameObject.name}!");
//             return;
//         }

//         if (aiPlayer == null)
//         {
//             Debug.LogError("[AIBuildingState] AIPlayer is NULL when updating AI building material!");
//             return;
//         }

//         if (isDiscovered)
//         {
//             // ✅ AI is discovered → restore normal materials
//             buildingRenderer.materials = originalMaterials;
//             Debug.Log($"[AIBuildingState] {gameObject.name} switched to original materials (AI is discovered).");
//         }
//         else
//         {
//             // ✅ Fetch base materials from AIColorRegistry
//             if (AIColorRegistry.Instance != null)
//             {
//                 Material baseUndiscovered = AIColorRegistry.Instance.GetBaseUndiscoveredMaterial();
//                 Material baseGlowingUndiscovered = AIColorRegistry.Instance.GetBaseGlowingUndiscoveredMaterial();

//                 // ✅ Apply AI-colored undiscovered materials
//                 ApplyAIMaterials(baseUndiscovered, baseGlowingUndiscovered, aiPlayer.aiColor);
//             }
//             else
//             {
//                 Debug.LogError("[AIBuildingState] AIColorRegistry is NULL! Cannot fetch base materials.");
//             }
//         }
//     }

//     /// **🔹 Applies AI-colored versions of the base materials**
//     private void ApplyAIMaterials(Material baseUndiscovered, Material baseGlowingUndiscovered, Color aiColor)
//     {
//         if (baseUndiscovered == null || baseGlowingUndiscovered == null)
//         {
//             Debug.LogError("[AIBuildingState] Base AI materials are NULL! Cannot apply AI colors.");
//             return;
//         }

//         // ✅ Clone base materials to avoid affecting all AI players
//         undiscoveredAIMaterial = new Material(baseUndiscovered);
//         glowingUndiscoveredAIMaterial = new Material(baseGlowingUndiscovered);

//         // ✅ Modify AI materials to match the AI color
//         Color aiUndiscoveredColor = MatchRGBWithBrightness(baseUndiscovered.color, aiColor);
//         Color aiGlowingColor = MatchRGBWithBrightness(baseGlowingUndiscovered.color, aiColor);

//         // ✅ Apply colors
//         SetMaterialColor(undiscoveredAIMaterial, aiUndiscoveredColor, applyEmission: false);
//         SetMaterialColor(glowingUndiscoveredAIMaterial, aiGlowingColor, applyEmission: true);

//         // ✅ Update building's renderer with the AI-colored materials
//         Material[] aiMaterials = new Material[buildingRenderer.materials.Length];
//         for (int i = 0; i < aiMaterials.Length; i++)
//         {
//             aiMaterials[i] = undiscoveredAIMaterial;
//         }

//         buildingRenderer.materials = aiMaterials;

//         Debug.Log($"[AIBuildingState] {gameObject.name} updated with AI-colored undiscovered materials.");
//     }

//     /// **🔹 Matches AI color while keeping original brightness**
//     private Color MatchRGBWithBrightness(Color baseColor, Color aiColor)
//     {
//         float baseH, baseS, baseV;
//         Color.RGBToHSV(baseColor, out baseH, out baseS, out baseV);

//         float aiH, aiS, aiV;
//         Color.RGBToHSV(aiColor, out aiH, out aiS, out aiV);

//         // ✅ Ensure minimum saturation for proper hue shift
//         if (baseS < 0.5f) baseS = 0.75f;

//         Color modifiedColor = Color.HSVToRGB(aiH, baseS, baseV);
        
//         Debug.Log($"[MatchRGBWithBrightness] Base: {baseColor} -> AI Adjusted: {modifiedColor} (Hue: {aiH}, Sat: {baseS}, Bright: {baseV})");
//         return modifiedColor;
//     }

//     /// **🔹 Sets color properties for materials**
//     private void SetMaterialColor(Material material, Color color, bool applyEmission)
//     {
//         if (material == null) return;

//         if (material.HasProperty("_BaseColor"))
//             material.SetColor("_BaseColor", color);
//         if (material.HasProperty("_MainColor"))
//             material.SetColor("_MainColor", color);
//         if (material.HasProperty("_Color"))
//             material.SetColor("_Color", color);
//         if (material.HasProperty("_TintColor"))
//             material.SetColor("_TintColor", color);

//         if (material.HasProperty("_MainTex"))
//             material.SetTexture("_MainTex", null); // Remove texture

//         if (applyEmission && material.HasProperty("_EmissionColor"))
//         {
//             material.SetColor("_EmissionColor", color * 2f);
//             material.EnableKeyword("_EMISSION");
//         }
//         else
//         {
//             material.DisableKeyword("_EMISSION");
//         }

//         Debug.Log($"[SetMaterialColor] Applied color: {color} to {material.name}");
//     }

//     public void SetDiscovered(bool discovered, AIPlayer aiPlayer)
//     {
//         if (aiPlayer == null)
//         {
//             Debug.LogError("[AIBuildingState] AIPlayer is NULL when setting AI discovered status!");
//             return;
//         }

//         isDiscovered = discovered;
//         UpdateBuildingMaterialForAI(aiPlayer);
//     }
// }