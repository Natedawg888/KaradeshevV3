using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnvironmentIconLibrary",
    menuName = "Kardashev/Visuals/Environment Icon Library")]
public class EnvironmentIconLibrary : ScriptableObject
{
    [Serializable]
    public struct EnvTypeIcon
    {
        public EnvironmentType type;
        public Sprite icon;
    }

    [Serializable]
    public struct TileTypeIcon
    {
        public EnvironmentTileType type;
        public Sprite icon;
    }

    [Header("Environment Type Icons")]
    public EnvTypeIcon[] envTypeIcons;

    [Header("Tile Type Icons")]
    public TileTypeIcon[] tileTypeIcons;

    private Dictionary<EnvironmentType, Sprite> _envLookup;
    private Dictionary<EnvironmentTileType, Sprite> _tileLookup;

    private void OnEnable()
    {
        BuildLookups();
    }

    private void BuildLookups()
    {
        _envLookup = new Dictionary<EnvironmentType, Sprite>();
        _tileLookup = new Dictionary<EnvironmentTileType, Sprite>();

        if (envTypeIcons != null)
        {
            foreach (var e in envTypeIcons)
                if (!_envLookup.ContainsKey(e.type) && e.icon != null)
                    _envLookup.Add(e.type, e.icon);
        }

        if (tileTypeIcons != null)
        {
            foreach (var t in tileTypeIcons)
                if (!_tileLookup.ContainsKey(t.type) && t.icon != null)
                    _tileLookup.Add(t.type, t.icon);
        }
    }

    public Sprite GetEnvIcon(EnvironmentType type)
    {
        if (_envLookup == null) BuildLookups();
        return _envLookup.TryGetValue(type, out var sprite) ? sprite : null;
    }

    public Sprite GetTileIcon(EnvironmentTileType type)
    {
        if (_tileLookup == null) BuildLookups();
        return _tileLookup.TryGetValue(type, out var sprite) ? sprite : null;
    }
}
