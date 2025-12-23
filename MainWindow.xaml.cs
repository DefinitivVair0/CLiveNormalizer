using AtgDev.Voicemeeter;
using AtgDev.Voicemeeter.Utils;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using ScottPlot;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace CLiveNormalizer;

public partial class MainWindow : Window
{
    //Public variables
    public VolumeProvider provider;

    //Private variables
    private System.Windows.Forms.Timer mainTimer;
    private readonly List<MMDevice> mMDevices = [];
    private readonly RemoteApiWrapper vmApi;

    private bool bus, isRunning = false;
    private int api, busNr, fadeRate, plotlen, timeUntilTick;
    private float returnTo, threshold;
    private double[] ys, ysAvg;


    //Initialization
    public MainWindow()
    {
        InitializeComponent();

        LoadSettings();

        Plot_DB.Plot.PlotControl.UserInputProcessor.IsEnabled = false;
        L_Value.Content = $"Current:\t{-60:+00.0;-00.0}dB\nAverage:\t{-60:+00.0;-00.0}dB\nTimer active:   N";

        BTN_Bus.Content = bus ? "Bus" : "Strip";

        try
        {
            vmApi = new RemoteApiWrapper(PathHelper.GetDllPath());
        }
        catch { MessageBox.Show("Error getting VBVMR-API dll", "VBVMR-API Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }

        if (vmApi.Login() < 0) { MessageBox.Show("Failed To Login", "VBVMR-API Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }
    }


    //Settings management
    private void LoadSettings()
    {
        var settings = Properties.User.Default;
        CkB_Format.IsChecked = settings.Format;
        TB_Samplerate.Text = settings.Samplerate;
        TB_Channels.Text = settings.Channels;
        TB_Time.Text = settings.Time;
        TB_Dbcorr.Text = settings.Dbcorr;
        TB_Bus.Text = settings.BusNr;
        TB_Threshold.Text = settings.Threshold;
        TB_ReturnTo.Text = settings.ReturnTo;
        TB_FadeTime.Text = settings.FadeTime;
        bus = settings.Bus;
    }

    private void SaveSettings()
    {
        var settings = Properties.User.Default;
        settings.Format = (bool)CkB_Format.IsChecked;
        settings.Samplerate = TB_Samplerate.Text;
        settings.Channels = TB_Channels.Text;
        settings.Time = TB_Time.Text;
        settings.Dbcorr = TB_Dbcorr.Text;
        settings.BusNr = TB_Bus.Text;
        settings.Threshold = TB_Threshold.Text;
        settings.ReturnTo = TB_ReturnTo.Text;
        settings.FadeTime = TB_FadeTime.Text;
        settings.Bus = bus;

        settings.Save();
    }


    //Interface
    private void BTN_Start_Click(object sender, RoutedEventArgs e)
    {
        if (isRunning) { MessageBox.Show("Already running.", "Information", MessageBoxButton.OK, MessageBoxImage.Information); return; }

        if (CB_Device.SelectedIndex == -1)
        {
            MessageBox.Show("No device selected. Check your settings!", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        int channels, srate;
        float dbcorr;
        try
        {
            channels = Int32.Parse(TB_Channels.Text);
            srate = Int32.Parse(TB_Samplerate.Text) * 1000;
            plotlen = Int32.Parse(TB_Time.Text) * 60;
            dbcorr = float.Parse(TB_Dbcorr.Text);
            threshold = float.Parse(TB_Threshold.Text);
            fadeRate = Int32.Parse(TB_FadeTime.Text);
            busNr = Int32.Parse(TB_Bus.Text);
            returnTo = float.Parse(TB_ReturnTo.Text);
        }
        catch
        {
            MessageBox.Show("Error parsing input. Check your settings!", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if ((channels <= 0) || (channels > 8) || (srate < 8000) || (srate > 192000))
        {
            MessageBox.Show("Unreasonable input. Check the sample rate and channels!", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Plot_DB.Plot.Clear();



        ys = Generate.Repeating(plotlen, -60);
        ysAvg = Generate.Repeating(plotlen, -60);

        Plot_DB.Plot.Axes.SetLimits(-plotlen, -1, -60, 0 + dbcorr);

        Plot_DB.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericFixedInterval() { Interval = 60, LabelFormatter = Helpers.MinuteLabelFormatter };
        Plot_DB.Plot.Add.ScatterLine(Generate.Consecutive(plotlen, first: -plotlen), ys);
        Plot_DB.Plot.Add.ScatterLine(Generate.Consecutive(plotlen, first: -plotlen), ysAvg).Smooth = true;


        Plot_DB.Refresh();


        if (api != 1) //WaveIn / WASAPI / WASAPIlb
        {
            WaveFormat wf = (CkB_Format.IsChecked ?? false) ? WaveFormat.CreateIeeeFloatWaveFormat(srate, channels) :
            new WaveFormat(srate, 24, channels);

            provider = api == 0 ? new WaveInVolumeProvider(ys, ysAvg, dbcorr, CB_Device.SelectedIndex, wf) : new WasapiVolumeProvider(ys, ysAvg, dbcorr, api == 3, mMDevices.ElementAt(CB_Device.SelectedIndex), wf);
        }
        else //ASIO
        {
            AsioOut device = new(CB_Device.SelectedItem.ToString()) { InputChannelOffset = CB_Channel.SelectedIndex };

            provider = new AsioVolumeProvider(ys, ysAvg, dbcorr, device, srate);
        }

        timeUntilTick = plotlen;
        mainTimer = new() { Interval = 1000 };
        mainTimer.Tick += new EventHandler(MainTimer_Tick);


        mainTimer.Start();
        provider.Start();
        isRunning = true;
    }

    private void BTN_Restart_Click(object sender, RoutedEventArgs e)
    {
        vmApi.Logout();

        BTN_Stop_Click(BTN_Stop, null);

        provider = null;

        Task.Delay(3000);

        BTN_Start_Click(BTN_Start, null);

        vmApi.Login();
    }

    private void BTN_Stop_Click(object sender, RoutedEventArgs e)
    {
        if (isRunning) provider.Stop();
        mainTimer.Stop();
        isRunning = false;
    }

    private void BTN_Bus_Click(object sender, RoutedEventArgs e)
    {
        if (BTN_Bus.Content == "Bus")
        {
            BTN_Bus.Content = "Strip";
            bus = false;
        }
        else
        {
            BTN_Bus.Content = "Bus";
            bus = true;
        }
    }

    private void GetDevices(object sender, SelectionChangedEventArgs e)
    {
        api = CB_Api.SelectedIndex;

        CB_Device.Items.Clear();

        if (api == 1)
        {
            CkB_Format.IsEnabled = false;
            TB_Channels.IsEnabled = false;
        }
        else
        {
            CkB_Format.IsEnabled = true;
            TB_Channels.IsEnabled = true;
        }

        if (api == 0) //WaveIn
        {
            int WIdevct = WaveIn.DeviceCount;
            for (int i = 0; i < WIdevct; i++)
            {
                WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(i);
                CB_Device.Items.Add(deviceInfo.ProductName);
            }
        }
        else if (api == 1) //ASIO
        {
            if (!AsioOut.isSupported())
            {
                MessageBox.Show("ASIO not supported", "ASIO Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string[] ASIOdevs = AsioOut.GetDriverNames();
            foreach (string s in ASIOdevs) { CB_Device.Items.Add(s); }
        }
        else //WASAPI
        {
            mMDevices.Clear();

            MMDeviceEnumerator enumerator = new();
            foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(api == 2 ? DataFlow.Capture : DataFlow.Render, DeviceState.Active))
            {
                CB_Device.Items.Add(device.FriendlyName);
                mMDevices.Add(device);
            }
        }
    }

    private void SetDevice(object sender, SelectionChangedEventArgs e)
    {
        if (api == 1)
        {
            try
            {
                AsioOut asioOut = new(CB_Device.SelectedItem.ToString());

                int ASIOchanct = asioOut.DriverInputChannelCount;
                for (int i = 0; i < ASIOchanct; i++)
                {
                    CB_Channel.Items.Add(i);
                }

                asioOut.Dispose();
            }
            catch
            {
                MessageBox.Show("Error opening ASIO device. Is it used by another application?", "ASIO device error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CB_Channel.SelectedIndex = 0;
            CB_Channel.IsEnabled = true;
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        vmApi.Logout();
        if (isRunning) provider.Stop();
        SaveSettings();
    }


    //Run logic
    private void MainTimer_Tick(object? sender, EventArgs e)
    {
        if (timeUntilTick > 0) timeUntilTick--;
        else
        {
            timeUntilTick = plotlen;

            double lastAvg = ysAvg[^1];
            if (lastAvg > threshold)
            {
                float diff = (int)(returnTo - lastAvg);
                float fadeTime = Math.Abs(diff / fadeRate) * 60000;
                FadeAsync((int)diff, (int)fadeTime);
            }
        }
    }

    private async Task FadeAsync(int diff, int fadeTime)
    {
        mainTimer.Enabled = false;
        vmApi.SetParameter($"{(bus ? "Bus" : "Strip")}[{busNr}].FadeBy", $"{diff}, {fadeTime}").ToString();
        await Task.Delay(fadeTime);
        mainTimer.Enabled = true;
    }

    public void RefreshUI(double _dbfs, double _avg)
    {
        Plot_DB.Refresh();
        L_Value.Content = $"Current:\t{_dbfs:+00.0;-00.0;}dB\nAverage:\t{_avg:+00.0;-00.0;}dB\nTimer active:   {(mainTimer.Enabled ? "Y" : "N")}\nTick in:\t{timeUntilTick}s";
    }
}