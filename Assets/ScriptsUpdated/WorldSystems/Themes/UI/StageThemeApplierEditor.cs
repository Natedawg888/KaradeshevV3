#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StageThemeApplierPreview))]
public class StageThemeApplierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var preview = (StageThemeApplierPreview)target;
        var applier = preview.applier ? preview.applier : preview.GetComponent<StageThemeApplier>();
        if (!applier) return;

        var lib = applier.themeLibrary;

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Theme Preview Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = lib && lib.themes != null && lib.themes.Count > 0;

            if (GUILayout.Button("◀ Prev Theme"))
            {
                preview.CycleTheme(-1);
                EditorUtility.SetDirty(preview);
            }

            if (GUILayout.Button("Apply Preview Now"))
            {
                preview.ApplyPreviewNow();
                EditorUtility.SetDirty(preview);
            }

            if (GUILayout.Button("Next Theme ▶"))
            {
                preview.CycleTheme(+1);
                EditorUtility.SetDirty(preview);
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif