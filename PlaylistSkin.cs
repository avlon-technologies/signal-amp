using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsMp3Player;

internal sealed partial class PlayerWindow
{
    private Action? showPlaylistSkin;

    private void AddPlaylistFaceplate(Action<Canvas> navigate, Action back, TextBlock queueTitle, ContextMenu queueMenu)
    {
        var lettering = ColorOf("#E9DCCA");
        var amber = ColorOf("#FFAB18");
        var manager = new Canvas { Width = 1312, Height = 1199 };
        EnableFaceplateDrag(manager);
        manager.Clip = Geometry.Parse("M 87,46 H 1028 C 1167,46 1263,133 1267,261 V 1086 Q 1267,1126 1222,1126 H 89 Q 46,1126 46,1086 V 91 Q 46,46 87,46 Z");
        var managerArt = new BitmapImage(new Uri("pack://application:,,,/Assets/playlist-faceplate.png"));
        Put(manager, new Image { Source = managerArt, Stretch = Stretch.Fill, IsHitTestVisible = false }, 0, 0, 1312, 1199);

        var names = StyledList(lettering, 39, "#152B2B", 83);
        names.ItemTemplate = (DataTemplate)XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <DockPanel>
                <TextBlock DockPanel.Dock="Right" Text="{Binding CountLabel}" FontSize="31" Margin="18,0,123,0"/>
                <TextBlock DockPanel.Dock="Left" Text="▱" Width="88"/>
                <TextBlock Text="{Binding Name}" TextTrimming="CharacterEllipsis"/>
              </DockPanel>
            </DataTemplate>
            """);
        Put(manager, names, 114, 318, 1087, 254);
        var tracks = StyledList(amber, 38, "#3B2C16", 79);
        tracks.ItemTemplate = (DataTemplate)XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <DockPanel>
                <TextBlock DockPanel.Dock="Right" Text="{Binding Track.DurationLabel}" Margin="16,0,10,0"/>
                <TextBlock DockPanel.Dock="Left" Text="{Binding Number}" Width="105"/>
                <TextBlock Text="{Binding Track.Title}" TextTrimming="CharacterEllipsis"/>
              </DockPanel>
            </DataTemplate>
            """);
        var slots = new ContextMenu();
        foreach (var (label, delta) in new[] { ("Move up", -1), ("Move down", 1) })
        {
            var move = new MenuItem { Header = label };
            move.Click += (_, _) => RunAction(() =>
            {
                if (names.SelectedItem is not PlaylistChoice choice) return;
                var entries = catalog.Tracks(choice.Name);
                var from = tracks.SelectedIndex;
                var to = from + delta;
                if (from < 0 || to < 0 || to >= entries.Count) return;
                (entries[from], entries[to]) = (entries[to], entries[from]);
                catalog.Save(choice.Name, entries);
                Refresh(choice.Name);
                tracks.SelectedIndex = to;
            });
            slots.Items.Add(move);
        }
        var remove = new MenuItem { Header = "Remove track from playlist" };
        remove.Click += (_, _) => RunAction(() =>
        {
            if (names.SelectedItem is not PlaylistChoice choice || tracks.SelectedIndex < 0) return;
            var entries = catalog.Tracks(choice.Name);
            entries.RemoveAt(tracks.SelectedIndex);
            catalog.Save(choice.Name, entries);
            Refresh(choice.Name);
        });
        slots.Items.Add(remove);
        var saveQueue = new MenuItem { Header = "Save current playback queue as playlist" };
        saveQueue.Click += (_, _) => RunAction(() =>
        {
            var name = AskForPlaylistName("Save queue as playlist", "");
            if (name is null) return;
            if (catalog.Playlists().Contains(name) && MessageBox.Show(this, $"Replace {name}?", Title,
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            catalog.Save(name, queue);
            queueTitle.Text = name;
            RefreshPlaylists(name);
            Refresh(name);
        });
        slots.Items.Add(saveQueue);
        tracks.ContextMenu = slots;
        Put(manager, tracks, 115, 749, 1085, 243);

        Put(manager, new Border
        {
            Background = new LinearGradientBrush(Color.FromRgb(22, 20, 18), Color.FromRgb(16, 15, 14), 0)
        }, 128, 664, 704, 69);
        var listTitle = new TextBlock { Foreground = amber, FontSize = 49, TextTrimming = TextTrimming.CharacterEllipsis };
        Put(manager, listTitle, 141, 663, 674, 73);

        var load = ArtAction("Load selected playlist", Load, 1082, 329, 102, 65, manager);
        // The artwork places the load control over the first row. Move its visible artwork to the selected row.
        var loadArt = new Image { Source = new CroppedBitmap(managerArt, new Int32Rect(1081, 329, 106, 65)), IsHitTestVisible = false };
        Put(manager, loadArt, 1081, 329, 106, 65);
        names.SelectionChanged += (_, _) =>
        {
            if (names.SelectedItem is PlaylistChoice choice)
            {
                listTitle.Text = choice.Name;
                tracks.ItemsSource = catalog.Tracks(choice.Name).Select((track, i) => new PlaylistTrack(i + 1, track)).ToArray();
                var top = 329 + Math.Clamp(names.SelectedIndex, 0, 2) * 83;
                Canvas.SetTop(loadArt, top);
                Canvas.SetTop(load, top);
                loadArt.Visibility = load.Visibility = Visibility.Visible;
            }
            else
            {
                listTitle.Text = "No saved playlists";
                tracks.ItemsSource = null;
                loadArt.Visibility = load.Visibility = Visibility.Collapsed;
            }
        };
        names.MouseDoubleClick += (_, _) => RunAction(Load);
        ArtAction("Back to player", back, 117, 214, 91, 67, manager);
        ArtAction("Add MP3s to playlist", AddTracks, 880, 665, 92, 67, manager);
        ArtAction("Rename playlist", () =>
        {
            if (names.SelectedItem is not PlaylistChoice choice) return;
            var name = AskForPlaylistName("Rename playlist", choice.Name);
            if (name is null || name == choice.Name) return;
            catalog.RenamePlaylist(choice.Name, name);
            if (queueTitle.Text == choice.Name) queueTitle.Text = name;
            RefreshPlaylists(name);
            Refresh(name);
        }, 995, 665, 88, 67, manager);
        ArtAction("Delete playlist", () =>
        {
            if (names.SelectedItem is not PlaylistChoice choice || MessageBox.Show(this, $"Delete {choice.Name}?",
                Title, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            catalog.RemovePlaylist(choice.Name);
            if (queueTitle.Text == choice.Name) queueTitle.Text = "Playback queue";
            RefreshPlaylists();
            Refresh();
        }, 1106, 665, 88, 67, manager);
        var openMenu = new MenuItem { Header = "Open playlists" };
        openMenu.Click += (_, _) => RunAction(Show);
        queueMenu.Items.Insert(0, openMenu);
        showPlaylistSkin = Show;

        void Show()
        {
            Refresh(queueTitle.Text);
            navigate(manager);
        }

        void Load()
        {
            if (names.SelectedItem is not PlaylistChoice choice) return;
            var entries = catalog.Tracks(choice.Name);
            ClearQueue();
            foreach (var track in entries) queue.Add(track);
            queueTitle.Text = choice.Name;
            back();
            status.Text = $"Loaded {choice.Name}.";
        }

        void Refresh(string? selection = null)
        {
            var lists = catalog.Playlists().Select(name => new PlaylistChoice(name, catalog.Tracks(name).Count)).ToArray();
            names.ItemsSource = lists;
            names.SelectedItem = lists.FirstOrDefault(item => item.Name == selection) ?? lists.FirstOrDefault();
        }

        async void AddTracks()
        {
            var existing = names.SelectedItem as PlaylistChoice;
            var name = existing?.Name ?? AskForPlaylistName("Create playlist", "");
            if (name is null) return;
            if (existing is null && catalog.Playlists().Contains(name))
            {
                MessageBox.Show(this, "Choose a different playlist name.", Title);
                return;
            }
            var picker = FileDialog(multiple: true);
            if (picker.ShowDialog(this) != true) return;
            var files = picker.FileNames;
            try
            {
                await Task.Run(() =>
                {
                    var entries = catalog.Tracks(name);
                    var imported = new List<string>();
                    foreach (var file in files)
                    {
                        catalog.Import(file);
                        imported.Add(Path.GetFullPath(file));
                    }
                    var byPath = catalog.Tracks().ToDictionary(track => track.Path, StringComparer.Ordinal);
                    entries.AddRange(imported.Select(path => byPath[path]));
                    catalog.Save(name, entries);
                });
                RefreshPlaylists(name);
                RefreshTracks();
                Refresh(name);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
            {
                MessageBox.Show(this, ex.Message, Title);
            }
        }

        Button ArtAction(string label, Action action, double x, double y, double width, double height, Canvas canvas)
        {
            var button = Button("", action);
            button.ToolTip = label;
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.Template = (ControlTemplate)XamlReader.Parse("""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
                  <Border x:Name="Hit" Background="Transparent" CornerRadius="12" BorderThickness="2"/>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Hit" Property="Background" Value="#22FFFFFF"/></Trigger>
                    <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Hit" Property="BorderBrush" Value="#82F7F3"/></Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
                """);
            button.Margin = new Thickness(0);
            Put(canvas, button, x, y, width, height);
            return button;
        }
    }

    private string? AskForPlaylistName(string title, string initial)
    {
        var input = new TextBox { Text = initial, Margin = new Thickness(0, 0, 0, 12) };
        var body = new StackPanel { Margin = new Thickness(14) };
        body.Children.Add(new Label { Content = "_Playlist name", Target = input });
        body.Children.Add(input);
        var dialog = new Window
        {
            Owner = this, Title = title, Width = 320, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = body, ResizeMode = ResizeMode.NoResize
        };
        var save = new Button { Content = "Save", IsDefault = true, MinWidth = 72 };
        save.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(input.Text)) dialog.DialogResult = true; };
        body.Children.Add(Row(save, new Button { Content = "Cancel", IsCancel = true, MinWidth = 72 }));
        dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return dialog.ShowDialog() == true ? input.Text.Trim() : null;
    }

    private static ListBox StyledList(Brush foreground, double size, string selection, int height)
    {
        var list = new ListBox
        {
            Background = ColorOf("#131211"), Foreground = foreground, FontSize = size,
            BorderThickness = new Thickness(0), FontFamily = new FontFamily("Segoe UI")
        };
        list.ItemContainerStyle = (Style)XamlReader.Parse($$"""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ListBoxItem">
              <Setter Property="Template">
                <Setter.Value>
                  <ControlTemplate TargetType="ListBoxItem">
                    <Border x:Name="Row" MinHeight="{{height}}" Background="#131211" BorderBrush="#4D4740" BorderThickness="0,0,0,1" Padding="26,6">
                      <ContentPresenter HorizontalAlignment="Stretch" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                      <Trigger Property="IsSelected" Value="True"><Setter TargetName="Row" Property="Background" Value="{{selection}}"/></Trigger>
                    </ControlTemplate.Triggers>
                  </ControlTemplate>
                </Setter.Value>
              </Setter>
            </Style>
            """);
        return list;
    }

    private static void Put(Canvas canvas, FrameworkElement element, double x, double y, double width, double height)
    {
        element.Width = width;
        element.Height = height;
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        canvas.Children.Add(element);
    }

    private sealed record PlaylistChoice(string Name, int Count)
    {
        public string CountLabel => Count == 1 ? "1 track" : $"{Count} tracks";
    }

    private sealed record PlaylistTrack(int Index, Track Track)
    {
        public string Number => Index.ToString("00");
    }
}
