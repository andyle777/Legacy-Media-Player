using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using LegacyMediaPlayer.Application;

namespace LegacyMediaPlayer.App;

public partial class MainWindow
{
    private IBrush? _normalVideoBorder;

    private void InitializeMediaDrop()
    {
        _normalVideoBorder = VideoBorder.BorderBrush;
        QueuePanel.Background = Brushes.Transparent;
        DragDrop.SetAllowDrop(VideoBorder, true);
        DragDrop.SetAllowDrop(QueuePanel, true);
        AddHandler(DragDrop.DragEnterEvent, OnMediaDragOver);
        AddHandler(DragDrop.DragOverEvent, OnMediaDragOver);
        AddHandler(DragDrop.DragLeaveEvent, (_, _) => ShowDropTarget(null));
        AddHandler(DragDrop.DropEvent, OnMediaDrop);
        Closing += (_, _) => ShowDropTarget(null);
    }

    private Control? DropTarget(DragEventArgs e)
    {
        if (PlaylistBusy || (e.DragEffects & DragDropEffects.Copy) == 0) return null;
        foreach (var target in new Control[] { QueuePanel, VideoBorder })
            if (target.IsEffectivelyVisible && new Rect(target.Bounds.Size).Contains(e.GetPosition(target))) return target;
        return null;
    }

    private static string?[] ReadDroppedPaths(DragEventArgs e) => e.DataTransfer.TryGetFiles()?
        .Select(item => item is IStorageFile ? item.TryGetLocalPath() : null).ToArray() ?? [];

    private void OnMediaDragOver(object? sender, DragEventArgs e)
    {
        Control? target = null;
        try
        {
            target = DropTarget(e);
            // Hover only inspects names; existence and access are rechecked on Drop.
            if (target is not null && !ReadDroppedPaths(e).Any(LocalMediaDrop.IsSupportedPath)) target = null;
        }
        catch (Exception ex) when (IsDropError(ex)) { target = null; }
        ShowDropTarget(target);
        e.DragEffects = target is null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void OnMediaDrop(object? sender, DragEventArgs e)
    {
        var target = DropTarget(e);
        ShowDropTarget(null);
        e.DragEffects = DragDropEffects.None;
        e.Handled = true;
        if (target is null) return;
        try
        {
            // Avalonia releases the OLE transfer when this callback returns. Materialize
            // all paths before the first await; never keep/dispose the incoming transfer.
            var selection = LocalMediaDrop.Select(ReadDroppedPaths(e));
            if (selection.Paths.Count == 0)
            {
                _session.ReportError("No supported local media files were dropped.");
                return;
            }
            var append = target == QueuePanel || e.KeyModifiers.HasFlag(KeyModifiers.Control);
            e.DragEffects = DragDropEffects.Copy; // Never ask Explorer to move/delete its source.
            await OpenFilesAsync(selection.Paths, append);
            if (!_closed && selection.SkippedCount > 0)
            {
                var warning = $"Skipped {selection.SkippedCount} unsupported, missing or nonlocal dropped item(s).";
                _session.ReportError(_session.ErrorMessage is { } error ? error + " " + warning : warning);
            }
        }
        catch (Exception ex) when (IsDropError(ex))
        {
            if (!_closed) _session.ReportError($"Could not read dropped files: {ex.Message}");
        }
    }

    private void ShowDropTarget(Control? target)
    {
        VideoBorder.BorderBrush = target == VideoBorder ? Brushes.DodgerBlue : _normalVideoBorder;
        VideoBorder.BorderThickness = new Thickness(_fullscreen && target != VideoBorder ? 0 : 2);
        QueuePanel.Background = target == QueuePanel ? Brushes.LightBlue : Brushes.Transparent;
    }

    private static bool IsDropError(Exception ex) => ex is ArgumentException or IOException or
        UnauthorizedAccessException or InvalidOperationException or NotSupportedException or
        System.Runtime.InteropServices.ExternalException or System.Security.SecurityException;
}
