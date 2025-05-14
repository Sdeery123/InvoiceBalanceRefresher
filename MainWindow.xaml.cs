using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using IOPath = System.IO.Path;
using System.Windows.Documents;
using System.Windows.Controls.Primitives;
using System.Linq;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;
using System.Windows.Media.Animation;

namespace InvoiceBalanceRefresher
{
    public partial class MainWindow : Window
    {
        private readonly InvoiceCloudApiService _apiService;
        private readonly LoggingHelper _loggingHelper;
        private readonly SearchHelper _searchHelper;
        private readonly ThemeManager _themeManager;
        private readonly BatchProcessingHelper _batchProcessingHelper;
        private readonly ScheduleManager _scheduleManager;

        private MaintenanceConfig _maintenanceConfig = null!;


        // Command properties for keyboard shortcuts
        // Command properties for keyboard shortcuts
        public ICommand SaveCredentialsCommand => new RelayCommand(param => SaveCredentials_Click(this, new RoutedEventArgs()));
        public ICommand BrowseCSVCommand => new RelayCommand(param => BrowseCSV_Click(this, new RoutedEventArgs()));
        public ICommand ProcessInvoiceCommand => new RelayCommand(param => ProcessSingleInvoice_Click(this, new RoutedEventArgs()));
        public ICommand ProcessBatchCommand => new RelayCommand(param => ProcessCSV_Click(this, new RoutedEventArgs()));
        public ICommand ClearLogCommand => new RelayCommand(param => ClearLog_Click(this, new RoutedEventArgs()));
        public ICommand FocusSearchCommand => new RelayCommand(param => SearchBox.Focus());
        public ICommand ShowDocumentationCommand => new RelayCommand(param => Documentation_Click(this, new RoutedEventArgs()));
        public ICommand ManageCredentialsCommand => new RelayCommand(param => ManageCredentials_Click(this, new RoutedEventArgs()));
        public ICommand SwitchLightModeCommand => new RelayCommand(param => LightMode_Click(this, new RoutedEventArgs()));
        public ICommand SwitchDarkModeCommand => new RelayCommand(param => DarkMode_Click(this, new RoutedEventArgs()));
        public ICommand CycleCredentialSetsCommand => new RelayCommand(param => CycleCredentialSets());
        public ICommand ToggleConsoleCommand => new RelayCommand(param => ToggleConsole());


        // Application version
        private const string APP_VERSION = "2.0.0"; // Updated version

        public MainWindow()
        {
            InitializeComponent();

            

            // Initialize helpers and services
            _loggingHelper = new LoggingHelper(ConsoleLog, paragraph => {
                ConsoleLog.Document.Blocks.Add(paragraph);
                ConsoleLog.ScrollToEnd();
            });

            // Check for command line arguments immediately after initialization
            ProcessCommandLineArguments();

            // Load maintenance settings
            _maintenanceConfig = MaintenanceConfig.Load() ?? new MaintenanceConfig
            {
                EnablePeriodicMaintenance = true, // Default values
                EnableLogCleanup = true,
                EnableOrphanedTaskCleanup = true,
                LogDirectory = "Logs",
                LogRetentionDays = 30,
                MaxSessionFilesPerDay = 10 // Add default for MaxSessionFilesPerDay
            };


            // Ensure the configuration is saved if it was newly created
            if (_maintenanceConfig != null && !_maintenanceConfig.Save())
            {
                Log(LogLevel.Warning, "Failed to save default maintenance configuration.");
            }

            // Run maintenance tasks
            try
            {
                RunMaintenanceTasks();
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error during maintenance tasks: {ex.Message}");
            }

            _apiService = new InvoiceCloudApiService((level, message) =>
                Log((LogLevel)(int)level, message));

            _searchHelper = new SearchHelper(ConsoleLog, SearchResultsCount,
                BatchResults, BatchSearchResultsCount);

            _themeManager = new ThemeManager(this, (level, message) => Log(level, message));

            _batchProcessingHelper = new BatchProcessingHelper(
                _apiService, (level, message) => Log(level, message),
                BatchResults, BatchProgress, BatchStatus);

            // Initialize scheduler
            _scheduleManager = ScheduleManager.GetInstance(
    (message) => Log(LogLevel.Info, message),
    async (billerGUID, webServiceKey, csvFilePath, hasAccountNumbers, defaultAccountNumber) =>
    {
        try
        {
            Log(LogLevel.Debug, $"Starting batch processing with: BillerGUID={billerGUID}, WebServiceKey={webServiceKey}, CSV={csvFilePath}");
            Log(LogLevel.Debug, $"HasAccountNumbers={hasAccountNumbers}, DefaultAccountNumber={defaultAccountNumber}");

            // Verify credentials before processing
            if (string.IsNullOrEmpty(billerGUID) || string.IsNullOrEmpty(webServiceKey))
            {
                Log(LogLevel.Error, "Invalid BillerGUID or WebServiceKey (empty or null)");
                return false;
            }

            // Check file exists and has content
            if (!File.Exists(csvFilePath))
            {
                Log(LogLevel.Error, $"CSV file not found: {csvFilePath}");
                return false;
            }

            var fileInfo = new FileInfo(csvFilePath);
            if (fileInfo.Length == 0)
            {
                Log(LogLevel.Error, "CSV file is empty");
                return false;
            }

            // Process the batch with detailed error handling
            bool result = await ProcessBatchInternal(billerGUID, webServiceKey, csvFilePath, hasAccountNumbers, defaultAccountNumber);
            Log(LogLevel.Debug, $"Batch processing completed with result: {result}");
            return result;
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Scheduled batch processing failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                Log(LogLevel.Error, $"Inner exception: {ex.InnerException.Message}");
            }
            Log(LogLevel.Debug, $"Stack trace: {ex.StackTrace}");
            return false;
        }
    });

            // Initialize the scheduler grid
            ScheduledTasksGrid.ItemsSource = _scheduleManager.ScheduledTasks;

            // Handle selection change to enable/disable buttons
            ScheduledTasksGrid.SelectionChanged += (s, e) =>
            {
                bool hasSelection = ScheduledTasksGrid.SelectedItem != null;
                EditButton.IsEnabled = hasSelection;
                DeleteButton.IsEnabled = hasSelection;
                RunNowButton.IsEnabled = hasSelection;
            };

            // Initially disable buttons
            EditButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
            RunNowButton.IsEnabled = false;

            _scheduleManager.TasksChanged += ScheduleManager_TasksChanged;

            // Set up initial UI state
            InitializeLogging();

            InitializeCredentialManager();

            // Try to load saved credentials
            LoadSavedCredentials();

            // Add this call at the end of your MainWindow constructor
            RefreshSchedulerTab();

            // In the MainWindow constructor, after initializing other components
            InitializeTaskSchedulerTab();



            // Log startup
            Log(LogLevel.Info, $"Invoice Balance Refresher v{APP_VERSION} started");
            Log(LogLevel.Info, "Keyboard shortcuts initialized");
        }



        private void ProcessCommandLineArguments()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                Log(LogLevel.Debug, $"Application started with {args.Length} arguments");

                for (int i = 0; i < args.Length; i++)
                {
                    Log(LogLevel.Debug, $"Arg[{i}]: {args[i]}");
                }

                // Check for --schedule parameter
                int scheduleIndex = Array.IndexOf(args, "--schedule");
                if (scheduleIndex >= 0 && scheduleIndex < args.Length - 1)
                {
                    string taskIdStr = args[scheduleIndex + 1];
                    Log(LogLevel.Info, $"Application started with --schedule {taskIdStr}");

                    if (Guid.TryParse(taskIdStr, out Guid taskId))
                    {
                        // We need to ensure the ScheduleManager is initialized before using it
                        // We can't use the same initialization code as in the constructor
                        // because it references UI elements which might not be ready yet

                        // Create a simple initialization that doesn't depend on UI
                        InitializeMinimalForScheduledTask();

                        // Execute the task in a delayed manner to ensure all components are initialized
                        Dispatcher.BeginInvoke(new System.Action(async () =>
                        {
                            try
                            {
                                Log(LogLevel.Info, $"Attempting to run scheduled task: {taskId}");

                                // Find the task by ID
                                var task = _scheduleManager.ScheduledTasks.FirstOrDefault(t => t.Id == taskId);
                                if (task != null)
                                {
                                    Log(LogLevel.Info, $"Found task: {task.Name}, running it now...");

                                    // Run the task
                                    bool success = await ProcessBatchInternal(
                                        task.BillerGUID,
                                        task.WebServiceKey,
                                        task.CsvFilePath,
                                        task.HasAccountNumbers);

                                    // Update task details
                                    task.LastRunTime = DateTime.Now;
                                    task.LastRunSuccessful = success;
                                    task.LastRunResult = success ? "Success" : "Failed";
                                    _scheduleManager.UpdateSchedule(task);

                                    Log(LogLevel.Info, $"Scheduled task {task.Name} completed with result: {(success ? "Success" : "Failed")}");
                                }
                                else
                                {
                                    Log(LogLevel.Error, $"Task with ID {taskId} not found");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log(LogLevel.Error, $"Error running task from command line: {ex.Message}");
                            }
                            finally
                            {
                                // Always exit after processing a scheduled task from command line
                                Log(LogLevel.Info, "Exiting application after scheduled task execution");
                                Application.Current.Shutdown();
                            }
                        }), DispatcherPriority.ApplicationIdle);
                    }
                    else
                    {
                        Log(LogLevel.Error, $"Invalid task ID format: {taskIdStr}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Can't use Log here if not initialized yet
                Console.WriteLine($"Error processing command line arguments: {ex.Message}");
            }
        }

        private void InitializeMinimalForScheduledTask()
        {
            try
            {
                // Create a local minimal context instead of reassigning readonly fields
                var minimalLoggingHelper = new LoggingHelper(null!, null!);

                var minimalApiService = new InvoiceCloudApiService((level, message) =>
                    minimalLoggingHelper.Log((LogLevel)(int)level, message));

                var minimalBatchProcessingHelper = new BatchProcessingHelper(
                    minimalApiService,
                    (level, message) => minimalLoggingHelper.Log(level, message));

                // Get a new instance instead of reassigning the readonly field
                var minimalScheduleManager = ScheduleManager.GetInstance(
                    (message) => minimalLoggingHelper.Log(LogLevel.Info, message),
                    async (billerGUID, webServiceKey, csvFilePath, hasAccountNumbers, defaultAccountNumber) =>
                    {
                        // Changed ProcessBatchInternal to ProcessBatchFile which actually exists
                        return await minimalBatchProcessingHelper.ProcessBatchFile(
                            billerGUID, webServiceKey, csvFilePath, hasAccountNumbers, defaultAccountNumber);
                    });

                // Process the command line task using the minimal context
                ProcessCommandLineTask(minimalScheduleManager, minimalBatchProcessingHelper);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing minimal components: {ex.Message}");
            }
        }


        // Method to toggle the console programmatically
        // Method to toggle the console programmatically
        private void ToggleConsole()
        {
            if (ConsoleToggleButton != null)
            {
                ConsoleToggleButton.IsChecked = !ConsoleToggleButton.IsChecked;
            }
        }

        // Handler for the checked event
        private void ConsoleToggle_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                // Begin the expand animation
                var storyboard = (Storyboard)FindResource("ExpandConsole");
                if (ConsoleLog != null)
                {
                    storyboard.Begin(ConsoleLog);
                }
                else
                {
                    Log(LogLevel.Warning, "Console log control not initialized when trying to expand");
                }

                // Rotate icon to point down
                if (ConsoleToggleIcon != null)
                {
                    ConsoleToggleIcon.Data = Geometry.Parse("M7,10L12,15L17,10H7Z");
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error expanding console: {ex.Message}");
            }
        }

        // Handler for the unchecked event
        private void ConsoleToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                // Begin the collapse animation
                var storyboard = (Storyboard)FindResource("CollapseConsole");
                if (ConsoleLog != null)
                {
                    storyboard.Begin(ConsoleLog);
                }
                else
                {
                    Log(LogLevel.Warning, "Console log control not initialized when trying to collapse");
                }

                // Rotate icon to point up
                if (ConsoleToggleIcon != null)
                {
                    ConsoleToggleIcon.Data = Geometry.Parse("M7,15L12,10L17,15H7Z");
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error collapsing console: {ex.Message}");
            }
        }


