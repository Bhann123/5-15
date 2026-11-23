using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FimmFimmtan.Core;

namespace FimmFimmtan.App.ViewModels;

public interface IFileDialogs
{
    Task<string?> PickExcelFileAsync();
    Task<string?> PickSavePathAsync(string suggestedName, string extension, string description, string title = "Vista skýrslu");
    void OpenFile(string path);
}

public partial class MainViewModel : ObservableObject
{
    private static readonly CultureInfo Is = CultureInfo.GetCultureInfo("is-IS");
    private readonly IFileDialogs _dialogs;
    private readonly AnswerRow[] _rows;
    private readonly ImpactRow[] _impactRows;
    private List<string> _fileWarnings = [];
    private bool _editedSinceLoad;
    private bool _loadingRows;

    public MainViewModel(IFileDialogs dialogs)
    {
        _dialogs = dialogs;
        _rows = Enumerable.Range(1, Scales.ItemCount).Select(i => new AnswerRow(i, ScaleNameFor(i), OnAnswerChanged)).ToArray();
        _impactRows = ImpactQuestions.All.Select(q => new ImpactRow(q, () => OnAnswerChanged(null))).ToArray();
        RebuildAnswerRows();
    }

    /// <summary>Raised when the view should move keyboard focus to an answer row (index into <see cref="AnswerRows"/>).</summary>
    public event Action<int>? FocusAnswerRequested;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanExport))] private string _name = "";
    [ObservableProperty] private decimal? _age;
    [ObservableProperty] private bool _isMale;
    [ObservableProperty] private bool _isFemale;
    [ObservableProperty] private bool _showOnlyProblemAnswers;

    [ObservableProperty] private string? _fileName;
    [ObservableProperty] private string? _fileDetails;
    [ObservableProperty] private string? _loadError;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _lastExportPath;
    [ObservableProperty] private int _selectedTab;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ClearButtonText))] private bool _confirmClear;

    public string ClearButtonText => ConfirmClear ? "Staðfesta hreinsun" : "Hreinsa svör";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasReport), nameof(CanExport), nameof(ReportHeading), nameof(FlaggedSummary))]
    private Report? _report;

    [ObservableProperty] private string? _missingInputMessage = NothingYetMessage;

    private const string NothingYetMessage = "Fylltu inn aldur og kyn barnsins og hladdu inn Excel-skrá með svörum foreldra eða svaraðu spurningunum í flipanum „Svör“.";
    public const int AnswersTabIndex = 2;

    public ObservableCollection<ResultRow> Results { get; } = [];
    public ObservableCollection<ResultRow> DomainChart { get; } = [];
    public ObservableCollection<ResultRow> SubdomainChart { get; } = [];
    public ObservableCollection<IAnswerRow> AnswerRows { get; } = [];
    public ObservableCollection<ImpactResultRow> ImpactResults { get; } = [];
    public bool HasImpactResults => ImpactResults.Count > 0;
    public ObservableCollection<string> Warnings { get; } = [];

    public bool HasReport => Report != null;
    public bool HasFile => FileName != null;
    public int AnsweredCount => _rows.Count(r => r.IsAnswered);
    public bool HasAnyAnswer => _rows.Any(r => r.IsAnswered || r.IsUnrecognized) || _impactRows.Any(r => r.IsAnswered);
    public bool HasWarnings => Warnings.Count > 0;
    public bool CanExport => Report != null;
    public string AnswersTabHeader => $"Svör ({AnsweredCount}/{Scales.ItemCount})";
    public string AnswerProgress => $"{AnsweredCount} af {Scales.ItemCount} spurningum svarað";
    public double AnswerProgressValue => AnsweredCount;

    public string? ReportHeading => Report is null ? null :
        $"{(string.IsNullOrWhiteSpace(Report.Child.Name) ? "Barn" : Report.Child.Name)} · {Report.Child.Age} ára · borið saman við {Norms.Label(Report.Group)} {Norms.Label(Report.Child.Sex)}";

    public string? FlaggedSummary
    {
        get
        {
            if (Report is null) return null;
            var high = Report.Results.Count(r => r.Level == Level.Above98);
            var mid = Report.Results.Count(r => r.Level == Level.Above90);
            if (high + mid == 0) return "Engin svið ná 90. hundraðsmarki.";
            return $"{high} svið við/yfir 98. hundraðsmarki · {mid} svið við/yfir 90. hundraðsmarki";
        }
    }

    partial void OnAgeChanged(decimal? value) => Recalculate();
    partial void OnIsMaleChanged(bool value) { if (value) IsFemale = false; Recalculate(); }
    partial void OnIsFemaleChanged(bool value) { if (value) IsMale = false; Recalculate(); }
    partial void OnNameChanged(string value) => Recalculate();
    partial void OnShowOnlyProblemAnswersChanged(bool value) => RebuildAnswerRows();

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var path = await _dialogs.PickExcelFileAsync();
        if (path != null) await LoadFileAsync(path);
    }

    public async Task LoadFileAsync(string path)
    {
        LoadError = null;
        StatusMessage = null;
        ConfirmClear = false;
        IsLoading = true;
        try
        {
            var answers = await Task.Run(() => ExcelAnswerReader.Read(path));
            SetAnswers(answers);
            FileName = Path.GetFileName(path);
            if (answers.ChildName is { } childName && string.IsNullOrWhiteSpace(Name)) Name = childName;
            if (answers.ChildAge is { } childAge && Age is null) Age = childAge;
            if (answers.ChildSex is { } childSex && !IsMale && !IsFemale)
            {
                if (childSex == Sex.Male) IsMale = true; else IsFemale = true;
            }
        }
        catch (IOException ex) when (ex is not FileNotFoundException)
        {
            LoadError = "Ekki tókst að opna skrána. Er hún opin í Excel? Lokaðu henni og reyndu aftur.";
        }
        catch (Exception ex)
        {
            LoadError = ex is InvalidDataException
                ? ex.Message
                : $"Ekki tókst að lesa skrána sem Excel-skjal (.xlsx). {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
        AnswersChanged();
    }

    private void SetAnswers(QuestionnaireAnswers answers)
    {
        _loadingRows = true;
        foreach (var row in _rows) row.Load(answers.Items[row.Item - 1]);
        foreach (var row in _impactRows) row.Load(answers.Impact.FirstOrDefault(i => i.Question == row.Question.Number));
        _loadingRows = false;
        _fileWarnings = answers.Warnings.ToList();
        _editedSinceLoad = false;
        RebuildAnswerRows();
    }

    private void OnAnswerChanged(AnswerRow? row)
    {
        if (_loadingRows) return;
        _editedSinceLoad = true;
        ConfirmClear = false;
        _fileWarnings.Clear();
        AnswersChanged();
    }

    private void AnswersChanged()
    {
        OnPropertyChanged(nameof(HasFile));
        OnPropertyChanged(nameof(HasAnyAnswer));
        OnPropertyChanged(nameof(AnsweredCount));
        OnPropertyChanged(nameof(AnswersTabHeader));
        OnPropertyChanged(nameof(AnswerProgress));
        OnPropertyChanged(nameof(AnswerProgressValue));
        if (FileName != null)
        {
            var impactCount = _impactRows.Count(r => r.IsAnswered);
            var impact = impactCount > 0 ? $" · {impactCount} svör um hömlun í daglegu lífi" : "";
            FileDetails = _editedSinceLoad ? $"{AnswerProgress} · breytt í forritinu" : AnswerProgress + impact;
        }
        Recalculate();
    }

    private QuestionnaireAnswers BuildAnswers()
    {
        var source = FileName is null ? "Svarað í forritinu"
            : _editedSinceLoad ? $"{FileName} (breytt í forritinu)" : FileName;
        var answers = new QuestionnaireAnswers { Items = _rows.Select(r => r.Answer).ToList(),
            Impact = _impactRows.Select(r => r.Answer).OfType<ImpactAnswer>().ToList(), SourceDescription = source };
        answers.Warnings.AddRange(_fileWarnings);
        return answers;
    }

    [RelayCommand]
    private void ClearAnswers()
    {
        if (_editedSinceLoad && !ConfirmClear)
        {
            ConfirmClear = true;
            return;
        }
        ResetAnswers();
    }

    private void ResetAnswers()
    {
        ConfirmClear = false;
        FileName = null;
        FileDetails = null;
        LoadError = null;
        SetAnswers(AnswerParser.Empty());
        AnswersChanged();
    }

    [RelayCommand]
    private void AnswerInApp()
    {
        SelectedTab = AnswersTabIndex;
        var first = AnswerRows.ToList().FindIndex(r => !r.IsAnswered);
        FocusAnswerRequested?.Invoke(first < 0 ? 0 : first);
    }

    [RelayCommand]
    private async Task SaveAnswersAsync()
    {
        if (!HasAnyAnswer) return;
        var safeName = string.Concat((string.IsNullOrWhiteSpace(Name) ? "barn" : Name.Trim()).Split(Path.GetInvalidFileNameChars()));
        var suggested = $"5-15 svör {safeName} {DateTime.Today.ToString("yyyy-MM-dd", Is)}.xlsx";
        var path = await _dialogs.PickSavePathAsync(suggested, "xlsx", "Excel-skjal", "Vista svör");
        if (path is null) return;
        try
        {
            var answers = BuildAnswers();
            var child = CurrentChild();
            await Task.Run(() => ExcelAnswerWriter.Write(answers, child, path));
            LastExportPath = path;
            StatusMessage = $"Svör vistuð: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            LastExportPath = null;
            StatusMessage = $"Ekki tókst að vista svörin: {ex.Message}";
        }
    }

    /// <summary>Answers the given row and moves focus to the next one, for fast entry from a paper form.</summary>
    public void AnswerAndAdvance(IAnswerRow row, int? score)
    {
        if (score > row.MaxScore) return;
        row.SetScore(score);
        var index = AnswerRows.IndexOf(row);
        if (score is not null && index >= 0 && index + 1 < AnswerRows.Count) FocusAnswerRequested?.Invoke(index + 1);
    }

    private ChildInfo? CurrentChild() =>
        Age is { } a && (IsMale || IsFemale) ? new ChildInfo(Name.Trim(), (int)a, IsMale ? Sex.Male : Sex.Female) : null;

    [RelayCommand]
    private void NewChild()
    {
        Name = "";
        Age = null;
        IsMale = false;
        IsFemale = false;
        StatusMessage = null;
        LastExportPath = null;
        ResetAnswers();
        SelectedTab = 0;
    }

    [RelayCommand]
    private Task ExportExcelAsync() => ExportAsync("xlsx", "Excel-skjal", ExcelReportWriter.Write);

    [RelayCommand]
    private Task ExportPdfAsync() => ExportAsync("pdf", "PDF-skjal", PdfReportWriter.Write);

    [RelayCommand]
    private void OpenLastExport()
    {
        if (LastExportPath != null) _dialogs.OpenFile(LastExportPath);
    }

    private async Task ExportAsync(string extension, string description, Action<Report, string> write)
    {
        if (Report is null) return;
        var safeName = string.Concat((string.IsNullOrWhiteSpace(Name) ? "barn" : Name.Trim()).Split(Path.GetInvalidFileNameChars()));
        var suggested = $"5-15 {safeName} {DateTime.Today.ToString("yyyy-MM-dd", Is)}.{extension}";
        var path = await _dialogs.PickSavePathAsync(suggested, extension, description);
        if (path is null) return;
        try
        {
            var report = Report;
            await Task.Run(() => write(report, path));
            LastExportPath = path;
            StatusMessage = $"Vistað: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            LastExportPath = null;
            StatusMessage = $"Ekki tókst að vista skrána: {ex.Message}";
        }
    }

    private void Recalculate()
    {
        Results.Clear();
        DomainChart.Clear();
        SubdomainChart.Clear();
        Warnings.Clear();
        ImpactResults.Clear();
        foreach (var r in _impactRows)
            if (r.CurrentScore is { } level) ImpactResults.Add(new ImpactResultRow(r.Question, level));
        OnPropertyChanged(nameof(HasImpactResults));

        var age = Age is { } a ? (int)a : (int?)null;
        string? missing = null;
        if (!HasAnyAnswer) missing = "Hladdu inn Excel-skrá með svörum foreldra eða svaraðu spurningunum í flipanum „Svör“.";
        if (!IsMale && !IsFemale) missing = "Veldu kyn barnsins – viðmiðin eru ólík fyrir drengi og stúlkur.";
        if (age is null) missing = "Sláðu inn aldur barnsins (5–17 ára).";
        else if (Norms.GroupForAge(age.Value) is null) missing = $"Viðmið eru aðeins til fyrir {Norms.MinAge}–{Norms.MaxAge} ára börn.";
        if (!HasAnyAnswer && age is null && !IsMale && !IsFemale)
            missing = NothingYetMessage;

        MissingInputMessage = missing;
        if (missing != null || age is null)
        {
            Report = null;
            OnPropertyChanged(nameof(HasWarnings));
            return;
        }

        var answers = BuildAnswers();
        var report = Scorer.Score(new ChildInfo(Name.Trim(), age.Value, IsMale ? Sex.Male : Sex.Female), answers);
        foreach (var r in report.Results) Results.Add(new ResultRow(r));
        foreach (var r in Profile.Domains(report.Results)) DomainChart.Add(new ResultRow(r));
        foreach (var r in Profile.Subdomains(report.Results)) SubdomainChart.Add(new ResultRow(r));
        foreach (var w in answers.Warnings) Warnings.Add(w);
        if (report.Group == AgeGroup.Age5_7 && answers.Items.Skip(92).Take(29).Any(i => i.Score.HasValue))
            Warnings.Add("Spurningum um nám (93–121) var svarað, en engin viðmið eru til fyrir 5–7 ára og námssviðið er því ekki metið.");
        var few = report.Results.Where(r => r.FewAnswers).Select(r => r.Scale.NameIs).ToList();
        if (few.Count > 0)
            Warnings.Add($"Minna en {Scorer.MinPercentAnswered:0}% spurninga svarað á: {string.Join(", ", few)}. Túlkaðu með varúð.");
        Report = report;
        OnPropertyChanged(nameof(HasWarnings));
    }

    private void RebuildAnswerRows()
    {
        AnswerRows.Clear();
        foreach (var row in _rows)
        {
            if (!ShowOnlyProblemAnswers || !row.IsAnswered) AnswerRows.Add(row);
            if (ImpactQuestions.AfterItem(row.Item) is { } q && _impactRows[q.Number - 1] is var impact && (!ShowOnlyProblemAnswers || !impact.IsAnswered))
                AnswerRows.Add(impact);
        }
    }

    private static string ScaleNameFor(int item) =>
        Scales.All.Where(s => s.Items.Contains(item)).OrderBy(s => s.IsDomain).Select(s => s.NameIs).FirstOrDefault() ?? "Ekki skorað (styrkleiki)";
}
