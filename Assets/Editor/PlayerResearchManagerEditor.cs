using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerResearchManager))]
public class PlayerResearchManagerEditor : Editor
{
    private SerializedProperty technologyManagerProp;
    private SerializedProperty debugStartWithResearchedProp;
    private SerializedProperty debugStartWithResearchedPreviewProp;

    private int addTechPopupIndex;

    private void OnEnable()
    {
        technologyManagerProp = serializedObject.FindProperty("technologyManager");
        debugStartWithResearchedProp = serializedObject.FindProperty("debugStartWithResearched");
        debugStartWithResearchedPreviewProp = serializedObject.FindProperty("debugStartWithResearchedPreview");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "debugStartWithResearched",
            "debugStartWithResearchedPreview"
        );

        EditorGUILayout.Space(10f);
        DrawDebugTechDropdownSection((PlayerResearchManager)target);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDebugTechDropdownSection(PlayerResearchManager manager)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Debug Tech Unlock Picker", EditorStyles.boldLabel);

        TechnologyManager techManager = manager.technologyManager;
        if (techManager == null)
            techManager = FindObjectOfType<TechnologyManager>();

        if (techManager == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a TechnologyManager on PlayerResearchManager to use the dropdown picker.",
                MessageType.Info
            );

            EditorGUILayout.PropertyField(debugStartWithResearchedProp, true);
            EditorGUILayout.PropertyField(debugStartWithResearchedPreviewProp, true);
            EditorGUILayout.EndVertical();
            return;
        }

        var allTechs = techManager.GetAll()
            .Where(t => t != null && !string.IsNullOrWhiteSpace(t.techID))
            .OrderBy(t => t.requiredPlayerLevel)
            .ThenBy(t => t.techName ?? t.techID)
            .ToList();

        if (allTechs.Count == 0)
        {
            EditorGUILayout.HelpBox("No technologies found on TechnologyManager.", MessageType.Warning);
            EditorGUILayout.PropertyField(debugStartWithResearchedProp, true);
            EditorGUILayout.PropertyField(debugStartWithResearchedPreviewProp, true);
            EditorGUILayout.EndVertical();
            return;
        }

        string[] popupOptions = new string[allTechs.Count];
        for (int i = 0; i < allTechs.Count; i++)
        {
            var tech = allTechs[i];
            popupOptions[i] =
                $"{tech.techName ?? tech.techID}  [id:{tech.techID}]  Lvl≥{tech.requiredPlayerLevel}  K≥{tech.requiredKnowledge}%";
        }

        addTechPopupIndex = Mathf.Clamp(addTechPopupIndex, 0, Mathf.Max(0, popupOptions.Length - 1));

        EditorGUILayout.BeginHorizontal();
        addTechPopupIndex = EditorGUILayout.Popup("Tech To Add", addTechPopupIndex, popupOptions);

        if (GUILayout.Button("Add", GUILayout.Width(60)))
        {
            string techIDToAdd = allTechs[addTechPopupIndex].techID;
            AddTechIDIfMissing(debugStartWithResearchedProp, techIDToAdd);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Selected Debug Tech IDs", EditorStyles.boldLabel);

        if (debugStartWithResearchedProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No debug tech IDs added yet.", MessageType.None);
        }
        else
        {
            for (int i = 0; i < debugStartWithResearchedProp.arraySize; i++)
            {
                var element = debugStartWithResearchedProp.GetArrayElementAtIndex(i);
                string techID = element.stringValue;

                Technology tech = techManager.GetByID(techID);
                string label = tech != null
                    ? $"{tech.techName ?? tech.techID}  [id:{techID}]"
                    : $"MISSING TECH ID: {techID}";

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField($"{i + 1}. {label}");

                GUI.enabled = i > 0;
                if (GUILayout.Button("▲", GUILayout.Width(28)))
                {
                    debugStartWithResearchedProp.MoveArrayElement(i, i - 1);
                }

                GUI.enabled = i < debugStartWithResearchedProp.arraySize - 1;
                if (GUILayout.Button("▼", GUILayout.Width(28)))
                {
                    debugStartWithResearchedProp.MoveArrayElement(i, i + 1);
                }

                GUI.enabled = true;
                if (GUILayout.Button("X", GUILayout.Width(28)))
                {
                    debugStartWithResearchedProp.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.Space(6f);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Remove Duplicates"))
        {
            RemoveDuplicateIDs(debugStartWithResearchedProp);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
        }

        if (GUILayout.Button("Clear All"))
        {
            debugStartWithResearchedProp.ClearArray();
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.PropertyField(debugStartWithResearchedPreviewProp, true);

        EditorGUILayout.EndVertical();
    }

    private void AddTechIDIfMissing(SerializedProperty listProp, string techID)
    {
        if (string.IsNullOrWhiteSpace(techID))
            return;

        for (int i = 0; i < listProp.arraySize; i++)
        {
            var element = listProp.GetArrayElementAtIndex(i);
            if (element.stringValue == techID)
                return;
        }

        int index = listProp.arraySize;
        listProp.InsertArrayElementAtIndex(index);
        listProp.GetArrayElementAtIndex(index).stringValue = techID;
    }

    private void RemoveDuplicateIDs(SerializedProperty listProp)
    {
        var seen = new HashSet<string>();

        for (int i = listProp.arraySize - 1; i >= 0; i--)
        {
            var element = listProp.GetArrayElementAtIndex(i);
            string id = element.stringValue;

            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                listProp.DeleteArrayElementAtIndex(i);
        }
    }
}