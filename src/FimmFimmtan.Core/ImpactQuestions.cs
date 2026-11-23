namespace FimmFimmtan.Core;

/// <summary>
/// The ten unnumbered "Hamla erfiðleikar ... barninu í daglegu lífi?" questions that close each section of the form.
/// They are answered on a four-point scale and are reported as they are; there are no norms for them.
/// </summary>
public sealed record ImpactQuestion(int Number, int AfterItem, string DomainKey, string TextIs, string TextEn)
{
    public string Key => $"H{Number}";
}

public static class ImpactQuestions
{
    public static readonly string[] Options = ["Alls ekki", "Lítið", "Mikið", "Mjög mikið"];

    public static readonly IReadOnlyList<ImpactQuestion> All =
    [
        new(1, 17, "motor", "Hamla erfiðleikar með gróf- eða fínhreyfingar barninu í daglegu lífi?",
            "Do problems with motor function interfere with your child's daily function?"),
        new(2, 35, "exec", "Hamla erfiðleikar með athygli, einbeitingu, ofvirkni eða hvatvísi barninu í daglegu lífi?",
            "Do problems with attention, hyperactivity or impulsivity interfere with your child's daily function?"),
        new(3, 39, "exec", "Hamlar vanvirkni/drifleysi barninu í daglegu lífi?",
            "Does passivity or lack of initiative interfere with your child's daily function?"),
        new(4, 42, "exec", "Hafa skipulagserfiðleikar hamlandi áhrif á barnið í daglegu lífi?",
            "Do problems with planning interfere with your child's daily function?"),
        new(5, 60, "perc", "Hafa erfiðleikar með skynjun á rými eða áttum, tíma, eigin líkama eða formum áhrif á barnið í daglegu lífi?",
            "Do problems with perception of own body, time or room interfere with your child's daily function?"),
        new(6, 71, "memory", "Hafa minniserfiðleikar hamlandi áhrif á barnið í daglegu lífi?",
            "Do problems with memory interfere with your child's daily function?"),
        new(7, 92, "lang", "Hafa erfiðleikar með málskilning, málnotkun eða tjáskipti hamlandi áhrif á barnið í daglegu lífi?",
            "Do problems with language/communication interfere with your child's daily function?"),
        new(8, 121, "learn", "Hafa námserfiðleikar hamlandi áhrif á barnið í daglegu lífi?",
            "Do academic problems or learning difficulties interfere with your child's daily function?"),
        new(9, 148, "social", "Hafa erfiðleikar með félagsfærni hamlandi áhrif á barnið í daglegu lífi?",
            "Do problems with social skills interfere with your child's daily function?"),
        new(10, 181, "emo", "Hafa tilfinningaerfiðleikar, áráttur eða þráhyggjur hamlandi áhrif á barnið í daglegu lífi?",
            "Do emotional or behavioral problems interfere with your child's daily function?"),
    ];

    public static ImpactQuestion? AfterItem(int item) => All.FirstOrDefault(q => q.AfterItem == item);

    public static ImpactQuestion? ByKey(string key) =>
        All.FirstOrDefault(q => string.Equals(q.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Label(int level) => Options[level];
}
