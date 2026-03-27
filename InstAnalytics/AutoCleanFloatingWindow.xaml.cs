using System.Windows;

namespace InstAnalytics;

public partial class AutoCleanFloatingWindow : Window
{
    /// <summary>Fired when the user clicks "Annulla" in the floating window.</summary>
    public event Action? CancelRequested;

    /// <summary>Fired when the user clicks "Chiudi" in the floating window after completion.</summary>
    public event Action? CloseRequested;

    /// <summary>True once the process is finished (progress panel hidden, done panel shown).</summary>
    public bool IsDone { get; private set; }

    public AutoCleanFloatingWindow()
    {
        InitializeComponent();
        PositionBottomRight();
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 20;
        Top  = area.Bottom - Height - 20;
    }

    /// <summary>Updates all progress values. Must be called from the UI thread.</summary>
    public void UpdateProgress(string progressText, double progressValue, string user, string status)
    {
        FloatProgressText.Text = progressText;
        FloatProgressBar.Value = progressValue;
        FloatUserText.Text     = user;
        FloatStatusText.Text   = status;
    }

    /// <summary>Switches the floating window to the "done" state.</summary>
    public void ShowDone(string summary)
    {
        IsDone = true;
        FloatHeaderText.Text = "🧹 Auto-Clean completato";
        FloatProgressPanel.Visibility = Visibility.Collapsed;
        FloatDonePanel.Visibility     = Visibility.Visible;
        FloatDoneSummaryText.Text     = summary;

        // Resize to fit done panel (smaller height)
        Height = 150;
        PositionBottomRight();

        // Always show when done, regardless of main window focus
        Show();
    }

    private void FloatCancel_Click(object sender, RoutedEventArgs e)
        => CancelRequested?.Invoke();

    private void FloatClose_Click(object sender, RoutedEventArgs e)
        => CloseRequested?.Invoke();
}
