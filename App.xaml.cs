using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Diagnostics;

namespace InvoiceBalanceRefresher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private string _logDirectory = string.Empty;
        private Action<string> _debugLog = _ => { };
        private string _debugLogPath = string.Empty;


        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                InitializeLogging();

                // Log startup
                _debugLog("Application starting");
                _debugLog($"Application version: {GetApplicationVersion()}");
                _debugLog($"Current directory: {Environment.CurrentDirectory}");
                _debugLog($"Base directory: {AppDomain.CurrentDomain.BaseDirectory}");

                // Get command-line arguments
                string[] args = Environment.GetCommandLineArgs();
                _debugLog($"Command-line arguments count: {args.Length}");
                for (int i = 0; i < args.Length; i++)
                {
                    _debugLog($"Arg[{i}]: {args[i]}");
                }

                // Continue with base startup
                base.OnStartup(e);
                _debugLog("Base.OnStartup completed");

                // Process command-line arguments if any
                if (args.Length > 1)
                {
                    _debugLog($"First argument: {args[1]}");

                    if (args[1].Equals("--schedule", StringComparison.OrdinalIgnoreCase) && args.Length > 2)
                    {
                        _debugLog($"Schedule argument found: {args[2]}");

                        if (Guid.TryParse(args[2], out Guid taskId))
                        {
                            _debugLog($"Parsed task ID: {taskId}");
                            VerifySchedulesFile();

                            // Run the scheduled task
                            _debugLog("Starting RunScheduledTaskAndExit");
                            RunScheduledTaskAndExit(taskId);
                            _debugLog("RunScheduledTaskAndExit method returned (should not happen)");
                            return;
                        }
                        else
                        {
                            _debugLog($"Failed to parse task ID: {args[2]}");
                        }
                    }
                    else
                    {
                        _debugLog("Schedule argument not found or insufficient arguments");
                    }
                }
                else
                {
                    _debugLog("No command-line arguments for scheduled task");
                }

                // Normal application startup - show the main window
                _debugLog("Starting normal UI application");
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                _debugLog("MainWindow shown");
            }
            catch (Exception ex)
            {
                LogStartupError(ex);

                // Try to continue with normal startup
                try
                {
                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();
                }
                catch (Exception innerEx)
                {
                    // Log the inner exception if we failed to create the main window
                    LogStartupError(innerEx, "SECONDARY_STARTUP_ERROR");

                    // Last resort - show a message box
                    MessageBox.Show($"Critical error during application startup: {ex.Message}\n\nAdditional error: {innerEx.Message}",
                        "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void InitializeLogging()
        {
            // Set up logging directory
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // Set up debug log
            _debugLogPath = Path.Combine(_logDirectory, $"AppStartup_Detailed_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            // Create debug log action
            _debugLog = (message) => {
                try
                {
                    string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}";
                    File.AppendAllText(_debugLogPath, entry + Environment.NewLine);
                }
                catch
                {
                    // Silent failure if logging fails - we can't do much about it
                }
            };
        }

        private void LogStartupError(Exception ex, string prefix = "STARTUP_ERROR")
        {
            try
            {
                string errorLogPath = Path.Combine(
                    _logDirectory,
                    $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                StringBuilder errorInfo = new StringBuilder();
                errorInfo.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {prefix}: {ex.Message}");
                errorInfo.AppendLine($"Stack Trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    errorInfo.AppendLine($"Inner Exception: {ex.InnerException.Message}");
                    errorInfo.AppendLine($"Inner Stack Trace: {ex.InnerException.StackTrace}");
                }

                File.WriteAllText(errorLogPath, errorInfo.ToString());
            }
            catch
            {
                // If we can't log the error, there's not much we can do
            }
        }

        private void VerifySchedulesFile()
        {
            string schedulesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Schedules.xml");
            if (File.Exists(schedulesPath))
            {
                _debugLog($"Schedules.xml exists: {schedulesPath}");
                _debugLog($"File size: {new FileInfo(schedulesPath).Length} bytes");

                try
                {
                    string fileContent = File.ReadAllText(schedulesPath);
                    _debugLog($"First 100 characters of file: {fileContent.Substring(0, Math.Min(100, fileContent.Length))}");
                }
                catch (Exception ex)
                {
                    _debugLog($"Error reading Schedules.xml: {ex.Message}");
                }
            }
            else
            {
                _debugLog($"ERROR: Schedules.xml not found at {schedulesPath}");
            }
        }

        private string GetApplicationVersion()
        {
            try
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown (Error getting version)";
            }
        }

        private async void RunScheduledTaskAndExit(Guid taskId)
        {
            string logFile = Path.Combine(_logDirectory, $"ScheduledTask_{taskId}_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            Action<string> logAction = message => { }; // Initialize with a default no-op action


            try
            {
                // Create log action
                logAction = (message) =>
                {
                    try
                    {
                        string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}";
                        File.AppendAllText(logFile, logEntry + Environment.NewLine);
                    }
                    catch
                    {
                        // Silent failure if logging fails
                    }
                };

                logAction($"Starting scheduled task execution for task ID: {taskId}");
                logAction($"Machine name: {Environment.MachineName}");
                logAction($"OS version: {Environment.OSVersion}");
                logAction($"Application path: {AppDomain.CurrentDomain.BaseDirectory}");

                // Create API service with logging
                var apiService = new InvoiceCloudApiService((level, message) =>
                    logAction($"[{level}] {message}"));

                // Create BatchProcessingHelper in headless mode (no UI dependencies)
                var batchProcessingHelper = new BatchProcessingHelper(
                    apiService,
                    (level, message) => logAction($"[{level}] {message}"));

                // FIX: Reset the singleton before creating the instance for scheduled/background runs
                ScheduleManager.ResetInstance();

                // Get schedule manager instance
                // Inside the RunScheduledTaskAndExit method, update the ScheduleManager.GetInstance call
                var scheduleManager = ScheduleManager.GetInstance(
                    logAction,
                    async (billerGUID, webServiceKey, csvFilePath, hasAccountNumbers, defaultAccountNumber) =>
                    {
                        try
                        {
                            logAction($"Processing batch: BillerGUID: {billerGUID}, CSV: {csvFilePath}");

                            // Use defaultAccountNumber if available
                            logAction($"Default account number: {(string.IsNullOrEmpty(defaultAccountNumber) ? "(none)" : defaultAccountNumber)}");

                            // Use the headless BatchProcessingHelper for processing
                            return await batchProcessingHelper.ProcessBatchFile(
                                billerGUID, webServiceKey, csvFilePath, hasAccountNumbers, defaultAccountNumber);
                        }
                        catch (Exception ex)
                        {
                            logAction($"Error in batch processing: {ex.Message}");
                            if (ex.InnerException != null)
                            {
                                logAction($"Inner exception: {ex.InnerException.Message}");
                            }
                            return false;
                        }
                    });


                // Verify the schedule manager has loaded tasks
                if (scheduleManager.ScheduledTasks == null || !scheduleManager.ScheduledTasks.Any())
                {
                    logAction("ERROR: No scheduled tasks loaded from Schedules.xml");
                    return;
                }

                // Log all scheduled tasks
                logAction($"Found {scheduleManager.ScheduledTasks.Count} scheduled tasks:");
                foreach (var task in scheduleManager.ScheduledTasks)
                {
                    logAction($"Task ID: {task.Id}, Name: {task.Name}, Next Run Time: {task.NextRunTime}, Is Enabled: {task.IsEnabled}");
                }

                // Check if the task exists
                var taskToRun = scheduleManager.ScheduledTasks.FirstOrDefault(t => t.Id == taskId);
                if (taskToRun == null)
                {
                    logAction($"ERROR: Task with ID {taskId} not found in Schedules.xml. Available Task IDs:");
                    foreach (var task in scheduleManager.ScheduledTasks)
                    {
                        logAction($"- {task.Id} ({task.Name})");
                    }
                    return;
                }

                logAction($"Found task: {taskToRun.Name}, CsvFilePath: {taskToRun.CsvFilePath}");

                // Verify CSV file exists
                if (!File.Exists(taskToRun.CsvFilePath))
                {
                    logAction($"ERROR: CSV file does not exist: {taskToRun.CsvFilePath}");
                    return;
                }

                // Execute the task
                logAction($"Executing task with ID: {taskId}");
                bool result = await scheduleManager.RunTaskNow(taskId);

                if (result)
                {
                    logAction($"Task execution completed successfully for task ID: {taskId}");
                }
                else
                {
                    logAction($"Task execution failed for task ID: {taskId}");
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Critical error executing task: {ex.Message}\r\n{ex.StackTrace}";

                if (ex.InnerException != null)
                {
                    errorMessage += $"\r\nInner Exception: {ex.InnerException.Message}\r\n{ex.InnerException.StackTrace}";
                }

                try
                {
                    string errorLogPath = Path.Combine(
                        _logDirectory,
                        $"ScheduledTaskError_{taskId}_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                    File.WriteAllText(errorLogPath,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {errorMessage}");
                }
                catch
                {
                    // If we can't log the error, try using the original logAction
                    logAction?.Invoke($"ERROR: {errorMessage}");
                }
            }
            finally
            {
                if (logAction != null)
                {
                    logAction("Task execution completed, application will now exit.");
                }

                // Add a small delay to ensure all file operations complete
                await Task.Delay(500);

                // Ensure the application exits after task completion
                Environment.Exit(0);
            }
        }
    }
}
