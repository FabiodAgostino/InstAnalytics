using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
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
    private readonly RemovalHistoryService _removalHistoryService;
    private readonly StatisticsViewModel _statisticsViewModel;
    private readonly ExclusionService _exclusionService;
    private LocalHttpServer? _httpServer;
    private DispatcherTimer? _toastTimer;
    // Raw (pre-exclusion) results — re-filtered on every exclusion change
    private List<string> _rawNotFollowingBack = [];
    private List<string> _rawNotFollowing = [];
    private List<string> _rawMutualFollowers = [];
    private string? _zipFilePath;
    private string? _oldZipFilePath;
    private bool? _analyzed;

    // Auto-Clean state
    private Queue<string>? _autoCleanQueue;
    private int _autoCleanTotal;
    private int _autoCleanProcessed;
    private int _autoCleanUnfollowed;
    private int _autoCleanExcluded;
    private bool _autoCleanCancelled;
    private string _autoCleanCurrentUser = "";
    private AutoCleanFloatingWindow? _floatingWindow;


    public MainWindow()
    {
        InitializeComponent();
        _analyzer = new InstagramAnalyzerService();
        _historicalDataService = new HistoricalDataService();
        _removalHistoryService = new RemovalHistoryService();
        _statisticsViewModel = new StatisticsViewModel();
        _exclusionService = new ExclusionService();

        // Set DataContext for statistics
        this.DataContext = this;

        // Load historical data on startup
        Loaded += MainWindow_Loaded;

        // Floating window visibility: hide when main window is active, show when background
        Activated   += (_, _) => { if (_floatingWindow?.IsDone == false) _floatingWindow.Hide(); };
        Deactivated += (_, _) => { if (_autoCleanQueue != null) _floatingWindow?.Show(); };
    }

    public StatisticsViewModel StatisticsViewModel => _statisticsViewModel;

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _exclusionService.LoadAsync();
        InitializeHttpServer();
        await LoadHistoricalDataAsync();
        RefreshExcludedList();
    }

    private void InitializeHttpServer()
    {
        _httpServer = new LocalHttpServer(_exclusionService);
        _httpServer.UserExcluded += username =>
            Dispatcher.Invoke(() =>
            {
                ShowExclusionToast($"@{username} aggiunto agli esclusi");
                RefreshExcludedList();
                ApplyFiltersAndRefreshLists();
            });

        _httpServer.UnfollowResultReceived += (username, status) =>
            Dispatcher.Invoke(() => HandleUnfollowResult(username, status));

        _httpServer.Start();
        UpdateHttpServerStatus();
    }

    private void UpdateHttpServerStatus()
    {
        if (_httpServer is null) return;
        if (_httpServer.IsRunning)
        {
            HttpServerStatusText.Text =
                $"Server locale in esecuzione sulla porta {LocalHttpServer.Port}";
            HttpServerStatusText.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x4E, 0xCC, 0xA3));
        }
        else
        {
            HttpServerStatusText.Text =
                $"Server locale non disponibile (porta {LocalHttpServer.Port} occupata)";
            HttpServerStatusText.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xE9, 0x45, 0x60));
        }
    }

    private void RefreshExcludedList()
    {
        ExcludedListBox.ItemsSource = _exclusionService.GetAll();
    }

    private void ApplyFiltersAndRefreshLists()
    {
        var notFollowingBack  = _rawNotFollowingBack.Where(u => !_exclusionService.IsExcluded(u)).ToList();
        var notFollowing      = _rawNotFollowing.Where(u => !_exclusionService.IsExcluded(u)).ToList();
        var mutualFollowers   = _rawMutualFollowers.Where(u => !_exclusionService.IsExcluded(u)).ToList();

        NotFollowingBackListBox.ItemsSource = notFollowingBack;
        NotFollowingListBox.ItemsSource     = notFollowing;
        MutualFollowersListBox.ItemsSource  = mutualFollowers;

        if (ResultsCard.Visibility == Visibility.Visible)
        {
            NotFollowingBackCountText.Text   = notFollowingBack.Count.ToString();
            NotFollowingCountText.Text       = notFollowing.Count.ToString();
            MutualFollowersCountText.Text    = mutualFollowers.Count.ToString();
        }
    }

    private void ShowExclusionToast(string message)
    {
        ExclusionToastText.Text = message;
        ExclusionToast.Visibility = Visibility.Visible;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _toastTimer.Tick += (_, _) =>
        {
            ExclusionToast.Visibility = Visibility.Collapsed;
            _toastTimer?.Stop();
        };
        _toastTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _floatingWindow?.Close();
        _httpServer?.Dispose();
        base.OnClosed(e);
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

            // Load removal sessions
            var sessions = await _removalHistoryService.LoadSessionsAsync();
            UpdateRemovalChart(sessions);
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

    private void UpdateRemovalChart(List<InstAnalytics.Models.RemovalSession> sessions)
    {
        // Update summary cards
        TotalRemovalSessionsText.Text = sessions.Count.ToString();
        TotalUnfollowedText.Text = sessions.Sum(s => s.UnfollowedCount).ToString();
        TotalExcludedText.Text = sessions.Sum(s => s.ExcludedCount).ToString();

        RemovalChart.Plot.Clear();

        if (!sessions.Any())
        {
            RemovalChart.Refresh();
            return;
        }

        var ordered = sessions.OrderBy(s => s.Date).ToList();
        var xValues = ordered.Select((_, i) => (double)i).ToArray();
        var unfollowedValues = ordered.Select(s => (double)s.UnfollowedCount).ToArray();
        var excludedValues   = ordered.Select(s => (double)s.ExcludedCount).ToArray();

        var unfollowedPlot = RemovalChart.Plot.Add.Scatter(xValues, unfollowedValues);
        unfollowedPlot.LegendText = "Rimossi dagli amici";
        unfollowedPlot.Color = ScottPlot.Color.FromHex("#4ECCA3");
        unfollowedPlot.LineWidth = 3;
        unfollowedPlot.MarkerSize = 10;

        var excludedPlot = RemovalChart.Plot.Add.Scatter(xValues, excludedValues);
        excludedPlot.LegendText = "Rimossi dal tracking";
        excludedPlot.Color = ScottPlot.Color.FromHex("#FFD93D");
        excludedPlot.LineWidth = 3;
        excludedPlot.MarkerSize = 10;

        RemovalChart.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(
            xValues.Select((x, i) => new ScottPlot.Tick(x, ordered[i].Date.ToString("dd/MM"))).ToArray()
        );

        RemovalChart.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#1A1A2E");
        RemovalChart.Plot.DataBackground.Color   = ScottPlot.Color.FromHex("#1A1A2E");
        RemovalChart.Plot.Axes.Color(ScottPlot.Color.FromHex("#A0A0A0"));
        RemovalChart.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#2A2A3E");

        RemovalChart.Plot.ShowLegend();
        RemovalChart.Plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#16213E");
        RemovalChart.Plot.Legend.FontColor       = ScottPlot.Color.FromHex("#EAEAEA");
        RemovalChart.Plot.Legend.OutlineColor    = ScottPlot.Color.FromHex("#2A2A3E");

        RemovalChart.Plot.Axes.AutoScale();
        RemovalChart.Refresh();
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

            _rawNotFollowingBack = following
                .Where(f => !followersUsernames.Contains(f.Username))
                .Select(f => f.Username)
                .ToList();
            _rawNotFollowing = followers
                .Where(f => !followingUsernames.Contains(f.Username))
                .Select(f => f.Username)
                .ToList();
            _rawMutualFollowers = followers
                .Where(f => followingUsernames.Contains(f.Username))
                .Select(f => f.Username)
                .ToList();

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
            ApplyFiltersAndRefreshLists();

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

    #region Profile Links

    private void ProfileLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink link && link.Tag is string username)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                $"https://www.instagram.com/{username}/?ia_unfollow=1")
            { UseShellExecute = true });
        }
    }

    #endregion

    #region Exclusion Management

    private async void RemoveExclusion_Click(object sender, RoutedEventArgs e)
    {
        if (ExcludedListBox.SelectedItem is string username)
        {
            await _exclusionService.RemoveAsync(username);
            RefreshExcludedList();
            ApplyFiltersAndRefreshLists();
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

    #region Auto-Clean

    // ── Resume persistence ────────────────────────────────────────────────────

    private static readonly string _resumeFilePath = System.IO.Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "HistoricalData", "autoclean_resume.json");

    private static readonly System.Text.Json.JsonSerializerOptions _resumeJsonOpts =
        new() { WriteIndented = true };

    private sealed class AutoCleanResume
    {
        public List<string> RemainingUsers  { get; set; } = [];
        public int          Total           { get; set; }
        public int          ProcessedBefore { get; set; }
        public int          UnfollowedBefore{ get; set; }
        public int          ExcludedBefore  { get; set; }
    }

    private AutoCleanResume? LoadResume()
    {
        if (!System.IO.File.Exists(_resumeFilePath)) return null;
        try
        {
            var json = System.IO.File.ReadAllText(_resumeFilePath);
            return System.Text.Json.JsonSerializer.Deserialize<AutoCleanResume>(json, _resumeJsonOpts);
        }
        catch { return null; }
    }

    private void SaveResume(IEnumerable<string> remaining)
    {
        var list = remaining.ToList();
        if (!list.Any()) { DeleteResume(); return; }

        var data = new AutoCleanResume
        {
            RemainingUsers   = list,
            Total            = _autoCleanTotal,
            ProcessedBefore  = _autoCleanProcessed,
            UnfollowedBefore = _autoCleanUnfollowed,
            ExcludedBefore   = _autoCleanExcluded,
        };
        System.IO.Directory.CreateDirectory(
            System.IO.Path.GetDirectoryName(_resumeFilePath)!);
        System.IO.File.WriteAllText(
            _resumeFilePath,
            System.Text.Json.JsonSerializer.Serialize(data, _resumeJsonOpts));
    }

    private static void DeleteResume()
    {
        try { System.IO.File.Delete(_resumeFilePath); } catch { }
    }

    // ── Button handler ────────────────────────────────────────────────────────

    private void AutoCleanButton_Click(object sender, RoutedEventArgs e)
    {
        var fullList = _rawNotFollowingBack.Where(u => !_exclusionService.IsExcluded(u)).ToList();
        if (!fullList.Any())
        {
            MessageBox.Show("Nessun utente da pulire nella lista 'Non ti seguono'.",
                "Auto-Clean", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Check for a saved resume
        var resume = LoadResume();
        if (resume?.RemainingUsers.Any() == true)
        {
            // Keep only users still present in the current filtered list
            var validRemaining = resume.RemainingUsers
                .Where(u => fullList.Contains(u))
                .ToList();

            if (validRemaining.Any())
            {
                _autoCleanQueue      = new Queue<string>(validRemaining);
                _autoCleanTotal      = resume.Total;
                _autoCleanProcessed  = resume.ProcessedBefore;
                _autoCleanUnfollowed = resume.UnfollowedBefore;
                _autoCleanExcluded   = resume.ExcludedBefore;
            }
            else
            {
                // Resume obsolete — start fresh
                DeleteResume();
                _autoCleanQueue      = new Queue<string>(fullList);
                _autoCleanTotal      = fullList.Count;
                _autoCleanProcessed  = 0;
                _autoCleanUnfollowed = 0;
                _autoCleanExcluded   = 0;
            }
        }
        else
        {
            _autoCleanQueue      = new Queue<string>(fullList);
            _autoCleanTotal      = fullList.Count;
            _autoCleanProcessed  = 0;
            _autoCleanUnfollowed = 0;
            _autoCleanExcluded   = 0;
        }

        _autoCleanCancelled = false;

        AutoCleanModalOverlay.Visibility  = Visibility.Visible;
        AutoCleanDonePanel.Visibility     = Visibility.Collapsed;
        AutoCleanProgressPanel.Visibility = Visibility.Visible;

        _floatingWindow?.Close();
        _floatingWindow = new AutoCleanFloatingWindow();
        _floatingWindow.CancelRequested += () => Dispatcher.Invoke(CancelAutoClean_Click, this, null!);
        _floatingWindow.CloseRequested  += () => Dispatcher.Invoke(CloseAutoCleanModal_Click, this, null!);

        ProcessNextAutoClean();
    }

    // ── Core loop ─────────────────────────────────────────────────────────────

    private void ProcessNextAutoClean()
    {
        if (_autoCleanCancelled || _autoCleanQueue is null || _autoCleanQueue.Count == 0)
        {
            FinishAutoClean();
            return;
        }

        _autoCleanCurrentUser = _autoCleanQueue.Dequeue();
        UpdateAutoCleanProgress(_autoCleanCurrentUser, "In corso…");

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            $"https://www.instagram.com/{_autoCleanCurrentUser}/?ia_unfollow=1")
        { UseShellExecute = true });
    }

    private void HandleUnfollowResult(string username, string status)
    {
        if (AutoCleanModalOverlay.Visibility != Visibility.Visible) return;

        _autoCleanProcessed++;

        string statusLabel = status switch
        {
            "unfollowed" => "Smesso di seguire",
            "excluded"   => "Rimosso dal tracking",
            _            => "Già rimosso"
        };

        if (status == "unfollowed") _autoCleanUnfollowed++;
        if (status == "excluded")   _autoCleanExcluded++;

        UpdateAutoCleanProgress(username, statusLabel);

        if (_autoCleanCancelled || _autoCleanQueue is null || _autoCleanQueue.Count == 0)
        {
            FinishAutoClean();
            return;
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        timer.Tick += (_, _) => { timer.Stop(); ProcessNextAutoClean(); };
        timer.Start();
    }

    private void UpdateAutoCleanProgress(string username, string statusText)
    {
        int    done     = _autoCleanProcessed;
        int    total    = _autoCleanTotal;
        double pct      = total > 0 ? (double)done / total * 100 : 0;
        string userLabel = string.IsNullOrEmpty(username) ? "" : $"@{username}";

        AutoCleanProgressBar.Value    = pct;
        AutoCleanProgressText.Text    = $"{done} / {total}";
        AutoCleanCurrentUserText.Text = userLabel;
        AutoCleanStatusText.Text      = statusText;

        _floatingWindow?.UpdateProgress($"{done} / {total}", pct, userLabel, statusText);
    }

    private async void FinishAutoClean()
    {
        if (_autoCleanCancelled)
            SaveResume(_autoCleanQueue ?? Enumerable.Empty<string>());
        else
            DeleteResume();

        _autoCleanQueue = null;

        var summary = $"Rimossi dagli amici: {_autoCleanUnfollowed}\nRimossi dal tracking: {_autoCleanExcluded}";

        // Modal
        AutoCleanProgressPanel.Visibility = Visibility.Collapsed;
        AutoCleanDonePanel.Visibility     = Visibility.Visible;
        AutoCleanDoneSummaryText.Text     = summary;

        // Floating window switches to done state (stays visible even when main is focused)
        _floatingWindow?.ShowDone(summary);

        if (_autoCleanUnfollowed > 0 || _autoCleanExcluded > 0)
        {
            var session = new InstAnalytics.Models.RemovalSession
            {
                Date           = DateTime.Now,
                UnfollowedCount = _autoCleanUnfollowed,
                ExcludedCount   = _autoCleanExcluded,
            };
            await _removalHistoryService.SaveSessionAsync(session);
            await LoadHistoricalDataAsync();
        }
    }

    private void CancelAutoClean_Click(object sender, RoutedEventArgs e)
    {
        _autoCleanCancelled = true;
        // The actual save happens in FinishAutoClean once the current
        // in-flight request completes (so counts are accurate).
    }

    private void CloseAutoCleanModal_Click(object sender, RoutedEventArgs e)
    {
        _floatingWindow?.Close();
        _floatingWindow = null;
        _autoCleanQueue = null;
        AutoCleanModalOverlay.Visibility = Visibility.Collapsed;
        ApplyFiltersAndRefreshLists();
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
