namespace FimmFimmtan.Core;

public enum Sex { Male, Female }

public enum AgeGroup { Age5_7, Age8_11, Age12_15, Age16_17 }

/// <summary>Norm values for one scale in one age/sex group, as printed in the 5-15 norm tables.</summary>
public sealed record Norm(double Median, double Mean, double SD, double P98, double P90)
{
    /// <summary>The 5-7 tables have all-zero rows for Learning, which means "no norms for this age".</summary>
    public bool IsAvailable => SD > 0;
}

public static partial class Norms
{
    public const int MinAge = 5;
    public const int MaxAge = 17;

    public static AgeGroup? GroupForAge(int age) => age switch
    {
        >= 5 and <= 7 => AgeGroup.Age5_7,
        >= 8 and <= 11 => AgeGroup.Age8_11,
        >= 12 and <= 15 => AgeGroup.Age12_15,
        16 or 17 => AgeGroup.Age16_17,
        _ => null,
    };

    public static string Label(AgeGroup group) => group switch
    {
        AgeGroup.Age5_7 => "5–7 ára",
        AgeGroup.Age8_11 => "8–11 ára",
        AgeGroup.Age12_15 => "12–15 ára",
        _ => "16–17 ára",
    };

    public static string Label(Sex sex) => sex == Sex.Male ? "drengir" : "stúlkur";

    public static Norm? Get(AgeGroup group, Sex sex, string scaleKey) =>
        Data.TryGetValue((group, sex), out var table) && table.TryGetValue(scaleKey, out var norm) ? norm : null;
}
