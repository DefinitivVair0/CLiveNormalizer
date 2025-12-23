using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Windows;

namespace CLiveNormalizer;

public sealed class WaveInVolumeProvider : VolumeProvider
{
    private readonly WaveInEvent capture;

    public WaveInVolumeProvider(double[] peaks, double[] avgs, float dbcorr, int deviceNr, WaveFormat waveFormat)
    {
        _dbCorr = dbcorr;
        ieeeFloat = waveFormat.Encoding.ToString() == "IeeeFloat";

        window = Application.Current.MainWindow;
        _peaks = peaks;
        _avgs = avgs;
        stPeaks = [];
        timer = new() { Interval = 1000 };
        timer.Tick += new EventHandler(Timer_Tick);

        capture = new()
        {
            DeviceNumber = deviceNr,
            WaveFormat = waveFormat
        };

        capture.DataAvailable += ieeeFloat ? OnDataAvailableFP : OnDataAvailablePCM;
    }

    public override Task Start()
    {
        try
        {
            capture.StartRecording();
        }
        catch { MessageBox.Show("Error opening WaveIn stream. Check the sample rate and channels!", "Device Error", MessageBoxButton.OK, MessageBoxImage.Error); }

        timer.Start();

        return Task.CompletedTask;
    }

    public override Task Stop()
    {
        timer.Stop();
        capture.StopRecording();

        return Task.CompletedTask;
    }

    private void OnDataAvailableFP(object sender, WaveInEventArgs args)
    {
        float max = 0;
        float[] buffer = new WaveBuffer(args.Buffer).FloatBuffer;

        for (int index = 0; index < args.BytesRecorded / 4; index++)
        {
            var sample = Math.Abs(buffer[index]);

            if (sample > max) max = sample;
        }

        stPeaks.Add(max);
    }

    private void OnDataAvailablePCM(object sender, WaveInEventArgs args)
    {
        float max = 0;
        for (int i = 0; i < args.BytesRecorded; i += 3)
        {
            int sample = args.Buffer[i] | (args.Buffer[i + 1] << 8) | (args.Buffer[i + 2] << 16);

            if ((sample & 0x800000) != 0) sample |= unchecked((int)0xFF000000);

            sample = Math.Abs(sample);

            if (sample > max) max = sample;
        }

        stPeaks.Add(max);
    }
}

public sealed class WasapiVolumeProvider : VolumeProvider
{
    private readonly object capture;
    private readonly bool _lb;

    public WasapiVolumeProvider(double[] peaks, double[] avgs, float dbcorr, bool lb, MMDevice device, WaveFormat waveFormat)
    {
        _dbCorr = dbcorr;
        _lb = lb;
        ieeeFloat = waveFormat.Encoding.ToString() == "IeeeFloat";

        window = Application.Current.MainWindow;
        _peaks = peaks;
        _avgs = avgs;
        stPeaks = [];
        timer = new() { Interval = 1000 };
        timer.Tick += new EventHandler(Timer_Tick);

        capture = _lb ? new WasapiLoopbackCapture(device) { WaveFormat = waveFormat } :
            new WasapiCapture(device) { WaveFormat = waveFormat };

        if (_lb) { (capture as WasapiLoopbackCapture).DataAvailable += ieeeFloat ? OnDataAvailableFP : OnDataAvailablePCM; }
        else { (capture as WasapiCapture).DataAvailable += ieeeFloat ? OnDataAvailableFP : OnDataAvailablePCM; }
    }

    public override Task Start()
    {
        try
        {
            if (_lb) { (capture as WasapiLoopbackCapture).StartRecording(); }
            else { (capture as WasapiCapture).StartRecording(); }
        }
        catch { MessageBox.Show("Error opening WASAPI stream. Check the sample rate and channels!", "Device Error", MessageBoxButton.OK, MessageBoxImage.Error); }

        timer.Start();

        return Task.CompletedTask;
    }

    public override Task Stop()
    {
        timer.Stop();

        if (_lb) { (capture as WasapiLoopbackCapture).StopRecording(); }
        else { (capture as WasapiCapture).StopRecording(); }

        return Task.CompletedTask;
    }

    private void OnDataAvailableFP(object sender, WaveInEventArgs args)
    {
        float max = 0;
        float[] buffer = new WaveBuffer(args.Buffer).FloatBuffer;

        for (int index = 0; index < args.BytesRecorded / 4; index++)
        {
            var sample = Math.Abs(buffer[index]);

            if (sample > max) max = sample;
        }

        stPeaks.Add(max);
    }

    private void OnDataAvailablePCM(object sender, WaveInEventArgs args)
    {
        float max = 0;
        for (int i = 0; i < args.BytesRecorded; i += 3)
        {
            int sample = args.Buffer[i] | (args.Buffer[i + 1] << 8) | (args.Buffer[i + 2] << 16);

            if ((sample & 0x800000) != 0) sample |= unchecked((int)0xFF000000);

            sample = Math.Abs(sample);

            if (sample > max) max = sample;
        }

        stPeaks.Add(max);
    }
}

public sealed class AsioVolumeProvider : VolumeProvider
{
    private readonly AsioOut _device;

    public AsioVolumeProvider(double[] peaks, double[] avgs, float dbcorr, AsioOut device, int samplerate)
    {
        ieeeFloat = true;

        _device = device;
        _dbCorr = dbcorr;

        window = Application.Current.MainWindow;
        _peaks = peaks;
        _avgs = avgs;
        stPeaks = [];
        timer = new() { Interval = 1000 };
        timer.Tick += new EventHandler(Timer_Tick);

        _device.InitRecordAndPlayback(null, 1, samplerate);
        _device.AudioAvailable += OnDataAvailable;
    }

    public override Task Start()
    {
        try
        {
            _device.Play();
        }
        catch { MessageBox.Show("Error opening ASIO device. Check the sample rate and channel!", "Device Error", MessageBoxButton.OK, MessageBoxImage.Error); }

        timer.Start();

        return Task.CompletedTask;
    }

    public override Task Stop()
    {
        timer.Stop();

        _device.Stop();

        return Task.CompletedTask;
    }

    private void OnDataAvailable(object sender, AsioAudioAvailableEventArgs args)
    {
        float[] buffer = args.GetAsInterleavedSamples();

        stPeaks.Add(buffer.Select(Math.Abs).Max());
    }
}
