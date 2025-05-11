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


        // Application version
        private const string APP_VERSION = "1.4.0"; // Updated version

        public MainWindow()
        {
            InitializeComponent();

            // Initialize helpers and services
            _loggingHelper = new LoggingHelper(ConsoleLog, paragraph => {
                ConsoleLog.Document.Blocks.Add(paragraph);
                ConsoleLog.ScrollToEnd();
            });

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
                async (billerGUID, webServiceKey, csvFilePath, hasAccountNumbers) =>
                {
                    try
                    {
                        return await ProcessBatchInternal(billerGUID, webServiceKey, csvFilePath, hasAccountNumbers);
                    }
                    catch (Exception ex)
                    {
                        Log(LogLevel.Error, $"Scheduled batch processing failed: {ex.Message}");
                        return false;
                    }
                });

            // Set up initial UI state
            InitializeLogging();
            AddSchedulerMenuItem();

            InitializeCredentialManager();

            // Try to load saved credentials
            LoadSavedCredentials();

            // Log startup
            Log(LogLevel.Info, $"Invoice Balance Refresher v{APP_VERSION} started");
            
            // Log keyboard shortcuts availability
            Log(LogLevel.Info, "Keyboard shortcuts initialized");
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

        private async Task<bool> ProcessBatchInternal(string billerGUID, string webServiceKey, string filePath, bool hasAccountNumbers)
        {
            // This is a facade method that delegates to the BatchProcessingHelper
            return await _batchProcessingHelper.ProcessBatchFile(billerGUID, webServiceKey, filePath, hasAccountNumbers);
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

        private void AddSchedulerMenuItem()
        {
            // Get the first Menu control in the window
            var menu = LogicalTreeHelper.FindLogicalNode(this, "MainMenu") as Menu;

            if (menu == null)
            {
                // If not found by name, get the first Menu in the window
                menu = UIHelper.FindVisualChildren<Menu>(this).FirstOrDefault();
            }

            if (menu != null)
            {
                // Find the File menu
                var fileMenu = menu.Items.OfType<MenuItem>().FirstOrDefault(m =>
                    m.Header != null && m.Header.ToString()?.Contains("File") == true);

                if (fileMenu != null)
                {
                    // Find the Exit menu item
                    int exitIndex = -1;
                    for (int i = 0; i < fileMenu.Items.Count; i++)
                    {
                        if (fileMenu.Items[i] is MenuItem menuItem &&
                            menuItem.Header != null &&
                            menuItem.Header.ToString()?.Contains("Exit") == true)
                        {
                            exitIndex = i;
                            break;
                        }
                    }

                    // Find the separator before Exit
                    int separatorIndex = -1;
                    if (exitIndex > 0 && fileMenu.Items[exitIndex - 1] is Separator)
                    {
                        separatorIndex = exitIndex - 1;
                    }

                    // Create the Scheduler menu item
                    var scheduleMenuItem = new MenuItem { Header = "_Scheduler" };
                    scheduleMenuItem.Click += Scheduler_Click;

                    // If we found the Exit menu item position
                    if (exitIndex >= 0)
                    {
                        // Add the Scheduler item before the separator (if exists) or before Exit
                        int insertPosition = (separatorIndex >= 0) ? separatorIndex : exitIndex;
                        fileMenu.Items.Insert(insertPosition, scheduleMenuItem);

                        // If there was no separator before Exit, add one now between Scheduler and Exit
                        if (separatorIndex < 0)
                        {
                            fileMenu.Items.Insert(insertPosition + 1, new Separator());
                        }

                        Log(LogLevel.Info, "Scheduler menu item added successfully");
                    }
                    else
                    {
                        // If we couldn't find Exit, just add Scheduler to the end with a separator
                        if (fileMenu.Items.Count > 0 && !(fileMenu.Items[fileMenu.Items.Count - 1] is Separator))
                        {
                            fileMenu.Items.Add(new Separator());
                        }
                        fileMenu.Items.Add(scheduleMenuItem);
                        Log(LogLevel.Info, "Scheduler menu item added successfully at the end of File menu");
                    }
                }
                else
                {
                    Log(LogLevel.Warning, "Could not find File menu to add scheduler menu item");
                }
            }
            else
            {
                Log(LogLevel.Warning, "Could not find main menu to add scheduler menu item");
            }
        }

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
                AddToWindowsTaskScheduler = task.AddToWindowsTaskScheduler
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
                Height = 750, // Increased height to accommodate new options
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
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Windows Task Group
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

            // Add enough rows for all settings including new Windows Task Scheduler option
            for (int i = 0; i < 6; i++)
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

            // Add frequency options
            freqCombo.Items.Add(ScheduleFrequency.Once);
            freqCombo.Items.Add(ScheduleFrequency.Daily);
            freqCombo.Items.Add(ScheduleFrequency.Weekly);
            freqCombo.Items.Add(ScheduleFrequency.Monthly);
            freqCombo.SelectedItem = task.Frequency;

            Grid.SetRow(freqCombo, 2);
            Grid.SetColumn(freqCombo, 1);

            // Run time
            var timeLabel = CreateLabel("Run Time:", 3, 0);

            // Create time picker panel
            var timePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 5)
            };

            // Time components - Fixed step parameter for hours from 60 to 1
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

            Grid.SetRow(timePanel, 3);
            Grid.SetColumn(timePanel, 1);

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

            // Add Windows Task Scheduler checkbox
            var winTaskLabel = CreateLabel("Windows Task Scheduler:", 5, 0);
            var winTaskCheck = new CheckBox
            {
                Content = "Add to Windows Task Scheduler",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                IsChecked = task.AddToWindowsTaskScheduler,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                ToolTip = "When checked, this task will be added to Windows Task Scheduler to run even when the application is closed"
            };
            Grid.SetRow(winTaskCheck, 5);
            Grid.SetColumn(winTaskCheck, 1);

            // Add all controls to settings grid
            settingsGrid.Children.Add(nameLabel);
            settingsGrid.Children.Add(nameBox);
            settingsGrid.Children.Add(descLabel);
            settingsGrid.Children.Add(descBox);
            settingsGrid.Children.Add(freqLabel);
            settingsGrid.Children.Add(freqCombo);
            settingsGrid.Children.Add(timeLabel);
            settingsGrid.Children.Add(timePanel);
            settingsGrid.Children.Add(enabledCheck);
            settingsGrid.Children.Add(winTaskLabel);
            settingsGrid.Children.Add(winTaskCheck);

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

            // Windows Task Scheduler settings group
            var winTaskGroup = new GroupBox
            {
                Header = "Windows Task Scheduler Options",
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
                Background = new SolidColorBrush(Color.FromArgb(20, 24, 180, 233)),
                IsEnabled = task.AddToWindowsTaskScheduler
            };

            // Create Windows Task options grid
            var winTaskGrid = new Grid();
            winTaskGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            winTaskGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Add rows for all Windows Task options
            for (int i = 0; i < 9; i++)
            {
                winTaskGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // Frequency-specific options - using a content control to swap based on frequency
            var frequencyOptionsContentControl = new ContentControl();
            Grid.SetRow(frequencyOptionsContentControl, 0);
            Grid.SetColumn(frequencyOptionsContentControl, 0);
            Grid.SetColumnSpan(frequencyOptionsContentControl, 2);

            // Create panels for each frequency type
            var dailyPanel = CreateDailyOptionsPanel(task);
            var weeklyPanel = CreateWeeklyOptionsPanel(task);
            var monthlyPanel = CreateMonthlyOptionsPanel(task);

            // Switch panel based on selected frequency
            // Explicitly specify the namespace for Action to resolve ambiguity
            System.Action updateFrequencyOptionsPanel = () =>
            {
                switch (freqCombo.SelectedItem)
                {
                    case ScheduleFrequency.Daily:
                        frequencyOptionsContentControl.Content = dailyPanel;
                        break;
                    case ScheduleFrequency.Weekly:
                        frequencyOptionsContentControl.Content = weeklyPanel;
                        break;
                    case ScheduleFrequency.Monthly:
                        frequencyOptionsContentControl.Content = monthlyPanel;
                        break;
                    default:
                        frequencyOptionsContentControl.Content = null;
                        break;
                }
            };


            // Update panel when frequency changes
            freqCombo.SelectionChanged += (s, e) => updateFrequencyOptionsPanel();

            // Call once to initialize
            updateFrequencyOptionsPanel();

            winTaskGrid.Children.Add(frequencyOptionsContentControl);

            // Run with highest privileges
            var privilegesCheck = new CheckBox
            {
                Content = "Run with highest privileges",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                IsChecked = task.RunWithHighestPrivileges,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            Grid.SetRow(privilegesCheck, 1);
            Grid.SetColumn(privilegesCheck, 0);
            Grid.SetColumnSpan(privilegesCheck, 2);
            winTaskGrid.Children.Add(privilegesCheck);

            // Power options
            var batteryCheck = new CheckBox
            {
                Content = "Allow run on battery power",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                IsChecked = task.AllowRunOnBattery,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            Grid.SetRow(batteryCheck, 2);
            Grid.SetColumn(batteryCheck, 0);
            winTaskGrid.Children.Add(batteryCheck);

            var wakeCheck = new CheckBox
            {
                Content = "Wake computer to run task",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                IsChecked = task.WakeToRun,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            Grid.SetRow(wakeCheck, 2);
            Grid.SetColumn(wakeCheck, 1);
            winTaskGrid.Children.Add(wakeCheck);

            // Network condition
            var networkCheck = new CheckBox
            {
                Content = "Run only if network is available",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                IsChecked = task.RunOnlyIfNetworkAvailable,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            Grid.SetRow(networkCheck, 3);
            Grid.SetColumn(networkCheck, 0);
            Grid.SetColumnSpan(networkCheck, 2);
            winTaskGrid.Children.Add(networkCheck);

            // Execution time limit
            var timeLimitLabel = CreateLabel("Execution time limit (minutes):", 4, 0);
            Grid.SetRow(timeLimitLabel, 4);
            Grid.SetColumn(timeLimitLabel, 0);
            winTaskGrid.Children.Add(timeLimitLabel);

            var timeLimitBox = new TextBox
            {
                Text = task.ExecutionTimeLimitMinutes.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(5, 3, 5, 3),
                Width = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                ToolTip = "Set to 0 for no time limit"
            };
            Grid.SetRow(timeLimitBox, 4);
            Grid.SetColumn(timeLimitBox, 1);
            winTaskGrid.Children.Add(timeLimitBox);

            // Retry settings
            var retryPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 5)
            };

            var retryCountLabel = new TextBlock
            {
                Text = "Retry count:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };

            var retryCountBox = new TextBox
            {
                Text = task.MaxRetryCount.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(5, 3, 5, 3),
                Width = 50
            };

            var retryIntervalLabel = new TextBlock
            {
                Text = "Retry interval (minutes):",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(15, 0, 5, 0),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };

            var retryIntervalBox = new TextBox
            {
                Text = task.RetryIntervalMinutes.ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(5, 3, 5, 3),
                Width = 50
            };

            retryPanel.Children.Add(retryCountLabel);
            retryPanel.Children.Add(retryCountBox);
            retryPanel.Children.Add(retryIntervalLabel);
            retryPanel.Children.Add(retryIntervalBox);

            Grid.SetRow(retryPanel, 5);
            Grid.SetColumn(retryPanel, 0);
            Grid.SetColumnSpan(retryPanel, 2);
            winTaskGrid.Children.Add(retryPanel);

            // Custom command line option
            var customOptionLabel = CreateLabel("Custom command line option:", 6, 0);
            Grid.SetRow(customOptionLabel, 6);
            Grid.SetColumn(customOptionLabel, 0);
            winTaskGrid.Children.Add(customOptionLabel);

            var customOptionBox = new TextBox
            {
                Text = task.CustomOption,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(5, 3, 5, 3)
            };
            Grid.SetRow(customOptionBox, 6);
            Grid.SetColumn(customOptionBox, 1);
            winTaskGrid.Children.Add(customOptionBox);

            // Credentials
            var credentialsLabel = new TextBlock
            {
                Text = "Task Credentials:",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 10, 0, 5),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            Grid.SetRow(credentialsLabel, 7);
            Grid.SetColumn(credentialsLabel, 0);
            Grid.SetColumnSpan(credentialsLabel, 2);
            winTaskGrid.Children.Add(credentialsLabel);

            var credentialsPanel = new Grid();
            credentialsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            credentialsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            credentialsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            credentialsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var usernameLabel = new TextBlock
            {
                Text = "Username:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 5, 5),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            Grid.SetRow(usernameLabel, 0);
            Grid.SetColumn(usernameLabel, 0);
            credentialsPanel.Children.Add(usernameLabel);

            var usernameBox = new TextBox
            {
                Text = task.WindowsTaskUsername,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(5, 3, 5, 3),
                ToolTip = "Leave empty to use current user (recommended)"
            };
            Grid.SetRow(usernameBox, 0);
            Grid.SetColumn(usernameBox, 1);
            credentialsPanel.Children.Add(usernameBox);

            var passwordLabel = new TextBlock
            {
                Text = "Password:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 5, 5),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
            };
            Grid.SetRow(passwordLabel, 1);
            Grid.SetColumn(passwordLabel, 0);
            credentialsPanel.Children.Add(passwordLabel);

            var passwordBox = new PasswordBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(5, 3, 5, 3)
            };

            // Set password if exists
            if (!string.IsNullOrEmpty(task.WindowsTaskPassword))
            {
                passwordBox.Password = task.WindowsTaskPassword;
            }

            Grid.SetRow(passwordBox, 1);
            Grid.SetColumn(passwordBox, 1);
            credentialsPanel.Children.Add(passwordBox);

            Grid.SetRow(credentialsPanel, 8);
            Grid.SetColumn(credentialsPanel, 0);
            Grid.SetColumnSpan(credentialsPanel, 2);
            winTaskGrid.Children.Add(credentialsPanel);

            winTaskGroup.Content = winTaskGrid;
            Grid.SetRow(winTaskGroup, 3);
            mainGrid.Children.Add(winTaskGroup);

            // Toggle Windows Task Scheduler options when checkbox changes
            winTaskCheck.Checked += (s, e) => winTaskGroup.IsEnabled = true;
            winTaskCheck.Unchecked += (s, e) => winTaskGroup.IsEnabled = false;

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
                task.AddToWindowsTaskScheduler = winTaskCheck.IsChecked.GetValueOrDefault(false);

                // Calculate run time from the time picker controls
                int selectedHour = (int)hoursCombo.SelectedItem;
                int selectedMinute = Convert.ToInt32(minsCombo.SelectedItem.ToString());
                string ampm = (string)ampmCombo.SelectedItem;

                // Convert to 24-hour format
                if (ampm == "PM" && selectedHour < 12) selectedHour += 12;
                else if (ampm == "AM" && selectedHour == 12) selectedHour = 0;

                task.RunTime = new TimeSpan(selectedHour, selectedMinute, 0);

                // Windows Task Scheduler options
                task.RunWithHighestPrivileges = privilegesCheck.IsChecked ?? false;
                task.AllowRunOnBattery = batteryCheck.IsChecked ?? false;
                task.WakeToRun = wakeCheck.IsChecked ?? false;
                task.RunOnlyIfNetworkAvailable = networkCheck.IsChecked ?? false;

                // Parse numeric values with validation
                if (int.TryParse(timeLimitBox.Text, out int timeLimit))
                    task.ExecutionTimeLimitMinutes = timeLimit;

                if (int.TryParse(retryCountBox.Text, out int retryCount))
                    task.MaxRetryCount = (short)retryCount;

                if (int.TryParse(retryIntervalBox.Text, out int retryInterval))
                    task.RetryIntervalMinutes = retryInterval;


                task.CustomOption = customOptionBox.Text;
                task.WindowsTaskUsername = usernameBox.Text;
                task.WindowsTaskPassword = passwordBox.Password;

                // Update frequency-specific options
                if (task.Frequency == ScheduleFrequency.Daily)
                {
                    // Update daily options
                    var daysIntervalTextBox = dailyPanel.Children.OfType<StackPanel>().FirstOrDefault()?.Children.OfType<TextBox>().FirstOrDefault();
                    if (daysIntervalTextBox != null && int.TryParse(daysIntervalTextBox.Text, out int daysInterval))
                        task.DaysInterval = (short)(daysInterval > 0 ? daysInterval : 1);

                }
                else if (task.Frequency == ScheduleFrequency.Weekly)
                {
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
                }
                else if (task.Frequency == ScheduleFrequency.Monthly)
                {
                    // Update monthly options (selected days and months)
                    var dayCheckBoxes = monthlyPanel.Children.OfType<StackPanel>().FirstOrDefault(sp => sp.Name == "DaysPanel")?.Children.OfType<CheckBox>();
                    if (dayCheckBoxes != null)
                    {
                        // Initialize SelectedDaysOfMonth as an empty array instead of an integer
                        task.SelectedDaysOfMonth = new int[0];

                        // Add selected days to the array
                        var selectedDays = new List<int>();
                        foreach (var dayCheck in dayCheckBoxes)
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
            LightModeMenuItem.IsChecked = true;
            DarkModeMenuItem.IsChecked = false;
        }

        private void DarkMode_Click(object sender, RoutedEventArgs e)
        {
            _themeManager.SetDarkMode();
            LightModeMenuItem.IsChecked = false;
            DarkModeMenuItem.IsChecked = true;
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

                // If there are no credential sets, add an instruction
                if (CredentialManager.GetAllCredentialSets().Count == 0)
                {
                    CredentialSetNameTextBox.Text = "Enter a name for your first credential set";
                }

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
                    CredentialSetNameTextBox.Text = selectedCredentialSet.Name;

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
        /// Saves the current credentials with the specified name
        /// </summary>
        private void SaveCredentials_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string billerGUID = BillerGUID.Text.Trim();
                string webServiceKey = WebServiceKey.Text.Trim();
                string credentialSetName = CredentialSetNameTextBox.Text.Trim();

                // Validate inputs
                if (string.IsNullOrWhiteSpace(credentialSetName))
                {
                    MessageBox.Show("Please enter a name for this credential set.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    CredentialSetNameTextBox.Focus();
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
            _loggingHelper.Log(level, message);
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