        private void ProcessCommandLineTask(ScheduleManager scheduleManager, BatchProcessingHelper batchProcessingHelper)
        {
            string[] args = Environment.GetCommandLineArgs();
            int scheduleIndex = Array.IndexOf(args, "--schedule");

            if (scheduleIndex >= 0 && scheduleIndex < args.Length - 1)
            {
                string taskIdStr = args[scheduleIndex + 1];
                Console.WriteLine($"Processing command line task with ID: {taskIdStr}");

                if (Guid.TryParse(taskIdStr, out Guid taskId))
                {
                    // Find and execute the task
                    // Find and execute the task
                    Dispatcher.BeginInvoke(new System.Action(async () =>
                    {
                        try
                        {
                            // Find the task by ID
                            var task = scheduleManager.ScheduledTasks.FirstOrDefault(t => t.Id == taskId);
                            if (task != null)
                            {
                                Console.WriteLine($"Found task: {task.Name}, running it now...");

                                // Changed ProcessBatchInternal to ProcessBatchFile
                                bool success = await batchProcessingHelper.ProcessBatchFile(
                                    task.BillerGUID,
                                    task.WebServiceKey,
                                    task.CsvFilePath,
                                    task.HasAccountNumbers);

                                // Update task details
                                task.LastRunTime = DateTime.Now;
                                task.LastRunSuccessful = success;
                                task.LastRunResult = success ? "Success" : "Failed";
                                scheduleManager.UpdateSchedule(task);

                                Console.WriteLine($"Task completed with result: {(success ? "Success" : "Failed")}");
                            }
                            else
                            {
                                Console.WriteLine($"Task with ID {taskId} not found");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error running task from command line: {ex.Message}");
                        }
                        finally
                        {
                            // Always exit after processing a scheduled task from command line
                            Console.WriteLine("Exiting application after task execution");
                            Application.Current.Shutdown();
                        }
                    }), DispatcherPriority.ApplicationIdle);
                }
            }
        }



        // Add this method to MainWindow.xaml.cs
        private void RunMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Log(LogLevel.Info, "Manual maintenance task execution requested");

                // Make sure we have the latest configuration from file
                _maintenanceConfig = MaintenanceConfig.Load();

                // Check if configuration is null
                if (_maintenanceConfig == null)
                {
                    _maintenanceConfig = new MaintenanceConfig
                    {
                        EnablePeriodicMaintenance = true,
                        EnableLogCleanup = true,
                        EnableOrphanedTaskCleanup = true,
                        LogDirectory = "Logs",
                        LogRetentionDays = 30,
                        MaxSessionFilesPerDay = 10,
                        MaintenanceFrequency = MaintenanceFrequency.EveryStartup
                    };
                    Log(LogLevel.Warning, "Created default maintenance configuration as none was found.");
                }

                // Ensure the configuration is saved before running maintenance
                if (!_maintenanceConfig.Save())
                {
                    Log(LogLevel.Warning, "Failed to save maintenance configuration before running tasks.");
                }

                // Create and run maintenance helper
                var maintenanceHelper = new MaintenanceHelper(
                    _maintenanceConfig.LogDirectory,
                    _maintenanceConfig.LogRetentionDays,
                    _maintenanceConfig.MaxSessionFilesPerDay,
                    _maintenanceConfig.EnableLogCleanup,
                    _maintenanceConfig.EnableOrphanedTaskCleanup,
                    _maintenanceConfig.MaintenanceFrequency,
                    _maintenanceConfig.LastMaintenanceRun,
                    (level, message) => Log(level, message));

                // Run maintenance regardless of frequency since this is manual
                bool tasksRan = maintenanceHelper.RunMaintenance(_maintenanceConfig);

                if (tasksRan)
                {
                    MessageBox.Show("Maintenance tasks completed successfully.",
                        "Maintenance Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("No maintenance tasks were enabled to run.",
                        "Maintenance Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error running maintenance tasks: {ex.Message}");
                MessageBox.Show($"Error running maintenance tasks: {ex.Message}",
                    "Maintenance Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }




        // Update the RunMaintenanceTasks method to use the MaintenanceHelper class correctly
        private void RunMaintenanceTasks()
        {
            try
            {
                if (_maintenanceConfig.EnablePeriodicMaintenance)
                {
                    // Create maintenance helper with the new parameters
                    var maintenanceHelper = new MaintenanceHelper(
                        _maintenanceConfig.LogDirectory,
                        _maintenanceConfig.LogRetentionDays,
                        _maintenanceConfig.MaxSessionFilesPerDay,
                        _maintenanceConfig.EnableLogCleanup,
                        _maintenanceConfig.EnableOrphanedTaskCleanup,
                        _maintenanceConfig.MaintenanceFrequency,
                        _maintenanceConfig.LastMaintenanceRun,
                        (level, message) => Log(level, message));

                    // Only run maintenance if it should run based on frequency
                    if (maintenanceHelper.ShouldRunMaintenance())
                    {
                        Log(LogLevel.Info, $"Running maintenance (Frequency: {_maintenanceConfig.MaintenanceFrequency})");
                        maintenanceHelper.RunMaintenance(_maintenanceConfig);
                    }
                    else
                    {
                        Log(LogLevel.Info, $"Skipping maintenance based on frequency setting ({_maintenanceConfig.MaintenanceFrequency})");
                    }
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error while running maintenance tasks: {ex.Message}");
            }
        }


        private void RefreshSchedulerTab()
        {
            // Refresh the data grid by forcibly resetting its ItemsSource
            ScheduledTasksGrid.ItemsSource = null;
            ScheduledTasksGrid.ItemsSource = _scheduleManager.ScheduledTasks;

            // Log the refresh
            Log(LogLevel.Debug, $"Scheduler tab refreshed with {_scheduleManager.ScheduledTasks.Count} tasks");
        }



        private void OpenMaintenanceSettings_Click(object sender, RoutedEventArgs e)
        {
            // Load maintenance config
            if (_maintenanceConfig == null)
            {
                _maintenanceConfig = MaintenanceConfig.Load();
            }

            // Create and show the settings dialog
            var settingsWindow = new MaintenanceSettings(_maintenanceConfig, (level, message) => Log(level, message));

            // If user clicked Save, apply the settings
            if (settingsWindow.ShowDialog() == true)
            {
                // Create and run the maintenance helper with the updated settings
                var maintenanceHelper = new MaintenanceHelper(
                    _maintenanceConfig.LogDirectory,
                    _maintenanceConfig.LogRetentionDays,
                    _maintenanceConfig.MaxSessionFilesPerDay,
                    _maintenanceConfig.EnableLogCleanup,
                    _maintenanceConfig.EnableOrphanedTaskCleanup,
                    _maintenanceConfig.MaintenanceFrequency,
                    _maintenanceConfig.LastMaintenanceRun,
                    (level, message) => Log(level, message)
                );

                // Run maintenance tasks if enabled, but respect the frequency setting
                if ((_maintenanceConfig.EnableLogCleanup || _maintenanceConfig.EnableOrphanedTaskCleanup) &&
                    maintenanceHelper.ShouldRunMaintenance())
                {
                    maintenanceHelper.RunMaintenance(_maintenanceConfig);
                    Log(LogLevel.Info, "Maintenance tasks executed according to settings.");
                }
                else
                {
                    Log(LogLevel.Info, "Maintenance settings saved but no tasks executed at this time.");
                }
            }
        }

        private void ScheduleManager_TasksChanged(object? sender, EventArgs e)
        {
            // Refresh the UI on the UI thread
            Dispatcher.Invoke(() =>
            {
                RefreshSchedulerTab();
                Log(LogLevel.Debug, "UI refreshed due to schedule changes");
            });
        }


        // Method to cycle through credential sets
        private void CycleCredentialSets()
        {
            if (CredentialSetComboBox.Items.Count > 0)
            {
                int nextIndex = (CredentialSetComboBox.SelectedIndex + 1) % CredentialSetComboBox.Items.Count;
                CredentialSetComboBox.SelectedIndex = nextIndex;
                
                Log(LogLevel.Info, $"Cycled to credential set: {((CredentialManager.CredentialSet)CredentialSetComboBox.SelectedItem)?.Name}");
            }
        }

        private void LoadSavedCredentials()
        {
            try
            {
                var credentials = CredentialManager.LoadCredentials();
                if (credentials != null)
                {
                    BillerGUID.Text = credentials.BillerGUID;
                    WebServiceKey.Text = credentials.WebServiceKey;
                    Log(LogLevel.Info, "Loaded saved credentials");
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Warning, $"Failed to load saved credentials: {ex.Message}");
            }
        }

        




        private void InitializeLogging()
        {
            // Setup auto-scroll timer
            var autoScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            autoScrollTimer.Tick += (s, e) => ConsoleLog.ScrollToEnd();
            autoScrollTimer.Start();

            Log(LogLevel.Info, "=== Session Started ===");
            Log(LogLevel.Info, $"Log file: {_loggingHelper.SessionLogPath}");
        }

        private async Task<bool> ProcessBatchInternal(string billerGUID, string webServiceKey, string filePath, bool hasAccountNumbers, string defaultAccountNumber = "")
        {
            // This is a facade method that delegates to the BatchProcessingHelper
            return await _batchProcessingHelper.ProcessBatchFile(billerGUID, webServiceKey, filePath, hasAccountNumbers, defaultAccountNumber);
        }


        private async void RunScheduledTask(ScheduledTask task)
        {
            Log(LogLevel.Info, $"Running scheduled task: {task.Name}");

            try
            {
                bool success = await ProcessBatchInternal(
                    task.BillerGUID,
                    task.WebServiceKey,
                    task.CsvFilePath,
                    task.HasAccountNumbers);

                // Update task properties
                task.LastRunTime = DateTime.Now;
                task.LastRunSuccessful = success;
                task.LastRunResult = success ? "Success" : "Failed";

                // Update the task in the manager to persist changes
                _scheduleManager.UpdateSchedule(task);

                Log(LogLevel.Info, $"Scheduled task {task.Name} completed with result: {(success ? "Success" : "Failed")}");

                // Only exit if launched by Windows Task Scheduler, not when running from UI
                if (Environment.GetCommandLineArgs().Contains("--schedule"))
                {
                    Log(LogLevel.Info, "Exiting application after scheduled task completion (launched by scheduler)");
                    Application.Current.Shutdown();
                }
                else if (task.Frequency == ScheduleFrequency.Once)
                {
                    // For one-time tasks launched from UI, just show a message but don't exit
                    Log(LogLevel.Info, "One-time task completed successfully");
                    MessageBox.Show($"One-time task '{task.Name}' completed successfully.",
                        "Task Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Refresh the schedule manager UI if it's open
                    RefreshScheduleManagerUI();
                }
                else
                {
                    // For recurring tasks, show a simple message and refresh the UI
                    MessageBox.Show($"Task '{task.Name}' completed successfully.",
                        "Task Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Refresh the schedule manager UI if it's open
                    RefreshScheduleManagerUI();
                }
            }
            catch (Exception ex)
            {
                // Update task properties even on failure
                task.LastRunTime = DateTime.Now;
                task.LastRunSuccessful = false;
                task.LastRunResult = $"Error: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 50))}";

                // Update the task in the manager to persist changes
                _scheduleManager.UpdateSchedule(task);

                Log(LogLevel.Error, $"Error executing scheduled task {task.Name}: {ex.Message}");

                // Only exit on error if launched by scheduler
                if (Environment.GetCommandLineArgs().Contains("--schedule"))
                {
                    Environment.Exit(1);
                }
                else
                {
                    // Show error message when running from UI
                    MessageBox.Show($"Error executing task '{task.Name}': {ex.Message}",
                        "Task Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    // Refresh the schedule manager UI if it's open
                    RefreshScheduleManagerUI();
                }
            }
        }

        private void RefreshScheduleManagerUI()
        {
            // Find the Schedule Manager window if it's open
            var scheduleWindow = Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.Title == "Schedule Manager");

            if (scheduleWindow != null)
            {
                // Find the DataGrid in the window
                var tasksGrid = UIHelper.FindVisualChildren<DataGrid>(scheduleWindow).FirstOrDefault();
                if (tasksGrid != null)
                {
                    // Refresh the grid by resetting its ItemsSource
                    tasksGrid.ItemsSource = null;
                    tasksGrid.ItemsSource = _scheduleManager.ScheduledTasks;
                }
            }
        }

        #region Scheduler Menu and UI



        private void Scheduler_Click(object sender, RoutedEventArgs e)
        {
            OpenScheduleManager();
        }

        private void RateLimitSettings_Click(object sender, RoutedEventArgs e)
        {
            // Load current settings
            RateLimitingConfig config = RateLimitingConfig.Load();

            // Create and show the settings dialog
            var dialog = new RateLimitingSettingsDialog(config)
            {
                Owner = this
            };

            bool? result = dialog.ShowDialog();

            // If user clicked Save, update the service configuration
            if (result == true)
            {
                InvoiceCloudApiService.UpdateRateLimitConfig(config);
                Log(LogLevel.Info, "Rate limiting settings updated");
            }
        }

        private void OpenScheduleManager()
        {
            // Create the Schedule Manager window
            var scheduleWindow = new Window
            {
                Title = "Schedule Manager",
                Width = 1100,
                Height = 700,
                Background = (SolidColorBrush)FindResource("BackgroundBrush"),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.CanResize
            };

            // Create the main grid
            var mainGrid = new Grid
            {
                Margin = new Thickness(15)
            };

            // Define grid rows
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Add header
            var header = new TextBlock
            {
                Text = "SCHEDULED TASKS",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = (SolidColorBrush)FindResource("AccentBrush"),
                Margin = new Thickness(0, 0, 0, 15),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Create DataGrid for scheduled tasks
            var tasksGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                SelectionMode = DataGridSelectionMode.Single,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                BorderThickness = new Thickness(1),
                BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
                Margin = new Thickness(0, 0, 0, 15),
                RowBackground = new SolidColorBrush(Color.FromArgb(40, 24, 180, 233)),
                AlternatingRowBackground = new SolidColorBrush(Color.FromArgb(20, 24, 180, 233)),
                Background = (SolidColorBrush)FindResource("GroupBackgroundBrush")
            };

            // Define DataGrid columns
            tasksGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new System.Windows.Data.Binding("Name"),
                Width = new DataGridLength(150)
            });

            tasksGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Description",
                Binding = new System.Windows.Data.Binding("Description"),
                Width = new DataGridLength(200)
            });

            tasksGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Frequency",
                Binding = new System.Windows.Data.Binding("Frequency"),
                Width = new DataGridLength(80)
            });

            tasksGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Next Run Time",
                Binding = new System.Windows.Data.Binding("NextRunTime") { StringFormat = "MM/dd/yyyy hh:mm tt" },
                Width = new DataGridLength(150)
            });

            tasksGrid.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = "Enabled",
                Binding = new System.Windows.Data.Binding("IsEnabled"),
                Width = new DataGridLength(70)
            });

            tasksGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Last Run",
                Binding = new System.Windows.Data.Binding("LastRunTime") { StringFormat = "MM/dd/yyyy hh:mm tt" },
                Width = new DataGridLength(150)
            });

            tasksGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Last Result",
                Binding = new System.Windows.Data.Binding("LastRunResult"),
                Width = new DataGridLength(100)
            });

            // Load scheduled tasks
            tasksGrid.ItemsSource = _scheduleManager.ScheduledTasks;

            Grid.SetRow(tasksGrid, 1);
            mainGrid.Children.Add(tasksGrid);

            // Add buttons panel
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var addButton = CreateButton("[ ADD NEW ]");
            var editButton = CreateButton("[ EDIT ]");
            var deleteButton = CreateButton("[ DELETE ]");
            var runNowButton = CreateButton("[ RUN NOW ]");
            var closeButton = CreateButton("[ CLOSE ]");

            // Add button event handlers
            addButton.Click += (s, e) => AddSchedule();
            editButton.Click += (s, e) =>
            {
                if (tasksGrid.SelectedItem is ScheduledTask task)
                {
                    EditSchedule(task);
                }
            };
            deleteButton.Click += (s, e) =>
            {
                if (tasksGrid.SelectedItem is ScheduledTask task)
                {
                    DeleteSchedule(task);
                    tasksGrid.ItemsSource = null;
                    tasksGrid.ItemsSource = _scheduleManager.ScheduledTasks;
                }
            };
            runNowButton.Click += (s, e) =>
            {
                if (tasksGrid.SelectedItem is ScheduledTask task)
                {
                    RunScheduledTask(task);
                }
            };
            closeButton.Click += (s, e) => scheduleWindow.Close();

            // Disable edit/delete/run buttons when no selection
            tasksGrid.SelectionChanged += (s, e) =>
            {
                bool hasSelection = tasksGrid.SelectedItem != null;
                editButton.IsEnabled = hasSelection;
                deleteButton.IsEnabled = hasSelection;
                runNowButton.IsEnabled = hasSelection;
            };

            // Initially disable edit/delete/run buttons
            editButton.IsEnabled = false;
            deleteButton.IsEnabled = false;
            runNowButton.IsEnabled = false;

            buttonsPanel.Children.Add(addButton);
            buttonsPanel.Children.Add(editButton);
            buttonsPanel.Children.Add(deleteButton);
            buttonsPanel.Children.Add(runNowButton);
            buttonsPanel.Children.Add(closeButton);

            Grid.SetRow(buttonsPanel, 2);
            mainGrid.Children.Add(buttonsPanel);

            scheduleWindow.Content = mainGrid;
            scheduleWindow.ShowDialog();
        }

        private Button CreateButton(string content)
        {
            return new Button
            {
                Content = content,
                Width = 120,
                Height = 30,
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064557")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold
            };
        }

        // Add these event handlers to your MainWindow.xaml.cs file
        private void AddSchedule(object sender, RoutedEventArgs e)
        {
            // Call your existing AddSchedule method
            AddSchedule();
        }

        private void EditSchedule(object sender, RoutedEventArgs e)
        {
            if (ScheduledTasksGrid.SelectedItem is ScheduledTask task)
            {
                EditSchedule(task);
            }
            else
            {
                MessageBox.Show("Please select a scheduled task to edit.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteSchedule(object sender, RoutedEventArgs e)
        {
            if (ScheduledTasksGrid.SelectedItem is ScheduledTask task)
            {
                DeleteSchedule(task);
                // Refresh the data grid
                ScheduledTasksGrid.ItemsSource = null;
                ScheduledTasksGrid.ItemsSource = _scheduleManager.ScheduledTasks;
            }
            else
            {
                MessageBox.Show("Please select a scheduled task to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RunNowScheduledTask(object sender, RoutedEventArgs e)
        {
            if (ScheduledTasksGrid.SelectedItem is ScheduledTask task)
            {
                RunScheduledTask(task);
            }
            else
            {
                MessageBox.Show("Please select a scheduled task to run.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }


        private void AddSchedule()
        {
            // Create a new task
            var task = new ScheduledTask
            {
                Name = "New Schedule",
                Description = "Scheduled invoice processing",
                Frequency = ScheduleFrequency.Daily,
                RunTime = DateTime.Now.TimeOfDay
            };

            // Open the edit dialog
            if (OpenScheduleEditDialog(task, true))
            {
                _scheduleManager.AddSchedule(task);
                RefreshScheduleManagerUI();
            }
        }

        private void EditSchedule(ScheduledTask task)
        {
            // Make a copy of the task for editing
            var taskCopy = new ScheduledTask
            {
                Id = task.Id,
                Name = task.Name,
                Description = task.Description,
                CsvFilePath = task.CsvFilePath,
                BillerGUID = task.BillerGUID,
                WebServiceKey = task.WebServiceKey,
                HasAccountNumbers = task.HasAccountNumbers,
                NextRunTime = task.NextRunTime,
                Frequency = task.Frequency,
                RunTime = task.RunTime,
                IsEnabled = task.IsEnabled,
                LastRunTime = task.LastRunTime,
                LastRunSuccessful = task.LastRunSuccessful,
                LastRunResult = task.LastRunResult,
                CustomOption = task.CustomOption,

            };

            // Open the edit dialog
            if (OpenScheduleEditDialog(taskCopy, false))
            {
                _scheduleManager.UpdateSchedule(taskCopy);
                RefreshScheduleManagerUI();
            }
        }

        private void DeleteSchedule(ScheduledTask task)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete the schedule '{task.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _scheduleManager.RemoveSchedule(task.Id);
            }
        }

        private bool OpenScheduleEditDialog(ScheduledTask task, bool isNew)
        {
            // Create the edit dialog window
            var editWindow = new Window
            {
                Title = isNew ? "Add New Schedule" : "Edit Schedule",
                Width = 700,
                Height = 700, // Increased height for additional options
                Background = (SolidColorBrush)FindResource("BackgroundBrush"),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            // Create a ScrollViewer to handle potential overflow
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            // Create the main grid with margin
            var mainGrid = new Grid { Margin = new Thickness(15) };

            // Define grid rows
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Basic settings
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Separator
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Frequency options
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // CSV Group
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

            // Add header
            var header = new TextBlock
            {
                Text = isNew ? "ADD NEW SCHEDULED TASK" : "EDIT SCHEDULED TASK",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = (SolidColorBrush)FindResource("AccentBrush"),
                Margin = new Thickness(0, 0, 0, 15),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Create settings grid
            var settingsGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };

            // Define settings columns and rows
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Add enough rows for all settings
            for (int i = 0; i < 5; i++)
            {
                settingsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // Name
            var nameLabel = CreateLabel("Name:", 0, 0);
            var nameBox = CreateTextBox(task.Name, 0, 1);

            // Description
            var descLabel = CreateLabel("Description:", 1, 0);
            var descBox = CreateTextBox(task.Description, 1, 1);

            // Frequency
            var freqLabel = CreateLabel("Frequency:", 2, 0);
            var freqCombo = new ComboBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(5, 3, 5, 3),
                MinWidth = 150,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Add all frequency options from the enum
            foreach (ScheduleFrequency freq in Enum.GetValues(typeof(ScheduleFrequency)))
            {
                freqCombo.Items.Add(freq);
            }

            freqCombo.SelectedItem = task.Frequency;

            Grid.SetRow(freqCombo, 2);
            Grid.SetColumn(freqCombo, 1);

            // Run time - only shown for appropriate frequencies
            var timeLabel = CreateLabel("Run Time:", 3, 0);
            var timeControlsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 5)
            };

            // Time picker panel
            var timePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 10, 0)
            };

            // Time components
            var hoursCombo = CreateTimeCombo(1, 12, 1, task.RunTime.Hours % 12 == 0 ? 12 : task.RunTime.Hours % 12);
            var minsCombo = CreateTimeCombo(0, 55, 5, task.RunTime.Minutes - (task.RunTime.Minutes % 5));

            // AM/PM combobox
            var ampmCombo = new ComboBox { MinWidth = 60 };
            ampmCombo.Items.Add("AM");
            ampmCombo.Items.Add("PM");
            ampmCombo.SelectedItem = task.RunTime.Hours >= 12 ? "PM" : "AM";

            // Add time controls to panel
            timePanel.Children.Add(hoursCombo);
            timePanel.Children.Add(new TextBlock
            {
                Text = ":",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                Margin = new Thickness(0, 0, 5, 0)
            });
            timePanel.Children.Add(minsCombo);
            timePanel.Children.Add(ampmCombo);

            // Add the time panel to the main time controls panel
            timeControlsPanel.Children.Add(timePanel);

            // For schedules with multiple time ranges, add UI for managing them
            var addTimeButton = new Button
            {
                Content = "+ Add Time",
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064557")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                Visibility = Visibility.Collapsed // Initially hidden
            };
            timeControlsPanel.Children.Add(addTimeButton);

            Grid.SetRow(timeLabel, 3);
            Grid.SetColumn(timeLabel, 0);
            Grid.SetRow(timeControlsPanel, 3);
            Grid.SetColumn(timeControlsPanel, 1);

            // Time ranges panel for MultipleTimesDaily frequency
            var timeRangesPanel = new StackPanel
            {
                Margin = new Thickness(0, 5, 0, 0),
                Visibility = Visibility.Collapsed // Initially hidden
            };
            Grid.SetRow(timeRangesPanel, 4);
            Grid.SetColumn(timeRangesPanel, 1);

            // If we have time ranges, add them
            if (task.TimeRanges != null && task.TimeRanges.Count > 0)
            {
                foreach (var timeRange in task.TimeRanges)
                {
                    AddTimeRangeControls(timeRangesPanel, timeRange);
                }
            }

            // Add Time button click handler
            addTimeButton.Click += (s, e) =>
            {
                var newTimeRange = new TimeRange(DateTime.Now.Hour, DateTime.Now.Minute);
                AddTimeRangeControls(timeRangesPanel, newTimeRange);
                timeRangesPanel.Visibility = Visibility.Visible;
            };

            // Enabled checkbox
            var enabledCheck = new CheckBox
            {
                Content = "Enabled",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                IsChecked = task.IsEnabled,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            Grid.SetRow(enabledCheck, 4);
            Grid.SetColumn(enabledCheck, 1);

            // Add all controls to settings grid
            settingsGrid.Children.Add(nameLabel);
            settingsGrid.Children.Add(nameBox);
            settingsGrid.Children.Add(descLabel);
            settingsGrid.Children.Add(descBox);
            settingsGrid.Children.Add(freqLabel);
            settingsGrid.Children.Add(freqCombo);
            settingsGrid.Children.Add(timeLabel);
            settingsGrid.Children.Add(timeControlsPanel);
            settingsGrid.Children.Add(enabledCheck);
            settingsGrid.Children.Add(timeRangesPanel);

            Grid.SetRow(settingsGrid, 1);
            mainGrid.Children.Add(settingsGrid);

            // Add a separator
            var separator = new Rectangle
            {
                Height = 1,
                Fill = (SolidColorBrush)FindResource("BorderBrush"),
                Margin = new Thickness(0, 5, 0, 15)
            };
            Grid.SetRow(separator, 2);
            mainGrid.Children.Add(separator);

            // Frequency-specific options panel
            var frequencyOptionsPanel = new GroupBox
            {
                Header = "Schedule Options",
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
                Background = new SolidColorBrush(Color.FromArgb(20, 24, 180, 233))
            };

            // Content control for frequency-specific options
            var frequencyOptionsContentControl = new ContentControl();

            // Create panels for each frequency type
            var dailyPanel = CreateDailyOptionsPanel(task);
            var workdaysPanel = CreateWorkdaysPanel(task);
            var weeklyPanel = CreateWeeklyOptionsPanel(task);
            var biweeklyPanel = CreateBiWeeklyOptionsPanel(task);
            var monthlyPanel = CreateMonthlyOptionsPanel(task);
            var quarterlyPanel = CreateQuarterlyOptionsPanel(task);
            var hourlyPanel = CreateHourlyOptionsPanel(task);
            var customMinutesPanel = CreateCustomMinutesPanel(task);
            var multipleTimesPanel = CreateMultipleTimesPanel(task);

            // Switch panel based on selected frequency
            System.Action updateFrequencyOptionsPanel = () =>
            {
                switch (freqCombo.SelectedItem)
                {
                    case ScheduleFrequency.Daily:
                        frequencyOptionsContentControl.Content = dailyPanel;
                        timeLabel.Visibility = Visibility.Visible;
                        timeControlsPanel.Visibility = Visibility.Visible;
                        timeRangesPanel.Visibility = Visibility.Collapsed;
                        addTimeButton.Visibility = Visibility.Collapsed;
                        break;
                    case ScheduleFrequency.WorkDays:
                        frequencyOptionsContentControl.Content = workdaysPanel;
                        timeLabel.Visibility = Visibility.Visible;
                        timeControlsPanel.Visibility = Visibility.Visible;
                        timeRangesPanel.Visibility = Visibility.Collapsed;
                        addTimeButton.Visibility = Visibility.Collapsed;
                        break;
                    case ScheduleFrequency.Weekly:
                        frequencyOptionsContentControl.Content = weeklyPanel;
                        timeLabel.Visibility = Visibility.Visible;
                        timeControlsPanel.Visibility = Visibility.Visible;
                        timeRangesPanel.Visibility = Visibility.Collapsed;
                        addTimeButton.Visibility = Visibility.Collapsed;
                        break;
                    case ScheduleFrequency.BiWeekly:
                        frequencyOptionsContentControl.Content = biweeklyPanel;
                        timeLabel.Visibility = Visibility.Visible;
                        timeControlsPanel.Visibility = Visibility.Visible;
                        timeRangesPanel.Visibility = Visibility.Collapsed;
                        addTimeButton.Visibility = Visibility.Collapsed;
                        break;
                    case ScheduleFrequency.Monthly:
                        frequencyOptionsContentControl.Content = monthlyPanel;
                        timeLabel.Visibility = Visibility.Visible;
                        timeControlsPanel.Visibility = Visibility.Visible;
                        timeRangesPanel.Visibility = Visibility.Collapsed;
                        addTimeButton.Visibility = Visibility.Collapsed;
                        break;
                    case ScheduleFrequency.Quarterly:
                        frequencyOptionsContentControl.Content = quarterlyPanel;
                        timeLabel.Visibility = Visibility.Visible;
                        timeControlsPanel.Visibility = Visibility.Visible;
                        timeRangesPanel.Visibility = Visibility.Collapsed;
                        addTimeButton.Visibility = Visibility.Collapsed;
                        break;
                    case ScheduleFrequency.Hourly:
                        frequencyOptionsContentControl.Content = hourlyPanel;
                        timeLabel.Visibility = Visibility.Collapsed;
                        timeControlsPanel.Visibility = Visibility.Collapsed;
                        timeRangesPanel.Visibility = Visibility.Collapsed;
                        addTimeButton.Visibility = Visibility.Collapsed;
                        break;
                    case ScheduleFrequency.Custom:
                        frequencyOptionsContentControl.Content = customMinutesPanel;
                        timeLabel.Visibility = Visibility.Collapsed;
                        timeControlsPanel.Visibility = Visibility.Collapsed;
                        timeRangesPanel.Visibility = Visibility.Collapsed;
                        addTimeButton.Visibility = Visibility.Collapsed;
                        break;
                    case ScheduleFrequency.MultipleTimesDaily:
                        frequencyOptionsContentControl.Content = multipleTimesPanel;
                        timeLabel.Visibility = Visibility.Visible;
                        timeControlsPanel.Visibility = Visibility.Visible;
                        timeRangesPanel.Visibility = Visibility.Visible;
                        addTimeButton.Visibility = Visibility.Visible;
                        break;
                    default:
                        frequencyOptionsContentControl.Content = null;
                        timeLabel.Visibility = Visibility.Visible;
                        timeControlsPanel.Visibility = Visibility.Visible;
                        timeRangesPanel.Visibility = Visibility.Collapsed;
                        addTimeButton.Visibility = Visibility.Collapsed;
                        break;
                }
            };

            // Update panel when frequency changes
            freqCombo.SelectionChanged += (s, e) => updateFrequencyOptionsPanel();

            // Call once to initialize
            updateFrequencyOptionsPanel();

            frequencyOptionsPanel.Content = frequencyOptionsContentControl;
            Grid.SetRow(frequencyOptionsPanel, 3);
            mainGrid.Children.Add(frequencyOptionsPanel);

            // Add CSV settings group
            var csvGroup = new GroupBox
            {
                Header = "CSV Batch Processing Settings",
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
                Background = new SolidColorBrush(Color.FromArgb(20, 24, 180, 233))
            };

            var csvGrid = new Grid();

            // Define CSV settings columns and rows
            csvGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            csvGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < 4; i++)
            {
                csvGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // CSV File Path
            var csvPathLabel = CreateLabel("CSV File Path:", 0, 0, 130);

            // CSV File Path panel with browse button
            var csvPathPanel = new Grid();
            csvPathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            csvPathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var csvPathBox = new TextBox
            {
                Text = task.CsvFilePath,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 5, 5),
                Padding = new Thickness(5, 3, 5, 3)
            };
            Grid.SetColumn(csvPathBox, 0);

            var browseButton = new Button
            {
                Content = "Browse...",
                Padding = new Thickness(10, 2, 10, 2),
                Margin = new Thickness(0, 5, 0, 5),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064557")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"))
            };
            Grid.SetColumn(browseButton, 1);

            // Browse button event handler
            browseButton.Click += (s, e) =>
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv"
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    csvPathBox.Text = openFileDialog.FileName;
                }
            };

            csvPathPanel.Children.Add(csvPathBox);
            csvPathPanel.Children.Add(browseButton);

            Grid.SetRow(csvPathPanel, 0);
            Grid.SetColumn(csvPathPanel, 1);

            // Biller GUID and Web Service Key
            var billerLabel = CreateLabel("Biller GUID:", 1, 0);
            var billerBox = CreateTextBox(task.BillerGUID, 1, 1);

            var keyLabel = CreateLabel("Web Service Key:", 2, 0);
            var keyBox = CreateTextBox(task.WebServiceKey, 2, 1);

            // Has Account Numbers
            var hasAccountsCheck = new CheckBox
            {
                Content = "CSV has Account Numbers",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                IsChecked = task.HasAccountNumbers,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            Grid.SetRow(hasAccountsCheck, 3);
            Grid.SetColumn(hasAccountsCheck, 1);

            // Add all controls to CSV grid
            csvGrid.Children.Add(csvPathLabel);
            csvGrid.Children.Add(csvPathPanel);
            csvGrid.Children.Add(billerLabel);
            csvGrid.Children.Add(billerBox);
            csvGrid.Children.Add(keyLabel);
            csvGrid.Children.Add(keyBox);
            csvGrid.Children.Add(hasAccountsCheck);

            csvGroup.Content = csvGrid;
            Grid.SetRow(csvGroup, 4);
            mainGrid.Children.Add(csvGroup);

            // Add buttons panel
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var saveButton = CreateButton("[ SAVE ]");
            var cancelButton = CreateButton("[ CANCEL ]");

            // Add button event handlers
            bool dialogResult = false;

            saveButton.Click += (s, e) =>
            {
                // Validate input fields
                if (!ValidateScheduleInputs(nameBox.Text, csvPathBox.Text, billerBox.Text, keyBox.Text))
                    return;

                // Update task with form values
                task.Name = nameBox.Text;
                task.Description = descBox.Text;
                task.CsvFilePath = csvPathBox.Text;
                task.BillerGUID = billerBox.Text;
                task.WebServiceKey = keyBox.Text;
                task.HasAccountNumbers = hasAccountsCheck.IsChecked ?? false;
                task.Frequency = (ScheduleFrequency)freqCombo.SelectedItem;
                task.IsEnabled = enabledCheck.IsChecked ?? true;

                // Only set time for frequencies that use it
                if (task.Frequency != ScheduleFrequency.Hourly && task.Frequency != ScheduleFrequency.Custom)
                {
                    // Calculate run time from the time picker controls
                    int selectedHour = (int)hoursCombo.SelectedItem;
                    int selectedMinute = Convert.ToInt32(minsCombo.SelectedItem.ToString());
                    string ampm = (string)ampmCombo.SelectedItem;

                    // Convert to 24-hour format
                    if (ampm == "PM" && selectedHour < 12) selectedHour += 12;
                    else if (ampm == "AM" && selectedHour == 12) selectedHour = 0;

                    task.RunTime = new TimeSpan(selectedHour, selectedMinute, 0);
                }

                // Clear TimeRanges for non-multiple frequencies to prevent stale data
                // Ensure `task.TimeRanges` is not null before calling `Clear()`
                if (task.TimeRanges != null)
                {
                    task.TimeRanges.Clear();
                }


                // Update frequency-specific options
                switch (task.Frequency)
                {
                    case ScheduleFrequency.Daily:
                        // Update daily options
                        var daysIntervalTextBox = dailyPanel.Children.OfType<StackPanel>().FirstOrDefault()?.Children.OfType<TextBox>().FirstOrDefault();
                        if (daysIntervalTextBox != null && int.TryParse(daysIntervalTextBox.Text, out int daysInterval))
                            task.DaysInterval = (short)(daysInterval > 0 ? daysInterval : 1);

                        // Make sure WorkdaysOnly is disabled for Daily
                        task.WorkdaysOnly = false;
                        break;

                    case ScheduleFrequency.WorkDays:
                        // Daily with WorkdaysOnly set to true
                        task.DaysInterval = 1;
                        task.WorkdaysOnly = true;
                        break;

                    case ScheduleFrequency.Weekly:
                        // Update weekly options (selected days of week)
                        var dayCheckBoxes = weeklyPanel.Children.OfType<StackPanel>().FirstOrDefault()?.Children.OfType<CheckBox>();
                        if (dayCheckBoxes != null)
                        {
                            var selectedDays = new System.Collections.Specialized.BitVector32(0);
                            foreach (var dayCheck in dayCheckBoxes)
                            {
                                if (dayCheck.IsChecked == true && dayCheck.Tag is DayOfWeek dayOfWeek)
                                {
                                    selectedDays[(int)Math.Pow(2, (int)dayOfWeek)] = true;
                                }
                            }
                            task.SelectedDaysOfWeek = (DaysOfTheWeek)selectedDays.Data;
                        }
                        break;

                    case ScheduleFrequency.BiWeekly:
                        // Update biweekly options
                        var weekSelection = biweeklyPanel.Children.OfType<RadioButton>().FirstOrDefault(rb => rb.IsChecked == true);
                        if (weekSelection != null && weekSelection.Tag is int weekValue)
                        {
                            task.BiweeklyWeek = weekValue;
                        }

                        // Also update weekly days 
                        var biWeeklyDayCheckBoxes = biweeklyPanel.Children.OfType<StackPanel>().FirstOrDefault(sp => sp.Name == "BiWeeklyDaysPanel")?.Children.OfType<CheckBox>();
                        if (biWeeklyDayCheckBoxes != null)
                        {
                            var selectedBiWeeklyDays = new System.Collections.Specialized.BitVector32(0);
                            foreach (var dayCheck in biWeeklyDayCheckBoxes)
                            {
                                if (dayCheck.IsChecked == true && dayCheck.Tag is DayOfWeek dayOfWeek)
                                {
                                    selectedBiWeeklyDays[(int)Math.Pow(2, (int)dayOfWeek)] = true;
                                }
                            }
                            task.SelectedDaysOfWeek = (DaysOfTheWeek)selectedBiWeeklyDays.Data;
                        }
                        break;

                    case ScheduleFrequency.Monthly:
                        // Update monthly options (selected days and months)
                        var monthlyDayCheckBoxes = monthlyPanel.Children.OfType<StackPanel>().FirstOrDefault(sp => sp.Name == "DaysPanel")?.Children.OfType<CheckBox>();
                        if (monthlyDayCheckBoxes != null)
                        {
                            // Build selected days array
                            var selectedDays = new List<int>();
                            foreach (var dayCheck in monthlyDayCheckBoxes)
                            {
                                if (dayCheck.IsChecked == true && dayCheck.Tag is int day)
                                {
                                    selectedDays.Add(day);
                                }
                            }
                            task.SelectedDaysOfMonth = selectedDays.ToArray();
                        }

                        var monthCheckBoxes = monthlyPanel.Children.OfType<StackPanel>().FirstOrDefault(sp => sp.Name == "MonthsPanel")?.Children.OfType<CheckBox>();
                        if (monthCheckBoxes != null)
                        {
                            task.SelectedMonths = 0;
                            foreach (var monthCheck in monthCheckBoxes)
                            {
                                if (monthCheck.IsChecked == true && monthCheck.Tag is int month)
                                {
                                    task.SelectedMonths |= (MonthsOfTheYear)(1 << (month - 1));
                                }
                            }
                        }
                        break;

                    case ScheduleFrequency.Quarterly:
                        // Update quarterly options
                        var quarterlyMonthsPanel = quarterlyPanel.Children.OfType<StackPanel>().FirstOrDefault(sp => sp.Name == "QuarterlyMonthsPanel");
                        if (quarterlyMonthsPanel != null)
                        {
                            var quarterlyMonths = new List<int>();
                            var monthCheckboxes = quarterlyMonthsPanel.Children.OfType<CheckBox>();
                            foreach (var checkbox in monthCheckboxes)
                            {
                                if (checkbox.IsChecked == true && checkbox.Tag is int monthInQuarter)
                                {
                                    quarterlyMonths.Add(monthInQuarter);
                                }
                            }
                            task.QuarterlyMonths = quarterlyMonths.ToArray();
                        }

                        // Update selected days
                        var quarterlyDaysPanel = quarterlyPanel.Children.OfType<StackPanel>().FirstOrDefault(sp => sp.Name == "QuarterlyDaysPanel");
                        if (quarterlyDaysPanel != null)
                        {
                            var daysTextBox = quarterlyDaysPanel.Children.OfType<TextBox>().FirstOrDefault();
                            if (daysTextBox != null)
                            {
                                if (int.TryParse(daysTextBox.Text, out int day) && day >= 1 && day <= 31)
                                {
                                    task.SelectedDaysOfMonth = new int[] { day };
                                }
                            }
                        }
                        break;

                    case ScheduleFrequency.Hourly:
                        // Update hourly interval
                        var hoursTextBox = hourlyPanel.Children.OfType<TextBox>().FirstOrDefault();
                        if (hoursTextBox != null && int.TryParse(hoursTextBox.Text, out int hours))
                        {
                            task.HoursInterval = Math.Max(1, hours); // Ensure at least 1 hour
                        }
                        break;

                    case ScheduleFrequency.Custom:
                        // Update minutes interval
                        var minutesTextBox = customMinutesPanel.Children.OfType<TextBox>().FirstOrDefault();
                        if (minutesTextBox != null && int.TryParse(minutesTextBox.Text, out int minutes))
                        {
                            task.MinutesInterval = Math.Max(1, minutes); // Ensure at least 1 minute
                        }
                        break;

                    case ScheduleFrequency.MultipleTimesDaily:
                        // Clear existing times, then extract all times from UI
                        if (task.TimeRanges != null)
                        {
                            task.TimeRanges.Clear();
                        }


                        // Get all time ranges from UI
                        foreach (Grid timeRangeGrid in timeRangesPanel.Children)
                        {
                            int hour = 0, minute = 0;

                            // Find hour and minute combos
                            var combos = timeRangeGrid.Children.OfType<ComboBox>().ToList();
                            if (combos.Count >= 3)
                            {
                                var hrCombo = combos[0];
                                var minCombo = combos[1];
                                var ampmCombo = combos[2];

                                int hr = (int)hrCombo.SelectedItem;
                                int min = Convert.ToInt32(minCombo.SelectedItem.ToString());
                                string ampm = (string)ampmCombo.SelectedItem;

                                // Convert to 24-hour format
                                if (ampm == "PM" && hr < 12) hr += 12;
                                else if (ampm == "AM" && hr == 12) hr = 0;

                                hour = hr;
                                minute = min;
                            }

                            // Add to task's time ranges
                            // Ensure `task.TimeRanges` is not null before calling `Add()`
                            if (task.TimeRanges == null)
                            {
                                task.TimeRanges = new List<TimeRange>();
                            }
                            task.TimeRanges.Add(new TimeRange(hour, minute));

                        }

                        // If no time ranges were added, add the current time
                        // Ensure `task.TimeRanges` is not null before accessing its Count property
                        if (task.TimeRanges == null || task.TimeRanges.Count == 0)
                        {
                            int selectedHr = (int)hoursCombo.SelectedItem;
                            int selectedMin = Convert.ToInt32(minsCombo.SelectedItem.ToString());
                            string selectedAmPm = (string)ampmCombo.SelectedItem;

                            // Convert to 24-hour format
                            if (selectedAmPm == "PM" && selectedHr < 12) selectedHr += 12;
                            else if (selectedAmPm == "AM" && selectedHr == 12) selectedHr = 0;

                            task.TimeRanges = new List<TimeRange>
    {
        new TimeRange(selectedHr, selectedMin)
    };
                        }

                        break;
                }

                // Update NextRunTime based on frequency and run time
                task.UpdateNextRunTime();

                dialogResult = true;
                editWindow.Close();
            };

            cancelButton.Click += (s, e) =>
            {
                dialogResult = false;
                editWindow.Close();
            };

            buttonsPanel.Children.Add(saveButton);
            buttonsPanel.Children.Add(cancelButton);

            Grid.SetRow(buttonsPanel, 5);
            mainGrid.Children.Add(buttonsPanel);

            // Add UI to window
            scrollViewer.Content = mainGrid;
            editWindow.Content = scrollViewer;

            // Show dialog
            editWindow.ShowDialog();

            return dialogResult;
        }

        // Helper method to add time range controls for MultipleTimesDaily
        private void AddTimeRangeControls(StackPanel timeRangesPanel, TimeRange timeRange)
        {
            var timeRangeGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 5)
            };

            // Create columns for time components and delete button
            timeRangeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            timeRangeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            timeRangeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            timeRangeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            timeRangeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Create hour, minute, and ampm combos
            int hour12Format = timeRange.Hour % 12 == 0 ? 12 : timeRange.Hour % 12;
            var hourCombo = CreateTimeCombo(1, 12, 1, hour12Format);
            Grid.SetColumn(hourCombo, 0);

            // Colon separator
            var separator = new TextBlock
            {
                Text = ":",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetColumn(separator, 1);

            // Minute combo
            var minuteCombo = CreateTimeCombo(0, 55, 5, timeRange.Minute - (timeRange.Minute % 5));
            Grid.SetColumn(minuteCombo, 2);

            // AM/PM combo
            var ampmCombo = new ComboBox { MinWidth = 60 };
            ampmCombo.Items.Add("AM");
            ampmCombo.Items.Add("PM");
            ampmCombo.SelectedItem = timeRange.Hour >= 12 ? "PM" : "AM";
            Grid.SetColumn(ampmCombo, 3);

            // Delete button
            var deleteButton = new Button
            {
                Content = "X",
                Padding = new Thickness(5, 0, 5, 0),
                Margin = new Thickness(5, 0, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(deleteButton, 4);

            // Delete button handler
            deleteButton.Click += (s, e) =>
            {
                timeRangesPanel.Children.Remove(timeRangeGrid);
                if (timeRangesPanel.Children.Count == 0)
                {
                    timeRangesPanel.Visibility = Visibility.Collapsed;
                }
            };

            // Add all controls to grid
            timeRangeGrid.Children.Add(hourCombo);
            timeRangeGrid.Children.Add(separator);
            timeRangeGrid.Children.Add(minuteCombo);
            timeRangeGrid.Children.Add(ampmCombo);
            timeRangeGrid.Children.Add(deleteButton);

            // Add to panel
            timeRangesPanel.Children.Add(timeRangeGrid);
            timeRangesPanel.Visibility = Visibility.Visible;
        }

        // Helper method to create day options panel for daily frequency
        private StackPanel CreateDailyOptionsPanel(ScheduledTask task)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 10)
            };

            var label = new TextBlock
            {
                Text = "Run every",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };

            var daysTextBox = new TextBox
            {
                Text = task.DaysInterval.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Width = 40,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(5, 3, 5, 3)
            };

            var daysLabel = new TextBlock
            {
                Text = "day(s)",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };

            panel.Children.Add(label);
            panel.Children.Add(daysTextBox);
            panel.Children.Add(daysLabel);

            return panel;
        }

        // Helper method to create panel for workdays option (weekday-only daily schedule)
        private StackPanel CreateWorkdaysPanel(ScheduledTask task)
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(0, 10, 0, 10)
            };

            var infoText = new TextBlock
            {
                Text = "Run every workday (Monday through Friday)",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(infoText);

            return panel;
        }

        // Helper method to create panel for hourly frequency
        private StackPanel CreateHourlyOptionsPanel(ScheduledTask task)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 10)
            };

            var label = new TextBlock
            {
                Text = "Run every",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };

            var hoursTextBox = new TextBox
            {
                Text = task.HoursInterval.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Width = 40,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(5, 3, 5, 3)
            };

            var hoursLabel = new TextBlock
            {
                Text = "hour(s)",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };

            panel.Children.Add(label);
            panel.Children.Add(hoursTextBox);
            panel.Children.Add(hoursLabel);

            return panel;
        }

        // Helper method to create panel for custom minutes frequency
        private StackPanel CreateCustomMinutesPanel(ScheduledTask task)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 10)
            };

            var label = new TextBlock
            {
                Text = "Run every",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };

            var minutesTextBox = new TextBox
            {
                Text = task.MinutesInterval.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Width = 40,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(5, 3, 5, 3)
            };

            var minutesLabel = new TextBlock
            {
                Text = "minute(s)",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };

            panel.Children.Add(label);
            panel.Children.Add(minutesTextBox);
            panel.Children.Add(minutesLabel);

            return panel;
        }

        // Helper method to create panel for multiple times daily frequency
        private StackPanel CreateMultipleTimesPanel(ScheduledTask task)
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(0, 10, 0, 10)
            };

            var infoText = new TextBlock
            {
                Text = "Run at multiple specified times throughout the day",
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(infoText);

            var instructionsText = new TextBlock
            {
                Text = "Use the '+ Add Time' button to add additional times",
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyles.Italic
            };
            panel.Children.Add(instructionsText);

            return panel;
        }

        // Helper method to create panel for biweekly options
        private StackPanel CreateBiWeeklyOptionsPanel(ScheduledTask task)
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(0, 10, 0, 10)
            };

            // Add week selection (even/odd weeks)
            var weekPanel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 10)
            };

            var weekTitle = new TextBlock
            {
                Text = "Run during:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            weekPanel.Children.Add(weekTitle);

            // Even week option
            var evenWeekRadio = new RadioButton
            {
                Content = "Even weeks",
                Margin = new Thickness(0, 0, 0, 5),
                IsChecked = task.BiweeklyWeek == 0,
                Tag = 0,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            weekPanel.Children.Add(evenWeekRadio);

            // Odd week option
            var oddWeekRadio = new RadioButton
            {
                Content = "Odd weeks",
                IsChecked = task.BiweeklyWeek == 1,
                Tag = 1,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            weekPanel.Children.Add(oddWeekRadio);

            panel.Children.Add(weekPanel);

            // Add days selection
            var daysTitle = new TextBlock
            {
                Text = "Run on these days:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 5, 0, 5),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            panel.Children.Add(daysTitle);

            var daysPanel = new StackPanel
            {
                Name = "BiWeeklyDaysPanel",
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 5)
            };

            // Get the current selected days
            DaysOfTheWeek selectedDays = task.SelectedDaysOfWeek;

            // Create checkboxes for each day of the week
            foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
            {
                bool isChecked = ((int)selectedDays & (1 << (int)day)) != 0;

                var dayCheck = new CheckBox
                {
                    Content = day.ToString(),
                    IsChecked = isChecked,
                    Margin = new Thickness(5, 0, 5, 0),
                    Tag = day,
                    Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
                };

                daysPanel.Children.Add(dayCheck);
            }

            panel.Children.Add(daysPanel);

            return panel;
        }

        // Helper method for creating quarterly options
        private StackPanel CreateQuarterlyOptionsPanel(ScheduledTask task)
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(0, 10, 0, 10)
            };

            // Months in quarter selection
            var monthsTitle = new TextBlock
            {
                Text = "Run during these months of each quarter:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            panel.Children.Add(monthsTitle);

            var monthsPanel = new StackPanel
            {
                Name = "QuarterlyMonthsPanel",
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Quarter descriptions
            var quarterInfo = new TextBlock
            {
                Text = "Quarter 1: Jan, Feb, Mar | Quarter 2: Apr, May, Jun | Quarter 3: Jul, Aug, Sep | Quarter 4: Oct, Nov, Dec",
                FontStyle = FontStyles.Italic,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                Margin = new Thickness(0, 0, 0, 5),
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(quarterInfo);

            // Get the selected months within quarters
            int[] selectedMonths = task.QuarterlyMonths ?? new int[] { 1 };

            // First month of quarter option
            var firstMonthCheck = new CheckBox
            {
                Content = "First month (Jan, Apr, Jul, Oct)",
                IsChecked = selectedMonths.Contains(1),
                Tag = 1,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            monthsPanel.Children.Add(firstMonthCheck);

            // Second month of quarter option
            var secondMonthCheck = new CheckBox
            {
                Content = "Second month (Feb, May, Aug, Nov)",
                IsChecked = selectedMonths.Contains(2),
                Tag = 2,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            monthsPanel.Children.Add(secondMonthCheck);

            // Third month of quarter option
            var thirdMonthCheck = new CheckBox
            {
                Content = "Third month (Mar, Jun, Sep, Dec)",
                IsChecked = selectedMonths.Contains(3),
                Tag = 3,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            monthsPanel.Children.Add(thirdMonthCheck);

            panel.Children.Add(monthsPanel);

            // Day of month selection
            var daysTitle = new TextBlock
            {
                Text = "Run on day of month:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 5),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            panel.Children.Add(daysTitle);

            var daysPanel = new StackPanel
            {
                Name = "QuarterlyDaysPanel",
                Orientation = Orientation.Horizontal
            };

            var dayLabel = new TextBlock
            {
                Text = "Day:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            daysPanel.Children.Add(dayLabel);

            // Get the current day value
            int selectedDay = task.SelectedDaysOfMonth?.FirstOrDefault() ?? 1;

            var dayTextBox = new TextBox
            {
                Text = selectedDay.ToString(),
                Width = 40,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(5, 3, 5, 3)
            };
            daysPanel.Children.Add(dayTextBox);

            // Add a note about valid days
            var dayNote = new TextBlock
            {
                Text = "(1-31, will use last day of month if day exceeds month length)",
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            daysPanel.Children.Add(dayNote);

            panel.Children.Add(daysPanel);

            return panel;
        }

        // Helper method to create day options panel for weekly frequency
        private StackPanel CreateWeeklyOptionsPanel(ScheduledTask task)
        {
            var mainPanel = new StackPanel
            {
                Margin = new Thickness(0, 10, 0, 10)
            };

            var titleText = new TextBlock
            {
                Text = "Run on these days:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            mainPanel.Children.Add(titleText);

            var daysPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 5)
            };

            // Get the current selected days
            DaysOfTheWeek selectedDays = task.SelectedDaysOfWeek;

            // Create checkboxes for each day of the week
            foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
            {
                bool isChecked = ((int)selectedDays & (1 << (int)day)) != 0;

                var dayCheck = new CheckBox
                {
                    Content = day.ToString(),
                    IsChecked = isChecked,
                    Margin = new Thickness(5, 0, 5, 0),
                    Tag = day,
                    Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
                };

                daysPanel.Children.Add(dayCheck);
            }

            mainPanel.Children.Add(daysPanel);
            return mainPanel;
        }

        // Helper method to create options panel for monthly frequency
        private StackPanel CreateMonthlyOptionsPanel(ScheduledTask task)
        {
            var mainPanel = new StackPanel
            {
                Margin = new Thickness(0, 10, 0, 10)
            };

            // Days section
            var daysTitle = new TextBlock
            {
                Text = "Run on these days of the month:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            mainPanel.Children.Add(daysTitle);

            // Create days panel with checkboxes in a grid layout
            var daysPanel = new StackPanel
            {
                Name = "DaysPanel",
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 5, 0, 10)
            };

            var daysGrid = new Grid();

            // Create 7 columns for days
            for (int i = 0; i < 7; i++)
            {
                daysGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            // Create 5 rows (4 rows of 7 + 1 row of 3)
            for (int i = 0; i < 5; i++)
            {
                daysGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // Add checkboxes for days 1-31
            for (int day = 1; day <= 31; day++)
            {
                int rowIndex = (day - 1) / 7;
                int colIndex = (day - 1) % 7;

                bool isChecked = task.SelectedDaysOfMonth.Contains(day);

                var dayCheck = new CheckBox
                {
                    Content = day.ToString(),
                    IsChecked = isChecked,
                    Margin = new Thickness(5, 2, 5, 2),
                    Tag = day,
                    Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
                };

                Grid.SetRow(dayCheck, rowIndex);
                Grid.SetColumn(dayCheck, colIndex);

                daysGrid.Children.Add(dayCheck);
            }

            daysPanel.Children.Add(daysGrid);
            mainPanel.Children.Add(daysPanel);

            // Months section
            var monthsTitle = new TextBlock
            {
                Text = "Run in these months:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 5, 0, 5),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            mainPanel.Children.Add(monthsTitle);

            var monthsPanel = new StackPanel
            {
                Name = "MonthsPanel",
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 5, 0, 5)
            };

            var monthsGrid = new Grid();

            // Create 4 columns for months
            for (int i = 0; i < 4; i++)
            {
                monthsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            // Create 3 rows
            for (int i = 0; i < 3; i++)
            {
                monthsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // Add checkboxes for all 12 months
            string[] monthNames = new string[] {
        "January", "February", "March", "April",
        "May", "June", "July", "August",
        "September", "October", "November", "December"
    };

            for (int month = 1; month <= 12; month++)
            {
                int rowIndex = (month - 1) / 4;
                int colIndex = (month - 1) % 4;

                bool isChecked = ((int)task.SelectedMonths & (1 << (month - 1))) != 0;

                var monthCheck = new CheckBox
                {
                    Content = monthNames[month - 1],
                    IsChecked = isChecked,
                    Margin = new Thickness(5, 2, 5, 2),
                    Tag = month,
                    Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
                };

                Grid.SetRow(monthCheck, rowIndex);
                Grid.SetColumn(monthCheck, colIndex);

                monthsGrid.Children.Add(monthCheck);
            }

            monthsPanel.Children.Add(monthsGrid);
            mainPanel.Children.Add(monthsPanel);

            return mainPanel;
        }


        private bool ValidateScheduleInputs(string name, string csvPath, string billerGuid, string webServiceKey)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter a name for this schedule.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
            {
                MessageBox.Show("Please provide a valid CSV file path.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(billerGuid) || !ValidationHelper.ValidateGUID(billerGuid))
            {
                MessageBox.Show("Please enter a valid Biller GUID.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(webServiceKey) || !ValidationHelper.ValidateGUID(webServiceKey))
            {
                MessageBox.Show("Please enter a valid Web Service Key.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private TextBlock CreateLabel(string text, int row, int column, int minWidth = 0)
        {
            var label = new TextBlock
            {
                Text = text,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            if (minWidth > 0)
                label.MinWidth = minWidth;

            Grid.SetRow(label, row);
            Grid.SetColumn(label, column);
            return label;
        }

        private TextBox CreateTextBox(string text, int row, int column)
        {
            var textBox = new TextBox
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(5, 3, 5, 3)
            };
            Grid.SetRow(textBox, row);
            Grid.SetColumn(textBox, column);
            return textBox;
        }

        private ComboBox CreateTimeCombo(int min, int max, int step, int selectedValue)
        {
            var combo = new ComboBox
            {
                MinWidth = 60,
                Margin = new Thickness(0, 0, 5, 0)
            };

            for (int i = min; i <= max; i += step)
            {
                if (step == 1)
                    combo.Items.Add(i);
                else
                    combo.Items.Add(i.ToString("00"));
            }

            if (step == 1)
                combo.SelectedItem = selectedValue;
            else
                combo.SelectedItem = selectedValue.ToString("00");

            return combo;
        }

        #endregion

        #region UI Event Handlers

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.Trim().ToLower();
            _searchHelper.PerformSearch(searchText);
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Clear();
            _searchHelper.PerformSearch(string.Empty);
        }

        private void BatchSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = BatchSearchBox.Text.Trim().ToLower();
            _searchHelper.PerformBatchSearch(searchText);
        }

        private void ClearBatchSearch_Click(object sender, RoutedEventArgs e)
        {
            BatchSearchBox.Clear();
            _searchHelper.PerformBatchSearch(string.Empty);
        }

        private void LightMode_Click(object sender, RoutedEventArgs e)
        {
            _themeManager.SetLightMode();
            // Remove references to non-existent menu items
        }

        private void DarkMode_Click(object sender, RoutedEventArgs e)
        {
            _themeManager.SetDarkMode();
            // Use Button property rather than ToggleButton property and remove non-existent menu items
        }

        private async void ProcessSingleInvoice_Click(object sender, RoutedEventArgs e)
        {
            string billerGUID = BillerGUID.Text;
            string webServiceKey = WebServiceKey.Text;
            string accountNumber = AccountNumber.Text;
            string invoiceNumber = InvoiceNumber.Text;

            Log(LogLevel.Info, $"Processing single invoice: {invoiceNumber} for account: {accountNumber}");

            if (ValidationHelper.ValidateGUID(billerGUID) && ValidationHelper.ValidateGUID(webServiceKey) &&
                !string.IsNullOrEmpty(invoiceNumber) && !string.IsNullOrEmpty(accountNumber))
            {
                try
                {
                    SingleResult.Text = "Processing...";

                    // First, call customer record service to refresh the balance
                    Log(LogLevel.Debug, $"Calling customer record service to refresh balance for account {accountNumber}...");
                    await _apiService.GetCustomerRecord(billerGUID, webServiceKey, accountNumber);
                    Log(LogLevel.Info, $"Balance refreshed for account {accountNumber}");

                    // Now call invoice service
                    Log(LogLevel.Debug, $"Calling invoice service for invoice {invoiceNumber}...");
                    string result = await _apiService.GetInvoiceByNumber(billerGUID, webServiceKey, invoiceNumber);
                    SingleResult.Text = result;
                    Log(LogLevel.Info, $"Invoice {invoiceNumber} processed successfully");
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Error processing invoice: {ex.Message}";
                    SingleResult.Text = errorMessage;
                    Log(LogLevel.Error, errorMessage);
                }
            }
            else
            {
                string validationError = "Invalid or missing fields";
                MessageBox.Show("Please enter valid GUIDs, Account Number, and Invoice Number.");
                Log(LogLevel.Warning, validationError);
            }
        }

        private void BrowseCSV_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                CSVFilePath.Text = openFileDialog.FileName;
                Log(LogLevel.Info, $"Selected CSV file: {openFileDialog.FileName}");
            }
        }

        private async void ProcessCSV_Click(object sender, RoutedEventArgs e)
        {
            string filePath = CSVFilePath.Text.Trim();
            string billerGUID = BillerGUID.Text.Trim();
            string webServiceKey = WebServiceKey.Text.Trim();
            bool hasAccountNumbers = AccountInvoiceFormat.IsChecked ?? false;
            string defaultAccountNumber = AccountNumber.Text.Trim();

            // Validate input fields first
            if (string.IsNullOrWhiteSpace(filePath))
            {
                MessageBox.Show("Please select a CSV file to process.",
                            "Missing File Path",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                Log(LogLevel.Warning, "Batch processing cancelled: No CSV file selected");
                return;
            }

            // Validate BillerGUID and WebServiceKey
            if (!ValidationHelper.ValidateGUID(billerGUID) || !ValidationHelper.ValidateGUID(webServiceKey))
            {
                MessageBox.Show("Please enter valid Biller GUID and Web Service Key in the Single Invoice section before processing batch.",
                                "Validation Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                Log(LogLevel.Warning, "Batch processing cancelled: Invalid Biller GUID or Web Service Key");
                return;
            }

            if (!File.Exists(filePath))
            {
                MessageBox.Show("The selected file does not exist or cannot be accessed.",
                              "File Not Found",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
                Log(LogLevel.Warning, $"Batch processing cancelled: File not found: {filePath}");
                return;
            }

            // Process the batch through the helper
            await _batchProcessingHelper.ProcessBatchFile(billerGUID, webServiceKey, filePath, hasAccountNumbers, defaultAccountNumber);
        }

        private async void ProcessCustomerRecord_Click(object sender, RoutedEventArgs e)
        {
            string billerGUID = BillerGUID.Text;
            string webServiceKey = WebServiceKey.Text;
            string accountNumber = CustomerAccountNumber.Text;

            Log(LogLevel.Info, $"Looking up customer record for account: {accountNumber}");

            if (ValidationHelper.ValidateGUID(billerGUID) && ValidationHelper.ValidateGUID(webServiceKey) && !string.IsNullOrEmpty(accountNumber))
            {
                try
                {
                    Log(LogLevel.Debug, "Calling customer record web service...");
                    CustomerResult.Text = "Processing...";

                    // Add debug logging to see what's happening
                    Log(LogLevel.Debug, $"Using Biller GUID: {billerGUID}");
                    Log(LogLevel.Debug, $"Using Web Service Key: {webServiceKey}");

                    string result = await _apiService.GetCustomerRecord(billerGUID, webServiceKey, accountNumber);

                    // Add debug logging to see what result we got back
                    Log(LogLevel.Debug, $"Raw customer record result length: {result?.Length ?? 0}");
                    Log(LogLevel.Debug, $"First 100 chars of result: {result?.Substring(0, Math.Min(100, result?.Length ?? 0))}");

                    CustomerResult.Text = result;

                    // Force UI update
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

                    Log(LogLevel.Info, $"Customer record for account {accountNumber} processed successfully");
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Error processing customer record: {ex.Message}";
                    CustomerResult.Text = errorMessage;
                    Log(LogLevel.Error, errorMessage);
                }
            }
            else
            {
                string validationError = "Invalid GUIDs or Account Number";
                MessageBox.Show("Please enter valid GUIDs and Account Number.");
                Log(LogLevel.Warning, validationError);
            }
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            _loggingHelper.ClearLog();
        }

        private void SaveLogs_Click(object sender, RoutedEventArgs e)
        {
            _loggingHelper.SaveLogs();
        }

        private void GenerateSampleCSV_Click(object sender, RoutedEventArgs e)
        {
            // Fix the type mismatch by explicitly casting the enum
            CsvHelper.GenerateSampleCSV((level, message) => Log((LogLevel)(int)level, message));
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Log(LogLevel.Info, "Application exit requested from menu");
            Application.Current.Shutdown();
        }

        private void Documentation_Click(object sender, RoutedEventArgs e)
        {
            Log(LogLevel.Info, "Documentation requested");
            ShowDocumentationWindow();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            Log(LogLevel.Info, "About dialog requested");
            var aboutDialog = new AboutDialog(this, APP_VERSION,
                (level, message) => Log((LogLevel)(int)level, message));
            aboutDialog.Show();
        }

        private void ShowDocumentationWindow()
        {
            DocumentationHelper.ShowDocumentationWindow(this,
                (level, message) => Log((LogLevel)(int)level, message));
        }

        /// <summary>
        /// Initializes the credential manager and populates the credential set combo box
        /// </summary>
        private void InitializeCredentialManager()
        {
            try
            {
                // Setup credential set combo box
                RefreshCredentialSetComboBox();

                // No need to set text on a non-existent control
                Log(LogLevel.Info, "Credential manager initialized");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to initialize credential manager: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes the credential set combo box with current saved credential sets
        /// </summary>
        private void RefreshCredentialSetComboBox()
        {
            try
            {
                string? previouslySelected = (CredentialSetComboBox.SelectedItem as CredentialManager.CredentialSet)?.Name;

                // Get all credential sets and populate the combo box
                var credentialSets = CredentialManager.GetAllCredentialSets();
                CredentialSetComboBox.ItemsSource = credentialSets;

                // Try to reselect the previously selected item
                if (!string.IsNullOrEmpty(previouslySelected))
                {
                    var matchingSet = credentialSets.FirstOrDefault(c => c.Name.Equals(previouslySelected, StringComparison.OrdinalIgnoreCase));
                    if (matchingSet != null)
                    {
                        CredentialSetComboBox.SelectedItem = matchingSet;
                    }
                    else if (credentialSets.Count > 0)
                    {
                        CredentialSetComboBox.SelectedIndex = 0;
                    }
                }
                else if (credentialSets.Count > 0)
                {
                    CredentialSetComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to refresh credential sets: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles selection changes in the credential set combo box
        /// </summary>
        private void CredentialSetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Handle the selection change logic here
                var selectedCredentialSet = CredentialSetComboBox.SelectedItem as CredentialManager.CredentialSet;
                if (selectedCredentialSet != null)
                {
                    // Update the UI with the selected credential set
                    BillerGUID.Text = selectedCredentialSet.BillerGUID;
                    WebServiceKey.Text = selectedCredentialSet.WebServiceKey;
                    // Don't update non-existent text box

                    Log(LogLevel.Info, $"Loaded credential set: {selectedCredentialSet.Name}");
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error selecting credential set: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens the credential management window
        /// </summary>
        private void ManageCredentials_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create a credentials management dialog
                var credentialManagementWindow = new CredentialManagementWindow();
                credentialManagementWindow.Owner = this;

                // If the dialog was closed with OK, refresh the credential set combo box
                if (credentialManagementWindow.ShowDialog() == true)
                {
                    RefreshCredentialSetComboBox();
                    Log(LogLevel.Info, "Credential sets updated");
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error opening credential management: {ex.Message}");
                MessageBox.Show($"Error opening credential management: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Simple input dialog for collecting text input from the user
        /// </summary>
        public class InputDialog : Window
        {
            public string ResponseText { get; private set; } = string.Empty;

            public InputDialog(string title, string promptText)
            {
                this.Title = title;
                this.Width = 400;
                this.Height = 175;
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                this.ResizeMode = ResizeMode.NoResize;

                Grid grid = new Grid();
                grid.Margin = new Thickness(15);
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                TextBlock prompt = new TextBlock
                {
                    Text = promptText,
                    Margin = new Thickness(0, 0, 0, 10),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(prompt, 0);
                grid.Children.Add(prompt);

                TextBox input = new TextBox
                {
                    Margin = new Thickness(0, 0, 0, 20),
                    Padding = new Thickness(5),
                    Height = 35
                };
                Grid.SetRow(input, 1);
                grid.Children.Add(input);

                StackPanel buttons = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                Button okButton = new Button
                {
                    Content = "OK",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0),
                    IsDefault = true
                };
                okButton.Click += (s, e) =>
                {
                    ResponseText = input.Text;
                    DialogResult = true;
                    Close();
                };

                Button cancelButton = new Button
                {
                    Content = "Cancel",
                    Width = 80,
                    Height = 30,
                    IsCancel = true
                };
                cancelButton.Click += (s, e) =>
                {
                    DialogResult = false;
                    Close();
                };

                buttons.Children.Add(okButton);
                buttons.Children.Add(cancelButton);
                Grid.SetRow(buttons, 2);
                grid.Children.Add(buttons);

                this.Content = grid;

                input.Focus();
            }
        }

        #region Windows Task Scheduler

        private void InitializeTaskSchedulerTab()
        {
            // Refresh the task list when tab is initialized
            RefreshTaskSchedulerList();

            // Ensure the ComboBox has a selected item
            if (WindowsTaskScheduleComboBox.Items.Count > 0 && WindowsTaskScheduleComboBox.SelectedIndex == -1)
            {
                WindowsTaskScheduleComboBox.SelectedIndex = 0;
            }
        }

        private void RefreshTaskSchedulerList()
        {
            try
            {
                // Save current selection
                object currentSelection = WindowsTaskScheduleComboBox.SelectedItem;

                TaskSchedulerTaskList.ItemsSource = _scheduleManager.ScheduledTasks;
                Log(LogLevel.Info, "Refreshed task scheduler task list");

                // Ensure the ComboBox has a selected item
                if (WindowsTaskScheduleComboBox.Items.Count > 0)
                {
                    if (currentSelection != null)
                    {
                        // Try to restore previous selection
                        WindowsTaskScheduleComboBox.SelectedItem = currentSelection;
                    }

                    // If nothing is selected, select the first item
                    if (WindowsTaskScheduleComboBox.SelectedIndex == -1)
                    {
                        WindowsTaskScheduleComboBox.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error refreshing task list: {ex.Message}");
            }
        }



        private void TaskSchedulerTaskList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var selectedTask = TaskSchedulerTaskList.SelectedItem as ScheduledTask;
                if (selectedTask != null)
                {
                    // Generate suggested Windows task name
                    WindowsTaskNameTextBox.Text = $"InvoiceBalanceRefresher - {selectedTask.Name}";

                    // Generate the command line that would be used
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName
                        ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                    CommandLineTextBox.Text = $"\"{exePath}\" --schedule {selectedTask.Id}";

                    Log(LogLevel.Debug, $"Selected task for Windows Task Scheduler: {selectedTask.Name}");
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error selecting task: {ex.Message}");
            }
        }

        private void RefreshTaskList_Click(object sender, RoutedEventArgs e)
        {
            RefreshTaskSchedulerList();
        }

        private void TestWindowsTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedTask = TaskSchedulerTaskList.SelectedItem as ScheduledTask;
                if (selectedTask == null)
                {
                    MessageBox.Show("Please select a task to test.", "No Task Selected",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                TaskSchedulerStatusText.Text = "Running task...";
                Log(LogLevel.Info, $"Starting to run task: {selectedTask.Name}");

                // Run the task directly through the RunScheduledTask method
                RunScheduledTask(selectedTask);
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error in test task button click: {ex.Message}");
                TaskSchedulerStatusText.Text = $"Error: {ex.Message}";
                TaskSchedulerStatusText.Foreground = (SolidColorBrush)FindResource("ErrorBrush");
            }
        }

        private void CreateWindowsTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedTask = TaskSchedulerTaskList.SelectedItem as ScheduledTask;
                if (selectedTask == null)
                {
                    MessageBox.Show("Please select a task to schedule.", "No Task Selected",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string taskName = WindowsTaskNameTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(taskName))
                {
                    MessageBox.Show("Please enter a name for the Windows scheduled task.",
                        "Task Name Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get selected schedule type
                var scheduleComboItem = WindowsTaskScheduleComboBox.SelectedItem as ComboBoxItem;
                if (scheduleComboItem == null)
                {
                    MessageBox.Show("Please select a schedule type.", "Schedule Required",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string scheduleType = scheduleComboItem.Content?.ToString() ?? "Daily";

                // Get application path
                string exePath = Process.GetCurrentProcess().MainModule?.FileName
                    ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                string arguments = $"--schedule {selectedTask.Id}";

                // Create the task using Windows Task Scheduler API
                using (TaskService ts = new TaskService())
                {
                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = $"Runs Invoice Balance Refresher with task: {selectedTask.Name}";

                    // Set the action (run the program)
                    td.Actions.Add(new ExecAction(exePath, arguments, AppDomain.CurrentDomain.BaseDirectory));

                    // Configure the trigger based on selected schedule
                    switch (scheduleType)
                    {
                        case "Daily":
                            td.Triggers.Add(new DailyTrigger { DaysInterval = 1 });
                            break;
                        case "Weekly":
                            td.Triggers.Add(new WeeklyTrigger { DaysOfWeek = DaysOfTheWeek.Monday });
                            break;
                        case "Monthly":
                            td.Triggers.Add(new MonthlyTrigger { DaysOfMonth = new int[] { 1 } });
                            break;
                        case "At startup":
                            td.Triggers.Add(new BootTrigger());
                            break;
                        case "On idle":
                            td.Triggers.Add(new IdleTrigger());
                            break;
                        default:
                            td.Triggers.Add(new DailyTrigger { DaysInterval = 1 });
                            break;
                    }

                    // Set additional settings
                    td.Settings.Hidden = false;
                    td.Settings.StartWhenAvailable = true;
                    td.Settings.RunOnlyIfNetworkAvailable = true;
                    td.Principal.LogonType = TaskLogonType.InteractiveToken;

                    // Register the task
                    ts.RootFolder.RegisterTaskDefinition(taskName, td);

                    Log(LogLevel.Info, $"Created Windows scheduled task '{taskName}' for task '{selectedTask.Name}'");

                    TaskSchedulerStatusText.Text = $"Task '{taskName}' created successfully in Windows Task Scheduler.";
                    TaskSchedulerStatusText.Foreground = (SolidColorBrush)FindResource("SuccessBrush");

                    MessageBox.Show($"Windows scheduled task '{taskName}' created successfully!",
                        "Task Created", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error creating Windows scheduled task: {ex.Message}");
                TaskSchedulerStatusText.Text = $"Error creating task: {ex.Message}";
                TaskSchedulerStatusText.Foreground = (SolidColorBrush)FindResource("ErrorBrush");

                MessageBox.Show($"Error creating Windows scheduled task: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion Windows Task Scheduler


        /// <summary>
        /// Handles the title bar drag to move the window
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Double-click toggles maximize/restore
                MaximizeButton_Click(sender, e);
            }
            else
            {
                // Single click starts dragging
                this.DragMove();
            }
        }


        /// <summary>
        /// Minimizes the window
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Toggles maximize/restore window state
        /// </summary>
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                MaximizeButton.Content = "☐"; // Square symbol for maximize
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                MaximizeButton.Content = "❐"; // Different square symbol for restore
            }
        }

        /// <summary>
        /// Closes the application
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Log(LogLevel.Info, "Application closing");
            Application.Current.Shutdown();
        }


        /// <summary>
        /// Saves the current credentials with the specified name
        /// </summary>
        private void SaveCredentials_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string billerGUID = BillerGUID.Text.Trim();
                string webServiceKey = WebServiceKey.Text.Trim();
                // Use a prompt dialog to get the credential set name instead of non-existent TextBox
                string credentialSetName = "";

                // Show an input dialog to get credential set name
                var inputDialog = new InputDialog("Save Credentials", "Enter a name for this credential set:");
                if (inputDialog.ShowDialog() == true)
                {
                    credentialSetName = inputDialog.ResponseText.Trim();
                }
                else
                {
                    return; // User cancelled
                }

                // Validate inputs
                if (string.IsNullOrWhiteSpace(credentialSetName))
                {
                    MessageBox.Show("Please enter a name for this credential set.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validate GUIDs before saving
                if (!ValidationHelper.ValidateGUID(billerGUID))
                {
                    MessageBox.Show("Please enter a valid Biller GUID.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    BillerGUID.Focus();
                    return;
                }

                if (!ValidationHelper.ValidateGUID(webServiceKey))
                {
                    MessageBox.Show("Please enter a valid Web Service Key.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    WebServiceKey.Focus();
                    return;
                }

                // Check if this name already exists and confirm overwrite
                var existingCredentialSets = CredentialManager.GetAllCredentialSets();
                bool nameExists = existingCredentialSets.Any(c => c.Name.Equals(credentialSetName, StringComparison.OrdinalIgnoreCase));

                if (nameExists)
                {
                    var result = MessageBox.Show($"A credential set named '{credentialSetName}' already exists. Do you want to overwrite it?",
                        "Confirm Overwrite", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                // Save credentials
                CredentialManager.SaveCredentialSet(credentialSetName, billerGUID, webServiceKey);
                Log(LogLevel.Info, $"Credential set '{credentialSetName}' saved successfully");
                MessageBox.Show("Your credentials have been saved securely.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // Refresh the credential set combo box
                RefreshCredentialSetComboBox();

                // Select the newly saved credential set
                var credentialSets = CredentialManager.GetAllCredentialSets();
                var newSet = credentialSets.FirstOrDefault(c => c.Name.Equals(credentialSetName, StringComparison.OrdinalIgnoreCase));
                if (newSet != null)
                {
                    CredentialSetComboBox.SelectedItem = newSet;
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error saving credentials: {ex.Message}");
                MessageBox.Show($"Error saving credentials: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // Delete credential set method
        private void DeleteCredentialSet_Click(object sender, RoutedEventArgs e)
        {
            var selectedCredentialSet = CredentialSetComboBox.SelectedItem as CredentialManager.CredentialSet;
            if (selectedCredentialSet == null)
            {
                MessageBox.Show("Please select a credential set to delete.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Confirm deletion
            var result = MessageBox.Show($"Are you sure you want to delete the credential set '{selectedCredentialSet.Name}'?",
                "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Delete the credential set
                if (CredentialManager.DeleteCredentialSet(selectedCredentialSet.Name))
                {
                    Log(LogLevel.Info, $"Credential set '{selectedCredentialSet.Name}' deleted successfully");

                    // Refresh the credential set combo box
                    RefreshCredentialSetComboBox();
                }
                else
                {
                    Log(LogLevel.Error, $"Failed to delete credential set '{selectedCredentialSet.Name}'");
                }
            }
        }

        #endregion

        #region Helper Methods

        private void Log(LogLevel level, string message)
        {
            if (_loggingHelper != null)
            {
                _loggingHelper.Log(level, message);
            }
            else
            {
                // Fallback for logging before _loggingHelper is initialized
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}");
            }
        }


        #endregion

        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }
    }

    // RelayCommand implementation for keyboard shortcuts
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object>? _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter ?? new object());
        }

        public void Execute(object? parameter)
        {
            _execute(parameter ?? new object());
        }
    }

}