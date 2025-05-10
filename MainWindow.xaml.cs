using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Microsoft.Win32;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Media;
using System.Windows.Shapes;
using IOPath = System.IO.Path;
using System.Windows.Documents;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xml;

namespace InvoiceBalanceRefresher
{
    public partial class MainWindow : Window
    {
        private readonly List<LogEntry> _logEntries = new List<LogEntry>();
        private string _sessionLogPath = string.Empty;
        private DispatcherTimer? _autoScrollTimer;
        private FlowDocument _originalDocument = new FlowDocument();
        private int _searchResultCount = 0;
        private string _originalBatchResults = string.Empty;
        private int _batchSearchResultCount = 0;
        // Update the version constant and add schedule info to the About dialog
        private const string APP_VERSION = "1.2.0"; // Updated version
        private readonly ScheduleManager _scheduleManager;


        public MainWindow()
        {
            InitializeComponent();
            InitializeLogging();

            ConsoleLog.Document = new FlowDocument();
            _originalDocument = new FlowDocument();

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

            // Add the Scheduler menu item
            AddSchedulerMenuItem();
        }



        private void InitializeLogging()
        {
            // Create logs directory if it doesn't exist
            string logsDirectory = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(logsDirectory);

            // Create a new session log file
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _sessionLogPath = IOPath.Combine(logsDirectory, $"Session_{timestamp}.log");

            Log(LogLevel.Info, "=== Session Started ===");
            Log(LogLevel.Info, $"Log file: {_sessionLogPath}");

            // Setup auto-scroll timer
            _autoScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _autoScrollTimer.Tick += (s, e) => ConsoleLog.ScrollToEnd();
            _autoScrollTimer.Start();
        }

        private void InitializeScheduler()
        {
            // Check for command-line arguments for scheduled tasks
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--schedule" && i + 1 < args.Length && Guid.TryParse(args[i + 1], out Guid taskId))
                {
                    // This was launched by the Windows Task Scheduler
                    var task = _scheduleManager.ScheduledTasks.FirstOrDefault(t => t.Id == taskId);
                    if (task != null)
                    {
                        Log(LogLevel.Info, $"Application started by scheduler for task: {task.Name}");
                        // Run the task
                        RunScheduledTask(task);
                    }
                }
            }

            // Add scheduler menu item
            AddSchedulerMenuItem();
        }

        private async Task<bool> ProcessBatchInternal(string billerGUID, string webServiceKey, string filePath, bool hasAccountNumbers)
        {
            Log(LogLevel.Info, $"Running scheduled batch process for file: {filePath}");
            Log(LogLevel.Info, $"Using Biller GUID: {billerGUID}");
            Log(LogLevel.Info, $"Using Web Service Key: {webServiceKey}");
            Log(LogLevel.Info, $"CSV has account numbers: {hasAccountNumbers}");

            try
            {
                // Read all lines from the CSV file
                var lines = File.ReadAllLines(filePath);
                var results = new StringBuilder();

                Log(LogLevel.Info, $"Found {lines.Length} records to process");

                // Add header to CSV results
                if (hasAccountNumbers)
                {
                    results.AppendLine("AccountNumber,InvoiceNumber,Status,BalanceDue,DueDate,TotalAmount");
                }
                else
                {
                    results.AppendLine("InvoiceNumber,Status,BalanceDue,DueDate,TotalAmount");
                }

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    string accountNumber = string.Empty;
                    string invoiceNumber = string.Empty;

                    // Process CSV line
                    if (hasAccountNumbers)
                    {
                        // Format expected: AccountNumber,InvoiceNumber
                        var parts = line.Split(',');
                        if (parts.Length >= 2)
                        {
                            accountNumber = parts[0].Trim();
                            invoiceNumber = parts[1].Trim();
                        }
                    }
                    else
                    {
                        // Format expected: InvoiceNumber only
                        invoiceNumber = line;
                    }

                    if (!string.IsNullOrEmpty(invoiceNumber))
                    {
                        try
                        {
                            Log(LogLevel.Debug, $"Processing invoice: {invoiceNumber}" +
                                (string.IsNullOrEmpty(accountNumber) ? "" : $" for account: {accountNumber}"));

                            // First refresh the balance if account number is provided
                            if (!string.IsNullOrEmpty(accountNumber))
                            {
                                Log(LogLevel.Debug, $"Refreshing balance for account {accountNumber}...");
                                await CallCustomerRecordService(billerGUID, webServiceKey, accountNumber);
                                Log(LogLevel.Info, $"Balance refreshed for account {accountNumber}");
                            }

                            // Then get invoice data
                            string resultText = await CallWebService(billerGUID, webServiceKey, invoiceNumber);

                            // Extract basic data for CSV
                            string csvLine;
                            if (hasAccountNumbers)
                            {
                                csvLine = $"{accountNumber}," + FormatInvoiceDataForCSV(invoiceNumber, resultText);
                            }
                            else
                            {
                                csvLine = FormatInvoiceDataForCSV(invoiceNumber, resultText);
                            }
                            results.AppendLine(csvLine);

                            Log(LogLevel.Info, $"Invoice {invoiceNumber} processed successfully");
                        }
                        catch (Exception ex)
                        {
                            string errorMsg = $"Error: {ex.Message}";
                            Log(LogLevel.Error, $"Error processing invoice {invoiceNumber}: {ex.Message}");

                            // Add to CSV results
                            if (hasAccountNumbers)
                            {
                                results.AppendLine($"{accountNumber},{invoiceNumber},Error,\"{ex.Message}\",,");
                            }
                            else
                            {
                                results.AppendLine($"{invoiceNumber},Error,\"{ex.Message}\",,");
                            }
                        }
                    }
                    else
                    {
                        Log(LogLevel.Warning, $"Empty invoice number in line {i + 1}");

                        // Add to CSV results
                        if (hasAccountNumbers)
                        {
                            results.AppendLine($"{accountNumber},Line {i + 1},Error,\"Empty invoice number\",,");
                        }
                        else
                        {
                            results.AppendLine($"Line {i + 1},Error,\"Empty invoice number\",,");
                        }
                    }
                }

                string resultFilePath = IOPath.Combine(IOPath.GetDirectoryName(filePath) ?? AppDomain.CurrentDomain.BaseDirectory, "InvoiceResults.csv");
                File.WriteAllText(resultFilePath, results.ToString());

                Log(LogLevel.Info, $"Scheduled batch processing completed. Results saved to {resultFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Scheduled batch processing failed: {ex.Message}");
                return false;
            }
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

        // Add this helper method to refresh the Schedule Manager UI if it's open
        private void RefreshScheduleManagerUI()
        {
            // Find the Schedule Manager window if it's open
            var scheduleWindow = Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.Title == "Schedule Manager");

