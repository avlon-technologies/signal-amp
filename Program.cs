using System.IO;
using System.Windows;
using System.Windows.Threading;
using NAudio.Wave;

namespace WindowsMp3Player;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--catalog-smoke-test", var directory])
        {
            var catalog = new Catalog(Path.Combine(directory, "catalog"));
            var source = Path.Combine(directory, "first.mp3");
            var replacement = Path.Combine(directory, "replacement.mp3");
            var pcm = new byte[44100 * 2];
            using (var wave = new RawSourceWaveStream(new MemoryStream(pcm), new WaveFormat(44100, 16, 1)))
                MediaFoundationEncoder.EncodeToMp3(wave, source, 128000);
            using (var tags = TagLib.File.Create(source))
            {
                tags.Tag.Title = "Continuum playback fixture";
                tags.Save();
            }
            File.Copy(source, replacement);
            using (var decoded = new MediaFoundationReader(source))
                if (decoded.Read(pcm, 0, pcm.Length) == 0) return 3;
            catalog.Import(source);
            catalog.Import(source);
            var track = catalog.Tracks().Single();
            if (track.Title != "Continuum playback fixture") return 4;
            catalog.Save("Playlist", [track, track]);
            catalog.Relink(track.Id, replacement);
            var reopened = new Catalog(Path.Combine(directory, "catalog"));
            if (reopened.Tracks("Playlist").Count != 2 ||
                reopened.Tracks("Playlist").Any(item => item.Id != track.Id || item.Path != replacement)) return 1;
            reopened.RemoveTrack(track.Id);
            return reopened.Tracks("Playlist").Count == 0 && File.Exists(replacement) ? 0 : 2;
        }

        if (args is ["--playback-smoke-test", var file])
        {
            using var playback = new Playback();
            var frame = new DispatcherFrame();
            var completed = 0;
            playback.Ended += () => { completed++; frame.Continue = false; };
            var timeout = new DispatcherTimer();
            timeout.Tick += (_, _) => frame.Continue = false;
            playback.Start(new Track(1, file, "Playback fixture"));
            if (playback.State != "Playing") return 5;
            timeout.Interval = playback.Duration + TimeSpan.FromSeconds(8);
            var signalDetected = false;
            var spectrumPoll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(35) };
            spectrumPoll.Tick += (_, _) => signalDetected |= playback.Spectrum.Any(value => value > 0.05f);
            spectrumPoll.Start();
            timeout.Start();
            Dispatcher.PushFrame(frame);
            timeout.Stop();
            spectrumPoll.Stop();
            if (args.Length == 2 && Path.GetFileName(file).EndsWith("-chimes.mp3", StringComparison.OrdinalIgnoreCase) && !signalDetected) return 8;
            return completed == 1 && playback.State == "Stopped" ? 0 : 6;
        }

        var app = new Application();
        if (args is ["--ui-smoke-test", var fixture, var scratch])
        {
            var testCatalog = new Catalog(scratch);
            testCatalog.Import(fixture);
            var window = new PlayerWindow(testCatalog) { ShowInTaskbar = false, Opacity = 0 };
            window.Show();
            var succeeded = window.SmokeCheckLibraryPlayback();
            window.Close();
            return succeeded ? 0 : 7;
        }
        if (args is ["--import-smoke-test", var importFixture, var importScratch])
        {
            var window = new PlayerWindow(new Catalog(importScratch)) { ShowInTaskbar = false, Opacity = 0 };
            window.Show();
            var succeeded = window.SmokeCheckStagedImport(importFixture);
            window.Close();
            return succeeded ? 0 : 9;
        }
        if (args is [var render, var samples, var previewScratch, var png] && render is "--render-preview" or "--render-playlists" or "--render-import" or "--render-paused")
        {
            var previewCatalog = new Catalog(previewScratch);
            foreach (var sample in Directory.GetFiles(samples, "*.mp3")) previewCatalog.Import(sample);
            previewCatalog.Save("Favorites", previewCatalog.Tracks());
            var window = new PlayerWindow(previewCatalog) { ShowInTaskbar = false, Left = -1800, WindowStartupLocation = WindowStartupLocation.Manual };
            window.Show();
            window.SavePreview(png, render == "--render-playlists", render == "--render-import" ? Directory.GetFiles(samples, "*.mp3") : null, render == "--render-paused");
            window.Close();
            return 0;
        }
        try { return app.Run(new PlayerWindow(new Catalog())); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Windows MP3 Player", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }
}
