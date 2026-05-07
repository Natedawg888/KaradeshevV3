#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TempApplyAnimalFieldPatch
{
    [Serializable]
    private class AnimalPatch
    {
        public string key;
        public string[] aliases;

        public int huntingRangeTiles;
        public float huntingHungerThreshold;
        public float maxPreyPowerAdvantageToHunt;
        public float riskyHuntHungerThreshold;
        public int maxTargetEscapesBeforeGiveUp;
        public float preyRetaliationStrengthTolerance;
        public float preyLowHealthRetaliationThreshold;
        public float preyRetaliationDefenseTolerance;

        public int predatorConflictRangeTiles;
        public int predatorDensityConflictThreshold;

        public float ownSpeciesConflictBias;
        public bool allowOwnSpeciesConflict;
        public bool allowOwnSpeciesConflictInMatingSeason;
        public bool allowOwnSpeciesConflictOutOfMatingSeason;
        public float ownSpeciesConflictMaxHerding;
        public int ownSpeciesConflictMinNearbyGroups;
        public int ownSpeciesConflictMinNearbyAnimals;

        public float maxTargetPowerAdvantageToEngage;
        public float riskyConflictAggressionThreshold;
        public float weaknessThresholdToChallengeStrongerTarget;
        public float predatorTerritoriality;
        public float conflictNeedThreshold;

        public float ageWeaknessStartsFraction;
        public float maxAgeWeaknessContribution;

        public int fleeDistanceTiles;
        public float herdFleeTriggerThreshold;
        public int herdFleeSignalRangeTiles;

        public bool canLeaveStragglersOnEscape;
        public float baseEscapeSplitChance;
        public float maxExtraEscapeSplitChanceFromWeakness;
        public int minEscapeStragglers;
        public int maxEscapeStragglers;

        public string[] preferredPreyIds;
        public string[] dislikedPredatorIds;
        public string[] likedAnimalIds;

        public float hungerSatisfiedOnSuccessfulHunt;
        public float thirstSatisfiedOnSuccessfulHunt;
        public float abandonHuntForWaterNeedThreshold;
    }

    private static bool SetAnimalArray(
        SerializedObject so,
        string propertyName,
        string[] ids,
        Dictionary<string, AnimalDefinition> animalLookup)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null || !prop.isArray)
            return false;

        ids ??= Array.Empty<string>();

        bool changed = prop.arraySize != ids.Length;
        if (prop.arraySize != ids.Length)
            prop.arraySize = ids.Length;

        for (int i = 0; i < ids.Length; i++)
        {
            AnimalDefinition target = null;
            animalLookup.TryGetValue(Normalize(ids[i]), out target);

            SerializedProperty element = prop.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue != target)
            {
                element.objectReferenceValue = target;
                changed = true;
            }
        }

        return changed;
    }

    private static Dictionary<string, AnimalDefinition> BuildAnimalLookup()
    {
        var dict = new Dictionary<string, AnimalDefinition>();
        string[] guids = AssetDatabase.FindAssets("t:AnimalDefinition");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var animal = AssetDatabase.LoadAssetAtPath<AnimalDefinition>(path);
            if (animal == null)
                continue;

            RegisterAnimalLookup(dict, animal.name, animal);
            RegisterAnimalLookup(dict, animal.id, animal);
            RegisterAnimalLookup(dict, animal.displayName, animal);
        }

        return dict;
    }

    private static void RegisterAnimalLookup(Dictionary<string, AnimalDefinition> dict, string rawKey, AnimalDefinition animal)
    {
        string key = Normalize(rawKey);
        if (!string.IsNullOrEmpty(key))
            dict[key] = animal;
    }

    [MenuItem("Tools/Animals/Apply Temporary Field Patch")]
    public static void Apply()
    {
        var patches = BuildPatchLookup();
        var animalLookup = BuildAnimalLookup();
        string[] guids = AssetDatabase.FindAssets("t:AnimalDefinition");

        int matched = 0;
        int changed = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset == null)
                    continue;

                var so = new SerializedObject(asset);

                AnimalPatch patch = FindPatchForAsset(asset, so, patches);
                if (patch == null)
                    continue;

                matched++;

                bool dirty = false;

                dirty |= SetFloat(so, "hungerSatisfiedOnSuccessfulHunt", patch.hungerSatisfiedOnSuccessfulHunt);
