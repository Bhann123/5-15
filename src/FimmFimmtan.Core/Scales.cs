namespace FimmFimmtan.Core;

public sealed record Scale(string Key, string NameIs, string NameEn, IReadOnlyList<int> Items, bool IsDomain)
{
    public string? ParentKey => IsDomain ? null : Key.Split('.')[0];
}

/// <summary>
/// Item-to-scale mapping of the 5-15 (FTF) parent questionnaire. Each scale score is the mean of its
/// answered items (0, 1 or 2); a domain score is the mean of all its items, not the mean of its subdomains.
/// Verified against the worked example in the norm tables.
/// </summary>
public static class Scales
{
    public const int ItemCount = 181;
    public static readonly int[] PositiveItems = [110, 111];

    private static int[] R(int from, int to) => Enumerable.Range(from, to - from + 1).ToArray();

    private static Scale D(string key, string nameIs, string nameEn, int from, int to) => new(key, nameIs, nameEn, R(from, to), true);
    private static Scale S(string key, string nameIs, string nameEn, int from, int to) => new(key, nameIs, nameEn, R(from, to), false);

    public static readonly IReadOnlyList<Scale> All =
    [
        D("motor", "Hreyfifærni", "Motor skills", 1, 17),
        S("motor.gross", "Grófhreyfingar", "Gross motor skills", 1, 7),
        S("motor.fine", "Fínhreyfingar", "Fine motor skills", 8, 17),

        D("exec", "Stýrifærni", "Executive functions", 18, 42),
        S("exec.attention", "Athygli og einbeiting", "Attention and concentration", 18, 26),
        S("exec.hyper", "Ofvirkni og hvatvísi", "Overactivity and impulsivity", 27, 35),
        S("exec.passivity", "Vanvirkni og drifleysi", "Passivity and inactivity", 36, 39),
        S("exec.planning", "Skipulag og áætlanagerð", "Planning and organizing", 40, 42),

        D("perc", "Skynjun", "Perception", 43, 60),
        S("perc.space", "Skynjun á rými og áttum", "Perception of space and directions", 43, 47),
        S("perc.time", "Tímaskyn", "Concepts of time", 48, 51),
        S("perc.body", "Skynjun eigin líkama", "Perception of own body", 52, 56),
        S("perc.visual", "Skynjun forma og mynda", "Perception of visual forms and figures", 57, 60),

        D("memory", "Minni", "Memory", 61, 71),

        D("lang", "Mál og tal", "Language", 72, 92),
        S("lang.comprehension", "Skilningur á mæltu máli", "Comprehension of spoken language", 72, 76),
        S("lang.expressive", "Máltjáning", "Expressive language", 77, 89),
        S("lang.communication", "Tjáskipti", "Verbal communication", 90, 92),

        D("learn", "Nám", "Learning", 93, 121),
        S("learn.reading", "Lestur og skrift", "Reading and writing", 93, 100),
        S("learn.arithmetic", "Reikningur", "Arithmetic", 101, 105),
        S("learn.general", "Almenn námsgeta", "General learning", 106, 111),
        S("learn.coping", "Að takast á við nám", "Coping with learning", 112, 121),

        D("social", "Félagsfærni", "Social skills", 122, 148),

        D("emo", "Tilfinninga- og hegðunarerfiðleikar", "Emotional/behavioral difficulties", 149, 181),
        S("emo.internal", "Innhverf einkenni", "Internalisation", 149, 160),
        S("emo.acting", "Úthverf hegðun", "Acting out", 161, 173),
        S("emo.obsessive", "Áráttur, þráhyggja og kippir", "Obsessive actions or thoughts", 174, 181),
    ];

    /// <summary>Domains with a single subdomain (Memory, Social skills) are shown once.</summary>
    public static IEnumerable<Scale> SubdomainsOf(string domainKey) => All.Where(s => s.ParentKey == domainKey);
}
