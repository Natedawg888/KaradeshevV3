using System;
using System.Reflection;

public static class SaveReflectionUtil
{
    private const BindingFlags Flags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static T Get<T>(object target, string memberName, T fallback = default)
    {
        if (target == null || string.IsNullOrWhiteSpace(memberName))
            return fallback;

        Type type = target.GetType();

        PropertyInfo prop = type.GetProperty(memberName, Flags);
        if (prop != null)
        {
            try
            {
                object value = prop.GetValue(target);
                if (value == null) return fallback;
                return ConvertValue<T>(value, fallback);
            }
            catch { }
        }

        FieldInfo field =
            type.GetField(memberName, Flags) ??
            type.GetField($"<{memberName}>k__BackingField", Flags);

        if (field != null)
        {
            try
            {
                object value = field.GetValue(target);
                if (value == null) return fallback;
                return ConvertValue<T>(value, fallback);
            }
            catch { }
        }

        return fallback;
    }

    public static bool Set(object target, string memberName, object value)
    {
        if (target == null || string.IsNullOrWhiteSpace(memberName))
            return false;

        Type type = target.GetType();

        PropertyInfo prop = type.GetProperty(memberName, Flags);
        if (prop != null && prop.CanWrite)
        {
            try
            {
                object converted = ConvertTo(value, prop.PropertyType);
                prop.SetValue(target, converted);
                return true;
            }
            catch { }
        }

        FieldInfo field =
            type.GetField(memberName, Flags) ??
            type.GetField($"<{memberName}>k__BackingField", Flags);

        if (field != null)
        {
            try
            {
                object converted = ConvertTo(value, field.FieldType);
                field.SetValue(target, converted);
                return true;
            }
            catch { }
        }

        return false;
    }

    private static T ConvertValue<T>(object value, T fallback)
    {
        try
        {
            object converted = ConvertTo(value, typeof(T));
            if (converted == null) return fallback;
            return (T)converted;
        }
        catch
        {
            return fallback;
        }
    }

    private static object ConvertTo(object value, Type targetType)
    {
        if (value == null)
            return null;

        Type dest = Nullable.GetUnderlyingType(targetType) ?? targetType;
        Type src = value.GetType();

        if (dest.IsAssignableFrom(src))
            return value;

        if (dest == typeof(Guid))
        {
            if (value is string s && Guid.TryParse(s, out Guid g))
                return g;
        }

        if (dest == typeof(string))
        {
            return value.ToString();
        }

        if (dest.IsEnum)
        {
            if (value is string es)
                return Enum.Parse(dest, es);

            return Enum.ToObject(dest, value);
        }

        return Convert.ChangeType(value, dest);
    }
}