using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsMp3Player;

internal sealed partial class PlayerWindow
{
    private Action<IEnumerable<string>>? stageImportPreview;
    private Func<Task>? commitImportPreview;

    private void AddImportFaceplate(Action<Canvas> navigate, Action back)
    {
        var pending = new ObservableCollection<ImportChoice>();
        var comparer = StringComparer.OrdinalIgnoreCase;
        var artwork = new BitmapImage(new Uri("pack://application:,,,/Assets/import-faceplate.png"));
        var page = new Canvas { Width = 1338, Height = 1176 };
        EnableFaceplateDrag(page);
        page.Clip = Geometry.Parse("M 47,0 H 1228 C 1291,0 1337,46 1337,102 V 1130 Q 1337,1174 1294,1174 H 46 Q 2,1174 2,1130 V 47 Q 2,0 47,0 Z");
        Put(page, new Image { Source = artwork, IsHitTestVisible = false }, 0, 0, 1338, 1176);
        var backButton = Button("", back);
        WireArtworkButton(backButton, "Back to player", null, page, 86, 175, 95, 73);
        var lettering = ColorOf("#E8D9CA");
        var panel = ColorOf("#141312");
        var message = new TextBlock { Foreground = lettering, FontSize = 26, TextWrapping = TextWrapping.Wrap };
        var dropZone = new Border { Background = Brushes.Transparent, AllowDrop = true };
        Put(page, dropZone, 79, 294, 1194, 301);
        dropZone.DragOver += (_, e) =>
        {
            e.Effects = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        dropZone.Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files) Stage(files);
            e.Handled = true;
        };
        var browse = Button("", () =>
        {
            var picker = FileDialog(multiple: true);
            if (picker.ShowDialog(this) == true) Stage(picker.FileNames);
        });
        WireArtworkButton(browse, "Browse for MP3s", null, page, 496, 507, 344, 73);
        Put(page, new Border { Background = panel }, 1122, 701, 145, 57);
        var count = new TextBlock
        {
            Foreground = lettering, Background = panel, FontSize = 35,
            TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center
        };
        Put(page, count, 1119, 704, 134, 46);

        var filesList = StyledList(lettering, 32, "#283A36", 70);
        filesList.ItemsSource = pending;
        filesList.ItemTemplate = (DataTemplate)XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <DockPanel>
                <Button DockPanel.Dock="Right" Tag="{Binding}" Content="×" Width="58" MinHeight="43"
                        Background="#342E2B" Foreground="#ECD9C5" FontSize="32" ToolTip="Remove file" AutomationProperties.Name="Remove file"/>
                <TextBlock Text="{Binding Name}" TextTrimming="CharacterEllipsis" VerticalAlignment="Center"/>
              </DockPanel>
            </DataTemplate>
            """);
        filesList.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, e) =>
        {
            if (!busy && e.OriginalSource is Button { Tag: ImportChoice item }) pending.Remove(item);
        }));
        Put(page, filesList, 88, 769, 1175, 149);
        var importLabel = new Border
        {
            Background = new LinearGradientBrush(Color.FromRgb(240, 221, 194), Color.FromRgb(218, 199, 172), 90),
            CornerRadius = new CornerRadius(13), IsHitTestVisible = false
        };
        var actionText = new TextBlock
        {
            Foreground = ColorOf("#282421"), FontWeight = FontWeights.SemiBold, FontSize = 37,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        importLabel.Child = actionText;
        Put(page, importLabel, 783, 942, 479, 80);
        var submit = new Button();
        ConfigureButton(submit, "", async () => await Commit());
        WireArtworkButton(submit, "Import selected songs", null, page, 777, 936, 492, 91);

        Put(page, message, 98, 939, 655, 97);
        pending.CollectionChanged += (_, _) => Refresh();
        Refresh();
        stageImportPreview = Stage;
        commitImportPreview = Commit;
        showImportSkin = () => navigate(page);

        void Stage(IEnumerable<string> candidates)
        {
            if (busy) return;
            var rejected = 0;
            foreach (var file in candidates)
            {
                if (!Path.GetExtension(file).Equals(".mp3", StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
                {
                    rejected++;
                    continue;
                }
                var path = Path.GetFullPath(file);
                if (pending.All(item => !comparer.Equals(item.Path, path))) pending.Add(new ImportChoice(path));
            }
            message.Text = rejected == 0 ? "" : $"Skipped {rejected} unavailable or non-MP3 file(s).";
        }

        void Refresh()
        {
            count.Text = pending.Count == 1 ? "1 file" : $"{pending.Count} files";
            actionText.Text = busy ? "Importing…" : pending.Count == 0 ? "Import songs" :
                pending.Count == 1 ? "Import 1 song" : $"Import {pending.Count} songs";
            submit.IsEnabled = !busy && pending.Count > 0;
            browse.IsEnabled = !busy;
        }

        async Task Commit()
        {
            if (busy || pending.Count == 0) return;
            busy = true;
            backButton.IsEnabled = false;
            Refresh();
            var batch = pending.ToArray();
            try
            {
                var (added, errors) = await Task.Run(() =>
                {
                    var paths = new HashSet<string>(comparer);
                    var failures = new List<string>();
                    foreach (var item in batch)
                    {
                        try { catalog.Import(item.Path); paths.Add(item.Path); }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
                        { failures.Add($"{item.Name}: {ex.Message}"); }
                    }
                    var imported = catalog.Tracks().Where(track => paths.Contains(track.Path))
                        .ToDictionary(track => track.Path, comparer);
                    return (batch.Where(item => imported.ContainsKey(item.Path)).Select(item => imported[item.Path]).ToArray(), failures);
                });
                foreach (var track in added)
                {
                    if (queue.All(item => item.Id != track.Id)) queue.Add(track);
                    var item = pending.FirstOrDefault(candidate => comparer.Equals(candidate.Path, track.Path));
                    if (item is not null) pending.Remove(item);
                }
                RefreshTracks();
                if (errors.Count > 0)
                {
                    message.Text = $"Imported {added.Length}. {errors[0]}";
                    return;
                }
                if (playback.State is "Stopped" or "Error" && current < 0 && queue.Count > 0)
                {
                    current = 0;
                    queued.SelectedIndex = 0;
                    nowPlaying.Text = queue[0].Title;
                    trackArtist.Text = queue[0].Artist;
                }
                message.Text = "";
                back();
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) { message.Text = ex.Message; }
            finally
            {
                busy = false;
                backButton.IsEnabled = true;
                Refresh();
            }
        }
    }

    internal bool SmokeCheckStagedImport(string file)
    {
        if (showImportSkin is null || stageImportPreview is null || commitImportPreview is null) return false;
        showImportSkin();
        stageImportPreview([file, file]);
        var frame = new System.Windows.Threading.DispatcherFrame();
        Exception? failure = null;
        Dispatcher.BeginInvoke(async () =>
        {
            try { await commitImportPreview(); }
            catch (Exception ex) { failure = ex; }
            finally { frame.Continue = false; }
        });
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        if (failure is not null) throw new InvalidOperationException("Staged import failed.", failure);
        return catalog.Tracks().Count == 1 && queue.Count == 1 && nowPlaying.Text == queue[0].Title &&
            Math.Abs(Height - MainSkinHeight) < 1;
    }

    private bool busy;

    private sealed record ImportChoice(string Path)
    {
        public string Name => System.IO.Path.GetFileName(Path);
    }
}
