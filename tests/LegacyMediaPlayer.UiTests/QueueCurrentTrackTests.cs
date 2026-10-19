using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LegacyMediaPlayer.App;
using LegacyMediaPlayer.Application;
using Xunit;

namespace LegacyMediaPlayer.UiTests;

public partial class PlayerWindowTests
{
    [AvaloniaFact]
    public void Current_track_marker_follows_identity_without_changing_edit_selection()
    {
        using var files = new DropTestFiles("first.wav", "second.flac");
        using var engine = new MockPlaybackEngine();
        var window = new MainWindow(engine, true);
        try
        {
            window.Show(); window.OpenFiles([files[0], files[1], files[0]]);
            var queue = window.FindControl<ListBox>("QueueList")!;
            void AssertCurrent(int index)
            {
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                var markers = queue.GetVisualDescendants().OfType<TextBlock>()
                    .Where(t => t.Name == "CurrentTrackMarker" && t.IsVisible).ToArray();
                if (index < 0) Assert.Empty(markers);
                else
                {
                    var marker = Assert.Single(markers);
                    Assert.Same(queue.Items[index], marker.DataContext);
                    Assert.Equal("Current track", Avalonia.Automation.AutomationProperties.GetName(marker));
                }
            }
            AssertCurrent(0);
            queue.SelectedIndex = 2; var selected = queue.SelectedItem;
            Click(window, "NextButton"); AssertCurrent(1);
            Assert.Same(selected, queue.SelectedItem);
            Click(window, "PauseButton"); AssertCurrent(1);
            Click(window, "StopButton"); AssertCurrent(1);
            Click(window, "PlaySelectedButton"); AssertCurrent(2);
            Click(window, "MoveUpButton"); AssertCurrent(1);
            Assert.Same(selected, queue.SelectedItem);
            Click(window, "RemoveButton"); AssertCurrent(-1);
        }
        finally { window.Close(); }
    }
}
