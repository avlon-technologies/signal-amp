using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace WindowsMp3Player;

internal sealed partial class PlayerWindow : Window
{
    private readonly Catalog catalog;
    private readonly Playback playback = new();
    private readonly ObservableCollection<Track> queue = new();
    private readonly ListBox library = new();
    private readonly ListBox queued = new();
    private readonly ListBox playlists = new() { BorderThickness = new Thickness(0) };
    private readonly TextBox playlistName = new() { MinWidth = 145 };
    private readonly TextBox search = new();
    private readonly TextBlock status = new() { Text = "Import an MP3 to start.", TextWrapping = TextWrapping.Wrap };
    private readonly Slider seek = new() { Minimum = 0, Maximum = 1 };
    private readonly TextBlock elapsed = new() { MinWidth = 100 };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private int current = -1;
    private bool updatingSeek;

    private static Brush ColorOf(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;
    private static readonly Brush Surface = ColorOf("#171C21");
    private static readonly Brush Panel = ColorOf("#303840");
    private static readonly Brush Text = ColorOf("#EFF1EC");
    private static readonly Brush Accent = ColorOf("#EFC36A");
    private static readonly ControlTemplate RoundedButton = (ControlTemplate)XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
          <Border x:Name="Frame" BorderThickness="1" CornerRadius="5">
            <Border.BorderBrush>
              <LinearGradientBrush StartPoint="0,0" EndPoint="0,1"><GradientStop Color="#A6B0B0"/><GradientStop Color="#111719" Offset="1"/></LinearGradientBrush>
            </Border.BorderBrush>
            <Border Background="{TemplateBinding Background}" CornerRadius="4" Padding="{TemplateBinding Padding}">
              <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="True"/>
            </Border>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Frame" Property="Opacity" Value="0.84"/></Trigger>
            <Trigger Property="IsPressed" Value="True"><Setter TargetName="Frame" Property="Opacity" Value="0.70"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter TargetName="Frame" Property="Opacity" Value="0.48"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """);
    private static readonly ControlTemplate CompactSlider = (ControlTemplate)XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Slider">
          <Grid MinHeight="24">
            <Border Background="#222A2D" CornerRadius="2" Height="4" VerticalAlignment="Center"/>
            <Track Name="PART_Track" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{Binding Value, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}">
              <Track.DecreaseRepeatButton>
                <RepeatButton Command="Slider.DecreaseLarge">
                  <RepeatButton.Template>
                    <ControlTemplate TargetType="RepeatButton"><Border Background="#E7AD4D" Height="4" CornerRadius="2"/></ControlTemplate>
                  </RepeatButton.Template>
                </RepeatButton>
              </Track.DecreaseRepeatButton>
              <Track.Thumb>
                <Thumb Width="15" Height="15">
                  <Thumb.Template>
                    <ControlTemplate TargetType="Thumb"><Ellipse Fill="White" Stroke="#E7AD4D" StrokeThickness="2"/></ControlTemplate>
                  </Thumb.Template>
                </Thumb>
              </Track.Thumb>
              <Track.IncreaseRepeatButton>
                <RepeatButton Command="Slider.IncreaseLarge">
                  <RepeatButton.Template>
                    <ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate>
                  </RepeatButton.Template>
                </RepeatButton>
              </Track.IncreaseRepeatButton>
            </Track>
          </Grid>
        </ControlTemplate>
        """);
    private readonly TextBlock nowPlaying = new() { Text = "Nothing playing", FontSize = 18, FontWeight = FontWeights.SemiBold };
    private readonly List<System.Windows.Shapes.Rectangle> spectrumBars = [];
    private readonly TextBlock trackArtist = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock transportState = new() { Text = "Stopped" };
    private readonly TextBlock viewTitle = new() { Text = "Library", FontSize = 44, FontWeight = FontWeights.SemiBold };
    private readonly Button playButton = new();
    private string? viewedPlaylist;
    private Track? activeTrack;

