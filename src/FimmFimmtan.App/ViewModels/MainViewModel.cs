using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FimmFimmtan.Core;

namespace FimmFimmtan.App.ViewModels;

public interface IFileDialogs
{
    Task<string?> PickExcelFileAsync();
    Task<string?> PickSavePathAsync(string suggestedName, string extension, string description);
    void OpenFile(string path);
}

public partial class MainViewModel : ObservableObject
{
    private static readonly CultureInfo Is = CultureInfo.GetCultureInfo("is-IS");
    private readonly IFileDialogs _dialogs;
    private QuestionnaireAnswers? _answers;

    public MainViewModel(IFileDialogs dialogs) => _dialogs = dialogs;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanExport))] private string _name = "";
    [ObservableProperty] private decimal? _age;
    [ObservableProperty] private bool _isMale;
    [ObservableProperty] private bool _isFemale;
    [ObservableProperty] private bool _includePositiveItems = true;
    [ObservableProperty] private bool _showOnlyProblemAnswers;

    [ObservableProperty] private string? _fileName;
    [ObservableProperty] private string? _fileDetails;
    [ObservableProperty] private string? _loadError;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _lastExportPath;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasReport), nameof(CanExport), nameof(ReportHeading), nameof(FlaggedSummary))]
    private Report? _report;

    [ObservableProperty] private string? _missingInputMessage = "Fylltu inn aldur og kyn barnsins og hladdu inn Excel-skrá með svörum foreldra.";

    public ObservableCollection<ResultRow> Results { get; } = [];
    public ObservableCollection<AnswerRow> AnswerRows { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];

    public bool HasReport => Report != null;
    public bool HasFile => _answers != null;
    public bool HasWarnings => Warnings.Count > 0;
    public bool HasAnswers => AnswerRows.Count > 0;
    public bool CanExport => Report != null;
    public string AnswersTabHeader => _answers is null ? "Svör" : $"Svör ({_answers.AnsweredCount}/{Scales.ItemCount})";

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
    partial void OnIncludePositiveItemsChanged(bool value) => Recalculate();
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
        IsLoading = true;
        try
        {
            var answers = await Task.Run(() => ExcelAnswerReader.Read(path));
            _answers = answers;
            FileName = Path.GetFileName(path);
            var impact = answers.Impact.Count > 0 ? $" · {answers.Impact.Count} svör um hömlun í daglegu lífi" : "";
            FileDetails = $"{answers.AnsweredCount} af {Scales.ItemCount} spurningum svarað{impact}";
        }
        catch (IOException ex) when (ex is not FileNotFoundException)
        {
            _answers = null;
            FileName = null;
            FileDetails = null;
            LoadError = "Ekki tókst að opna skrána. Er hún opin í Excel? Lokaðu henni og reyndu aftur.";
        }
        catch (Exception ex)
        {
            _answers = null;
            FileName = null;
            FileDetails = null;
            LoadError = ex is InvalidDataException
                ? ex.Message
                : $"Ekki tókst að lesa skrána sem Excel-skjal (.xlsx). {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
        OnPropertyChanged(nameof(HasFile));
        OnPropertyChanged(nameof(AnswersTabHeader));
        RebuildAnswerRows();
        Recalculate();
    }

    [RelayCommand]
    private void ClearFile()
    {
        _answers = null;
        FileName = null;
        FileDetails = null;
        LoadError = null;
        OnPropertyChanged(nameof(HasFile));
        OnPropertyChanged(nameof(AnswersTabHeader));
        RebuildAnswerRows();
        Recalculate();
    }

    [RelayCommand]
    private void NewChild()
    {
        Name = "";
        Age = null;
        IsMale = false;
        IsFemale = false;
        StatusMessage = null;
        LastExportPath = null;
        ClearFile();
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
        Warnings.Clear();

        var age = Age is { } a ? (int)a : (int?)null;
        string? missing = null;
        if (_answers is null) missing = "Hladdu inn Excel-skrá með svörum foreldra (svörin byrja í reit E2 og halda áfram niður).";
        if (!IsMale && !IsFemale) missing = "Veldu kyn barnsins – viðmiðin eru ólík fyrir drengi og stúlkur.";
        if (age is null) missing = "Sláðu inn aldur barnsins (5–17 ára).";
        else if (Norms.GroupForAge(age.Value) is null) missing = $"Viðmið eru aðeins til fyrir {Norms.MinAge}–{Norms.MaxAge} ára börn.";
        if (_answers is null && age is null && !IsMale && !IsFemale)
            missing = "Fylltu inn aldur og kyn barnsins og hladdu inn Excel-skrá með svörum foreldra.";

        MissingInputMessage = missing;
        if (missing != null || _answers is null || age is null)
        {
            Report = null;
            OnPropertyChanged(nameof(HasWarnings));
            return;
        }

        var report = Scorer.Score(new ChildInfo(Name.Trim(), age.Value, IsMale ? Sex.Male : Sex.Female), _answers,
            new ScoringOptions(IncludePositiveItems));
        foreach (var r in report.Results) Results.Add(new ResultRow(r));
        foreach (var w in _answers.Warnings) Warnings.Add(w);
        if (report.Group == AgeGroup.Age5_7 && _answers.Items.Skip(92).Take(29).Any(i => i.Score.HasValue))
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
        if (_answers is not null)
            foreach (var a in _answers.Items)
                if (!ShowOnlyProblemAnswers || a.Score is null)
                    AnswerRows.Add(new AnswerRow(a, ScaleNameFor(a.Item)));
        OnPropertyChanged(nameof(HasAnswers));
    }

    private static string ScaleNameFor(int item) =>
        Scales.All.Where(s => s.Items.Contains(item)).OrderBy(s => s.IsDomain).Select(s => s.NameIs).FirstOrDefault() ?? "";
}
