using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[CreateAssetMenu(menuName = "Kardashev/Task Failure Story Database", fileName = "TaskFailureStoryDatabase")]
public class TaskFailureStoryDatabase : ScriptableObject
{
    [Serializable]
    public class FailureStorySet
    {
        public TaskFailureType type;

        [Header("Intro / Outro")]
        [TextArea] public string[] intros;
        [TextArea] public string[] outros;

        [Header("Per EnvironmentType")]
        public EnvLines[] environmentLines;

        [Header("Per EnvironmentTileType")]
        public TileLines[] tileLines;

        [Header("Per TileSize")]
        public SizeLines[] sizeLines;

        [Header("Population Loss Lines")]
        [TextArea] public string[] lossLines;   // uses {LOSS}
        [TextArea] public string[] noLossLines; // when {LOSS} == 0
    }

    [Serializable] public struct EnvLines { public EnvironmentType env; public string[] lines; }
    [Serializable] public struct TileLines { public EnvironmentTileType tile; public string[] lines; }
    [Serializable] public struct SizeLines { public TileSize size; public string[] lines; }

    [SerializeField] private FailureStorySet[] sets;

    public string BuildStory(EnvironmentControl env, TaskFailureType type, int populationLost)
    {
        if (env == null) return "";

        var set = GetSet(type);
        if (set == null)
            return DefaultFallback(env, type, populationLost);

        string intro = Pick(set.intros);
        string envLine = Pick(FindLines(set.environmentLines, env.environmentType));
        string tileLine = Pick(FindLines(set.tileLines, env.environmentTileType));
        string sizeLine = Pick(FindLines(set.sizeLines, env.tileSize));
        string outro = Pick(set.outros);

        string popTemplate = (populationLost > 0) ? Pick(set.lossLines) : Pick(set.noLossLines);

        // ✅ No List alloc, no Join alloc
        var sb = new StringBuilder(256);

        AppendPart(sb, ReplacePlaceholders(intro, env, type, populationLost));
        AppendPart(sb, ReplacePlaceholders(envLine, env, type, populationLost));
        AppendPart(sb, ReplacePlaceholders(tileLine, env, type, populationLost));
        AppendPart(sb, ReplacePlaceholders(sizeLine, env, type, populationLost));
        AppendPart(sb, ReplacePlaceholders(outro, env, type, populationLost));
        AppendPart(sb, ReplacePlaceholders(popTemplate, env, type, populationLost));

        return sb.ToString();
    }

    private static void AppendPart(StringBuilder sb, string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return;

        s = s.Trim();
        if (s.Length == 0) return;

        if (sb.Length > 0)
            sb.Append(' ');

        sb.Append(s);

        // ✅ punctuation without allocating a new string
        char c = s[s.Length - 1];
        if (c != '.' && c != '!' && c != '?')
            sb.Append('.');
    }

    private FailureStorySet GetSet(TaskFailureType type)
    {
        if (sets == null) return null;
        for (int i = 0; i < sets.Length; i++)
            if (sets[i] != null && sets[i].type == type)
                return sets[i];
        return null;
    }

    private static string[] FindLines(EnvLines[] arr, EnvironmentType key)
    {
        if (arr == null) return null;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i].lines != null && arr[i].env.Equals(key))
                return arr[i].lines;
        return null;
    }

    private static string[] FindLines(TileLines[] arr, EnvironmentTileType key)
    {
        if (arr == null) return null;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i].lines != null && arr[i].tile.Equals(key))
                return arr[i].lines;
        return null;
    }

    private static string[] FindLines(SizeLines[] arr, TileSize key)
    {
        if (arr == null) return null;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i].lines != null && arr[i].size.Equals(key))
                return arr[i].lines;
        return null;
    }

    private static string Pick(string[] arr)
    {
        if (arr == null || arr.Length == 0) return "";
        int idx = UnityEngine.Random.Range(0, arr.Length);
        return arr[idx] ?? "";
    }

    private static string ReplacePlaceholders(string s, EnvironmentControl env, TaskFailureType type, int loss)
    {
        if (string.IsNullOrEmpty(s) || env == null) return s ?? "";

        return s
            .Replace("{ENV_NAME}", string.IsNullOrEmpty(env.environmentName) ? "this place" : env.environmentName)
            .Replace("{ENV}", Nicify(env.environmentType.ToString()))
            .Replace("{TILE}", Nicify(env.environmentTileType.ToString()))
            .Replace("{SIZE}", Nicify(env.tileSize.ToString()))
            .Replace("{TYPE}", type.ToString())
            .Replace("{LOSS}", Mathf.Max(0, loss).ToString());
    }

    private static string Nicify(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var sb = new StringBuilder(raw.Length + 8);
        sb.Append(raw[0]);
        for (int i = 1; i < raw.Length; i++)
        {
            char c = raw[i];
            char p = raw[i - 1];
            if (char.IsUpper(c) && !char.IsUpper(p))
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string DefaultFallback(EnvironmentControl env, TaskFailureType type, int loss)
    {
        string kind = type == TaskFailureType.Discovery ? "discovery" : "gathering";
        string baseLine =
            $"The {kind} attempt in the {Nicify(env.environmentType.ToString())} ({Nicify(env.environmentTileType.ToString())}, {Nicify(env.tileSize.ToString())}) ran into trouble.";
        if (loss > 0) baseLine += $" In the chaos, {loss} were lost.";
        return baseLine;
    }
}