    internal PlayerWindow(Catalog catalog)
    {
        this.catalog = catalog;
        Title = "Signal Deck";
        Width = 960;
        Height = 350;
        MinWidth = 860;
        MinHeight = 350;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 14;
        if (!SystemParameters.HighContrast)
        {
            Background = Surface;
            Foreground = Text;
            Resources[SystemColors.WindowBrushKey] = Panel;
            Resources[SystemColors.WindowTextBrushKey] = Text;
            Resources[SystemColors.ControlTextBrushKey] = Text;
            Resources[SystemColors.HighlightBrushKey] = Accent;
            Resources[SystemColors.HighlightTextBrushKey] = Surface;
        }

        var root = new DockPanel { Margin = new Thickness(23, 14, 23, 14) };
        if (SystemParameters.HighContrast) Content = root;
        else
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            var chassis = new Grid();
            chassis.Children.Add(new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 26,0 H 934 L 960,27 V 323 L 934,350 H 26 L 0,323 V 27 Z"),
                Stretch = Stretch.Fill, Stroke = ColorOf("#7C729B"), StrokeThickness = 2,
                Fill = new LinearGradientBrush(Color.FromRgb(61, 53, 89), Color.FromRgb(22, 19, 38), 90)
            });
            chassis.Children.Add(root);
            Content = chassis;
        }
        var footer = new StackPanel { Margin = new Thickness(0, 0, 17, 0) };
        var frame = new DockPanel();
        var face = new DockPanel { Height = 322 };
        DockPanel.SetDock(face, Dock.Top);
        frame.Children.Add(face);
        var badgeRow = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };
        var badge = new TextBlock
        {
            Text = "SIGNAL DECK", FontFamily = new FontFamily("Consolas"),
            FontWeight = FontWeights.Bold, FontSize = 13
        };
        badge.FontSize = 17;
        badge.VerticalAlignment = VerticalAlignment.Center;
        badgeRow.MouseLeftButtonDown += (_, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        };
        var close = TransportButton("×", "Close window", Close);
        var minimize = TransportButton("−", "Minimize window", () => WindowState = WindowState.Minimized);
        var windowControls = Row(minimize, close);
        DockPanel.SetDock(windowControls, Dock.Right);
        badgeRow.Children.Add(windowControls);
        badgeRow.Children.Add(badge);
        DockPanel.SetDock(badgeRow, Dock.Top);
        root.Children.Add(badgeRow);
        root.Children.Add(frame);
        face.Height = 260;
        var display = new DockPanel { MinHeight = 66 };
        var identity = new StackPanel();
        identity.Children.Add(nowPlaying);
        identity.Children.Add(trackArtist);
        identity.Children.Add(transportState);
        var displayTime = elapsed;
        displayTime.VerticalAlignment = VerticalAlignment.Center;
        displayTime.TextAlignment = TextAlignment.Left;
        displayTime.Margin = new Thickness(0, 0, 17, 0);
        DockPanel.SetDock(displayTime, Dock.Left);
        display.Children.Add(displayTime);
        display.Children.Add(identity);
        var readout = new Border
        {
            Padding = new Thickness(14, 12, 14, 7), CornerRadius = new CornerRadius(15),
            BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 5)
        };
        if (!SystemParameters.HighContrast)
        {
            readout.Background = new LinearGradientBrush(Color.FromRgb(4, 7, 8), Color.FromRgb(11, 21, 14), 90);
            readout.BorderBrush = ColorOf("#827493");
            display.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, ColorOf("#9EFA53"));
            transportState.Opacity = 0.74;
        }
        nowPlaying.FontFamily = new FontFamily("Consolas");
        nowPlaying.FontSize = 29;
        trackArtist.FontFamily = nowPlaying.FontFamily;
        nowPlaying.TextTrimming = TextTrimming.CharacterEllipsis;
        elapsed.FontFamily = nowPlaying.FontFamily;
        elapsed.FontSize = 48;
        elapsed.Text = "00:00";
        transportState.FontFamily = nowPlaying.FontFamily;
        var screen = new StackPanel();
        screen.Children.Add(display);
        var spectrumGrid = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1, Height = 42, Margin = new Thickness(0, 8, 0, 0) };
        if (!SystemParameters.HighContrast)
        {
            for (var band = 0; band < 40; band++)
            {
                var bar = new System.Windows.Shapes.Rectangle
                {
                    Height = 2, Margin = new Thickness(1, 0, 1, 0), VerticalAlignment = VerticalAlignment.Bottom,
                    Fill = ColorOf("#83EC36"), IsHitTestVisible = false
                };
                spectrumBars.Add(bar);
                spectrumGrid.Children.Add(bar);
            }
            screen.Children.Add(spectrumGrid);
        }
        screen.Children.Add(seek);
        readout.Child = screen;
        footer.Children.Add(readout);
        var transport = new DockPanel { Margin = new Thickness(0, 3, 0, 0) };
        footer.Children.Add(transport);

        var volume = new Slider { Minimum = 0, Maximum = 1, Value = 0.75, Width = 65, VerticalAlignment = VerticalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(volume, "Volume");
        System.Windows.Automation.AutomationProperties.SetName(seek, "Playback position");
        if (!SystemParameters.HighContrast)
        {
            volume.Template = CompactSlider;
            seek.Template = CompactSlider;
        }
        volume.ValueChanged += (_, _) => playback.Volume(volume.Value);

        var queuePanel = new DockPanel { Margin = new Thickness(11, 7, 11, 8) };
        var queueBorder = new Border { Child = queuePanel, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(13), Width = 304, Margin = new Thickness(0, 0, 0, 20) };
        var libraryOpen = false;
        DockPanel? libraryDrawer = null;
        var libraryToggle = Button("_Library", () =>
        {
            libraryOpen = libraryDrawer?.Visibility != Visibility.Visible;
            if (libraryDrawer is not null) libraryDrawer.Visibility = libraryOpen ? Visibility.Visible : Visibility.Collapsed;
            Height = SystemParameters.HighContrast ? (libraryOpen ? 575 : 350) : (MainSkinHeight + (libraryOpen ? 225 : 0));
            if (libraryOpen) search.Focus();
        });
        var rightControls = Row(new TextBlock { Text = "VOL", VerticalAlignment = VerticalAlignment.Center }, volume, libraryToggle);
        DockPanel.SetDock(rightControls, Dock.Right);
        transport.Children.Add(rightControls);

        ConfigureButton(playButton, "▶", PlayOrPause, primary: true);
        playButton.Width = 82;
        playButton.Height = 49;
        System.Windows.Automation.AutomationProperties.SetName(playButton, "Play");
        var previous = TransportButton("|◀", "Previous track", Previous);
        var next = TransportButton("▶|", "Next track", Next);
        foreach (var button in new[] { previous, next })
        {
            button.Width = 75;
            button.Height = 49;
        }
        var buttons = Row(previous, playButton, next);
        transport.Children.Add(buttons);
        status.Margin = new Thickness(0, 3, 0, 0);
        status.Opacity = 0.78;
        footer.Children.Add(status);

        var content = frame;
        libraryDrawer = new DockPanel { Visibility = Visibility.Collapsed, Height = 215, Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(libraryDrawer, Dock.Top);
        content.Children.Add(libraryDrawer);

        var sidebar = new StackPanel { Width = 109, Margin = new Thickness(0, 0, 20, 0) };
        var sidebarBorder = new Border { Child = sidebar, BorderThickness = new Thickness(0, 0, 1, 0), Margin = new Thickness(0, 0, 16, 0) };
        if (!SystemParameters.HighContrast) sidebarBorder.BorderBrush = ColorOf("#4B575F");
        DockPanel.SetDock(sidebarBorder, Dock.Left);
        libraryDrawer.Children.Add(sidebarBorder);
        var libraryButton = Button("_Library", () => { playlists.SelectedItem = null; Browse(null); });
        if (!SystemParameters.HighContrast) libraryButton.Background = ColorOf("#465B5D");
        sidebar.Children.Add(libraryButton);
        sidebar.Children.Add(new Label { Content = "_Playlists", Target = playlists, Foreground = Foreground, Margin = new Thickness(0, 21, 0, 4) });
        playlists.Background = Background;
        sidebar.Children.Add(playlists);
        playlists.SelectionChanged += (_, _) =>
        {
            if (playlists.SelectedItem is string name) Browse(name);
        };
        playlists.ContextMenu = new ContextMenu();
        var deletePlaylist = new MenuItem { Header = "Delete playlist" };
        deletePlaylist.Click += (_, _) => RunAction(DeletePlaylist);
        playlists.ContextMenu.Items.Add(deletePlaylist);

        if (!SystemParameters.HighContrast)
        {
            queueBorder.BorderBrush = ColorOf("#827493");
            queueBorder.Background = ColorOf("#090D0D");
        }
        DockPanel.SetDock(queueBorder, Dock.Right);
        face.Children.Add(queueBorder);
        face.Children.Add(footer);

        var queueActions = new Button { ToolTip = "Queue options" };
        System.Windows.Automation.AutomationProperties.SetName(queueActions, "Queue options");
        var quickImport = Button("_Import MP3s", () => { if (!libraryOpen) libraryToggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Import(); });
        var queueMenu = new ContextMenu();
        foreach (var (label, action) in new (string, Action)[]
        {
            ("Move up", () => Move(-1)), ("Move down", () => Move(1)),
            ("Remove entry", RemoveEntry), ("Clear queue", ClearQueue)
        })
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => RunAction(action);
            queueMenu.Items.Add(item);
        }
        queueActions.ContextMenu = queueMenu;
        ConfigureButton(queueActions, "…", () => queueMenu.IsOpen = true);
        var queueFooter = new StackPanel { Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
        var saveQueue = new MenuItem { Header = "Save queue as playlist" };
        saveQueue.Click += (_, _) => { queueFooter.Visibility = Visibility.Visible; playlistName.Focus(); };
        queueMenu.Items.Add(saveQueue);
        queueFooter.Children.Add(new Label { Content = "Save queue as a playlist", Target = playlistName, Foreground = Foreground });
        queueFooter.Children.Add(Row(playlistName, Button("_Save", () => { SavePlaylist(); if (playlistName.Text.Trim().Length > 0) queueFooter.Visibility = Visibility.Collapsed; })));
        DockPanel.SetDock(queueFooter, Dock.Bottom);
        queuePanel.Children.Add(queueFooter);
        var queueHeader = new DockPanel { Margin = new Thickness(0, 2, 0, 7) };
        var queueTools = Row(quickImport, queueActions);
        DockPanel.SetDock(queueTools, Dock.Right);
        queueHeader.Children.Add(queueTools);
        queueHeader.Children.Add(new TextBlock
        {
            Text = "Up Next", FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center
        });
        AddTop(queuePanel, queueHeader);
        System.Windows.Automation.AutomationProperties.SetName(queued, "Playback queue");
        queuePanel.Children.Add(queued);

        var libraryPanel = new DockPanel();
        libraryDrawer.Children.Add(libraryPanel);
        viewTitle.FontSize = 17;
        AddTop(libraryPanel, viewTitle);
        search.MinHeight = 35;
        search.MinWidth = 160;
        search.Margin = new Thickness(0);
        search.Padding = new Thickness(9, 5, 9, 5);
        System.Windows.Automation.AutomationProperties.SetName(search, "Search tracks");
        var searchField = new Grid { Margin = new Thickness(0, 2, 10, 2) };
        searchField.Children.Add(search);
        var searchHint = new TextBlock
        {
            Text = "Search tracks", IsHitTestVisible = false, Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7
        };
        searchField.Children.Add(searchHint);
        search.TextChanged += (_, _) => searchHint.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var importButton = new Button();
        ConfigureButton(importButton, "_Import MP3s", Import, primary: true);
        var searchRow = new DockPanel { Margin = new Thickness(0, 12, 0, 10) };
        DockPanel.SetDock(importButton, Dock.Right);
        searchRow.Children.Add(importButton);
        searchRow.Children.Add(searchField);
        AddTop(libraryPanel, searchRow);
        AddTop(libraryPanel, new TextBlock { Text = "Track", Margin = new Thickness(12, 3, 0, 8), FontWeight = FontWeights.SemiBold });
        library.ContextMenu = new ContextMenu();
        foreach (var (label, action) in new (string, Action)[]
        {
            ("Play selected", () => AddToQueue(true)), ("Play next", PlayNext),
            ("Add to queue", () => AddToQueue(false)), ("Play playlist", LoadPlaylist),
            ("Locate file", Relink), ("Remove track", RemoveTrack)
        })
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => RunAction(action);
            library.ContextMenu.Items.Add(item);
        }

        System.Windows.Automation.AutomationProperties.SetName(library, "Library tracks");
        library.SelectionMode = SelectionMode.Extended;
        libraryPanel.Children.Add(library);
        var trackRows = new Style(typeof(ListBoxItem));
        trackRows.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 9, 12, 9)));
        trackRows.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        if (!SystemParameters.HighContrast)
        {
            var rowTemplate = (ControlTemplate)XamlReader.Parse("""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ListBoxItem">
                  <Border x:Name="Row" Background="Transparent" CornerRadius="7" Padding="{TemplateBinding Padding}" Margin="3,1">
                    <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="Center"/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Row" Property="Background" Value="#29373C"/></Trigger>
                    <Trigger Property="IsSelected" Value="True"><Setter TargetName="Row" Property="Background" Value="#465B5D"/></Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
                """);
            trackRows.Setters.Add(new Setter(Control.TemplateProperty, rowTemplate));
        }
        library.ItemContainerStyle = trackRows;
        library.ItemTemplate = (DataTemplate)XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <DockPanel LastChildFill="True">
                <Button DockPanel.Dock="Right" Tag="{Binding}" ToolTip="Play this track"
                        AutomationProperties.Name="{Binding Title}" Content="▶"
                                                MinWidth="34" MinHeight="29" Margin="10,0,0,0">
                  <Button.Template>
                    <ControlTemplate TargetType="Button"><Border Background="#39474E" CornerRadius="6">
                      <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border></ControlTemplate>
                  </Button.Template>
                </Button>
                <Border DockPanel.Dock="Left" CornerRadius="7" Background="#2B363C" Width="34" Height="34" Margin="0,0,12,0">
                  <TextBlock Text="♫" Foreground="#E6BD70" HorizontalAlignment="Center" VerticalAlignment="Center" FontSize="19"/>
                </Border>
                <TextBlock Text="{Binding Title}" TextTrimming="CharacterEllipsis" VerticalAlignment="Center" FontWeight="SemiBold"/>
              </DockPanel>
            </DataTemplate>
            """);
        library.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, e) =>
        {
            if (e.OriginalSource is Button { Tag: Track track })
            {
                queue.Insert(Math.Min(current + 1, queue.Count), track);
                Start(Math.Min(current + 1, queue.Count - 1));
                e.Handled = true;
            }
        }));
        queued.ItemContainerStyle = trackRows;
        queued.FontFamily = new FontFamily("Consolas");
        queued.FontSize = 16;
        queued.ItemTemplate = (DataTemplate)XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <DockPanel>
                <TextBlock DockPanel.Dock="Right" Text="{Binding DurationLabel}" Margin="10,0,0,0"/>
                <TextBlock Text="{Binding Title}" TextTrimming="CharacterEllipsis"/>
              </DockPanel>
            </DataTemplate>
            """);
        var shortcuts = new RoutedCommand();
        CommandBindings.Add(new CommandBinding(shortcuts, (_, _) => Import()));
        InputBindings.Add(new KeyBinding(shortcuts, Key.O, ModifierKeys.Control));

        if (!SystemParameters.HighContrast)
        {
            Background = Surface;
            foreach (var control in new Control[] { library, queued, search, playlistName })
            {
                control.Background = Panel;
                control.Foreground = Text;
                control.BorderBrush = ColorOf("#4B575F");
            }
        }
        if (!SystemParameters.HighContrast)
        {
            library.Background = Surface;
            queued.Background = Brushes.Transparent;
            queued.Foreground = ColorOf("#9EFA53");
            library.BorderThickness = new Thickness(0);
            queued.BorderThickness = new Thickness(0);
        }
        queued.ItemsSource = queue;
        library.MouseDoubleClick += (_, e) => { if (e.OriginalSource is not ButtonBase) AddToQueue(true); };
        library.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { AddToQueue(true); e.Handled = true; }
        };
        queued.MouseDoubleClick += (_, _) => Start(queued.SelectedIndex);
        search.TextChanged += (_, _) => RefreshTracks();
        playback.Changed += message =>
        {
            status.Text = message;
            transportState.Text = playback.State;
            var playing = playback.State == "Playing";
            playButton.Content = playing ? "Ⅱ" : "▶";
            System.Windows.Automation.AutomationProperties.SetName(playButton, playing ? "Pause" : "Play");
        };
        playback.Ended += Next;
        seek.ValueChanged += (_, _) => { if (!updatingSeek) playback.Seek(seek.Value); };
        timer.Tick += (_, _) => UpdatePosition();
        timer.Start();
        Closed += (_, _) => { timer.Stop(); playback.Dispose(); };
        RefreshTracks();
        RefreshPlaylists();
        if (!SystemParameters.HighContrast)
        {
            display.Children.Remove(elapsed);
            identity.Children.Clear();
            screen.Children.Remove(spectrumGrid);
            screen.Children.Remove(seek);
            queuePanel.Children.Remove(queued);
            frame.Children.Remove(libraryDrawer);
            transport.Children.Remove(rightControls);
            buttons.Children.Clear();
            rightControls.Children.Clear();
            queueTools.Children.Remove(quickImport);
            footer.Children.Remove(status);
            BuildArtworkSkin(libraryDrawer, volume, previous, next, libraryToggle, queueMenu);
        }
    }

    internal void SavePreview(string path, bool playlistPage = false, string[]? importFiles = null, bool pausePreview = false)
    {
        foreach (var track in catalog.Tracks().Reverse<Track>()) queue.Add(track);
        library.SelectedItem = library.Items.Cast<Track>().FirstOrDefault(track => track.Title == "Rising Chimes");
        if (queue.Count > 0)
        {
            queued.SelectedIndex = 0;
            nowPlaying.Text = queue[0].Title;
            transportState.Text = "Ready to play";
            status.Text = "2 tracks ready";
            elapsed.Text = "00:00";
            vinylDial?.InvalidateVisual();
        vinylSpectrum?.InvalidateVisual();
            trackArtist.Text = queue[0].Artist;
        }
        if (playlistPage) showPlaylistSkin?.Invoke();
        if (importFiles is not null) { showImportSkin?.Invoke(); stageImportPreview?.Invoke(importFiles); }
        if (!playlistPage && importFiles is null && vinylDial is not null && queue.Count > 0)
        {
            playback.Volume(0.10);
            Start(0);
            if (playback.State == "Playing")
            {
                var frame = new DispatcherFrame();
                var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                delay.Interval = TimeSpan.FromMilliseconds(60);
                var waiting = System.Diagnostics.Stopwatch.StartNew();
                delay.Tick += (_, _) =>
                {
                    if (waiting.ElapsedMilliseconds < 1500 &&
                        (waiting.ElapsedMilliseconds < 200 || !playback.Spectrum.Any(value => value > 0.14f)))
                        return;
                    delay.Stop();
                    frame.Continue = false;
                };
                delay.Start();
                Dispatcher.PushFrame(frame);
                if (pausePreview)
                {
                    playback.Toggle();
                    playback.Seek(playback.Duration.TotalSeconds * 0.94);
                }
                UpdatePosition();
            }
        }
        UpdateLayout();
        var content = (FrameworkElement)Content;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    internal bool SmokeCheckLibraryPlayback()
    {
        library.SelectedIndex = 0;
        playButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        return playback.State == "Playing" && queue.Count == 1 && nowPlaying.Text == queue[0].Title;
    }

    private void Browse(string? name)
    {
        viewedPlaylist = name;
        viewTitle.Text = name ?? "Library";
        RefreshTracks();
    }

    private void Previous()
    {
        if (current > 0) Start(current - 1);
        else playback.Seek(0);
    }

    private void PlayNext()
    {
        var index = Math.Min(current + 1, queue.Count);
        foreach (var track in library.SelectedItems.Cast<Track>().ToArray()) queue.Insert(index++, track);
        if (library.SelectedItems.Count > 0) status.Text = "Added to Up Next.";
    }

    private static void AddTop(DockPanel panel, UIElement child)
    {
        DockPanel.SetDock(child, Dock.Top);
        panel.Children.Add(child);
    }

    private static WrapPanel Row(params UIElement[] children)
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    private Button Button(string label, Action action)
    {
        var button = new Button();
        ConfigureButton(button, label, action);
        return button;
    }

    private void ConfigureButton(Button button, string label, Action action, bool primary = false)
    {
        button.Content = label;
        button.Padding = new Thickness(10, 6, 10, 6);
        button.Margin = new Thickness(0, 2, 5, 2);
        if (!SystemParameters.HighContrast)
        {
            button.Template = RoundedButton;
            button.Background = primary
                ? new LinearGradientBrush(Color.FromRgb(255, 220, 147), Color.FromRgb(206, 150, 57), 90)
                : new LinearGradientBrush(Color.FromRgb(79, 91, 99), Color.FromRgb(39, 49, 55), 90);
            button.Foreground = primary ? ColorOf("#16170D") : Text;
            button.BorderBrush = primary ? Accent : ColorOf("#879399");
        }
        button.Click += (_, _) => RunAction(action);
    }

    private Button TransportButton(string glyph, string label, Action action)
    {
        var button = Button(glyph, action);
        button.ToolTip = label;
        button.MinWidth = 38;
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        return button;
    }

    private void RunAction(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        { status.Text = ex.Message; }
    }

    private void RefreshTracks() => library.ItemsSource = catalog.Tracks(viewedPlaylist)
        .Where(track => track.Title.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToList();

    private void RefreshPlaylists(string? selected = null)
    {
        playlists.ItemsSource = catalog.Playlists();
        playlists.SelectedItem = selected;
    }

    private static OpenFileDialog FileDialog(bool multiple = false) =>
        new() { Filter = "MP3 files (*.mp3)|*.mp3", Multiselect = multiple, CheckFileExists = true };

    private async void Import()
    {
        var picker = FileDialog(true);
        if (picker.ShowDialog(this) != true) return;
        status.Text = "Importing tracks...";
        var failures = await Task.Run(() =>
        {
            var errors = new List<string>();
            foreach (var file in picker.FileNames)
            {
                try { catalog.Import(file); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
                { errors.Add($"{Path.GetFileName(file)}: {ex.Message}"); }
            }
            return errors;
        });
        try { RefreshTracks(); }
        catch (Microsoft.Data.Sqlite.SqliteException ex) { status.Text = ex.Message; return; }
        var imported = picker.FileNames.Length - failures.Count;
        status.Text = failures.Count == 0 ? $"Imported {imported} track(s)." :
            $"Imported {imported}; {failures.Count} failed. {failures[0]}";
    }

    private void Relink()
    {
        if (library.SelectedItem is not Track track) return;
        var picker = FileDialog();
        if (picker.ShowDialog(this) != true) return;
        catalog.Relink(track.Id, picker.FileName);
        var updated = catalog.Tracks().Single(item => item.Id == track.Id);
        for (var index = 0; index < queue.Count; index++)
            if (queue[index].Id == track.Id) queue[index] = updated;
        RefreshTracks();
        status.Text = $"Relinked {updated.Title}.";
    }

    private void RemoveTrack()
    {
        if (library.SelectedItem is not Track track) return;
        if (MessageBox.Show(this, $"Remove {track.Title} from the library and its playlists?", Title,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        catalog.RemoveTrack(track.Id);
        for (var index = queue.Count - 1; index >= 0; index--)
        {
            if (queue[index].Id != track.Id) continue;
            queued.SelectedIndex = index;
            RemoveEntry();
        }
        RefreshTracks();
    }

    private void AddToQueue(bool play)
    {
        var selection = library.SelectedItems.Cast<Track>().ToList();
        var first = queue.Count;
        foreach (var track in selection) queue.Add(track);
        if (!play && selection.Count > 0) status.Text = $"Added {selection.Count} track(s) to Up Next.";
        if (play && selection.Count > 0) Start(first);
    }

    private void Start(int index)
    {
        if (index < 0 || index >= queue.Count) return;
        current = index;
        queued.SelectedIndex = index;
        queued.ScrollIntoView(queue[index]);
        activeTrack = queue[index];
        nowPlaying.Text = activeTrack.Title;
        trackArtist.Text = activeTrack.Artist;
        playback.Start(activeTrack);
    }

    private void PlayOrPause()
    {
        if (playback.State is "Playing" or "Paused") playback.Toggle();
        else if (playback.State != "Loading")
        {
            if (queue.Count == 0 && library.SelectedItem is Track selected)
            {
                queue.Add(selected);
                Start(0);
            }
            else if (queue.Count > 0) Start(queued.SelectedIndex >= 0 ? queued.SelectedIndex : 0);
            else status.Text = "Select a track in your library or import MP3s to start.";
        }
    }

    private void Next()
    {
        if (current + 1 < queue.Count) Start(current + 1);
        else { playback.Stop(); current = -1; status.Text = "End of queue."; }
    }

    private void Move(int delta)
    {
        var from = queued.SelectedIndex;
        var to = from + delta;
        if (from < 0 || to < 0 || to >= queue.Count) return;
        if (current == from) current = to;
        else if (current == to) current = from;
        queue.Move(from, to);
        queued.SelectedIndex = to;
    }

    private void RemoveEntry()
    {
        var index = queued.SelectedIndex;
        if (index < 0) return;
        if (index == current) { playback.Stop(); current--; }
        else if (index < current) current--;
        queue.RemoveAt(index);
        queued.SelectedIndex = Math.Min(index, queue.Count - 1);
    }

    private void ClearQueue()
    {
        playback.Stop();
        current = -1;
        queue.Clear();
    }

    private void LoadPlaylist()
    {
        if (playlists.SelectedItem is not string name) return;
        var tracks = catalog.Tracks(name);
        ClearQueue();
        foreach (var track in tracks) queue.Add(track);
        playlistName.Text = name;
        if (queue.Count > 0) Start(0);
        else status.Text = $"{name} has no tracks.";
    }

    private void SavePlaylist()
    {
        var name = playlistName.Text.Trim();
        if (name.Length == 0) { status.Text = "Enter a playlist name."; return; }
        if (catalog.Playlists().Contains(name) &&
            MessageBox.Show(this, $"Replace the saved playlist {name}?", Title, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        catalog.Save(name, queue);
        RefreshPlaylists(name);
        status.Text = $"Saved {name}.";
    }

    private void DeletePlaylist()
    {
        if (playlists.SelectedItem is not string name) return;
        if (MessageBox.Show(this, $"Delete the saved playlist {name}?", Title, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        catalog.RemovePlaylist(name);
        if (viewedPlaylist == name) Browse(null);
        RefreshPlaylists();
        status.Text = $"Deleted {name}.";
    }

    private void UpdatePosition()
    {
        var position = playback.Position;
        var duration = playback.Duration;
        updatingSeek = true;
        seek.Maximum = Math.Max(1, duration.TotalSeconds);
        if (Mouse.Captured is not Thumb) seek.Value = position.TotalSeconds;
        updatingSeek = false;
        elapsed.Text = position.ToString(position.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss");
        vinylDial?.InvalidateVisual();
        vinylSpectrum?.InvalidateVisual();
        var levels = playback.Spectrum;
        for (var band = 0; band < spectrumBars.Count; band++)
            spectrumBars[band].Height = band < levels.Length ? 2 + 39 * levels[band] : 2;
    }
}
