using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FimmFimmtan.App.ViewModels;

namespace FimmFimmtan.App.Views;

public partial class MainWindow : Window, IFileDialogs
{
    private static readonly string[] ExcelExtensions = [".xlsx", ".xlsm"];
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm = new MainViewModel(this);

        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDragLeaveHandler(this, (_, _) => DragOverlay.IsVisible = false);
        DragDrop.AddDropHandler(this, OnDrop);

        AnswerList.AddHandler(KeyDownEvent, OnAnswerKeyDown, RoutingStrategies.Tunnel);
        _vm.FocusAnswerRequested += index => Dispatcher.UIThread.Post(() => FocusAnswerRow(index), DispatcherPriority.Loaded);
    }

    private void OnAnswerKeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is not IAnswerRow row) return;
        var index = _vm.AnswerRows.IndexOf(row);
        switch (e.Key)
        {
            case Key.D0 or Key.NumPad0: _vm.AnswerAndAdvance(row, 0); break;
            case Key.D1 or Key.NumPad1: _vm.AnswerAndAdvance(row, 1); break;
            case Key.D2 or Key.NumPad2: _vm.AnswerAndAdvance(row, 2); break;
            case Key.D3 or Key.NumPad3 when row.MaxScore >= 3: _vm.AnswerAndAdvance(row, 3); break;
            case Key.Delete or Key.Back: _vm.AnswerAndAdvance(row, null); break;
            case Key.Down when index + 1 < _vm.AnswerRows.Count: FocusAnswerRow(index + 1); break;
            case Key.Up when index > 0: FocusAnswerRow(index - 1); break;
            default: return;
        }
        e.Handled = true;
    }

    private void FocusAnswerRow(int index)
    {
        if (AnswerList.ContainerFromIndex(index) is not Control container) return;
        var row = (IAnswerRow)container.DataContext!;
        var buttons = container.GetVisualDescendants().OfType<ToggleButton>().ToList();
        if (buttons.Count == 0) return;
        var target = buttons[Math.Min(row.CurrentScore ?? 0, buttons.Count - 1)];
        target.Focus(NavigationMethod.Tab);
        container.BringIntoView(new Avalonia.Rect(0, -60, container.Bounds.Width, container.Bounds.Height + 120));
    }

    public MainViewModel ViewModel => _vm;

    private static string? ExcelPathFrom(DragEventArgs e)
    {
        var file = e.DataTransfer.TryGetFiles()?.FirstOrDefault();
        var path = file?.TryGetLocalPath();
        return path != null && ExcelExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()) ? path : null;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var ok = ExcelPathFrom(e) != null;
        e.DragEffects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        DragOverlay.IsVisible = ok;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        DragOverlay.IsVisible = false;
        if (ExcelPathFrom(e) is { } path) await _vm.LoadFileAsync(path);
    }

    public async Task<string?> PickExcelFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Veldu Excel-skrá með svörum foreldra",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Excel-skjöl") { Patterns = ["*.xlsx", "*.xlsm"] }, FilePickerFileTypes.All],
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickSavePathAsync(string suggestedName, string extension, string description, string title = "Vista skýrslu")
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(description) { Patterns = [$"*.{extension}"] }],
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath();
    }

    public void OpenFile(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }
}