            if (scheduleWindow != null)
            {
                // Find the DataGrid in the window
                var tasksGrid = FindVisualChildren<DataGrid>(scheduleWindow).FirstOrDefault();
                if (tasksGrid != null)
                {
                    // Refresh the grid by resetting its ItemsSource
                    tasksGrid.ItemsSource = null;
                    tasksGrid.ItemsSource = _scheduleManager.ScheduledTasks;
                }
            }
        }



        // Updated code to fix CS8602: Dereference of a possibly null reference.
        private void AddSchedulerMenuItem()
        {
            // Get the first Menu control in the window
            var menu = LogicalTreeHelper.FindLogicalNode(this, "MainMenu") as Menu;

            if (menu == null)
            {
                // If not found by name, get the first Menu in the window
                menu = this.FindVisualChildren<Menu>(this).FirstOrDefault();
            }

            if (menu != null)
            {
                // Find the File menu
                var fileMenu = menu.Items.OfType<MenuItem>().FirstOrDefault(m =>
                    m.Header != null && m.Header.ToString()?.Contains("File") == true); // Added null-conditional operator and null check

                if (fileMenu != null)
                {
                    // Find the Exit menu item
                    int exitIndex = -1;
                    for (int i = 0; i < fileMenu.Items.Count; i++)
                    {
                        if (fileMenu.Items[i] is MenuItem menuItem &&
                            menuItem.Header != null &&
                            menuItem.Header.ToString()?.Contains("Exit") == true) // Added null-conditional operator and null check
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

        private void OpenScheduleManager()
        {
            // Create the Schedule Manager window
            var scheduleWindow = new Window
            {
                Title = "Schedule Manager",
                Width = 1100,
                Height = 700,
                Background = (System.Windows.Media.SolidColorBrush)FindResource("BackgroundBrush"),
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

            var addButton = new Button
            {
                Content = "[ ADD NEW ]",
                Width = 120,
                Height = 30,
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064557")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold
            };

            var editButton = new Button
            {
                Content = "[ EDIT ]",
                Width = 120,
                Height = 30,
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064557")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold
            };

            var deleteButton = new Button
            {
                Content = "[ DELETE ]",
                Width = 120,
                Height = 30,
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064557")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold
            };

            var runNowButton = new Button
            {
                Content = "[ RUN NOW ]",
                Width = 120,
                Height = 30,
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064557")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold
            };

            var closeButton = new Button
            {
                Content = "[ CLOSE ]",
                Width = 120,
                Height = 30,
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064557")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold
            };

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

                // Find the DataGrid in the current window and refresh its ItemsSource
                var currentWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Schedule Manager");
                if (currentWindow != null)
                {
                    var tasksGrid = FindVisualChildren<DataGrid>(currentWindow).FirstOrDefault();
                    if (tasksGrid != null)
                    {
                        // Refresh the grid by resetting its ItemsSource
                        tasksGrid.ItemsSource = null;
                        tasksGrid.ItemsSource = _scheduleManager.ScheduledTasks;
                    }
                }
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
                CustomOption = task.CustomOption
            };

            // Open the edit dialog
            if (OpenScheduleEditDialog(taskCopy, false))
            {
                _scheduleManager.UpdateSchedule(taskCopy);

                // Find the DataGrid in the current window and refresh its ItemsSource
                var currentWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Schedule Manager");
                if (currentWindow != null)
                {
                    var tasksGrid = FindVisualChildren<DataGrid>(currentWindow).FirstOrDefault();
                    if (tasksGrid != null)
                    {
                        // Refresh the grid by resetting its ItemsSource
                        tasksGrid.ItemsSource = null;
                        tasksGrid.ItemsSource = _scheduleManager.ScheduledTasks;
                    }
                }
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
                Height = 650, // Increased height to accommodate new controls
                Background = (System.Windows.Media.SolidColorBrush)FindResource("BackgroundBrush"),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            // Create the main grid with margin
            var mainGrid = new Grid
            {
                Margin = new Thickness(15)
            };

            // Define grid rows
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

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
            var settingsGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Define settings columns and rows
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Add enough rows for all settings including new Windows Task Scheduler option
            for (int i = 0; i < 6; i++) // Changed from 5 to 6 to accommodate new row
            {
                settingsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // Name
            var nameLabel = new TextBlock
            {
                Text = "Name:",
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                MinWidth = 130
            };
            Grid.SetRow(nameLabel, 0);
            Grid.SetColumn(nameLabel, 0);

            var nameBox = new TextBox
            {
                Text = task.Name,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(5, 3, 5, 3)
            };
            Grid.SetRow(nameBox, 0);
            Grid.SetColumn(nameBox, 1);

            // Description
            var descLabel = new TextBlock
            {
                Text = "Description:",
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetRow(descLabel, 1);
            Grid.SetColumn(descLabel, 0);

            var descBox = new TextBox
            {
                Text = task.Description,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(5, 3, 5, 3)
            };
            Grid.SetRow(descBox, 1);
            Grid.SetColumn(descBox, 1);

            // Frequency
            var freqLabel = new TextBlock
            {
                Text = "Frequency:",
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetRow(freqLabel, 2);
            Grid.SetColumn(freqLabel, 0);

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
            var timeLabel = new TextBlock
            {
                Text = "Run Time:",
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetRow(timeLabel, 3);
            Grid.SetColumn(timeLabel, 0);

            // Create time picker panel
            var timePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 5)
            };

            // Hours combobox
            var hoursCombo = new ComboBox
            {
                MinWidth = 60,
                Margin = new Thickness(0, 0, 5, 0)
            };

            // Add hour options (12-hour format)
            for (int i = 1; i <= 12; i++)
            {
                hoursCombo.Items.Add(i);
            }

            // Minutes combobox
            var minsCombo = new ComboBox
            {
                MinWidth = 60,
                Margin = new Thickness(0, 0, 5, 0)
            };

            // Add minute options (in 5-minute increments)
            for (int i = 0; i < 60; i += 5)
            {
                minsCombo.Items.Add(i.ToString("00"));
            }

            // AM/PM combobox
            var ampmCombo = new ComboBox
            {
                MinWidth = 60
            };
            ampmCombo.Items.Add("AM");
            ampmCombo.Items.Add("PM");

            // Set initial values based on task's RunTime
            DateTime timeValue = DateTime.Today.Add(task.RunTime);
            int hour = timeValue.Hour % 12;
            if (hour == 0) hour = 12;
            hoursCombo.SelectedItem = hour;
            minsCombo.SelectedItem = timeValue.Minute.ToString("00");
            ampmCombo.SelectedItem = timeValue.Hour >= 12 ? "PM" : "AM";

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
            var winTaskLabel = new TextBlock
            {
                Text = "Windows Task Scheduler:",
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetRow(winTaskLabel, 5);
            Grid.SetColumn(winTaskLabel, 0);

            var winTaskCheck = new CheckBox
            {
                Content = "Add to Windows Task Scheduler",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                IsChecked = task.AddToWindowsTaskScheduler, // Default to true if null
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
            var csvPathLabel = new TextBlock
            {
                Text = "CSV File Path:",
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                MinWidth = 130
            };
            Grid.SetRow(csvPathLabel, 0);
            Grid.SetColumn(csvPathLabel, 0);

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

            // Biller GUID
            var billerLabel = new TextBlock
            {
                Text = "Biller GUID:",
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetRow(billerLabel, 1);
            Grid.SetColumn(billerLabel, 0);

            var billerBox = new TextBox
            {
                Text = task.BillerGUID,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(5, 3, 5, 3)
            };
            Grid.SetRow(billerBox, 1);
            Grid.SetColumn(billerBox, 1);

            // Web Service Key
            var keyLabel = new TextBlock
            {
                Text = "Web Service Key:",
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetRow(keyLabel, 2);
            Grid.SetColumn(keyLabel, 0);

            var keyBox = new TextBox
            {
                Text = task.WebServiceKey,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(5, 3, 5, 3)
            };
            Grid.SetRow(keyBox, 2);
            Grid.SetColumn(keyBox, 1);

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
            Grid.SetRow(csvGroup, 3);
            mainGrid.Children.Add(csvGroup);

            // Add buttons panel
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var saveButton = new Button
            {
                Content = "[ SAVE ]",
                Width = 120,
                Height = 30,
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064557")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold
            };

            var cancelButton = new Button
            {
                Content = "[ CANCEL ]",
                Width = 120,
                Height = 30,
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064557")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold
            };

            // Add button event handlers
            bool dialogResult = false;

            saveButton.Click += (s, e) =>
            {
                // Validate input fields
                if (string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    MessageBox.Show("Please enter a name for this schedule.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(csvPathBox.Text) || !File.Exists(csvPathBox.Text))
                {
                    MessageBox.Show("Please provide a valid CSV file path.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(billerBox.Text) || !ValidateGUID(billerBox.Text))
                {
                    MessageBox.Show("Please enter a valid Biller GUID.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(keyBox.Text) || !ValidateGUID(keyBox.Text))
                {
                    MessageBox.Show("Please enter a valid Web Service Key.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Update task with form values
                task.Name = nameBox.Text;
                task.Description = descBox.Text;
                task.CsvFilePath = csvPathBox.Text;
                task.BillerGUID = billerBox.Text;
                task.WebServiceKey = keyBox.Text;
                task.HasAccountNumbers = hasAccountsCheck.IsChecked ?? false;
                task.Frequency = (ScheduleFrequency)freqCombo.SelectedItem;
                task.IsEnabled = enabledCheck.IsChecked ?? true;
                task.AddToWindowsTaskScheduler = winTaskCheck.IsChecked.GetValueOrDefault(true);

                // Calculate run time from the time picker controls
                int selectedHour = (int)hoursCombo.SelectedItem;
                int selectedMinute = int.Parse((string)minsCombo.SelectedItem);
                string ampm = (string)ampmCombo.SelectedItem;

                // Convert to 24-hour format
                if (ampm == "PM" && selectedHour < 12)
                {
                    selectedHour += 12;
                }
                else if (ampm == "AM" && selectedHour == 12)
                {
                    selectedHour = 0;
                }

                task.RunTime = new TimeSpan(selectedHour, selectedMinute, 0);

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

            Grid.SetRow(buttonsPanel, 4);
            mainGrid.Children.Add(buttonsPanel);

            // Add UI to window
            editWindow.Content = mainGrid;

            // Show dialog
            editWindow.ShowDialog();

            return dialogResult;
        }



        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.Trim().ToLower();
            PerformSearch(searchText);
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Clear();
            PerformSearch(string.Empty);
        }

        private void LightMode_Click(object sender, RoutedEventArgs e)
        {
            SetLightMode();
            LightModeMenuItem.IsChecked = true;
            DarkModeMenuItem.IsChecked = false;
        }

        private void DarkMode_Click(object sender, RoutedEventArgs e)
        {
            SetDarkMode();
            LightModeMenuItem.IsChecked = false;
            DarkModeMenuItem.IsChecked = true;
        }

        // Update the calling code to handle the nullable return type
        private void SetLightMode()
        {
            // Change resources to light mode
            Application.Current.Resources["BackgroundBrush"] = Application.Current.Resources["LightBackgroundBrush"];
            Application.Current.Resources["ForegroundBrush"] = Application.Current.Resources["LightForegroundBrush"];
            Application.Current.Resources["BorderBrush"] = Application.Current.Resources["LightBorderBrush"];
            Application.Current.Resources["GroupBackgroundBrush"] = Application.Current.Resources["LightGroupBackgroundBrush"];
            Application.Current.Resources["ConsoleBackgroundBrush"] = Application.Current.Resources["LightConsoleBackgroundBrush"];
            Application.Current.Resources["HighlightBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F4F7"));
            Application.Current.Resources["SeparatorBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
            Application.Current.Resources["ConsoleHeaderBrush"] = Application.Current.Resources["LightConsoleHeaderBrush"];

            // Update button background and text color for light mode
            // Use MintGreenBrush for button backgrounds instead of hardcoded color
            foreach (Button button in FindVisualChildren<Button>(this))
            {
                button.Background = (SolidColorBrush)Application.Current.Resources["MintGreenBrush"];
                button.Foreground = (SolidColorBrush)Application.Current.Resources["CharcoalBrush"];
                button.BorderBrush = (SolidColorBrush)Application.Current.Resources["MintGreenBrush"];
            }

            // Update controls that aren't automatically updated by resource changes
            UpdateControlsForLightMode();

            // Update the console header grid background - find it by position instead of name
            Grid? consoleHeaderGrid = FindConsoleHeaderGrid();
            if (consoleHeaderGrid != null)
            {
                consoleHeaderGrid.Background = Application.Current.Resources["LightConsoleHeaderBrush"] as Brush;
            }

            Log(LogLevel.Info, "Switched to light mode");
        }





        // Helper method to find visual children of a specific type
        private IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
                yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T)
                    yield return (T)child;

                foreach (T childOfChild in FindVisualChildren<T>(child!))

                    yield return childOfChild;
            }
        }

        private void SetDarkMode()
        {
            // Change resources to dark mode
            Application.Current.Resources["BackgroundBrush"] = Application.Current.Resources["DarkBackgroundBrush"];
            Application.Current.Resources["ForegroundBrush"] = Application.Current.Resources["DarkForegroundBrush"];
            Application.Current.Resources["BorderBrush"] = Application.Current.Resources["DarkBorderBrush"];
            Application.Current.Resources["GroupBackgroundBrush"] = Application.Current.Resources["DarkGroupBackgroundBrush"];
            Application.Current.Resources["ConsoleBackgroundBrush"] = Application.Current.Resources["DarkConsoleBackgroundBrush"];
            Application.Current.Resources["HighlightBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A4A56"));
            Application.Current.Resources["SeparatorBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444444"));
            Application.Current.Resources["ConsoleHeaderBrush"] = Application.Current.Resources["DarkConsoleHeaderBrush"];

            // Update controls that aren't automatically updated by resource changes
            UpdateControlsForDarkMode();

            // Update the console header grid background - find it by position instead of name
            Grid? consoleHeaderGrid = FindConsoleHeaderGrid();
            if (consoleHeaderGrid != null)
            {
                consoleHeaderGrid.Background = Application.Current.Resources["DarkConsoleHeaderBrush"] as Brush;
            }

            Log(LogLevel.Info, "Switched to dark mode");
        }

        private void UpdateControlsForDarkMode()
        {
            // First update all TextBlocks to ensure they use the correct foreground color
            foreach (TextBlock textBlock in FindVisualChildren<TextBlock>(this))
            {
                // Only update if not part of a style that should keep its color
                if (!(textBlock.Parent is GroupBox) &&
                    !(textBlock.Parent is MenuItem) &&
                    !textBlock.Text.StartsWith("CONSOLE LOG") &&
                    !textBlock.Text.StartsWith("SYSTEM INFORMATION:") &&
                    !textBlock.Text.StartsWith("FEATURES:") &&
                    !textBlock.Text.StartsWith("TECHNICAL INFORMATION:"))
                {
                    textBlock.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;
                }
            }

            // Apply dark mode to TextBox backgrounds (they often have hardcoded white backgrounds)
            foreach (TextBox textBox in FindVisualChildren<TextBox>(this))
            {
                textBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252525"));
                textBox.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;
                textBox.CaretBrush = Application.Current.Resources["DarkForegroundBrush"] as Brush;
                textBox.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
                textBox.BorderBrush = Application.Current.Resources["DarkBorderBrush"] as Brush;
            }

            // Apply dark mode to any RichTextBox that may have hardcoded backgrounds
            foreach (RichTextBox richTextBox in FindVisualChildren<RichTextBox>(this))
            {
                richTextBox.Background = new SolidColorBrush(Colors.Transparent);
                richTextBox.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;
                richTextBox.BorderBrush = Application.Current.Resources["DarkBorderBrush"] as Brush;

                // Update selection colors
                richTextBox.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
                richTextBox.SelectionTextBrush = Brushes.White;
            }

            // Apply dark mode to Borders including those in deeply nested controls
            foreach (Border border in FindVisualChildren<Border>(this))
            {
                if (border.Background != null)
                {
                    // Update any white or light-colored backgrounds
                    var brush = border.Background as SolidColorBrush;
                    if (brush != null &&
                        (brush.Color == Colors.White ||
                         brush.Color.ToString() == "#FFF5F5F5" ||
                         brush.Color.ToString() == "#FFF0F0F0"))
                    {
                        border.Background = Application.Current.Resources["DarkGroupBackgroundBrush"] as Brush;
                    }
                }

                border.BorderBrush = Application.Current.Resources["DarkBorderBrush"] as Brush;
            }

            // Apply dark mode to BatchResults TextBox (special case with formatting)
            if (BatchResults != null)
            {
                BatchResults.Background = new SolidColorBrush(Colors.Transparent);
                BatchResults.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;
            }

            // Update SingleResult and CustomerResult TextBlocks
            if (SingleResult != null)
                SingleResult.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;

            if (CustomerResult != null)
                CustomerResult.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;

            // Update GroupBox headers and backgrounds
            foreach (GroupBox groupBox in FindVisualChildren<GroupBox>(this))
            {
                groupBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
                groupBox.Background = Application.Current.Resources["DarkGroupBackgroundBrush"] as Brush;
            }

            // Update Menu and MenuItem styles
            foreach (Menu menu in FindVisualChildren<Menu>(this))
            {
                menu.Background = Application.Current.Resources["DarkBackgroundBrush"] as Brush;
                menu.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
                menu.BorderBrush = Application.Current.Resources["DarkBorderBrush"] as Brush;
            }

            foreach (MenuItem menuItem in FindVisualChildren<MenuItem>(this))
            {
                menuItem.Background = Application.Current.Resources["DarkBackgroundBrush"] as Brush;
                menuItem.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
                menuItem.BorderBrush = Application.Current.Resources["DarkBorderBrush"] as Brush;
            }

            // Update any ScrollViewer backgrounds
            foreach (ScrollViewer scrollViewer in FindVisualChildren<ScrollViewer>(this))
            {
                scrollViewer.Background = new SolidColorBrush(Colors.Transparent);
            }

            // Update ProgressBar colors
            foreach (ProgressBar progressBar in FindVisualChildren<ProgressBar>(this))
            {
                progressBar.Background = Application.Current.Resources["DarkConsoleBackgroundBrush"] as Brush;
                progressBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
                progressBar.BorderBrush = Application.Current.Resources["DarkBorderBrush"] as Brush;
            }

            // Update Buttons in dark mode
            foreach (Button button in FindVisualChildren<Button>(this))
            {
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064557"));
                button.Foreground = Brushes.White;
                button.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
            }

            // Update RadioButtons in dark mode
            foreach (RadioButton radioButton in FindVisualChildren<RadioButton>(this))
            {
                radioButton.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;
                radioButton.Background = Application.Current.Resources["DarkBackgroundBrush"] as Brush;
            }

            // Update CheckBoxes in dark mode
            foreach (CheckBox checkBox in FindVisualChildren<CheckBox>(this))
            {
                checkBox.Foreground = Application.Current.Resources["DarkForegroundBrush"] as Brush;
                checkBox.Background = Application.Current.Resources["DarkBackgroundBrush"] as Brush;
            }

            // Force visual refresh
            InvalidateVisual();
        }


        // Also need to add the matching Light mode method for consistency
        private void UpdateControlsForLightMode()
        {
            // First update all TextBlocks to ensure they use the correct foreground color
            foreach (TextBlock textBlock in FindVisualChildren<TextBlock>(this))
            {
                // Only update if not part of a style that should keep its color
                if (!(textBlock.Parent is GroupBox) &&
                    !(textBlock.Parent is MenuItem) &&
                    !textBlock.Text.StartsWith("CONSOLE LOG") &&
                    !textBlock.Text.StartsWith("SYSTEM INFORMATION:") &&
                    !textBlock.Text.StartsWith("FEATURES:") &&
                    !textBlock.Text.StartsWith("TECHNICAL INFORMATION:"))
                {
                    textBlock.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;
                }
            }

            // Apply light mode to TextBox backgrounds
            foreach (TextBox textBox in FindVisualChildren<TextBox>(this))
            {
                textBox.Background = new SolidColorBrush(Colors.White);
                textBox.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;
                textBox.CaretBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#085368"));
                textBox.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F4F7"));
                textBox.BorderBrush = Application.Current.Resources["LightBorderBrush"] as Brush;
            }

            // Apply light mode to any RichTextBox
            foreach (RichTextBox richTextBox in FindVisualChildren<RichTextBox>(this))
            {
                richTextBox.Background = new SolidColorBrush(Colors.Transparent);
                richTextBox.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;
                richTextBox.BorderBrush = Application.Current.Resources["LightBorderBrush"] as Brush;

                // Update selection colors
                richTextBox.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F4F7"));
                richTextBox.SelectionTextBrush = Brushes.Black;
            }

            // Apply light mode to Borders
            foreach (Border border in FindVisualChildren<Border>(this))
            {
                if (border.Background != null)
                {
                    // Update any dark backgrounds
                    var brush = border.Background as SolidColorBrush;
                    if (brush != null &&
                        (brush.Color.ToString() == "#FF252525" ||
                         brush.Color.ToString() == "#FF2D2D2D" ||
                         brush.Color.ToString() == "#FF1E1E1E"))
                    {
                        border.Background = Application.Current.Resources["LightGroupBackgroundBrush"] as Brush;
                    }
                }

                border.BorderBrush = Application.Current.Resources["LightBorderBrush"] as Brush;
            }

            // Apply light mode to BatchResults TextBox
            if (BatchResults != null)
            {
                BatchResults.Background = new SolidColorBrush(Colors.Transparent);
                BatchResults.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;
            }

            // Update SingleResult and CustomerResult TextBlocks
            if (SingleResult != null)
                SingleResult.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;

            if (CustomerResult != null)
                CustomerResult.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;

            // Update GroupBox headers and backgrounds
            foreach (GroupBox groupBox in FindVisualChildren<GroupBox>(this))
            {
                groupBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#085368"));
                groupBox.Background = Application.Current.Resources["LightGroupBackgroundBrush"] as Brush;
            }

            // Update Menu and MenuItem styles
            foreach (Menu menu in FindVisualChildren<Menu>(this))
            {
                menu.Background = Application.Current.Resources["LightBackgroundBrush"] as Brush;
                menu.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#085368"));
                menu.BorderBrush = Application.Current.Resources["LightBorderBrush"] as Brush;
            }

            foreach (MenuItem menuItem in FindVisualChildren<MenuItem>(this))
            {
                menuItem.Background = Application.Current.Resources["LightBackgroundBrush"] as Brush;
                menuItem.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#085368"));
                menuItem.BorderBrush = Application.Current.Resources["LightBorderBrush"] as Brush;
            }

            // Update any ScrollViewer backgrounds
            foreach (ScrollViewer scrollViewer in FindVisualChildren<ScrollViewer>(this))
            {
                scrollViewer.Background = new SolidColorBrush(Colors.Transparent);
            }

            // Update ProgressBar colors
            foreach (ProgressBar progressBar in FindVisualChildren<ProgressBar>(this))
            {
                progressBar.Background = Application.Current.Resources["LightConsoleBackgroundBrush"] as Brush;
                progressBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9"));
                progressBar.BorderBrush = Application.Current.Resources["LightBorderBrush"] as Brush;
            }

            // Update Buttons in light mode - Use MintGreenBrush for the new style
            foreach (Button button in FindVisualChildren<Button>(this))
            {
                button.Background = (SolidColorBrush)Application.Current.Resources["MintGreenBrush"];
                button.Foreground = (SolidColorBrush)Application.Current.Resources["CharcoalBrush"];
                button.BorderBrush = (SolidColorBrush)Application.Current.Resources["MintGreenBrush"];
            }

            // Update RadioButtons in light mode
            foreach (RadioButton radioButton in FindVisualChildren<RadioButton>(this))
            {
                radioButton.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;
                radioButton.Background = Application.Current.Resources["LightBackgroundBrush"] as Brush;
            }

            // Update CheckBoxes in light mode
            foreach (CheckBox checkBox in FindVisualChildren<CheckBox>(this))
            {
                checkBox.Foreground = Application.Current.Resources["LightForegroundBrush"] as Brush;
                checkBox.Background = Application.Current.Resources["LightBackgroundBrush"] as Brush;
            }

            // Force visual refresh
            InvalidateVisual();
        }



        // Update the method to handle the nullability issue by using a nullable type and null check.
        private Grid? FindConsoleHeaderGrid()
        {
            // The console header grid is in Grid.Row="2" of the main grid
            if (Content is Grid mainGrid)
            {
                foreach (UIElement element in mainGrid.Children)
                {
                    if (element is Grid grid && Grid.GetRow(grid) == 2)
                    {
                        return grid;
                    }
                }
            }
            return null; // Return null if no matching grid is found
        }

        // Also update the search highlight code in PerformSearch method to use theme-appropriate colors
        private void HighlightSearchText(Paragraph newParagraph, string runText, string searchText, ref int pos, ref int searchResultCount)
        {
            // Find all occurrences of search text in this run
            while ((pos = runText.ToLower().IndexOf(searchText, pos, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                // Add text before match
                if (pos > 0)
                    newParagraph.Inlines.Add(new Run(runText.Substring(0, pos)));

                // Choose highlight color based on theme
                Color highlightColor;
                if (Application.Current.Resources["BackgroundBrush"] == Application.Current.Resources["LightBackgroundBrush"])
                {
                    // Light mode highlight
                    highlightColor = (Color)ColorConverter.ConvertFromString("#085368"); // Deep Teal
                }
                else
                {
                    // Dark mode highlight
                    highlightColor = (Color)ColorConverter.ConvertFromString("#18B4E9"); // Sky Blue
                }

                // Add the match with highlight
                var highlightRun = new Run(runText.Substring(pos, searchText.Length))
                {
                    Background = new SolidColorBrush(highlightColor),
                    Foreground = Brushes.White
                };
                newParagraph.Inlines.Add(highlightRun);

                // Update for next iteration
                runText = runText.Substring(pos + searchText.Length);
                pos = 0;
                searchResultCount++;
            }

            // Add any remaining text
            if (!string.IsNullOrEmpty(runText))
                newParagraph.Inlines.Add(new Run(runText));
        }



        private void PerformSearch(string searchText)
        {
            // Reset search results count
            _searchResultCount = 0;

            if (string.IsNullOrEmpty(searchText))
            {
                // If search text is empty, restore original content
                ConsoleLog.Document = new FlowDocument();
                foreach (var block in _originalDocument.Blocks.ToList())
                {
                    // Create a copy instead of using Clone
                    if (block is Paragraph paragraph)
                    {
                        var newParagraph = new Paragraph();
                        foreach (var inline in paragraph.Inlines)
                        {
                            if (inline is Run run)
                            {
                                newParagraph.Inlines.Add(new Run(run.Text));
                            }
                        }
                        ConsoleLog.Document.Blocks.Add(newParagraph);
                    }
                }
                SearchResultsCount.Text = string.Empty;
                return;
            }

            // Create new document for search results
            var searchResultDocument = new FlowDocument();

            // Go through each paragraph in the original document
            foreach (Paragraph originalParagraph in _originalDocument.Blocks)
            {
                string paragraphText = new TextRange(originalParagraph.ContentStart, originalParagraph.ContentEnd).Text.ToLower();

                // Check if this paragraph contains the search text
                if (paragraphText.Contains(searchText))
                {
                    // Found a match, create a new paragraph
                    var newParagraph = new Paragraph();

                    // Copy the run content
                    foreach (var inline in originalParagraph.Inlines)
                    {
                        if (inline is Run run)
                        {
                            string runText = run.Text;
                            int pos = 0;

                            // Use our new helper method for highlighting text based on theme
                            HighlightSearchText(newParagraph, runText, searchText, ref pos, ref _searchResultCount);
                        }
                        else
                        {
                            // For non-Run inlines, create a copy instead of using Clone
                            if (inline is LineBreak)
                                newParagraph.Inlines.Add(new LineBreak());
                            else if (inline is Span span)
                            {
                                var newSpan = new Span();
                                foreach (var childInline in span.Inlines)
                                {
                                    if (childInline is Run childRun)
                                        newSpan.Inlines.Add(new Run(childRun.Text));
                                }
                                newParagraph.Inlines.Add(newSpan);
                            }
                        }
                    }

                    // Add this paragraph to search results
                    searchResultDocument.Blocks.Add(newParagraph);
                }
            }

            // Update the console with search results
            ConsoleLog.Document = searchResultDocument;

            // Update search results count
            SearchResultsCount.Text = $"Found: {_searchResultCount}";
        }


        private void BatchSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = BatchSearchBox.Text.Trim().ToLower();
            PerformBatchSearch(searchText);
        }

        private void ClearBatchSearch_Click(object sender, RoutedEventArgs e)
        {
            BatchSearchBox.Clear();
            PerformBatchSearch(string.Empty);
        }

        private void PerformBatchSearch(string searchText)
        {
            // If we don't have the original text stored yet, store it
            if (string.IsNullOrEmpty(_originalBatchResults) && !string.IsNullOrEmpty(BatchResults.Text))
            {
                _originalBatchResults = BatchResults.Text;
            }

            // Reset search counter
            _batchSearchResultCount = 0;

            // If search is empty, restore original content
            if (string.IsNullOrEmpty(searchText))
            {
                BatchResults.Text = _originalBatchResults;
                BatchSearchResultsCount.Text = string.Empty;
                return;
            }

            // If there's no content to search
            if (string.IsNullOrEmpty(_originalBatchResults))
            {
                BatchSearchResultsCount.Text = "No results";
                return;
            }

            // Filter the lines based on search text
            StringBuilder filteredContent = new StringBuilder();
            string[] lines = _originalBatchResults.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

            bool foundInCurrentInvoice = false;
            StringBuilder currentInvoiceBlock = new StringBuilder();
            string currentInvoice = string.Empty;

            foreach (string line in lines)
            {
                // Check if this is an invoice header line
                if (line.StartsWith("INVOICE:"))
                {
                    // If we had a previous invoice and it matched, add it to results
                    if (foundInCurrentInvoice && currentInvoiceBlock.Length > 0)
                    {
                        filteredContent.Append(currentInvoiceBlock);
                        _batchSearchResultCount++;
                    }

                    // Start a new invoice block
                    currentInvoice = line;
                    currentInvoiceBlock.Clear();
                    currentInvoiceBlock.AppendLine(line);
                    foundInCurrentInvoice = line.ToLower().Contains(searchText);
                }
                else if (line.StartsWith("------"))
                {
                    // This is a separator line, add it to the current invoice block
                    currentInvoiceBlock.AppendLine(line);
                }
                else
                {
                    // This is a content line, add it to current invoice block
                    currentInvoiceBlock.AppendLine(line);

                    // If we haven't already matched this invoice, check if this line matches
                    if (!foundInCurrentInvoice && line.ToLower().Contains(searchText))
                    {
                        foundInCurrentInvoice = true;
                    }
                }
            }

            // Handle the last invoice block if it matched
            if (foundInCurrentInvoice && currentInvoiceBlock.Length > 0)
            {
                filteredContent.Append(currentInvoiceBlock);
                _batchSearchResultCount++;
            }

            // Update the display
            BatchResults.Text = filteredContent.ToString();
            BatchSearchResultsCount.Text = $"Found: {_batchSearchResultCount}";
        }


        private void Log(LogLevel level, string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logMessage = $"[{timestamp}] [{level}] {message}";

            // Add to in-memory log collection
            _logEntries.Add(new LogEntry(timestamp, level, message));

            // Update UI
            Dispatcher.Invoke(() =>
            {
                // Create a new paragraph for this log entry
                Paragraph paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run(logMessage));

                // Add to original document - create a deep copy
                Paragraph originalCopy = new Paragraph();
                originalCopy.Inlines.Add(new Run(logMessage));
                _originalDocument.Blocks.Add(originalCopy);

                // Add to console
                ConsoleLog.Document.Blocks.Add(paragraph);

                // Auto-scroll to end
                ConsoleLog.ScrollToEnd();

                // If search is active, update search results
                if (!string.IsNullOrEmpty(SearchBox.Text))
                    PerformSearch(SearchBox.Text.Trim().ToLower());
            });

            // Write to session log file
            try
            {
                File.AppendAllText(_sessionLogPath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Handle file writing errors
                Dispatcher.Invoke(() =>
                {
                    Paragraph paragraph = new Paragraph();
                    paragraph.Inlines.Add(new Run($"[ERROR] Failed to write to log file: {ex.Message}"));

                    // Add to original document - create a deep copy
                    Paragraph originalCopy = new Paragraph();
                    originalCopy.Inlines.Add(new Run($"[ERROR] Failed to write to log file: {ex.Message}"));
                    _originalDocument.Blocks.Add(originalCopy);

                    // Add to console
                    ConsoleLog.Document.Blocks.Add(paragraph);
                });
            }
        }

        private async void ProcessSingleInvoice_Click(object sender, RoutedEventArgs e)
        {
            string billerGUID = BillerGUID.Text;
            string webServiceKey = WebServiceKey.Text;
            string accountNumber = AccountNumber.Text;
            string invoiceNumber = InvoiceNumber.Text;

            Log(LogLevel.Info, $"Processing single invoice: {invoiceNumber} for account: {accountNumber}");

            if (ValidateGUID(billerGUID) && ValidateGUID(webServiceKey) &&
                !string.IsNullOrEmpty(invoiceNumber) && !string.IsNullOrEmpty(accountNumber))
            {
                try
                {
                    SingleResult.Text = "Processing...";

                    // First, call customer record service to refresh the balance
                    Log(LogLevel.Debug, $"Calling customer record service to refresh balance for account {accountNumber}...");
                    await CallCustomerRecordService(billerGUID, webServiceKey, accountNumber);
                    Log(LogLevel.Info, $"Balance refreshed for account {accountNumber}");

                    // Now call invoice service
                    Log(LogLevel.Debug, $"Calling invoice service for invoice {invoiceNumber}...");
                    string result = await CallWebService(billerGUID, webServiceKey, invoiceNumber);
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

            // Validate BillerGUID and WebServiceKey from the single invoice fields
            if (!ValidateGUID(billerGUID) || !ValidateGUID(webServiceKey))
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

            try
            {
                // Clear previous results and search state
                BatchResults.Clear();
                _originalBatchResults = string.Empty;
                BatchSearchBox.Clear();
                BatchSearchResultsCount.Text = string.Empty;

                Log(LogLevel.Info, $"Starting batch processing of file: {filePath}");
                Log(LogLevel.Info, $"Using Biller GUID: {billerGUID}");
                Log(LogLevel.Info, $"Using Web Service Key: {webServiceKey}");
                Log(LogLevel.Info, $"CSV has account numbers: {hasAccountNumbers}");

                // Read all lines from the CSV file
                var lines = File.ReadAllLines(filePath);

                if (lines.Length == 0)
                {
                    MessageBox.Show("The selected CSV file is empty.",
                                  "Empty File",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                    Log(LogLevel.Warning, "Batch processing cancelled: Empty CSV file");
                    return;
                }

                var results = new StringBuilder();
                BatchProgress.Maximum = lines.Length;
                BatchProgress.Value = 0;

                Log(LogLevel.Info, $"Found {lines.Length} records to process");

                // Add header to results display and CSV
                BatchResults.AppendText($"PROCESSING INVOICES\n");
                BatchResults.AppendText($"----------------------------------------\n");

                if (hasAccountNumbers)
                {
                    results.AppendLine("AccountNumber,InvoiceNumber,Status,BalanceDue,DueDate,TotalAmount");
                }
                else
                {
                    results.AppendLine("InvoiceNumber,Status,BalanceDue,DueDate,TotalAmount");
                }

                int successCount = 0;
                int errorCount = 0;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    string accountNumber = string.Empty;
                    string invoiceNumber = string.Empty;

                    // Process CSV line
                    if (hasAccountNumbers)
                    {
                        // Format expected: AccountNumber,InvoiceNumber
                        var parts = line.Split(',');
                        if (parts.Length >= 2)
                        {
                            accountNumber = parts[0].Trim();
                            invoiceNumber = parts[1].Trim();
                        }
                    }
                    else
                    {
                        // Format expected: InvoiceNumber only
                        invoiceNumber = line;
                        // Use the account number from the single invoice section if available
                        accountNumber = AccountNumber.Text.Trim();
                    }

                    BatchProgress.Value = i + 1;
                    BatchStatus.Text = $"Processing {i + 1} of {lines.Length}...";

                    // Force UI update to show progress
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

                    if (!string.IsNullOrEmpty(invoiceNumber))
                    {
                        try
                        {
                            Log(LogLevel.Debug, $"Processing invoice: {invoiceNumber}" +
                                (string.IsNullOrEmpty(accountNumber) ? "" : $" for account: {accountNumber}"));

                            // First refresh the balance if account number is provided
                            if (!string.IsNullOrEmpty(accountNumber))
                            {
                                Log(LogLevel.Debug, $"Refreshing balance for account {accountNumber}...");
                                await CallCustomerRecordService(billerGUID, webServiceKey, accountNumber);
                                Log(LogLevel.Info, $"Balance refreshed for account {accountNumber}");
                            }

                            // Then get invoice data
                            string resultText = await CallWebService(billerGUID, webServiceKey, invoiceNumber);

                            // Extract basic data for CSV
                            string csvLine;
                            if (hasAccountNumbers)
                            {
                                csvLine = $"{accountNumber}," + FormatInvoiceDataForCSV(invoiceNumber, resultText);
                            }
                            else
                            {
                                csvLine = FormatInvoiceDataForCSV(invoiceNumber, resultText);
                            }
                            results.AppendLine(csvLine);

                            // Update UI with full details
                            BatchResults.AppendText($"INVOICE: {invoiceNumber}" +
                                                  (string.IsNullOrEmpty(accountNumber) ? "" : $" (Account: {accountNumber})") + "\n");
                            BatchResults.AppendText($"{resultText}\n");
                            BatchResults.AppendText($"----------------------------------------\n");
                            BatchResults.ScrollToEnd();

                            Log(LogLevel.Info, $"Invoice {invoiceNumber} processed successfully");
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            string errorMsg = $"Error: {ex.Message}";
                            Log(LogLevel.Error, $"Error processing invoice {invoiceNumber}: {ex.Message}");

                            // Add to CSV results
                            if (hasAccountNumbers)
                            {
                                results.AppendLine($"{accountNumber},{invoiceNumber},Error,\"{EscapeCsvField(ex.Message)}\",,");
                            }
                            else
                            {
                                results.AppendLine($"{invoiceNumber},Error,\"{EscapeCsvField(ex.Message)}\",,");
                            }

                            // Update UI with error
                            BatchResults.AppendText($"INVOICE: {invoiceNumber}" +
                                                  (string.IsNullOrEmpty(accountNumber) ? "" : $" (Account: {accountNumber})") + "\n");
                            BatchResults.AppendText($"{errorMsg}\n");
                            BatchResults.AppendText($"----------------------------------------\n");
                            BatchResults.ScrollToEnd();
                            errorCount++;
                        }
                    }
                    else
                    {
                        Log(LogLevel.Warning, $"Empty invoice number in line {i + 1}");

                        // Add to CSV results
                        if (hasAccountNumbers)
                        {
                            results.AppendLine($"{accountNumber},Line {i + 1},Error,\"Empty invoice number\",,");
                        }
                        else
                        {
                            results.AppendLine($"Line {i + 1},Error,\"Empty invoice number\",,");
                        }

                        // Update UI with error
                        BatchResults.AppendText($"Line {i + 1}: Error - Empty invoice number\n");
                        BatchResults.AppendText($"----------------------------------------\n");
                        BatchResults.ScrollToEnd();
                        errorCount++;
                    }
                }

                string resultFilePath = IOPath.Combine(IOPath.GetDirectoryName(filePath) ?? AppDomain.CurrentDomain.BaseDirectory, "InvoiceResults.csv");
                File.WriteAllText(resultFilePath, results.ToString());

                // Add summary to results display
                BatchResults.AppendText($"\n----------------------------------------\n");
                BatchResults.AppendText($"Processing complete! Results saved to:\n{resultFilePath}\n");
                BatchResults.AppendText($"Summary: {successCount} successful, {errorCount} failed, {lines.Length} total\n");
                BatchResults.ScrollToEnd();

                // Store the batch results for search functionality
                _originalBatchResults = BatchResults.Text;

                BatchStatus.Text = "Processing complete!";
                Log(LogLevel.Info, $"Batch processing completed. Results saved to {resultFilePath}");
                Log(LogLevel.Info, $"Batch summary: {successCount} successful, {errorCount} failed, {lines.Length} total");
                MessageBox.Show($"Batch processing completed.\nResults saved to: {resultFilePath}\n\nSummary: {successCount} successful, {errorCount} failed",
                              "Batch Complete",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Batch processing failed: {ex.Message}");
                BatchStatus.Text = "Error during processing!";

                // Add error to results display
                BatchResults.AppendText($"\n----------------------------------------\n");
                BatchResults.AppendText($"ERROR: {ex.Message}");
                BatchResults.ScrollToEnd();

                MessageBox.Show($"Error during batch processing: {ex.Message}",
                               "Processing Error",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }

        // Helper method to properly escape CSV fields
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            // If the field contains quotes, commas, or newlines, escape quotes by doubling them
            // and surround the whole field with quotes
            if (field.Contains("\"") || field.Contains(",") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }



        private string FormatInvoiceDataForCSV(string invoiceNumber, string resultText)
        {
            // Extract just the key fields for CSV
            string status = "Unknown";
            string balanceDue = "";
            string dueDate = "";
            string totalAmount = "";

            string[] lines = resultText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                if (line.StartsWith("Status:"))
                {
                    status = line.Substring("Status:".Length).Trim();
                }
                else if (line.StartsWith("Balance Due:"))
                {
                    balanceDue = line.Substring("Balance Due:".Length).Trim();
                }
                else if (line.StartsWith("Due Date:"))
                {
                    dueDate = line.Substring("Due Date:".Length).Trim();
                }
                else if (line.StartsWith("Total Amount Due:"))
                {
                    totalAmount = line.Substring("Total Amount Due:".Length).Trim();
                }
            }

            // Escape any commas in fields
            return $"{invoiceNumber},{status},\"{balanceDue}\",\"{dueDate}\",\"{totalAmount}\"";
        }

        private async void ProcessCustomerRecord_Click(object sender, RoutedEventArgs e)
        {
            string billerGUID = BillerGUID.Text;
            string webServiceKey = WebServiceKey.Text;
            string accountNumber = CustomerAccountNumber.Text;

            Log(LogLevel.Info, $"Looking up customer record for account: {accountNumber}");

            if (ValidateGUID(billerGUID) && ValidateGUID(webServiceKey) && !string.IsNullOrEmpty(accountNumber))
            {
                try
                {
                    Log(LogLevel.Debug, "Calling customer record web service...");
                    CustomerResult.Text = "Processing...";

                    // Add debug logging to see what's happening
                    Log(LogLevel.Debug, $"Using Biller GUID: {billerGUID}");
                    Log(LogLevel.Debug, $"Using Web Service Key: {webServiceKey}");

                    string result = await CallCustomerRecordService(billerGUID, webServiceKey, accountNumber);

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




        private async Task<string> CallWebService(string billerGUID, string webServiceKey, string invoiceNumber)
        {
            string soapRequest = $@"
        <soap12:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap12='http://www.w3.org/2003/05/soap-envelope'>
            <soap12:Body>
                <ViewInvoiceByInvoiceNumber xmlns='https://www.invoicecloud.com/portal/webservices/CloudInvoicing/'>
                    <Req>
                        <BillerGUID>{billerGUID}</BillerGUID>
                        <WebServiceKey>{webServiceKey}</WebServiceKey>
                        <InvoiceNumber>{invoiceNumber}</InvoiceNumber>
                    </Req>
                </ViewInvoiceByInvoiceNumber>
            </soap12:Body>
        </soap12:Envelope>";

            // Configure the HttpClient with appropriate timeout
            using (HttpClient client = new HttpClient())
            {
                // Set reasonable timeout and headers
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.ExpectContinue = false; // Can help with some SOAP services

                // Use retry pattern to handle intermittent failures
                int maxRetries = 3;
                int currentRetry = 0;
                int retryDelayMs = 1000; // Start with 1 second delay

                while (true)
                {
                    try
                    {
                        Log(LogLevel.Debug, $"Sending request for invoice {invoiceNumber} (Attempt {currentRetry + 1} of {maxRetries})");

                        var content = new StringContent(soapRequest, Encoding.UTF8, "application/soap+xml");

                        // Add important SOAP action header if needed
                        // client.DefaultRequestHeaders.Add("SOAPAction", "ViewInvoiceByInvoiceNumber");

                        var response = await client.PostAsync("https://www.invoicecloud.com/portal/webservices/CloudInvoicing.asmx", content);

                        // Check HTTP status code
                        if (!response.IsSuccessStatusCode)
                        {
                            Log(LogLevel.Warning, $"HTTP error: {(int)response.StatusCode} {response.StatusCode} for invoice {invoiceNumber}");

                            // For specific error codes that might be temporary, we'll retry
                            if ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                            {
                                throw new HttpRequestException($"Server error: {response.StatusCode}");
                            }
                        }

                        var responseText = await response.Content.ReadAsStringAsync();

                        // Verify the response actually contains XML data
                        if (string.IsNullOrWhiteSpace(responseText) || !responseText.Contains("<"))
                        {
                            Log(LogLevel.Warning, $"Empty or non-XML response received for invoice {invoiceNumber}");
                            throw new FormatException("Invalid response format received");
                        }

                        Log(LogLevel.Debug, $"Received response for invoice {invoiceNumber}");
                        return ParseResponse(responseText);
                    }
                    catch (Exception ex) when (ex is HttpRequestException ||
                                               ex is TaskCanceledException ||
                                               ex is TimeoutException ||
                                               ex is FormatException)
                    {
                        currentRetry++;
                        Log(LogLevel.Warning, $"API call attempt {currentRetry} failed: {ex.Message}");

                        if (currentRetry >= maxRetries)
                        {
                            Log(LogLevel.Error, $"All retry attempts failed for invoice {invoiceNumber}. Last error: {ex.Message}");
                            throw new Exception($"Failed to retrieve invoice data after {maxRetries} attempts. Last error: {ex.Message}", ex);
                        }

                        // Exponential backoff for retries
                        await Task.Delay(retryDelayMs);
                        retryDelayMs *= 2; // Double the delay for each retry
                    }
                }
            }
        }

        private async Task<string> CallCustomerRecordService(string billerGUID, string webServiceKey, string accountNumber)
        {
            string soapRequest = $@"
<soap12:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap12='http://www.w3.org/2003/05/soap-envelope'>
  <soap12:Body>
    <ViewCustomerRecord xmlns='https://www.invoicecloud.com/portal/webservices/CloudManagement/'>
      <BillerGUID>{billerGUID}</BillerGUID>
      <WebServiceKey>{webServiceKey}</WebServiceKey>
      <AccountNumber>{accountNumber}</AccountNumber>
    </ViewCustomerRecord>
  </soap12:Body>
</soap12:Envelope>";

            // Configure the HttpClient with appropriate timeout
            using (HttpClient client = new HttpClient())
            {
                // Set reasonable timeout and headers
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.ExpectContinue = false;

                // Use retry pattern to handle intermittent failures
                int maxRetries = 3;
                int currentRetry = 0;
                int retryDelayMs = 1000; // Start with 1 second delay

                while (true)
                {
                    try
                    {
                        Log(LogLevel.Debug, $"Sending customer record request for account {accountNumber} (Attempt {currentRetry + 1} of {maxRetries})");

                        var content = new StringContent(soapRequest, Encoding.UTF8, "application/soap+xml");

                        var response = await client.PostAsync("https://www.invoicecloud.com/portal/webservices/CloudManagement.asmx", content);

                        // Check HTTP status code
                        if (!response.IsSuccessStatusCode)
                        {
                            Log(LogLevel.Warning, $"HTTP error: {(int)response.StatusCode} {response.StatusCode} for account {accountNumber}");

                            // For specific error codes that might be temporary, we'll retry
                            if ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                            {
                                throw new HttpRequestException($"Server error: {response.StatusCode}");
                            }
                        }

                        var responseText = await response.Content.ReadAsStringAsync();

                        // Verify the response actually contains XML data
                        if (string.IsNullOrWhiteSpace(responseText) || !responseText.Contains("<"))
                        {
                            Log(LogLevel.Warning, $"Empty or non-XML response received for account {accountNumber}");
                            throw new FormatException("Invalid response format received");
                        }

                        Log(LogLevel.Debug, $"Received customer record response for account {accountNumber}");
                        return ParseCustomerRecordResponse(responseText);
                    }
                    catch (Exception ex) when (ex is HttpRequestException ||
                                               ex is TaskCanceledException ||
                                               ex is TimeoutException ||
                                               ex is FormatException)
                    {
                        currentRetry++;
                        Log(LogLevel.Warning, $"API call attempt {currentRetry} failed: {ex.Message}");

                        if (currentRetry >= maxRetries)
                        {
                            Log(LogLevel.Error, $"All retry attempts failed for account {accountNumber}. Last error: {ex.Message}");
                            throw new Exception($"Failed to retrieve customer data after {maxRetries} attempts. Last error: {ex.Message}", ex);
                        }

                        // Exponential backoff for retries
                        await Task.Delay(retryDelayMs);
                        retryDelayMs *= 2; // Double the delay for each retry
                    }
                }
            }
        }

        //
        private string ParseCustomerRecordResponse(string responseText)
        {
            try
            {
                // Add debug logging to see the raw response
                Log(LogLevel.Debug, $"Raw customer record XML starts with: {responseText.Substring(0, Math.Min(200, responseText.Length))}");

                // Check for top-level SOAP fault first
                if (responseText.Contains("<soap12:Fault") || responseText.Contains("<Fault"))
                {
                    Log(LogLevel.Warning, "SOAP Fault detected in customer record response");

                    // Try to extract fault reason
                    string faultReason = "Unknown fault";
                    int reasonStart = responseText.IndexOf("<faultstring>") + "<faultstring>".Length;
                    int reasonEnd = responseText.IndexOf("</faultstring>");

                    if (reasonStart > 0 && reasonEnd > reasonStart)
                    {
                        faultReason = responseText.Substring(reasonStart, reasonEnd - reasonStart);
                        Log(LogLevel.Warning, $"Fault reason: {faultReason}");
                    }

                    return $"Error: {faultReason}";
                }

                // Look for customer data in the response
                if (responseText.Contains("<CustomerID>") || responseText.Contains("<AccountNumber>"))
                {
                    try
                    {
                        StringBuilder result = new StringBuilder();
                        result.AppendLine("CUSTOMER RECORD:");
                        result.AppendLine("---------------");

                        // Extract all customer fields
                        string[] fields = new[] {
                    "CustomerID", "AccountNumber", "CustomerName", "Address1", "Address2",
                    "City", "State", "Zip", "Phone", "EmailAddress", "AutoPay",
                    "PaperInvoices", "Registered", "RegistrationDate", "LoginName"
                };

                        foreach (var field in fields)
                        {
                            string value = ExtractValue(responseText, field);
                            if (!string.IsNullOrEmpty(value))
                            {
                                // Format boolean values as Yes/No
                                if (field == "AutoPay" || field == "PaperInvoices" || field == "Registered" || field == "Active")
                                {
                                    value = value.ToLower() == "true" ? "Yes" : "No";
                                }
                                // Format dates
                                else if (field.Contains("Date") && DateTime.TryParse(value, out DateTime date) && date != DateTime.MinValue)
                                {
                                    value = date.ToString("MM/dd/yyyy");
                                }

                                result.AppendLine($"{FormatFieldName(field)}: {value}");
                            }
                        }

                        string finalResult = result.ToString();
                        Log(LogLevel.Debug, $"Formatted customer record result: {finalResult}");
                        return finalResult;
                    }
                    catch (Exception ex)
                    {
                        Log(LogLevel.Error, $"Error parsing customer data: {ex.Message}");
                        return "Error parsing customer data: " + ex.Message;
                    }
                }
                else if (responseText.Contains("ViewCustomerRecordResult") && !responseText.Contains("<CustomerID>"))
                {
                    Log(LogLevel.Warning, "No customer record found for this account number");
                    return "No customer record found for this account number.";
                }
                else
                {
                    Log(LogLevel.Warning, "Unexpected response format for customer record");
                    return "Unknown status: Response did not contain expected customer data format";
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error parsing customer response: {ex.Message}");
                return $"Error parsing customer response: {ex.Message}";
            }
        }


        private string FormatFieldName(string fieldName)
        {
            // Insert spaces before capital letters in the middle of the string
            string result = string.Empty;
            for (int i = 0; i < fieldName.Length; i++)
            {
                if (i > 0 && char.IsUpper(fieldName[i]))
                {
                    result += " ";
                }
                result += fieldName[i];
            }
            return result;
        }



        private string ParseResponse(string responseText)
        {
            try
            {
                // Check for top-level SOAP fault first
                if (responseText.Contains("<soap12:Fault") || responseText.Contains("<Fault"))
                {
                    Log(LogLevel.Warning, "SOAP Fault detected in response");

                    // Try to extract fault reason
                    string faultReason = "Unknown fault";
                    int reasonStart = responseText.IndexOf("<faultstring>") + "<faultstring>".Length;
                    int reasonEnd = responseText.IndexOf("</faultstring>");

                    if (reasonStart > 0 && reasonEnd > reasonStart)
                    {
                        faultReason = responseText.Substring(reasonStart, reasonEnd - reasonStart);
                        Log(LogLevel.Warning, $"Fault reason: {faultReason}");
                    }

                    return $"Error: {faultReason}";
                }

                // Check for success response
                if (responseText.Contains("<Success>true</Success>"))
                {
                    try
                    {
                        // Extract all available information from the response
                        StringBuilder result = new StringBuilder();

                        // Status
                        result.AppendLine("Status: Success");

                        // Invoice Number
                        string invoiceNumber = ExtractValue(responseText, "InvoiceNumber");
                        if (!string.IsNullOrEmpty(invoiceNumber))
                            result.AppendLine($"Invoice Number: {invoiceNumber}");

                        // Invoice Date
                        string invoiceDate = ExtractValue(responseText, "InvoiceDate");
                        if (!string.IsNullOrEmpty(invoiceDate))
                        {
                            if (DateTime.TryParse(invoiceDate, out DateTime parsedDate))
                                result.AppendLine($"Invoice Date: {parsedDate:MM/dd/yyyy}");
                            else
                                result.AppendLine($"Invoice Date: {invoiceDate}");
                        }

                        // Due Date
                        string dueDate = ExtractValue(responseText, "InvoiceDueDate");
                        if (!string.IsNullOrEmpty(dueDate))
                        {
                            if (DateTime.TryParse(dueDate, out DateTime parsedDate))
                                result.AppendLine($"Due Date: {parsedDate:MM/dd/yyyy}");
                            else
                                result.AppendLine($"Due Date: {dueDate}");
                        }

                        // AutoPay Collection Date
                        string autoPayDate = ExtractValue(responseText, "AutoPayCollectionDate");
                        if (!string.IsNullOrEmpty(autoPayDate) && autoPayDate != "1/1/0001")
                        {
                            if (DateTime.TryParse(autoPayDate, out DateTime parsedDate))
                                result.AppendLine($"AutoPay Date: {parsedDate:MM/dd/yyyy}");
                            else
                                result.AppendLine($"AutoPay Date: {autoPayDate}");
                        }

                        // Amount fields
                        string totalAmountDue = ExtractValue(responseText, "TotalAmountDue");
                        if (!string.IsNullOrEmpty(totalAmountDue))
                        {
                            if (decimal.TryParse(totalAmountDue, out decimal amount))
                                result.AppendLine($"Total Amount Due: {amount:C}");
                            else
                                result.AppendLine($"Total Amount Due: {totalAmountDue}");
                        }

                        string balanceDue = ExtractValue(responseText, "BalanceDue");
                        if (!string.IsNullOrEmpty(balanceDue))
                        {
                            if (decimal.TryParse(balanceDue, out decimal amount))
                                result.AppendLine($"Balance Due: {amount:C}");
                            else
                                result.AppendLine($"Balance Due: {balanceDue}");
                        }

                        string minimumAmountDue = ExtractValue(responseText, "MinimumAmountDue");
                        if (!string.IsNullOrEmpty(minimumAmountDue))
                        {
                            if (decimal.TryParse(minimumAmountDue, out decimal amount))
                                result.AppendLine($"Minimum Amount Due: {amount:C}");
                            else
                                result.AppendLine($"Minimum Amount Due: {minimumAmountDue}");
                        }

                        // Boolean fields
                        string allowPartialPayments = ExtractValue(responseText, "AllowPartialPayments");
                        if (!string.IsNullOrEmpty(allowPartialPayments))
                        {
                            string displayValue = allowPartialPayments.ToLower() == "true" ? "Yes" : "No";
                            result.AppendLine($"Allow Partial Payments: {displayValue}");
                        }

                        // Notification dates
                        string[] notificationFields = new[]
                        {
                            "FirstNotifyRequestedDate",
                            "SecondNotifyRequestedDate",
                            "ThirdNotifyRequestedDate"
                        };

                        foreach (string field in notificationFields)
                        {
                            string notifyDate = ExtractValue(responseText, field);
                            if (!string.IsNullOrEmpty(notifyDate) && notifyDate != "1/1/0001")
                            {
                                if (DateTime.TryParse(notifyDate, out DateTime parsedDate))
                                {
                                    string displayName = field.Replace("RequestedDate", "");
                                    result.AppendLine($"{displayName}: {parsedDate:MM/dd/yyyy}");
                                }
                            }
                        }

                        return result.ToString();
                    }
                    catch (Exception ex)
                    {
                        Log(LogLevel.Error, $"Error parsing data from success response: {ex.Message}");
                        return "Success: true, Error parsing detailed invoice data";
                    }
                }
                else if (responseText.Contains("<Success>false</Success>"))
                {
                    // Try to get error message if available
                    string errorMessage = "No details provided";
                    int messageStart = responseText.IndexOf("<ErrorMessage>") + "<ErrorMessage>".Length;
                    int messageEnd = responseText.IndexOf("</ErrorMessage>");

                    if (messageStart > 0 && messageEnd > messageStart)
                    {
                        errorMessage = responseText.Substring(messageStart, messageEnd - messageStart);
                    }

                    Log(LogLevel.Warning, $"API returned Success: false with message: {errorMessage}");
                    return $"Status: Failed\nReason: {errorMessage}";
                }
                else
                {
                    // Unexpected response format
                    Log(LogLevel.Warning, "Unexpected response format - could not determine success status");
                    return "Status: Unknown\nResponse did not contain expected format";
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error parsing response: {ex.Message}");
                return $"Error parsing response: {ex.Message}";
            }
        }

        private string ExtractValue(string xml, string tagName)
        {
            string openTag = $"<{tagName}>";
            string closeTag = $"</{tagName}>";

            int startIndex = xml.IndexOf(openTag);
            if (startIndex < 0)
                return string.Empty;

            startIndex += openTag.Length;
            int endIndex = xml.IndexOf(closeTag, startIndex);

            if (endIndex < 0)
                return string.Empty;

            return xml.Substring(startIndex, endIndex - startIndex).Trim();
        }

        private bool ValidateGUID(string guid)
        {
            // If input is empty, return false
            if (string.IsNullOrWhiteSpace(guid))
                return false;

            // Check if it's a valid GUID
            bool isValid = Guid.TryParse(guid, out _);

            // If validation fails, log more details for debugging
            if (!isValid)
            {
                Log(LogLevel.Debug, $"GUID validation failed for: '{guid}'");
            }

            return isValid;
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            // Clear RichTextBox content by creating a new document
            ConsoleLog.Document = new FlowDocument();
            _originalDocument = new FlowDocument();
            Log(LogLevel.Info, "Console cleared");
        }

        // Fixed SaveLogs_Click method
        private void SaveLogs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Log files (*.log)|*.log",
                FileName = $"InvoiceRefresher_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Extract text from RichTextBox
                    string consoleText = new TextRange(ConsoleLog.Document.ContentStart, ConsoleLog.Document.ContentEnd).Text;
                    File.WriteAllText(saveFileDialog.FileName, consoleText);
                    Log(LogLevel.Info, $"Logs saved to: {saveFileDialog.FileName}");
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Error, $"Failed to save logs: {ex.Message}");
                    MessageBox.Show($"Failed to save logs: {ex.Message}");
                }
            }
        }

        #region Menu Actions

        private void GenerateSampleCSV_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = "SampleInvoices.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var sampleContent = new StringBuilder();
                    sampleContent.AppendLine("INV0001");
                    sampleContent.AppendLine("INV0002");
                    sampleContent.AppendLine("INV0003");

                    File.WriteAllText(saveFileDialog.FileName, sampleContent.ToString());

                    Log(LogLevel.Info, $"Sample CSV file generated: {saveFileDialog.FileName}");

                    MessageBox.Show(
                        $"Sample CSV file created at:\n{saveFileDialog.FileName}\n\nFormat:\nOne invoice number per line\n\nThe Biller GUID and Web Service Key from the Single Invoice section will be used for processing.",
                        "Sample File Created",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Error, $"Failed to create sample file: {ex.Message}");
                    MessageBox.Show($"Error creating sample file: {ex.Message}");
                }
            }
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
            // Use a cast to convert between the two enums
            var aboutDialog = new AboutDialog(this, APP_VERSION,
                (level, message) => Log((MainWindow.LogLevel)(int)level, message));
            aboutDialog.Show();
        }




        private void ShowDocumentationWindow()
        {
            Log(LogLevel.Info, "Documentation requested");

            // Create documentation window with enhanced terminal styling
            var docWindow = new Window
            {
                Title = "Documentation - Invoice Balance Refresher",
                Width = 900,
                Height = 700,
                Background = (System.Windows.Media.SolidColorBrush)FindResource("BackgroundBrush"),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            // Create main grid with columns for TOC and content
            var mainGrid = new Grid();
            docWindow.Content = mainGrid;

            // Add scanlines overlay for terminal effect
            var scanlinesGrid = new Grid();
            scanlinesGrid.SetValue(Panel.ZIndexProperty, -1);
            scanlinesGrid.Background = new DrawingBrush
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 2, 2),
                ViewportUnits = BrushMappingMode.Absolute,
                Opacity = 0.07,
                Drawing = new DrawingGroup
                {
                    Children =
            {
                new GeometryDrawing
                {
                    Brush = System.Windows.Media.Brushes.Transparent,
                    Geometry = new RectangleGeometry(new Rect(0, 0, 2, 2))
                },
                new GeometryDrawing
                {
                    Brush = new System.Windows.Media.SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00")),
                    Geometry = new RectangleGeometry(new Rect(0, 0, 2, 1))
                }
            }
                }
            };
            mainGrid.Children.Add(scanlinesGrid);

            // Add a header bar
            var headerBar = new Border
            {
                Height = 40,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18536A")),
                VerticalAlignment = VerticalAlignment.Top,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#124050"))
            };

            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerBar.Child = headerPanel;

            var docHeaderIcon = new TextBlock
            {
                Text = "📚",
                FontSize = 20,
                Foreground = Brushes.White,
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var docHeaderText = new TextBlock
            {
                Text = "INVOICE BALANCE REFRESHER DOCUMENTATION",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B2F0FF")),
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            headerPanel.Children.Add(docHeaderIcon);
            headerPanel.Children.Add(docHeaderText);

            mainGrid.Children.Add(headerBar);

            // Create grid for main content area (below header)
            var contentGrid = new Grid { Margin = new Thickness(0, 40, 0, 0) };
            mainGrid.Children.Add(contentGrid);

            // Define columns for TOC and content
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Create TOC panel (left side)
            var tocBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 0),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#124050")),
                Background = new SolidColorBrush(Color.FromArgb(40, 18, 83, 106))
            };
            Grid.SetColumn(tocBorder, 0);
            contentGrid.Children.Add(tocBorder);

            var tocPanel = new StackPanel { Margin = new Thickness(10, 15, 10, 10) };
            tocBorder.Child = tocPanel;

            // TOC Header
            var tocHeader = new TextBlock
            {
                Text = "TABLE OF CONTENTS",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(5, 0, 0, 15),
                TextAlignment = TextAlignment.Left
            };
            tocPanel.Children.Add(tocHeader);

            // Create main content area (right side)
            var contentBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 24, 180, 233))
            };
            Grid.SetColumn(contentBorder, 1);
            contentGrid.Children.Add(contentBorder);

            var contentScrollViewer = new ScrollViewer
            {
                Margin = new Thickness(20, 15, 20, 20),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            contentBorder.Child = contentScrollViewer;

            var contentStackPanel = new StackPanel();
            contentScrollViewer.Content = contentStackPanel;

            // Define TOC sections with colors and icons
            string[][] tocSections = new string[][]
            {
        new string[] { "1", "Overview", "#18B4E9", "ℹ️" },
        new string[] { "2", "Single Invoice Processing", "#5BFF64", "📄" },
        new string[] { "3", "Batch Processing", "#5BFF64", "📚" },
        new string[] { "4", "Logging", "#F0A030", "📋" },
        new string[] { "5", "Sample CSV Generation", "#F0A030", "📁" },
        new string[] { "6", "API Format", "#E55555", "🔌" },
        new string[] { "7", "Frequently Asked Questions (FAQ)", "#18B4E9", "❓" }
            };

            // Document sections that will be populated
            var docSections = new Dictionary<string, StackPanel>();

            // Create TOC entries and document sections
            foreach (var section in tocSections)
            {
                string sectionId = section[0];
                string sectionName = section[1];
                string sectionColor = section[2];
                string sectionIcon = section[3];

                // Create TOC entry with interactive hover effect
                var tocEntry = new Border
                {
                    Margin = new Thickness(0, 3, 0, 3),
                    Padding = new Thickness(8, 5, 8, 5),
                    CornerRadius = new CornerRadius(4),
                    Cursor = Cursors.Hand
                };

                var tocEntryPanel = new StackPanel { Orientation = Orientation.Horizontal };
                tocEntry.Child = tocEntryPanel;

                var tocEntryIcon = new TextBlock
                {
                    Text = sectionIcon,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sectionColor)),
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var tocEntryText = new TextBlock
                {
                    Text = $"{sectionId}. {sectionName}",
                    Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                    FontFamily = new FontFamily("Consolas"),
                    TextWrapping = TextWrapping.Wrap
                };

                tocEntryPanel.Children.Add(tocEntryIcon);
                tocEntryPanel.Children.Add(tocEntryText);
                tocPanel.Children.Add(tocEntry);

                // Create document section in main content area
                var sectionPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 25) };
                docSections[sectionId] = sectionPanel;
                contentStackPanel.Children.Add(sectionPanel);

                // Create heading for section
                var sectionHeading = new TextBlock
                {
                    Text = $"{sectionId}. {sectionName.ToUpper()}",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sectionColor)),
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 18,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                sectionPanel.Children.Add(sectionHeading);

                // Add divider below heading
                var sectionDivider = new Rectangle
                {
                    Height = 2,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sectionColor)),
                    Margin = new Thickness(0, 0, 0, 15),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Opacity = 0.7
                };
                sectionPanel.Children.Add(sectionDivider);

                // Set up TOC entry click handler to scroll to section
                tocEntry.MouseLeftButtonDown += (s, e) =>
                {
                    sectionPanel.BringIntoView();
                    e.Handled = true;
                };

                // Add hover effect to TOC entries
                tocEntry.MouseEnter += (s, e) =>
                {
                    tocEntry.Background = new SolidColorBrush(Color.FromArgb(60, 24, 180, 233));
                    tocEntryText.FontWeight = FontWeights.Bold;
                };

                tocEntry.MouseLeave += (s, e) =>
                {
                    tocEntry.Background = null;
                    tocEntryText.FontWeight = FontWeights.Normal;
                };
            }

            // Populate section content from documentation text
            PopulateDocumentationContent(docSections);

            // Add search functionality at top of TOC
            var searchBox = new TextBox
            {
                Margin = new Thickness(5, 5, 5, 15),
                Padding = new Thickness(5, 3, 5, 3),
                FontFamily = new FontFamily("Consolas"),
                Background = new SolidColorBrush(Color.FromArgb(60, 24, 180, 233)),
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#124050")),
            };
            searchBox.SetValue(TextBoxBase.SelectionBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")));

            var searchPlaceholder = new TextBlock
            {
                Text = "🔍 Search documentation...",
                Foreground = new SolidColorBrush(Color.FromArgb(150, 180, 180, 180)),
                Margin = new Thickness(7, 3, 0, 0),
                IsHitTestVisible = false
            };

            var searchPanel = new Grid();
            searchPanel.Children.Add(searchBox);
            searchPanel.Children.Add(searchPlaceholder);

            // Add search box before TOC entries
            tocPanel.Children.Insert(0, searchPanel);

            // Hide placeholder when typing or when search has content
            searchBox.GotFocus += (s, e) => { if (searchBox.Text.Length == 0) searchPlaceholder.Visibility = Visibility.Collapsed; };
            searchBox.LostFocus += (s, e) => { if (searchBox.Text.Length == 0) searchPlaceholder.Visibility = Visibility.Visible; };
            searchBox.TextChanged += (s, e) =>
            {
                searchPlaceholder.Visibility = searchBox.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
                HighlightSearchTerms(searchBox.Text, contentStackPanel);
            };

            // Add footer with close button
            var footerPanel = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18536A")),
                Height = 50,
                VerticalAlignment = VerticalAlignment.Bottom,
                BorderThickness = new Thickness(0, 1, 0, 0),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#124050"))
            };

            var footerContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 20, 0)
            };

            var printButton = new Button
            {
                Content = "[ PRINT ]",
                Width = 120,
                Height = 30,
                Margin = new Thickness(10, 0, 10, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064557")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold
            };

            var closeButton = new Button
            {
                Content = "[ CLOSE ]",
                Width = 120,
                Height = 30,
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064557")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold
            };

            footerContent.Children.Add(printButton);
            footerContent.Children.Add(closeButton);
            footerPanel.Child = footerContent;
            mainGrid.Children.Add(footerPanel);

            // Button handlers
            closeButton.Click += (s, e) => docWindow.Close();
            printButton.Click += (s, e) => PrintDocumentation(contentStackPanel);

            // Show the window
            docWindow.ShowDialog();
        }

        private void PrintDocumentation(StackPanel contentPanel)
        {
            try
            {
                // Show print dialog
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    // Create a document for printing
                    FixedDocument document = new FixedDocument();

                    // Create page content
                    PageContent pageContent = new PageContent();
                    FixedPage fixedPage = new FixedPage();

                    // Use pattern matching to safely cast and check for null
                    if (CloneElement(contentPanel) is StackPanel printPanel)
                    {
                        // Set page size to A4
                        fixedPage.Width = 816; // 8.5 inches at 96 DPI
                        fixedPage.Height = 1056; // 11 inches at 96 DPI

                        // Add content
                        fixedPage.Children.Add(printPanel);

                        // Add to page content
                        ((IAddChild)pageContent).AddChild(fixedPage);
                        document.Pages.Add(pageContent);

                        // Print the document
                        printDialog.PrintDocument(document.DocumentPaginator, "Invoice Balance Refresher Documentation");

                        Log(LogLevel.Info, "Documentation printed");
                    }
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error printing documentation: {ex.Message}");
                MessageBox.Show($"Error printing documentation: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private UIElement? CloneElement(UIElement element)
        {
            if (element == null) return null;

            // Serialize to XAML
            string xaml = XamlWriter.Save(element);

            // Deserialize from XAML
            using (StringReader stringReader = new StringReader(xaml))
            using (XmlReader xmlReader = XmlReader.Create(stringReader))
            {
                return XamlReader.Load(xmlReader) as UIElement;
            }
        }


        private void HighlightSearchTerms(string searchText, StackPanel contentPanel)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Reset all highlighting
                ResetHighlighting(contentPanel);
                return;
            }

            searchText = searchText.Trim().ToLower();
            int matchCount = HighlightElementContent(contentPanel, searchText);

            // Update the search status
            foreach (var child in contentPanel.Children)
            {
                if (child is TextBlock block && block.Name == "SearchResults")
                {
                    block.Text = matchCount > 0 ? $"Found {matchCount} matches" : "No matches found";
                    block.Visibility = Visibility.Visible;
                    return;
                }
            }

            // If search results TextBlock doesn't exist, create it
            var searchResults = new TextBlock
            {
                Name = "SearchResults",
                Text = matchCount > 0 ? $"Found {matchCount} matches" : "No matches found",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 5, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            contentPanel.Children.Insert(0, searchResults);
        }

        private int HighlightElementContent(DependencyObject element, string searchText)
        {
            int matches = 0;

            if (element is TextBlock textBlock)
            {
                // Check if the TextBlock contains the search text
                string text = textBlock.Text.ToLower();
                if (text.Contains(searchText))
                {
                    // Store original font weight if not already stored
                    if (textBlock.Tag == null)
                    {
                        textBlock.Tag = textBlock.FontWeight.ToString();
                    }

                    // Apply highlight effect
                    textBlock.Background = new SolidColorBrush(Color.FromArgb(70, 24, 180, 233));
                    textBlock.FontWeight = FontWeights.Bold;
                    matches++;
                }
                else
                {
                    // Reset highlighting
                    textBlock.Background = null;

                    // Restore original font weight if we have it stored
                    if (textBlock.Tag is string storedWeight)
                    {
                        // Convert stored string back to FontWeight
                        if (FontWeightConverter.TryParse(storedWeight, out FontWeight originalWeight))
                        {
                            textBlock.FontWeight = originalWeight;
                        }
                        else
                        {
                            textBlock.FontWeight = FontWeights.Normal;
                        }
                    }
                    else
                    {
                        textBlock.FontWeight = FontWeights.Normal;
                    }
                }
            }
            else
            {
                // If not a TextBlock, check children
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(element, i);
                    matches += HighlightElementContent(child, searchText);
                }
            }

            return matches;
        }

        private void ResetHighlighting(DependencyObject element)
        {
            if (element is TextBlock textBlock && textBlock.Name != "SearchResults")
            {
                textBlock.Background = null;

                // Restore original font weight if we have it stored
                if (textBlock.Tag is string storedWeight)
                {
                    // Convert stored string back to FontWeight
                    if (FontWeightConverter.TryParse(storedWeight, out FontWeight originalWeight))
                    {
                        textBlock.FontWeight = originalWeight;
                    }
                    else
                    {
                        textBlock.FontWeight = FontWeights.Normal;
                    }
                }
                else
                {
                    textBlock.FontWeight = FontWeights.Normal;
                }
            }

            // Reset children
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(element, i);
                ResetHighlighting(child);
            }

            // Hide search results
            if (element is StackPanel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is TextBlock block && block.Name == "SearchResults")
                    {
                        block.Visibility = Visibility.Collapsed;
                        break;
                    }
                }
            }
        }

        // Helper method to parse FontWeight from string
        private static class FontWeightConverter
        {
            public static bool TryParse(string weightString, out FontWeight fontWeight)
            {
                fontWeight = FontWeights.Normal;

                try
                {
                    // Handle common FontWeight predefined values
                    switch (weightString)
                    {
                        case "Thin": fontWeight = FontWeights.Thin; return true;
                        case "ExtraLight": fontWeight = FontWeights.ExtraLight; return true;
                        case "Light": fontWeight = FontWeights.Light; return true;
                        case "Normal": fontWeight = FontWeights.Normal; return true;
                        case "Medium": fontWeight = FontWeights.Medium; return true;
                        case "SemiBold": fontWeight = FontWeights.SemiBold; return true;
                        case "Bold": fontWeight = FontWeights.Bold; return true;
                        case "ExtraBold": fontWeight = FontWeights.ExtraBold; return true;
                        case "Black": fontWeight = FontWeights.Black; return true;
                        case "ExtraBlack": fontWeight = FontWeights.ExtraBlack; return true;
                        default:
                            // Try to parse as numeric weight
                            if (int.TryParse(weightString, out int numericWeight))
                            {
                                fontWeight = FontWeight.FromOpenTypeWeight(numericWeight);
                                return true;
                            }
                            return false;
                    }
                }
                catch
                {
                    return false;
                }
            }
        }


        private void PopulateDocumentationContent(Dictionary<string, StackPanel> sections)
        {
            // SECTION 1: OVERVIEW
            var section1 = sections["1"];

            AddParagraph(section1,
                "The Invoice Balance Refresher is a terminal-style application designed to help Invoice Cloud clients " +
                "quickly refresh and validate invoice balances through the secure SOAP API service.",
                isIntro: true);

            AddParagraph(section1,
                "The application offers two primary modes of operation:");

            AddBulletPoint(section1, "Single invoice processing with real-time feedback");
            AddBulletPoint(section1, "Batch processing of multiple invoices via CSV files");

            AddParagraph(section1,
                "All operations are logged in the console at the bottom of the application with searchable history.");

            // SECTION 2: SINGLE INVOICE PROCESSING
            var section2 = sections["2"];

            AddParagraph(section2,
                "The Single Invoice Processing section allows you to refresh and check the balance of " +
                "an individual invoice, providing immediate feedback.");

            AddSteps(section2, "To process a single invoice:", new[]
            {
                "Enter the Biller GUID in the provided field",
                "Enter the Web Service Key in the provided field",
                "Enter the Account Number for the customer",
                "Enter the Invoice Number you wish to refresh",
                "Click [PROCESS INVOICE] to begin processing"
            });

            AddParagraph(section2,
                "The result will appear in the output box below the button showing the refreshed invoice data " +
                "including status, invoice date, due date, balance due, and other key information.");

            AddNote(section2,
                "The application will first refresh the customer balance, then retrieve the latest invoice information.");

            // SECTION 3: BATCH PROCESSING
            var section3 = sections["3"];

            AddParagraph(section3,
                "Batch processing enables you to refresh multiple invoices at once using a CSV file. " +
                "This is particularly useful for end-of-day or scheduled balance updates.");

            AddSteps(section3, "To process multiple invoices:", new[]
            {
                "Enter the Biller GUID and Web Service Key in the Single Invoice section",
                "Create a CSV file with one invoice number per line (or account/invoice pairs)",
                "Check the 'CSV has Account Numbers' box if your file contains account numbers",
                "Click [BROWSE] to select your CSV file",
                "Click [PROCESS CSV] to begin processing the batch"
            });

            AddParagraph(section3, "CSV File Format Options:");

            AddCodeBlock(section3,
                "Option 1: Invoice numbers only (one per line):\n" +
                "INV0001\n" +
                "INV0002\n" +
                "INV0003");

            AddCodeBlock(section3,
                "Option 2: Account numbers and invoice numbers:\n" +
                "ACCT001,INV0001\n" +
                "ACCT002,INV0002\n" +
                "ACCT003,INV0003");

            AddParagraph(section3,
                "Results will be saved to a file named 'InvoiceResults.csv' in the same directory as your input file. " +
                "This file will contain the processing status and updated balance information for each invoice.");

            // SECTION 3.1: SCHEDULING (NEW)
            AddSubheading(section3, "Automated Scheduling");

            AddParagraph(section3,
                "The application includes a built-in scheduling system that allows you to automate batch invoice processing at specific times. " +
                "You can schedule tasks to run once, daily, weekly, or monthly, and optionally have them run even when the application is closed by integrating with Windows Task Scheduler.");

            AddSteps(section3, "To schedule a batch process:", new[]
            {
                "Open the [File] menu and select [Scheduler]",
                "In the Schedule Manager window, click [ADD NEW] to create a new scheduled task",
                "Fill in the task details, including name, frequency (Once, Daily, Weekly, Monthly), and run time",
                "Specify the CSV file path, Biller GUID, Web Service Key, and whether the CSV includes account numbers",
                "Check 'Add to Windows Task Scheduler' if you want the task to run even when the app is closed",
                "Click [SAVE] to add the schedule"
            });

            AddParagraph(section3,
                "Scheduled tasks will appear in the Schedule Manager window, where you can edit, delete, or run them manually using the [RUN NOW] button. " +
                "The application will automatically execute enabled tasks at their scheduled times. If 'Add to Windows Task Scheduler' is checked, the task will be registered with Windows and can run independently of the app.");

            AddNote(section3,
                "You can manage all scheduled tasks from the Schedule Manager, including enabling/disabling, editing, or removing them. " +
                "Task results, last run time, and status are displayed in the manager for easy tracking.");

            // SECTION 4: LOGGING
            var section4 = sections["4"];

            AddParagraph(section4,
                "The application maintains detailed logs of all operations to help with troubleshooting " +
                "and to provide an audit trail of balance refresh activities.");

            AddSubheading(section4, "Log Features:");

            AddBulletPoint(section4, "All operations are logged in the console at the bottom of the application");
            AddBulletPoint(section4, "Session logs are automatically saved to the 'Logs' directory with timestamps");
            AddBulletPoint(section4, "You can manually save the current console log by clicking [SAVE LOGS]");
            AddBulletPoint(section4, "Clear the console display by clicking [CLEAR]");
            AddBulletPoint(section4, "Search functionality allows you to find specific entries quickly");

            AddParagraph(section4,
                "Log entries include timestamps, log levels (Info, Warning, Error, Debug), and detailed messages " +
                "about each operation performed by the application.");

            // SECTION 5: SAMPLE CSV GENERATION
            var section5 = sections["5"];

            AddParagraph(section5,
                "To help you get started with batch processing, the application can generate a sample CSV file " +
                "with the correct format.");

            AddSteps(section5, "To generate a sample CSV file:", new[]
            {
                "Click on [FILE] > Generate Sample CSV in the menu",
                "Choose where to save the file",
                "The file will be created with sample invoice numbers",
                "Edit the file with your actual invoice numbers before processing"
            });

            AddNote(section5,
                "Remember that the Biller GUID and Web Service Key from the Single Invoice section " +
                "will be used when processing any CSV file.");

            // SECTION 6: API FORMAT
            var section6 = sections["6"];

            AddParagraph(section6,
                "The application communicates with the Invoice Cloud SOAP API using a secure XML format. " +
                "Understanding this format may help when troubleshooting or developing integrations.");

            AddSubheading(section6, "Invoice Request Format:");

            AddCodeBlock(section6,
                "<soap12:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap12='http://www.w3.org/2003/05/soap-envelope'>\n" +
                "    <soap12:Body>\n" +
                "        <ViewInvoiceByInvoiceNumber xmlns='https://www.invoicecloud.com/portal/webservices/CloudInvoicing/'>\n" +
                "            <Req>\n" +
                "                <BillerGUID>{billerGUID}</BillerGUID>\n" +
                "                <WebServiceKey>{webServiceKey}</WebServiceKey>\n" +
                "                <InvoiceNumber>{invoiceNumber}</InvoiceNumber>\n" +
                "            </Req>\n" +
                "        </ViewInvoiceByInvoiceNumber>\n" +
                "    </soap12:Body>\n" +
                "</soap12:Envelope>");

            AddSubheading(section6, "Customer Record Request Format:");

            AddCodeBlock(section6,
                "<soap12:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap12='http://www.w3.org/2003/05/soap-envelope'>\n" +
                "  <soap12:Body>\n" +
                "    <ViewCustomerRecord xmlns='https://www.invoicecloud.com/portal/webservices/CloudManagement/'>\n" +
                "      <BillerGUID>{billerGUID}</BillerGUID>\n" +
                "      <WebServiceKey>{webServiceKey}</WebServiceKey>\n" +
                "      <AccountNumber>{accountNumber}</AccountNumber>\n" +
                "    </ViewCustomerRecord>\n" +
                "  </soap12:Body>\n" +
                "</soap12:Envelope>");

            AddNote(section6,
                "The application handles all the API communication details for you, including authentication, " +
                "error handling, and retry logic for intermittent failures.");

            // SECTION 7: FAQ
            var section7 = sections["7"];

            AddFAQItem(section7,
                "Where do I get my Biller GUID and Web Service Key?",
                "These credentials are provided by Invoice Cloud. Contact your account " +
                "representative or system administrator if you don't have them.");

            AddFAQItem(section7,
                "How do I know if an invoice balance was successfully refreshed?",
                "After processing, the status will show 'Success' and display the updated " +
                "balance information. The console log will also show a success message.");

            AddFAQItem(section7,
                "Can I process invoices with different account numbers in batch mode?",
                "Yes. Check the 'CSV has Account Numbers' option and format your CSV file with " +
                "both account number and invoice number on each line separated by a comma:\n" +
                "ACCT001,INV0001\n" +
                "ACCT002,INV0002");

            AddFAQItem(section7,
                "How can I search for specific invoices in batch results?",
                "Use the search box above the batch results. It will filter results to show " +
                "only invoices that contain your search text.");

            AddFAQItem(section7,
                "What do I do if I get a 'Server error' message?",
                "The application automatically retries server errors up to 3 times. If it still " +
                "fails, verify your network connection and check that the Invoice Cloud service " +
                "is operating normally.");

            AddFAQItem(section7,
                "Where are logs stored?",
                "Logs are automatically saved in the 'Logs' directory within the application folder. " +
                "Each session creates a new log file with a timestamp in the filename.");

            AddFAQItem(section7,
                "Can I use this application with multiple Biller GUIDs?",
                "Yes. You can switch between different Biller GUIDs by updating the fields in the " +
                "Single Invoice section before processing.");

            AddFAQItem(section7,
                "How do I switch between light and dark mode?",
                "Use the View menu and select either 'Light Mode' or 'Dark Mode'.");

            AddFAQItem(section7,
                "What happens if the CSV file has invalid invoice numbers?",
                "The application will process valid invoice numbers and log errors for invalid ones. " +
                "The results CSV will include error details for failed invoices.");

            AddFAQItem(section7,
                "Is there a limit to how many invoices I can process in batch mode?",
                "There is no hard limit in the application, but processing very large batches " +
                "may take significant time. Consider breaking very large batches into smaller files.");

            AddFAQItem(section7,
                "How does scheduling work?",
                "You can automate batch invoice processing by creating scheduled tasks in the Schedule Manager. " +
                "Tasks can be set to run at specific times and frequencies, and can be integrated with Windows Task Scheduler " +
                "to run even when the application is not open. The Schedule Manager allows you to add, edit, delete, enable/disable, " +
                "and manually run scheduled tasks. Task results and statuses are displayed for easy monitoring.");
        }

        // Helper methods for content formatting

        private void AddParagraph(StackPanel panel, string text, bool isIntro = false)
        {
            var paragraph = new TextBlock
            {
                Text = text,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };

            if (isIntro)
            {
                paragraph.FontSize += 2;
                paragraph.FontStyle = FontStyles.Italic;
            }

            // Store original font weight as a string
            paragraph.Tag = paragraph.FontWeight.ToString();
            panel.Children.Add(paragraph);
        }

        private void AddSubheading(StackPanel panel, string text)
        {
            var subheading = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 10, 0, 5)
            };

            // Store original font weight as a string
            subheading.Tag = subheading.FontWeight.ToString();
            panel.Children.Add(subheading);
        }

        private void AddBulletPoint(StackPanel panel, string text)
        {
            var bulletPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 5) };

            var bullet = new TextBlock
            {
                Text = "•",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                Margin = new Thickness(0, 0, 5, 0),
                FontWeight = FontWeights.Bold
            };

            var content = new TextBlock
            {
                Text = text,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap
            };

            // Store original font weights as strings
            bullet.Tag = bullet.FontWeight.ToString();
            content.Tag = content.FontWeight.ToString();

            bulletPanel.Children.Add(bullet);
            bulletPanel.Children.Add(content);
            panel.Children.Add(bulletPanel);
        }


        private void AddSteps(StackPanel panel, string title, string[] steps)
        {
            AddParagraph(panel, title);

            for (int i = 0; i < steps.Length; i++)
            {
                var stepPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 5) };

                var number = new TextBlock
                {
                    Text = (i + 1).ToString() + ".",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5BFF64")),
                    Margin = new Thickness(0, 0, 5, 0),
                    FontWeight = FontWeights.Bold,
                    Width = 20
                };

                var content = new TextBlock
                {
                    Text = steps[i],
                    Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                    TextWrapping = TextWrapping.Wrap
                };

                // Store original font weights as strings
                number.Tag = number.FontWeight.ToString();
                content.Tag = content.FontWeight.ToString();

                stepPanel.Children.Add(number);
                stepPanel.Children.Add(content);
                panel.Children.Add(stepPanel);
            }

            // Add space after steps
            panel.Children.Add(new Rectangle { Height = 10 });
        }

        private void AddNote(StackPanel panel, string text)
        {
            var noteBorder = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0A030")),
                Background = new SolidColorBrush(Color.FromArgb(20, 240, 160, 48)),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 5, 0, 15),
                CornerRadius = new CornerRadius(4)
            };

            var notePanel = new StackPanel { Orientation = Orientation.Horizontal };

            var noteIcon = new TextBlock
            {
                Text = "📝",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0A030")),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            var noteContent = new TextBlock
            {
                Text = "NOTE: " + text,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap
            };

            // Store original font weights as strings
            noteIcon.Tag = noteIcon.FontWeight.ToString();
            noteContent.Tag = noteContent.FontWeight.ToString();

            notePanel.Children.Add(noteIcon);
            notePanel.Children.Add(noteContent);
            noteBorder.Child = notePanel;
            panel.Children.Add(noteBorder);
        }

        private void AddCodeBlock(StackPanel panel, string code)
        {
            var codeBorder = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#124050")),
                Background = new SolidColorBrush(Color.FromArgb(30, 18, 83, 106)),
                Padding = new Thickness(10),
                Margin = new Thickness(10, 5, 10, 15),
                CornerRadius = new CornerRadius(4)
            };

            var codeText = new TextBlock
            {
                Text = code,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E55555")),
                FontFamily = new FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap
            };

            // Store original font weight as a string
            codeText.Tag = codeText.FontWeight.ToString();

            codeBorder.Child = codeText;
            panel.Children.Add(codeBorder);
        }

        



        private void AddFAQItem(StackPanel panel, string question, string answer)
        {
            var faqBorder = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#124050")),
                Background = new SolidColorBrush(Color.FromArgb(20, 24, 180, 233)),
                Padding = new Thickness(15, 10, 15, 15),
                Margin = new Thickness(0, 5, 0, 15),
                CornerRadius = new CornerRadius(4)
            };

            var faqPanel = new StackPanel();

            var questionText = new TextBlock
            {
                Text = "Q: " + question,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var answerText = new TextBlock
            {
                Text = "A: " + answer,
                Foreground = (SolidColorBrush)FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap
            };

            // Store original font weights as strings
            questionText.Tag = questionText.FontWeight.ToString();
            answerText.Tag = answerText.FontWeight.ToString();

            faqPanel.Children.Add(questionText);
            faqPanel.Children.Add(answerText);
            faqBorder.Child = faqPanel;
            panel.Children.Add(faqBorder);
        }




        


        #endregion

        // Log entry model
        public class LogEntry
        {
            public string Timestamp { get; }
            public LogLevel Level { get; }
            public string Message { get; }

            public LogEntry(string timestamp, LogLevel level, string message)
            {
                Timestamp = timestamp;
                Level = level;
                Message = message;
            }
        }

        // Log level enum
        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }
    }
}