dirty |= SetFloat(so, "thirstSatisfiedOnSuccessfulHunt", patch.thirstSatisfiedOnSuccessfulHunt);
dirty |= SetFloat(so, "abandonHuntForWaterNeedThreshold", patch.abandonHuntForWaterNeedThreshold);

                if (dirty)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                    changed++;
                    Debug.Log($"Patched animal fields: {asset.name}", asset);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Animal field patch complete. Matched: {matched}, Changed: {changed}.");
    }

    private static AnimalPatch FindPatchForAsset(UnityEngine.Object asset, SerializedObject so, Dictionary<string, AnimalPatch> patches)
    {
        var keysToTry = new List<string>();

        AddKey(keysToTry, asset.name);
        AddKey(keysToTry, ReadString(so, "id"));
        AddKey(keysToTry, ReadString(so, "displayName"));
        AddKey(keysToTry, ReadString(so, "animalID"));
        AddKey(keysToTry, ReadString(so, "animalName"));

        foreach (string key in keysToTry)
        {
            if (patches.TryGetValue(key, out AnimalPatch patch))
                return patch;
        }

        return null;
    }

    private static void AddKey(List<string> list, string raw)
    {
        string key = Normalize(raw);
        if (!string.IsNullOrEmpty(key) && !list.Contains(key))
            list.Add(key);
    }

    private static string ReadString(SerializedObject so, string propertyName)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null && prop.propertyType == SerializedPropertyType.String)
            return prop.stringValue;
        return null;
    }

    private static bool SetFloat(SerializedObject so, string propertyName, float value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null || prop.propertyType != SerializedPropertyType.Float)
            return false;

        if (!Mathf.Approximately(prop.floatValue, value))
        {
            prop.floatValue = value;
            return true;
        }

        return false;
    }

    private static bool SetInt(SerializedObject so, string propertyName, int value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null || prop.propertyType != SerializedPropertyType.Integer)
            return false;

        if (prop.intValue != value)
        {
            prop.intValue = value;
            return true;
        }

        return false;
    }

    private static bool SetBool(SerializedObject so, string propertyName, bool value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null || prop.propertyType != SerializedPropertyType.Boolean)
            return false;

        if (prop.boolValue != value)
        {
            prop.boolValue = value;
            return true;
        }

        return false;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = new List<char>(value.Length);
        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c))
                chars.Add(char.ToLowerInvariant(c));
        }
        return new string(chars.ToArray());
    }

    private static Dictionary<string, AnimalPatch> BuildPatchLookup()
    {
        var dict = new Dictionary<string, AnimalPatch>();

        string[] None = Array.Empty<string>();
        string[] MajorPredators =
        {
            "Lion",
            "Leopard",
            "SpottedHyena",
            "ShortNosedHyena",
            "AfricanWildDog",
            "NileCrocodile",
            "AfricanRockPython"
        };

        void Add(AnimalPatch patch)
        {
            Register(dict, patch.key, patch);
            if (patch.aliases != null)
            {
                foreach (string alias in patch.aliases)
                    Register(dict, alias, patch);
            }
        }

        Add(new AnimalPatch {
    key = "AbyssinianHornbill",
    aliases = new[] { "Abyssinian Ground Hornbill", "Bucorvus abyssinicus" },
    hungerSatisfiedOnSuccessfulHunt = 0.58f,
    thirstSatisfiedOnSuccessfulHunt = 0.08f,
    abandonHuntForWaterNeedThreshold = 0.82f
});

Add(new AnimalPatch {
    key = "HelmetedGuineafowl",
    aliases = new[] { "Helmeted Guineafowl", "Numida meleagris" },
    hungerSatisfiedOnSuccessfulHunt = 0.22f,
    thirstSatisfiedOnSuccessfulHunt = 0.04f,
    abandonHuntForWaterNeedThreshold = 0.78f
});

Add(new AnimalPatch {
    key = "KoriBustard",
    aliases = new[] { "Kori Bustard", "Ardeotis kori" },
    hungerSatisfiedOnSuccessfulHunt = 0.36f,
    thirstSatisfiedOnSuccessfulHunt = 0.06f,
    abandonHuntForWaterNeedThreshold = 0.80f
});

Add(new AnimalPatch {
    key = "Ostrich",
    aliases = new[] { "Struthio camelus" },
    hungerSatisfiedOnSuccessfulHunt = 0.12f,
    thirstSatisfiedOnSuccessfulHunt = 0.03f,
    abandonHuntForWaterNeedThreshold = 0.76f
});

Add(new AnimalPatch {
    key = "Secretarybird",
    aliases = new[] { "Secretary bird", "Sagittarius serpentarius" },
    hungerSatisfiedOnSuccessfulHunt = 0.68f,
    thirstSatisfiedOnSuccessfulHunt = 0.10f,
    abandonHuntForWaterNeedThreshold = 0.78f
});

Add(new AnimalPatch {
    key = "SouthernGroundHornbill",
    aliases = new[] { "Southern Ground Hornbill", "Bucorvus leadbeateri" },
    hungerSatisfiedOnSuccessfulHunt = 0.62f,
    thirstSatisfiedOnSuccessfulHunt = 0.08f,
    abandonHuntForWaterNeedThreshold = 0.80f
});

Add(new AnimalPatch {
    key = "Sandgrouse",
    hungerSatisfiedOnSuccessfulHunt = 0.00f,
    thirstSatisfiedOnSuccessfulHunt = 0.00f,
    abandonHuntForWaterNeedThreshold = 0.85f
});

Add(new AnimalPatch {
    key = "AfricanBushElephant",
    aliases = new[] { "African Bush Elephant", "Loxodonta africana" },
    hungerSatisfiedOnSuccessfulHunt = 0.00f,
    thirstSatisfiedOnSuccessfulHunt = 0.00f,
    abandonHuntForWaterNeedThreshold = 0.85f
});

Add(new AnimalPatch {
    key = "GiantLongHornedBuffalo",
    aliases = new[] { "Giant Long Horned Buffalo", "Giant Long-Horned Buffalo", "Syncerus antiquus", "Pelorovis antiquus" },
    hungerSatisfiedOnSuccessfulHunt = 0.00f,
    thirstSatisfiedOnSuccessfulHunt = 0.00f,
    abandonHuntForWaterNeedThreshold = 0.85f
});

Add(new AnimalPatch {
    key = "Hippopotamus",
    aliases = new[] { "Hippo" },
    hungerSatisfiedOnSuccessfulHunt = 0.00f,
    thirstSatisfiedOnSuccessfulHunt = 0.00f,
    abandonHuntForWaterNeedThreshold = 0.85f
});

Add(new AnimalPatch {
    key = "GiantCapeZebra",
    aliases = new[] { "Giant Cape Zebra", "Equus capensis" },
    hungerSatisfiedOnSuccessfulHunt = 0.00f,
    thirstSatisfiedOnSuccessfulHunt = 0.00f,
    abandonHuntForWaterNeedThreshold = 0.85f
});

Add(new AnimalPatch {
    key = "Lion",
    aliases = new[] { "Panthera leo" },
    hungerSatisfiedOnSuccessfulHunt = 0.85f,
    thirstSatisfiedOnSuccessfulHunt = 0.18f,
    abandonHuntForWaterNeedThreshold = 0.82f
});

Add(new AnimalPatch {
    key = "Megalotragus",
    aliases = new[] { "Megalotragus priscus" },
    hungerSatisfiedOnSuccessfulHunt = 0.00f,
    thirstSatisfiedOnSuccessfulHunt = 0.00f,
    abandonHuntForWaterNeedThreshold = 0.85f
});

Add(new AnimalPatch {
    key = "Rusingoryx",
    aliases = new[] { "Rusingoryx atopocranion" },
    hungerSatisfiedOnSuccessfulHunt = 0.00f,
    thirstSatisfiedOnSuccessfulHunt = 0.00f,
    abandonHuntForWaterNeedThreshold = 0.85f
});

Add(new AnimalPatch {
    key = "ShortNosedHyena",
    aliases = new[] { "Short-Nosed Hyena", "Pachycrocuta brevirostris" },
    hungerSatisfiedOnSuccessfulHunt = 0.80f,
    thirstSatisfiedOnSuccessfulHunt = 0.14f,
    abandonHuntForWaterNeedThreshold = 0.86f
});

Add(new AnimalPatch {
    key = "SpottedHyena",
    aliases = new[] { "Spotted Hyena", "Crocuta crocuta" },
    hungerSatisfiedOnSuccessfulHunt = 0.78f,
    thirstSatisfiedOnSuccessfulHunt = 0.14f,
    abandonHuntForWaterNeedThreshold = 0.86f
});

Add(new AnimalPatch {
    key = "Aardvark",
    aliases = new[] { "Orycteropus afer" },
    hungerSatisfiedOnSuccessfulHunt = 0.00f,
    thirstSatisfiedOnSuccessfulHunt = 0.00f,
    abandonHuntForWaterNeedThreshold = 0.85f
});

Add(new AnimalPatch {
    key = "AfricanWildDog",
    aliases = new[] { "African Wild Dog", "Lycaon pictus" },
    hungerSatisfiedOnSuccessfulHunt = 0.72f,
    thirstSatisfiedOnSuccessfulHunt = 0.16f,
    abandonHuntForWaterNeedThreshold = 0.80f
});

Add(new AnimalPatch {
    key = "AntidorcasBondi",
    aliases = new[] { "Antidorcas Bondi", "Bond's springbok", "Bonds springbok", "Antidorcas bondi" },
    hungerSatisfiedOnSuccessfulHunt = 0.00f,
    thirstSatisfiedOnSuccessfulHunt = 0.00f,
    abandonHuntForWaterNeedThreshold = 0.85f
});

Add(new AnimalPatch {
    key = "DamaliscusNiro",
    aliases = new[] { "Damaliscus Niro", "Damaliscus niro" },
    hungerSatisfiedOnSuccessfulHunt = 0.00f,
    thirstSatisfiedOnSuccessfulHunt = 0.00f,
    abandonHuntForWaterNeedThreshold = 0.85f
});

Add(new AnimalPatch {
    key = "Leopard",
    aliases = new[] { "Panthera pardus" },
    hungerSatisfiedOnSuccessfulHunt = 0.82f,
    thirstSatisfiedOnSuccessfulHunt = 0.16f,
    abandonHuntForWaterNeedThreshold = 0.80f
});

Add(new AnimalPatch {
    key = "Warthog",
    aliases = new[] { "Phacochoerus africanus" },
    hungerSatisfiedOnSuccessfulHunt = 0.00f,
    thirstSatisfiedOnSuccessfulHunt = 0.00f,
    abandonHuntForWaterNeedThreshold = 0.85f
});

Add(new AnimalPatch {
    key = "TheropithecusOswaldi",
    aliases = new[] { "Theropithecus Oswaldi", "Theropithecus oswaldi" },
    hungerSatisfiedOnSuccessfulHunt = 0.00f,
    thirstSatisfiedOnSuccessfulHunt = 0.00f,
    abandonHuntForWaterNeedThreshold = 0.85f
});

Add(new AnimalPatch {
    key = "OliveBaboon",
    aliases = new[] { "Olive Baboon", "Papio anubis" },
    hungerSatisfiedOnSuccessfulHunt = 0.46f,
    thirstSatisfiedOnSuccessfulHunt = 0.08f,
    abandonHuntForWaterNeedThreshold = 0.80f
});

Add(new AnimalPatch {
    key = "PatasMonkey",
    aliases = new[] { "Patas Monkey", "Erythrocebus patas" },
    hungerSatisfiedOnSuccessfulHunt = 0.38f,
    thirstSatisfiedOnSuccessfulHunt = 0.06f,
    abandonHuntForWaterNeedThreshold = 0.78f
});

Add(new AnimalPatch {
    key = "VervetMonkey",
    aliases = new[] { "Vervet Monkey", "Chlorocebus pygerythrus" },
    hungerSatisfiedOnSuccessfulHunt = 0.32f,
    thirstSatisfiedOnSuccessfulHunt = 0.06f,
    abandonHuntForWaterNeedThreshold = 0.78f
});

Add(new AnimalPatch {
    key = "Bushbaby",
    aliases = new[] { "Bush Baby", "Galago senegalensis", "Senegal bushbaby" },
    hungerSatisfiedOnSuccessfulHunt = 0.28f,
    thirstSatisfiedOnSuccessfulHunt = 0.04f,
    abandonHuntForWaterNeedThreshold = 0.76f
});

Add(new AnimalPatch {
    key = "AfricanRockPython",
    aliases = new[] { "African Rock Python", "Python sebae" },
    hungerSatisfiedOnSuccessfulHunt = 0.90f,
    thirstSatisfiedOnSuccessfulHunt = 0.06f,
    abandonHuntForWaterNeedThreshold = 0.90f
});

Add(new AnimalPatch {
    key = "LeopardTortoise",
    aliases = new[] { "Leopard Tortoise", "Stigmochelys pardalis" },
    hungerSatisfiedOnSuccessfulHunt = 0.00f,
    thirstSatisfiedOnSuccessfulHunt = 0.00f,
    abandonHuntForWaterNeedThreshold = 0.85f
});

Add(new AnimalPatch {
    key = "NileCrocodile",
    aliases = new[] { "Nile Crocodile", "Crocodylus niloticus" },
    hungerSatisfiedOnSuccessfulHunt = 0.88f,
    thirstSatisfiedOnSuccessfulHunt = 0.22f,
    abandonHuntForWaterNeedThreshold = 0.95f
});

Add(new AnimalPatch {
    key = "BlackMamba",
    aliases = new[] { "Black Mamba", "Dendroaspis polylepis" },
    hungerSatisfiedOnSuccessfulHunt = 0.60f,
    thirstSatisfiedOnSuccessfulHunt = 0.04f,
    abandonHuntForWaterNeedThreshold = 0.80f
});

Add(new AnimalPatch {
    key = "EgyptianCobra",
    aliases = new[] { "Egyptian Cobra", "Naja haje" },
    hungerSatisfiedOnSuccessfulHunt = 0.62f,
    thirstSatisfiedOnSuccessfulHunt = 0.05f,
    abandonHuntForWaterNeedThreshold = 0.80f
});

Add(new AnimalPatch {
    key = "NileMonitor",
    aliases = new[] { "Nile Monitor", "Varanus niloticus" },
    hungerSatisfiedOnSuccessfulHunt = 0.58f,
    thirstSatisfiedOnSuccessfulHunt = 0.08f,
    abandonHuntForWaterNeedThreshold = 0.82f
});

Add(new AnimalPatch {
    key = "PuffAdder",
    aliases = new[] { "Puff Adder", "Bitis arietans" },
    hungerSatisfiedOnSuccessfulHunt = 0.70f,
    thirstSatisfiedOnSuccessfulHunt = 0.03f,
    abandonHuntForWaterNeedThreshold = 0.88f
});

Add(new AnimalPatch {
    key = "SavannahMonitor",
    aliases = new[] { "Savannah Monitor", "Savannah monitor", "Varanus exanthematicus" },
    hungerSatisfiedOnSuccessfulHunt = 0.55f,
    thirstSatisfiedOnSuccessfulHunt = 0.06f,
    abandonHuntForWaterNeedThreshold = 0.82f
});

        return dict;
    }

    private static void Register(Dictionary<string, AnimalPatch> dict, string rawKey, AnimalPatch patch)
    {
        string key = Normalize(rawKey);
        if (string.IsNullOrEmpty(key))
            return;

        dict[key] = patch;
    }
}
#endif