using System.ComponentModel;
using System.Runtime.CompilerServices;
using InstAnalytics.Models;

namespace InstAnalytics.ViewModels;

/// <summary>
/// ViewModel for Statistics tab with historical data and charts
/// </summary>
public class StatisticsViewModel : INotifyPropertyChanged
{
    private List<AnalysisRecord> _analyses = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Total number of analyses performed
    /// </summary>
    public int TotalAnalyses => _analyses.Count;

    /// <summary>
    /// Date range of analyses
    /// </summary>
    public string DateRange
    {
        get
        {
            if (!_analyses.Any()) return "Nessun dato";
            var oldest = _analyses.Min(a => a.Timestamp);
            var newest = _analyses.Max(a => a.Timestamp);
            return $"{oldest:dd/MM/yyyy} - {newest:dd/MM/yyyy}";
        }
    }

    /// <summary>
    /// Current followers count (latest analysis)
    /// </summary>
    public int CurrentFollowers => _analyses.Any() ? _analyses.OrderByDescending(a => a.Timestamp).First().FollowersCount : 0;

    /// <summary>
    /// Current following count (latest analysis)
    /// </summary>
    public int CurrentFollowing => _analyses.Any() ? _analyses.OrderByDescending(a => a.Timestamp).First().FollowingCount : 0;

    /// <summary>
    /// Overall growth trend percentage (numeric value)
    /// </summary>
    public double GrowthTrendValue
    {
        get
        {
            if (_analyses.Count < 2) return 0;
            var ordered = _analyses.OrderBy(a => a.Timestamp).ToList();
            var first = ordered.First();
            var last = ordered.Last();

            var change = last.FollowersCount - first.FollowersCount;
            var percentage = first.FollowersCount > 0 ? (double)change / first.FollowersCount * 100 : 0;

            return percentage;
        }
    }

    /// <summary>
    /// Overall growth trend percentage
    /// </summary>
    public string GrowthTrend
    {
        get
        {
            if (_analyses.Count < 2) return "N/A";
            var percentage = GrowthTrendValue;
            return percentage >= 0 ? $"+{percentage:F1}%" : $"{percentage:F1}%";
        }
    }

    /// <summary>
    /// Gets the analysis records for chart rendering
    /// </summary>
    public List<AnalysisRecord> Analyses => _analyses;

    /// <summary>
    /// Updates the view model with new analysis data
    /// </summary>
    public void UpdateData(List<AnalysisRecord> analyses)
    {
        _analyses = analyses.OrderBy(a => a.Timestamp).ToList();

        OnPropertyChanged(nameof(TotalAnalyses));
        OnPropertyChanged(nameof(DateRange));
        OnPropertyChanged(nameof(CurrentFollowers));
        OnPropertyChanged(nameof(CurrentFollowing));
        OnPropertyChanged(nameof(GrowthTrendValue));
        OnPropertyChanged(nameof(GrowthTrend));
        OnPropertyChanged(nameof(Analyses));
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
