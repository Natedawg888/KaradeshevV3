using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "UI/Stage Theme Library", fileName = "StageThemeLibrary")]
public class StageThemeLibrary : ScriptableObject
{
    public List<StageTheme> themes = new();

    // ✅ Add this so callers can use .Themes
    public IReadOnlyList<StageTheme> Themes => themes;

    private Dictionary<Stage, StageTheme> _map;

    private void OnEnable() => BuildMap();

    public void BuildMap()
    {
        _map = themes
            .Where(t => t != null)
            .GroupBy(t => t.stage)
            .ToDictionary(g => g.Key, g => g.First());
    }

    public StageTheme Get(Stage stage)
    {
        if (_map == null || _map.Count != themes.Count) BuildMap();
        return (_map != null && _map.TryGetValue(stage, out var theme)) ? theme : null;
    }
}