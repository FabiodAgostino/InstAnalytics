using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using InstAnalytics.Models;
using InstAnalytics.Services;
using InstAnalytics.ViewModels;
using Microsoft.Win32;

namespace InstAnalytics;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly InstagramAnalyzerService _analyzer;
    private readonly HistoricalDataService _historicalDataService;
    private readonly StatisticsViewModel _statisticsViewModel;
    private string? _zipFilePath;
    private string? _oldZipFilePath;
    private bool? _analyzed;


    public MainWindow()
    {
        InitializeComponent();
        _analyzer = new InstagramAnalyzerService();
        _historicalDataService = new HistoricalDataService();
        _statisticsViewModel = new StatisticsViewModel();

        // Set DataContext for statistics
        this.DataContext = this;

        // Load historical data on startup
        Loaded += MainWindow_Loaded;
    }

    public StatisticsViewModel StatisticsViewModel => _statisticsViewModel;

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadHistoricalDataAsync();
    }

    private async Task LoadHistoricalDataAsync()
    {
        try
        {
            var analyses = await _historicalDataService.LoadStatisticsAsync();
            _statisticsViewModel.UpdateData(analyses);

            // Update DataGrid
            HistoricalDataGrid.ItemsSource = analyses;

            // Update chart
            UpdateTrendsChart(analyses);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading historical data: {ex.Message}");
        }
    }

    private void UpdateTrendsChart(List<AnalysisRecord> analyses)
    {
        TrendsChart.Plot.Clear();

        if (!analyses.Any())
        {
            TrendsChart.Refresh();
            return;
        }

        var orderedAnalyses = analyses.OrderBy(a => a.Timestamp).ToList();

        // Prepare data for plotting
        var xValues = orderedAnalyses.Select((_, index) => (double)index).ToArray();
        var followersValues = orderedAnalyses.Select(a => (double)a.FollowersCount).ToArray();
        var followingValues = orderedAnalyses.Select(a => (double)a.FollowingCount).ToArray();

        // Add line plots with tooltips
        var followersPlot = TrendsChart.Plot.Add.Scatter(xValues, followersValues);
        followersPlot.LegendText = "Followers";
        followersPlot.Color = ScottPlot.Color.FromHex("#4ECCA3");
        followersPlot.LineWidth = 3;
        followersPlot.MarkerSize = 8;

        var followingPlot = TrendsChart.Plot.Add.Scatter(xValues, followingValues);
        followingPlot.LegendText = "Following";
        followingPlot.Color = ScottPlot.Color.FromHex("#FFD93D");
        followingPlot.LineWidth = 3;
        followingPlot.MarkerSize = 8;

        // Configure axes
        TrendsChart.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(
            xValues.Select((x, i) => new ScottPlot.Tick(x, orderedAnalyses[i].Timestamp.ToString("dd/MM"))).ToArray()
        );

        // Style the plot
        TrendsChart.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#1A1A2E");
        TrendsChart.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#1A1A2E");
        TrendsChart.Plot.Axes.Color(ScottPlot.Color.FromHex("#A0A0A0"));
        TrendsChart.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#2A2A3E");

        // Show legend
        TrendsChart.Plot.ShowLegend();
        TrendsChart.Plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#16213E");
        TrendsChart.Plot.Legend.FontColor = ScottPlot.Color.FromHex("#EAEAEA");
        TrendsChart.Plot.Legend.OutlineColor = ScottPlot.Color.FromHex("#2A2A3E");

        // Set default zoom to 80% (zoom out a bit)
        TrendsChart.Plot.Axes.AutoScale();
        var currentLimits = TrendsChart.Plot.Axes.GetLimits();
        var xCenter = (currentLimits.Left + currentLimits.Right) / 2;
        var yCenter = (currentLimits.Bottom + currentLimits.Top) / 2;
        var xRange = (currentLimits.Right - currentLimits.Left) * 1.25; // 1/0.8 = 1.25
        var yRange = (currentLimits.Top - currentLimits.Bottom) * 1.25;

        TrendsChart.Plot.Axes.SetLimits(
            xCenter - xRange / 2,
            xCenter + xRange / 2,
            yCenter - yRange / 2,
            yCenter + yRange / 2
        );

        // Enable crosshair for interactive tooltip
        var crosshair = TrendsChart.Plot.Add.Crosshair(0, 0);
        crosshair.IsVisible = false;
        crosshair.LineColor = ScottPlot.Color.FromHex("#A0A0A0");

        // Store analyses for tooltip access
        var analysesForTooltip = orderedAnalyses;

        // Handle mouse move for tooltip
        TrendsChart.MouseMove += (s, e) =>
        {
            var mousePixel = new ScottPlot.Pixel(e.GetPosition(TrendsChart).X, e.GetPosition(TrendsChart).Y);
            var mouseLocation = TrendsChart.Plot.GetCoordinates(mousePixel);

            // Find nearest point
            int nearestIndex = -1;
            double minDistance = double.MaxValue;

            for (int i = 0; i < xValues.Length; i++)
            {
                double distance = Math.Abs(mouseLocation.X - xValues[i]);
                if (distance < minDistance && distance < 0.5) // Within 0.5 units
                {
                    minDistance = distance;
                    nearestIndex = i;
                }
            }

            if (nearestIndex >= 0 && nearestIndex < analysesForTooltip.Count)
            {
                var analysis = analysesForTooltip[nearestIndex];
                var xPos = xValues[nearestIndex];

                // Determine which line is closer
                var followersY = followersValues[nearestIndex];
                var followingY = followingValues[nearestIndex];
                var distToFollowers = Math.Abs(mouseLocation.Y - followersY);
                var distToFollowing = Math.Abs(mouseLocation.Y - followingY);

                string tooltipText;
                double yPos;

                if (distToFollowers < distToFollowing)
                {
                    tooltipText = $"Followers: {analysis.FollowersCount:N0}\nData: {analysis.Timestamp:dd/MM/yyyy HH:mm}";
                    yPos = followersY;
                }
                else
                {
                    tooltipText = $"Following: {analysis.FollowingCount:N0}\nData: {analysis.Timestamp:dd/MM/yyyy HH:mm}";
                    yPos = followingY;
                }

                // Position crosshair
                crosshair.IsVisible = true;
                crosshair.Position = new ScottPlot.Coordinates(xPos, yPos);

                // Update tooltip
                var tooltip = TrendsChart.Plot.Add.Text(tooltipText, xPos, yPos);
                tooltip.LabelBackgroundColor = ScottPlot.Color.FromHex("#16213E");
                tooltip.LabelFontColor = ScottPlot.Color.FromHex("#EAEAEA");
                tooltip.LabelBorderColor = ScottPlot.Color.FromHex("#A0A0A0");
                tooltip.LabelFontSize = 12;
                tooltip.LabelPadding = 8;
                tooltip.OffsetY = -40;

                TrendsChart.Refresh();

                // Remove old tooltips (keep only the last one)
                var textLabels = TrendsChart.Plot.GetPlottables().OfType<ScottPlot.Plottables.Text>().ToList();
                if (textLabels.Count > 1)
                {
                    for (int i = 0; i < textLabels.Count - 1; i++)
                    {
                        TrendsChart.Plot.Remove(textLabels[i]);
                    }
                }
            }
            else
            {
                crosshair.IsVisible = false;

                // Remove all text labels when not hovering
                var textLabels = TrendsChart.Plot.GetPlottables().OfType<ScottPlot.Plottables.Text>().ToList();
                foreach (var label in textLabels)
                {
                    TrendsChart.Plot.Remove(label);
                }

                TrendsChart.Refresh();
            }
        };

        // Hide tooltip when mouse leaves
        TrendsChart.MouseLeave += (s, e) =>
        {
            crosshair.IsVisible = false;

            // Remove all text labels
            var textLabels = TrendsChart.Plot.GetPlottables().OfType<ScottPlot.Plottables.Text>().ToList();
            foreach (var label in textLabels)
            {
                TrendsChart.Plot.Remove(label);
            }

            TrendsChart.Refresh();
        };

        // Refresh the plot
        TrendsChart.Refresh();
    }

    #region Window Controls

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    #endregion

    #region File Selection

    private void SelectZipFile_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Seleziona file ZIP Instagram",
            Filter = "ZIP Files (*.zip)|*.zip|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (openFileDialog.ShowDialog() == true)
        {
            _zipFilePath = openFileDialog.FileName;
            ZipFileTextBox.Text = Path.GetFileName(_zipFilePath);
            AnalyzeButton.IsEnabled = true;
        }
    }

    #endregion

    #region Analysis

    private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_zipFilePath))
        {
            MessageBox.Show("Seleziona il file ZIP prima di avviare l'analisi.",
                          "File Mancante",
                          MessageBoxButton.OK,
                          MessageBoxImage.Warning);
            return;
        }

        if(_zipFilePath == _oldZipFilePath && _analyzed == true)
        {
            MessageBox.Show("Hai già analizzato questo file ZIP.\n\n",
                          "Analisi Duplicata",
                          MessageBoxButton.OK,
                          MessageBoxImage.Information);
            return;
        }

        _analyzed = true;
        _oldZipFilePath = _zipFilePath;

        InstagramZipService? zipService = null;

        try
        {
            // Disable button during analysis
            AnalyzeButton.IsEnabled = false;
            // Open ZIP file
            zipService = new InstagramZipService();
            await zipService.OpenZipAsync(_zipFilePath);

            // Validate ZIP structure
            if (!zipService.ValidateZipStructure())
            {
                MessageBox.Show("Il file ZIP non contiene la struttura corretta di Instagram.\n\n" +
                              "Assicurati di aver selezionato il file ZIP esportato da Instagram/Meta.",
                              "Struttura ZIP Non Valida",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
                return;
            }

            // List all files in followers_and_following directory
            var allFiles = zipService.ListFollowersAndFollowingFiles();
            System.Diagnostics.Debug.WriteLine($"DEBUG: Files in followers_and_following directory:");
            foreach (var file in allFiles)
            {
                System.Diagnostics.Debug.WriteLine($"  - {file}");
            }

            var followersFileCount = zipService.GetFollowersFileCount();
            System.Diagnostics.Debug.WriteLine($"DEBUG: Found {followersFileCount} followers file(s) in ZIP");

            if (followersFileCount > 1)
            {
                MessageBox.Show($"Trovati {followersFileCount} file followers nel ZIP.\n" +
                              "Verranno combinati automaticamente.",
                              "File Multipli",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
            else if (followersFileCount == 1)
            {
                MessageBox.Show($"⚠️ ATTENZIONE ⚠️\n\n" +
                              $"Il tuo profilo ha 10.700+ followers, ma nel ZIP è presente solo 1 file (circa 3.000 utenti).\n\n" +
                              $"Instagram/Meta NON esporta tutti i dati nel ZIP standard.\n\n" +
                              $"Per ottenere TUTTI i dati, devi:\n" +
                              $"1. Andare su Instagram.com (non l'app)\n" +
                              $"2. Impostazioni → Privacy e sicurezza\n" +
                              $"3. Richiedere 'Dati completi del tuo account'\n" +
                              $"4. Scegliere 'JSON' come formato (non HTML)\n\n" +
                              $"Il file HTML che hai ora contiene solo un sottoinsieme dei dati.",
                              "Dati Incompleti",
                              MessageBoxButton.OK,
                              MessageBoxImage.Warning);
            }

            // Detect format (JSON or HTML)
            var isJsonFormat = zipService.IsJsonFormat();
            System.Diagnostics.Debug.WriteLine($"DEBUG: Detected format: {(isJsonFormat ? "JSON" : "HTML")}");

            string followersContent;
            string followingContent;
            (long Size, DateTime LastModified) followersMetadata;
            (long Size, DateTime LastModified) followingMetadata;

            if (isJsonFormat)
            {
                // Extract JSON files
                followersContent = await zipService.ExtractFollowersJsonAsync();
                followingContent = await zipService.ExtractFollowingJsonAsync();

                // Get JSON metadata
                followersMetadata = zipService.GetFollowersJsonMetadata();
                followingMetadata = zipService.GetFollowingJsonMetadata();
            }
            else
            {
                // Extract HTML files
                followersContent = await zipService.ExtractFollowersHtmlAsync();
                followingContent = await zipService.ExtractFollowingHtmlAsync();

                // Get HTML metadata
                followersMetadata = zipService.GetFollowersMetadata();
                followingMetadata = zipService.GetFollowingMetadata();
            }

            // Calculate hashes
            var followersHash = InstagramZipService.CalculateFileHash(followersContent);
            var followingHash = InstagramZipService.CalculateFileHash(followingContent);

            // Check for duplicates
            var isDuplicate = await _historicalDataService.IsDuplicateAnalysisAsync(followersHash, followingHash);

            if (isDuplicate)
            {
                var existingAnalysis = await _historicalDataService.GetAnalysisByHashAsync(followersHash, followingHash);
                var result = MessageBox.Show(
                    $"Questi file sono già stati analizzati il {existingAnalysis?.Timestamp:dd/MM/yyyy HH:mm}.\n\n" +
                    "Vuoi ri-analizzare comunque?",
                    "Analisi Duplicata",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            // Parse data based on format
            List<InstagramUser> followers;
            List<InstagramUser> following;

            if (isJsonFormat)
            {
                // Save JSON to temp files
                var tempFollowersPath = Path.Combine(Path.GetTempPath(), "followers_temp.json");
                var tempFollowingPath = Path.Combine(Path.GetTempPath(), "following_temp.json");

                await File.WriteAllTextAsync(tempFollowersPath, followersContent);
                await File.WriteAllTextAsync(tempFollowingPath, followingContent);

                // Use JSON parsers
                var followersParser = new InstagramFollowersJsonParser();
                var followingParser = new InstagramFollowingJsonParser();

                followers = await followersParser.ParseAsync(tempFollowersPath);
                following = await followingParser.ParseAsync(tempFollowingPath);

                // Clean up temp files
                File.Delete(tempFollowersPath);
                File.Delete(tempFollowingPath);
            }
            else
            {
                // Save HTML to temp files
                var tempFollowersPath = Path.Combine(Path.GetTempPath(), "followers_temp.html");
                var tempFollowingPath = Path.Combine(Path.GetTempPath(), "following_temp.html");

                await File.WriteAllTextAsync(tempFollowersPath, followersContent);
                await File.WriteAllTextAsync(tempFollowingPath, followingContent);

                // DEBUG: Analyze HTML structure (only for HTML format)
                if (!isJsonFormat)
                {
                    var followersDebug = await InstagramParserDebugger.AnalyzeHtmlStructureAsync(tempFollowersPath);
                    var followingDebug = await InstagramParserDebugger.AnalyzeHtmlStructureAsync(tempFollowingPath);
                    System.Diagnostics.Debug.WriteLine("===== FOLLOWERS DEBUG =====");
                    System.Diagnostics.Debug.WriteLine(followersDebug);
                    System.Diagnostics.Debug.WriteLine("===== FOLLOWING DEBUG =====");
                    System.Diagnostics.Debug.WriteLine(followingDebug);
                }

                // Use HTML parsers (via analyzer service)
                followers = await _analyzer.GetFollowersAsync(tempFollowersPath);
                following = await _analyzer.GetFollowingAsync(tempFollowingPath);

                // Clean up temp files
                File.Delete(tempFollowersPath);
                File.Delete(tempFollowingPath);
            }

            // Debug: Show extracted counts
            System.Diagnostics.Debug.WriteLine($"DEBUG: Extracted {followers.Count} followers and {following.Count} following from {(isJsonFormat ? "JSON" : "HTML")} format");

            // Calculate relationships
            var followersUsernames = followers.Select(f => f.Username).ToHashSet();
            var followingUsernames = following.Select(f => f.Username).ToHashSet();

            var notFollowingBack = following.Where(f => !followersUsernames.Contains(f.Username)).ToList();
            var notFollowing = followers.Where(f => !followingUsernames.Contains(f.Username)).ToList();
            var mutualFollowers = followers.Where(f => followingUsernames.Contains(f.Username)).ToList();

            // Save to historical data
            var followersList = followers.Select(f => f.Username).ToList();
            var followingList = following.Select(f => f.Username).ToList();

            await _historicalDataService.SaveAnalysisAsync(
                followersList,
                followingList,
                followersHash,
                followingHash,
                followersMetadata.LastModified,
                followingMetadata.LastModified);

            // Update Analysis Tab
            TotalFollowersCountText.Text = followers.Count.ToString();
            TotalFollowingCountText.Text = following.Count.ToString();
            NotFollowingBackCountText.Text = notFollowingBack.Count.ToString();
            NotFollowingCountText.Text = notFollowing.Count.ToString();
            MutualFollowersCountText.Text = mutualFollowers.Count.ToString();

            NotFollowingBackListBox.ItemsSource = notFollowingBack.Select(u => u.Username).ToList();
            NotFollowingListBox.ItemsSource = notFollowing.Select(u => u.Username).ToList();
            MutualFollowersListBox.ItemsSource = mutualFollowers.Select(u => u.Username).ToList();

            // Show results
            ResultsCard.Visibility = Visibility.Visible;

            // Reload historical data to update statistics
            await LoadHistoricalDataAsync();
        }
        catch (FileNotFoundException ex)
        {
            MessageBox.Show($"File non trovato: {ex.Message}",
                          "Errore File",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        }
        catch (InvalidDataException ex)
        {
            MessageBox.Show($"File ZIP non valido: {ex.Message}",
                          "Errore ZIP",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show($"Errore durante l'estrazione: {ex.Message}",
                          "Errore",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore durante l'analisi: {ex.Message}",
                          "Errore",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        }
        finally
        {
            // Clean up
            zipService?.Dispose();
            AnalyzeButton.IsEnabled = true;
        }
    }

    #endregion

    #region Modal Management

    private void InfoButton_Click(object sender, RoutedEventArgs e)
    {
        InfoModalOverlay.Visibility = Visibility.Visible;
    }

    private void CloseModal_Click(object sender, RoutedEventArgs e)
    {
        InfoModalOverlay.Visibility = Visibility.Collapsed;
    }

    private void ModalContent_Click(object sender, MouseButtonEventArgs e)
    {
        // Prevent closing when clicking inside the modal content
        e.Handled = true;
    }

    #endregion

    #region Statistics Management

    private async void DeleteAnalysis_Click(object sender, RoutedEventArgs e)
    {
        if (HistoricalDataGrid.SelectedItem is not AnalysisRecord selectedAnalysis)
        {
            MessageBox.Show("Seleziona un'analisi da eliminare.",
                          "Nessuna Selezione",
                          MessageBoxButton.OK,
                          MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Vuoi eliminare l'analisi del {selectedAnalysis.Timestamp:dd/MM/yyyy HH:mm}?\n\n" +
            $"Questa operazione eliminerà anche i file dati associati e non può essere annullata.",
            "Conferma Eliminazione",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            // Delete the analysis
            await _historicalDataService.DeleteAnalysisAsync(selectedAnalysis.Timestamp);

            // Reload historical data
            await LoadHistoricalDataAsync();

            MessageBox.Show("Analisi eliminata con successo!",
                          "Eliminazione Completata",
                          MessageBoxButton.OK,
                          MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore durante l'eliminazione: {ex.Message}",
                          "Errore",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        }
    }

    #endregion
}

/// <summary>
/// Converter to check if a value is not null
/// </summary>
public class NullToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for growth trend color based on value
/// </summary>
public class GrowthTrendColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double trendValue)
        {
            if (trendValue > 0)
                return "#6BCB77"; // Green for positive growth
            else if (trendValue < 0)
                return "#E94560"; // Red for negative growth
            else
                return "#EAEAEA"; // White for zero growth
        }
        return "#EAEAEA"; // Default white
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
