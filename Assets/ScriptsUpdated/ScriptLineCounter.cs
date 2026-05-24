using System;
using System.IO;
using System.Linq;
using UnityEngine;

public class ScriptLineCounter : MonoBehaviour
{
    private void Start()
    {
        CountLinesOfCode();
    }

    private void CountLinesOfCode()
    {
        string scriptsFolder = Application.dataPath; // Get the Unity Assets folder
        string[] scriptFiles = Directory.GetFiles(scriptsFolder, "*.cs", SearchOption.AllDirectories);

        int totalLines = 0;

        //Debug.Log($"[ScriptLineCounter] Found {scriptFiles.Length} scripts in project.");

        foreach (string script in scriptFiles)
        {
            int lineCount = File.ReadLines(script).Count();
            totalLines += lineCount;
            //Debug.Log($"[ScriptLineCounter] {Path.GetFileName(script)}: {lineCount} lines");
        }

        Debug.Log($"[ScriptLineCounter] 🔹 Total Lines of Code in Project: {totalLines}");
    }
}
