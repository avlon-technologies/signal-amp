using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsMp3Player;

internal sealed partial class PlayerWindow
{
    private const double CompactSkinSize = 470;
    private VinylDial? vinylDial;
    private SpectrumDisplay? vinylSpectrum;

    private const double MainSkinHeight = CompactSkinSize * 866 / 1496;
    private Action? showImportSkin;

    private void BuildArtworkSkin(DockPanel libraryDrawer, Slider volume,
        Button previous, Button next, Button libraryToggle, ContextMenu queueMenu)
    {
        Title = "Signal Amp";
        Width = MinWidth = CompactSkinSize;
        Height = MinHeight = MainSkinHeight;
        Background = Brushes.Transparent;
        var artwork = new BitmapImage(new Uri("pack://application:,,,/Assets/wide-player.png"));
        var scene = new Canvas { Width = 1622, Height = 970 };
        EnableFaceplateDrag(scene);
        scene.Clip = Geometry.Parse("M 166,54 H 1454 C 1510,54 1556,102 1556,158 V 812 C 1556,869 1511,915 1454,915 H 166 C 110,915 66,870 66,813 V 157 C 66,101 111,54 166,54 Z");
        Put(scene, new Image { Source = artwork, IsHitTestVisible = false }, 0, 0, 1622, 970);
        var playerPage = new Canvas { Width = 1496, Height = 866, ClipToBounds = true };
        Put(playerPage, scene, -63, -52, 1622, 970);
        var root = new DockPanel();
        var frame = new Viewbox { Width = Width, Height = Height, Child = playerPage };
        DockPanel.SetDock(frame, Dock.Top);
        root.Children.Add(frame);
        libraryDrawer.Margin = new Thickness(11, 0, 11, 9);
        libraryDrawer.Background = Surface;
        root.Children.Add(libraryDrawer);
        Content = root;

        void Navigate(Canvas page)
        {
            libraryDrawer.Visibility = Visibility.Collapsed;
            frame.Height = CompactSkinSize * page.Height / page.Width;
            MinHeight = frame.Height;
            Height = frame.Height;
            frame.Child = page;
        }

        void Back() => Navigate(playerPage);
        var lettering = ColorOf("#F3E5D6");
        var queueTitle = new TextBlock { Text = "Playback queue" };
        AddPlaylistFaceplate(Navigate, Back, queueTitle, queueMenu);
        AddImportFaceplate(Navigate, Back);

        var inset = new LinearGradientBrush(Color.FromRgb(33, 32, 31), Color.FromRgb(18, 18, 17), 100);
        Put(scene, new Border { Background = inset, CornerRadius = new CornerRadius(12), IsHitTestVisible = false },
            854, 257, 657, 129);
        nowPlaying.Foreground = lettering;
        nowPlaying.FontSize = 60;
        nowPlaying.FontFamily = new FontFamily("Segoe UI");
        nowPlaying.FontWeight = FontWeights.SemiBold;
        Put(scene, nowPlaying, 882, 263, 617, 78);
        trackArtist.Foreground = ColorOf("#BEB8B4");
        trackArtist.FontFamily = nowPlaying.FontFamily;
        trackArtist.FontSize = 46;
        Put(scene, trackArtist, 883, 331, 612, 57);
        vinylDial = new VinylDial(() => elapsed.Text, () => playback.Duration.TotalSeconds > 0
            ? playback.Position.TotalSeconds / playback.Duration.TotalSeconds : 0, () => playback.Spectrum, () => playback.State);
        vinylDial.Width = vinylDial.Height = 547;
        Put(scene, new Viewbox { Child = vinylDial }, 120, 113, 705, 705);
        var dialTarget = new Border { Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = "Seek around the record" };
        dialTarget.MouseLeftButtonDown += (_, e) =>
        {
            if (playback.Duration <= TimeSpan.Zero) return;
            var point = e.GetPosition(dialTarget);
            var x = (point.X - 352.5) * 547 / 705;
            var y = (point.Y - 352.5) * 547 / 705;
            if (x * x + y * y < 225 * 225 || x * x + y * y > 273 * 273) return;
            var turn = (Math.Atan2(x, -y) / (2 * Math.PI) + 1) % 1;
            playback.Seek(turn * playback.Duration.TotalSeconds);
        };
        Put(scene, dialTarget, 120, 113, 705, 705);
        seek.Opacity = 0;
        seek.IsHitTestVisible = false;
        Put(scene, seek, 275, 793, 390, 23);
        vinylSpectrum = new SpectrumDisplay(() => playback.Spectrum);
        Put(scene, vinylSpectrum, 853, 430, 658, 177);

        WireArtworkButton(previous, "Previous track", "M 53,0 H 205 L 163,130 H 0 Z", scene, 781, 650, 206, 132);
        WireArtworkButton(playButton, "Play", "M 46,0 H 206 L 164,130 H 0 Z", scene, 967, 650, 208, 132);
        WireArtworkButton(next, "Next track", "M 44,0 H 200 L 158,130 H 0 Z", scene, 1153, 650, 201, 132);
        var pauseOverlay = new Border
        {
            Background = new ImageBrush(new CroppedBitmap(artwork, new Int32Rect(1106, 677, 31, 70))),
            IsHitTestVisible = false, Visibility = Visibility.Collapsed,
            Child = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 0,0 H 12 V 40 H 0 Z M 22,0 H 34 V 40 H 22 Z"),
                Fill = ColorOf("#201E1B"), Width = 34, Height = 40, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        Put(scene, pauseOverlay, 1043, 677, 70, 72);
        playback.Changed += _ =>
        {
            playButton.Content = null;
            pauseOverlay.Visibility = playback.State == "Playing" ? Visibility.Visible : Visibility.Collapsed;
        };

        var importButton = Button("", () => showImportSkin?.Invoke());
        WireArtworkButton(importButton, "Import songs", null, scene, 1223, 101, 127, 102);
        var playlistsButton = Button("", () => showPlaylistSkin?.Invoke());
        WireArtworkButton(playlistsButton, "Playlists", null, scene, 1373, 101, 119, 102);
        Put(scene, new Border { Background = ColorOf("#171615"), CornerRadius = new CornerRadius(10), IsHitTestVisible = false },
            879, 824, 610, 43);
        volume.Template = (ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Slider">
              <Grid>
                <Border Height="9" Background="#565456" CornerRadius="5" VerticalAlignment="Center"/>
                <Track Name="PART_Track" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}"
                       Value="{Binding Value, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}">
                  <Track.DecreaseRepeatButton>
                    <RepeatButton Command="Slider.DecreaseLarge">
                      <RepeatButton.Template>
                        <ControlTemplate TargetType="RepeatButton"><Border Background="#16EBF5" Height="9" CornerRadius="5"/></ControlTemplate>
                      </RepeatButton.Template>
                    </RepeatButton>
                  </Track.DecreaseRepeatButton>
                  <Track.Thumb>
                    <Thumb Width="26" Height="26">
                      <Thumb.Template>
                        <ControlTemplate TargetType="Thumb"><Ellipse Fill="#16EBF5"/></ControlTemplate>
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
        volume.IsMoveToPointEnabled = true;
        Put(scene, volume, 883, 818, 598, 54);
        double lastAudibleVolume = volume.Value;
        var mute = Button("", () =>
        {
            if (volume.Value > 0) { lastAudibleVolume = volume.Value; volume.Value = 0; }
            else volume.Value = lastAudibleVolume;
        });
        WireArtworkButton(mute, "Mute or restore volume", null, scene, 1342, 650, 173, 134);
        var volumeButton = Button("", () => mute.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)));
        WireArtworkButton(volumeButton, "Mute or restore volume", null, scene, 804, 823, 61, 48);

