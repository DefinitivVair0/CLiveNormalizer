using System.Windows;

namespace CLiveNormalizer;

public static class Helpers
{
    public static string MinuteLabelFormatter(double position) => $"{position / 60}";
}

public abstract class VolumeProvider()
{
    protected List<float> stPeaks;
    protected System.Windows.Forms.Timer timer;
    protected Window window;

    protected bool ieeeFloat;
    protected float _dbCorr;
    protected double[] _avgs, _peaks;

    public abstract Task Start();
    public abstract Task Stop();

    protected void Timer_Tick(object sender, EventArgs e)
    {
        double rms = Math.Sqrt(stPeaks.DefaultIfEmpty().Average(v => (v * v)));
        double dbfs = 20 * Math.Log10(ieeeFloat ? rms : rms / 8388608.0);

        dbfs += _dbCorr;
        if (dbfs < -60) dbfs = -60;

        ShiftAdd(dbfs);

        (window as MainWindow).RefreshUI(dbfs, _avgs[^1]);

        stPeaks.Clear();
    }

    protected void ShiftAdd(double d)
    {
        for (int i = 0; i < _peaks.Length - 1; i++)
        {
            _peaks[i] = _peaks[i + 1];
            _avgs[i] = _avgs[i + 1];
        }
        _peaks[^1] = d;
        _avgs[^1] = _peaks.Average();
    }
}
