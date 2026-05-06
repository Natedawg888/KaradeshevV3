using System.Globalization;

public static class ShortNumberFormatter
{
    public static string Format(int value, int maxDecimals = 1) =>
        Format((double)value, maxDecimals);

    public static string Format(long value, int maxDecimals = 1) =>
        Format((double)value, maxDecimals);

    public static string Format(float value, int maxDecimals = 1) =>
        Format((double)value, maxDecimals);

    public static string Format(double value, int maxDecimals = 1)
    {
        double abs = System.Math.Abs(value);
        string suffix = "";
        double div = 1d;

        if (abs >= 1_000_000_000_000d)
        {
            suffix = "t";
            div = 1_000_000_000_000d;
        }
        else if (abs >= 1_000_000_000d)
        {
            suffix = "b";
            div = 1_000_000_000d;
        }
        else if (abs >= 1_000_000d)
        {
            suffix = "m";
            div = 1_000_000d;
        }
        else if (abs >= 1_000d)
        {
            suffix = "k";
            div = 1_000d;
        }

        double num = value / div;

        bool isWholeNumber = System.Math.Abs(num % 1d) < 0.0000001d;

        string fmt;
        if (isWholeNumber || maxDecimals <= 0)
            fmt = "0";
        else
            fmt = "0." + new string('#', maxDecimals);

        return num.ToString(fmt, CultureInfo.InvariantCulture) + suffix;
    }
}