        var queuePopup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = playlistsButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            StaysOpen = false, AllowsTransparency = true
        };
        queued.Background = ColorOf("#100C08");
        queued.Foreground = ColorOf("#FFAB18");
        queued.ContextMenu = queueMenu;
        queuePopup.Child = new Border
        {
            Background = ColorOf("#100C08"), BorderBrush = ColorOf("#544B44"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(6), Width = 300, MaxHeight = 215, Child = queued
        };
        queued.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Start(queued.SelectedIndex);
            e.Handled = true;
        };

        var closeButton = Button("", Close);
        WireArtworkButton(closeButton, "Close Signal Amp", null, scene, 122, 101, 94, 92);

        var menu = new ContextMenu();
        AddMenu("Import songs", () => showImportSkin?.Invoke());
        AddMenu("Playlists", () => showPlaylistSkin?.Invoke());
        AddMenu("Playback queue", () => queuePopup.IsOpen = true);
        AddMenu("Library", () => libraryToggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)));
        AddMenu("Stop", playback.Stop);
        AddMenu("Minimize", () => WindowState = WindowState.Minimized);
        AddMenu("Close", Close);
        scene.ContextMenu = menu;
        var importCommand = new RoutedCommand();
        CommandBindings.Add(new CommandBinding(importCommand, (_, _) => showImportSkin?.Invoke()));
        InputBindings.Add(new KeyBinding(importCommand, Key.I, ModifierKeys.Control));
        System.Windows.Automation.AutomationProperties.SetLiveSetting(status, System.Windows.Automation.AutomationLiveSetting.Polite);
        status.FontSize = 23;
        status.Foreground = lettering;
        status.Visibility = Visibility.Collapsed;
        Put(scene, status, 853, 607, 647, 40);
        playback.Changed += _ => status.Visibility = playback.State == "Error" ? Visibility.Visible : Visibility.Collapsed;

        void AddMenu(string label, Action action)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => RunAction(action);
            menu.Items.Add(item);
        }
    }

    private void WireArtworkButton(Button button, string label, string? shape, Canvas canvas,
        double x, double y, double width, double height)
    {
        button.Content = null;
        button.Tag = shape is null ? Geometry.Parse($"M 0,0 H {width} V {height} H 0 Z") : Geometry.Parse(shape);
        button.ToolTip = label;
        button.Margin = new Thickness(0);
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        button.Template = (ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
              <Path x:Name="Hit" Data="{TemplateBinding Tag}" Fill="Transparent" StrokeThickness="2"/>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Hit" Property="Fill" Value="#20FFFFFF"/></Trigger>
                <Trigger Property="IsPressed" Value="True"><Setter TargetName="Hit" Property="Fill" Value="#44000000"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Hit" Property="Stroke" Value="#80F9F9"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """);
        Put(canvas, button, x, y, width, height);
    }

    private void EnableFaceplateDrag(Canvas skin)
    {
        // A transparent background makes the artwork's exposed surface receive mouse input.
        skin.Background = Brushes.Transparent;
        skin.QueryCursor += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, skin))
            {
                e.Cursor = Cursors.SizeAll;
                e.Handled = true;
            }
        };
        skin.MouseLeftButtonDown += (_, e) =>
        {
            if (!ReferenceEquals(e.OriginalSource, skin) || e.ButtonState != MouseButtonState.Pressed) return;
            e.Handled = true;
            DragMove();
        };
    }

    private sealed class VinylDial(Func<string> time, Func<double> progress, Func<float[]> spectrum, Func<string> state) : FrameworkElement
    {
        private const int SpokeCount = 104;
        private readonly float[] envelopes = new float[SpokeCount];
        private double phase;
        private static readonly Pen[] Spokes = CreateSpokePens(5, 255);
        private static readonly Pen[] Glow = CreateSpokePens(12, 42);

        private static Pen[] CreateSpokePens(double width, byte alpha)
        {
            var pens = new Pen[20];
            for (var index = 0; index < pens.Length; index++)
            {
                var heat = Math.Clamp((index - 8) / 11.0, 0, 1);
                var color = Color.FromArgb(alpha, (byte)(13 + 242 * heat), (byte)(238 - 35 * heat), (byte)(250 - 224 * heat));
                var pen = new Pen(new SolidColorBrush(color), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                pen.Freeze();
                pens[index] = pen;
            }
            return pens;
        }

        private static readonly Pen RestingSpoke = new(ColorOf("#0F4F54"), 4.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        private static readonly Brush RingBase = ColorOf("#393634");
        private static readonly Brush RingActive = ColorOf("#83F4F8");
        private static readonly Brush Face = ColorOf("#191919");
        private static readonly Brush Type = ColorOf("#D5C9BE");
        private static readonly Typeface ClockFace = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);

        protected override void OnRender(DrawingContext drawing)
        {
            var center = new Point(273.5, 273.5);
            const double radius = 259;

            var glass = new RadialGradientBrush(Color.FromRgb(15, 20, 20), Color.FromRgb(5, 11, 11));
            drawing.DrawEllipse(glass, null, center, 273, 273);
            drawing.DrawEllipse(null, new Pen(RingBase, 11), center, radius, radius);
            var paused = state() == "Paused";
            var levels = spectrum();
            var peak = 0f;
            foreach (var level in levels) peak = Math.Max(peak, level);
            if (peak > 0.02f) phase = (phase + 0.032 + 0.018 * peak) % (2 * Math.PI);
            for (var index = 0; index < envelopes.Length; index++)
            {
                var theta = index * 2 * Math.PI / envelopes.Length;
                var ripple = 0.35 + 0.65 * Math.Pow((1 + Math.Cos(5 * theta - phase)) / 2, 2);
                var band = levels.Length == 0 ? 0 : (index * 3 * levels.Length / envelopes.Length) % levels.Length;
                var detail = levels.Length == 0 ? 0 : levels[band];
                var target = (float)Math.Min(1, peak * (0.29 + 0.47 * ripple) + 0.44 * detail);
                envelopes[index] += (target - envelopes[index]) * (target > envelopes[index] ? 0.64f : 0.22f);
                var strength = envelopes[index];
                if (strength < 0.018f && !paused) continue;
                var angle = (index / (double)envelopes.Length - 0.25) * 2 * Math.PI;
                var dx = Math.Cos(angle);
                var dy = Math.Sin(angle);
                const double inner = 112;
                var outer = paused ? 178 : inner + 130 * Math.Pow(strength, 0.72);
                var start = new Point(center.X + dx * inner, center.Y + dy * inner);
                var end = new Point(center.X + dx * outer, center.Y + dy * outer);
                var shade = Math.Clamp((int)(Math.Pow(strength, 0.75) * (Spokes.Length - 1)), 0, Spokes.Length - 1);
                if (paused) { drawing.DrawLine(RestingSpoke, start, end); continue; }
                drawing.DrawLine(Glow[shade], start, end);
                var tipStart = new Point(center.X + dx * (inner + (outer - inner) * 0.72), center.Y + dy * (inner + (outer - inner) * 0.72));
                drawing.DrawLine(Spokes[0], start, tipStart);
                drawing.DrawLine(Spokes[shade], tipStart, end);
            }
            var turn = Math.Clamp(progress(), 0, 0.9999);
            if (turn > 0)
            {
                var angle = turn * Math.PI * 2;
                var start = new Point(center.X, center.Y - radius);
                var end = new Point(center.X + Math.Sin(angle) * radius, center.Y - Math.Cos(angle) * radius);
                var arc = new StreamGeometry();
                using (var context = arc.Open())
                {
                    context.BeginFigure(start, false, false);
                    context.ArcTo(end, new Size(radius, radius), 0, turn > 0.5, SweepDirection.Clockwise, true, false);
                }
                drawing.DrawGeometry(null, new Pen(RingActive, 7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, arc);
                drawing.DrawEllipse(Type, new Pen(ColorOf("#F6EADB"), 1.5), end, 8, 8);
            }
            drawing.DrawEllipse(Face, new Pen(RingBase, 2), center, 100, 100);
            var label = new FormattedText(time(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                ClockFace, 49, Type, VisualTreeHelper.GetDpi(this).PixelsPerDip) { MaxTextWidth = 171, TextAlignment = TextAlignment.Center };
                        drawing.DrawText(label, new Point(188, paused ? 238 : 243));
            if (paused)
            {
                var indicator = new FormattedText("Paused", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    ClockFace, 24, Type, VisualTreeHelper.GetDpi(this).PixelsPerDip) { MaxTextWidth = 171, TextAlignment = TextAlignment.Center };
                drawing.DrawText(indicator, new Point(188, 299));
            }
        }
    }

    private sealed class SpectrumDisplay(Func<float[]> read) : FrameworkElement
    {
        private static readonly Brush Back = ColorOf("#090B0A");
        private static readonly Brush Cyan = ColorOf("#19F0EE");
        private static readonly Brush Mint = ColorOf("#64F59C");
        private static readonly Brush Amber = ColorOf("#FFCC1B");

        protected override void OnRender(DrawingContext drawing)
        {
            drawing.DrawRoundedRectangle(Back, null, new Rect(0, 0, ActualWidth, ActualHeight), 8, 8);
            var levels = read();
            const int bands = 25;
            var step = ActualWidth / bands;
            for (var band = 0; band < bands; band++)
            {
                var segmentCount = levels.Length == 0 ? 0 : (int)Math.Ceiling(12 * levels[band * levels.Length / bands]);
                for (var rung = 0; rung < segmentCount; rung++)
                {
                    var fill = rung < 4 ? Cyan : rung < 8 ? Mint : Amber;
                    drawing.DrawRectangle(fill, null, new Rect(band * step + 1, ActualHeight - (rung + 1) * 9, step - 2, 6));
                }
            }
        }
    }
}
