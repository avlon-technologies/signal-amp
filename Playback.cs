using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Dsp;

namespace WindowsMp3Player;

internal sealed class Playback : IDisposable
{
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private WasapiOut? output;
    private MediaFoundationReader? reader;
    private double volume = 0.75;
    internal string State { get; private set; } = "Stopped";
    internal event Action<string>? Changed;
    internal event Action? Ended;
    internal TimeSpan Position => reader?.CurrentTime ?? TimeSpan.Zero;
    internal TimeSpan Duration => reader?.TotalTime ?? TimeSpan.Zero;

    internal void Start(Track track)
    {
        Close();
        if (!File.Exists(track.Path))
        {
            SetState("Error", $"Missing file: {track.Title}. Relink it in the library.");
            return;
        }

        SetState("Loading", $"Opening {track.Title}");
        try
        {
            reader = new MediaFoundationReader(track.Path);
            var next = new WasapiOut(AudioClientShareMode.Shared, false, 100);
            output = next;
            spectrum = new SpectrumProvider(reader.ToSampleProvider());
            gain = new NAudio.Wave.SampleProviders.VolumeSampleProvider(spectrum) { Volume = (float)volume };
            next.Init(gain.ToWaveProvider());
            next.PlaybackStopped += (_, result) => dispatcher.BeginInvoke(() =>
            {
                if (output != next) return;
                Close();
                if (result.Exception is not null)
                {
                    SetState("Error", $"Could not play {track.Title}: {result.Exception.Message}");
                    return;
                }
                SetState("Stopped", "Track finished.");
                Ended?.Invoke();
            });
            next.Play();
            SetState("Playing", $"Playing {track.Title}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            Close();
            SetState("Error", $"Could not play {track.Title}: {ex.Message}");
        }
    }

    private NAudio.Wave.SampleProviders.VolumeSampleProvider? gain;
    private SpectrumProvider? spectrum;
    internal float[] Spectrum => State == "Playing" && spectrum is not null ? spectrum.Levels : [];

    private sealed class SpectrumProvider(ISampleProvider input) : ISampleProvider
    {
        private const int FftLength = 1024;
        private readonly Complex[] bins = new Complex[FftLength];
        private readonly float[] window = Enumerable.Range(0, FftLength)
            .Select(i => (float)FastFourierTransform.HannWindow(i, FftLength)).ToArray();
        private float[] levels = new float[40];
        private int filled;

        public WaveFormat WaveFormat => input.WaveFormat;
        internal float[] Levels => Volatile.Read(ref levels);

        public int Read(float[] buffer, int offset, int count)
        {
            var read = input.Read(buffer, offset, count);
            var channels = WaveFormat.Channels;
            for (var index = offset; index + channels <= offset + read; index += channels)
            {
                float sum = 0;
                for (var channel = 0; channel < channels; channel++) sum += buffer[index + channel];
                bins[filled] = new Complex { X = sum / channels * window[filled] };
                if (++filled == FftLength)
                {
                    Publish();
                    filled = 0;
                }
            }
            return read;
        }

        private void Publish()
        {
            FastFourierTransform.FFT(true, 10, bins);
            var next = new float[40];
            var start = 1;
            for (var band = 0; band < next.Length; band++)
            {
                var end = Math.Max(start + 1, (int)(Math.Pow(500, (band + 1.0) / next.Length)));
                float peak = 0;
                for (var bin = start; bin < end; bin++)
                    peak = Math.Max(peak, bins[bin].X * bins[bin].X + bins[bin].Y * bins[bin].Y);
                next[band] = (float)Math.Clamp((10 * Math.Log10(Math.Max(peak, 1e-10f)) + 70) / 57, 0, 1);
                start = end;
            }
            Volatile.Write(ref levels, next);
        }
    }

    internal void Toggle()
    {
        if (output is null) return;
        if (State == "Playing")
        {
            output.Pause();
            SetState("Paused", "Paused.");
        }
        else if (State == "Paused")
        {
            output.Play();
            SetState("Playing", "Playing.");
        }
    }

    internal void Seek(double seconds)
    {
        if (reader is not null && Duration > TimeSpan.Zero)
            reader.CurrentTime = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, Duration.TotalSeconds));
    }

    internal void Volume(double value)
    {
        volume = value;
        if (gain is not null) gain.Volume = (float)value;
    }

    internal void Stop()
    {
        Close();
        SetState("Stopped", "Stopped.");
    }

    private void SetState(string state, string message)
    {
        State = state;
        Changed?.Invoke(message);
    }

    private void Close()
    {
        var old = output;
        output = null;
        old?.Dispose();
        gain = null;
        spectrum = null;
        reader?.Dispose();
        reader = null;
    }

    public void Dispose() => Close();
}
