using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
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

    public async Task<string?> PickSavePathAsync(string suggestedName, string extension, string description)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Vista skýrslu",
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
