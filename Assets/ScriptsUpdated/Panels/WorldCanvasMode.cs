using System;

public static class WorldCanvasMode
{
    private static bool _unitsOnly;

    public static bool UnitsOnly => _unitsOnly;

    public static event Action<bool> OnChanged;

    public static void SetUnitsOnly(bool value)
    {
        if (_unitsOnly == value) return;
        _unitsOnly = value;
        OnChanged?.Invoke(_unitsOnly);
    }

    public static void Toggle() => SetUnitsOnly(!_unitsOnly);
}
