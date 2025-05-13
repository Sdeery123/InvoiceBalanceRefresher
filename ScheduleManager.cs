using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;
using System.Collections.ObjectModel;


namespace InvoiceBalanceRefresher
{
    public class ScheduleManager
    {
        private static ScheduleManager _instance = null!;
        private static readonly object _lock = new object();

        public ObservableCollection<ScheduledTask> ScheduledTasks { get; private set; }
        private string _schedulesFilePath;
        private DispatcherTimer _checkTimer;
        private readonly Action<string> _logAction = _ => { }; // Default to a no-op action
        private readonly Func<string, string, string, bool, string, System.Threading.Tasks.Task<bool>> _processBatchAction =
            (_, _, _, _, _) => System.Threading.Tasks.Task.FromResult(false); // Default to a no-op function

        // Event for notifying subscribers when tasks change
        public event EventHandler? TasksChanged;

        private ScheduleManager(Action<string> logAction, Func<string, string, string, bool, string, Task<bool>> processBatchAction)
        {
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
            _processBatchAction = processBatchAction ?? throw new ArgumentNullException(nameof(processBatchAction));
            ScheduledTasks = new ObservableCollection<ScheduledTask>();
            _schedulesFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Schedules.xml");

            // Create a timer to check for scheduled tasks
            _checkTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1) // Check every minute
            };
            _checkTimer.Tick += CheckScheduledTasks;
            _checkTimer.Start();

            _logAction?.Invoke($"Schedule manager initialized. Checking schedules every {_checkTimer.Interval.TotalMinutes} minutes.");

            LoadSchedules();
        }

        public static ScheduleManager GetInstance(Action<string> logAction, Func<string, string, string, bool, string, Task<bool>> processBatchAction)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new ScheduleManager(logAction, processBatchAction);
                    }
                }
            }

            return _instance;
        }

        public static void ResetInstance()
        {
            lock (_lock)
            {
                _instance = null!;
            }
        }

        // Helper method to notify subscribers that tasks have changed
        protected virtual void OnTasksChanged()
        {
            TasksChanged?.Invoke(this, EventArgs.Empty);
        }

        private void CheckScheduledTasks(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            var tasksToRun = ScheduledTasks.Where(t => t.IsEnabled && t.NextRunTime <= now).ToList();

            if (tasksToRun.Any())
            {
                _logAction?.Invoke($"Found {tasksToRun.Count} scheduled tasks to run.");
                bool anyTasksRun = false;

                foreach (var task in tasksToRun)
                {
                    _logAction?.Invoke($"Executing scheduled task: {task.Name}");
                    ExecuteTask(task);

                    // Update last run time and next run time
                    task.LastRunTime = now;
                    task.UpdateNextRunTime();

                    // If it's a one-time task, disable it
                    if (task.Frequency == ScheduleFrequency.Once)
                    {
                        task.IsEnabled = false;
                    }

                    anyTasksRun = true;
                }

                // Save the updated schedules
                SaveSchedules();

                // Notify subscribers of changes
                if (anyTasksRun)
                {
                    OnTasksChanged();
                }
            }
        }

        private async void ExecuteTask(ScheduledTask task)
        {
            var originalDirectory = Environment.CurrentDirectory;
            try
            {
                _logAction?.Invoke($"[Scheduled Task] Executing task: {task.Name}");
                _logAction?.Invoke($"[Scheduled Task] Original working directory: {originalDirectory}");
                _logAction?.Invoke($"[Scheduled Task] Task parameters: BillerGUID={task.BillerGUID}, HasAccountNumbers={task.HasAccountNumbers}");

                // Always use absolute file paths
                string csvFullPath = Path.IsPathRooted(task.CsvFilePath)
                    ? task.CsvFilePath
                    : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, task.CsvFilePath));

                _logAction?.Invoke($"[Scheduled Task] Using absolute CSV path: {csvFullPath}");

                // Set working directory
                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
                _logAction?.Invoke($"[Scheduled Task] Set working directory to: {AppDomain.CurrentDomain.BaseDirectory}");

                if (!File.Exists(csvFullPath))
                {
                    task.LastRunSuccessful = false;
                    task.LastRunResult = $"Error: CSV file not found at {csvFullPath}";
                    _logAction?.Invoke($"[Scheduled Task] {task.LastRunResult}");
                    return;
                }

                try
                {
                    // Check file content
                    string fileContent = "";
                    try
                    {
                        fileContent = File.ReadAllText(csvFullPath);
                        _logAction?.Invoke($"[Scheduled Task] CSV file content (first 100 chars): {fileContent.Substring(0, Math.Min(fileContent.Length, 100))}");
                        _logAction?.Invoke($"[Scheduled Task] CSV file size: {new FileInfo(csvFullPath).Length} bytes");

                        if (string.IsNullOrWhiteSpace(fileContent))
                        {
                            task.LastRunSuccessful = false;
                            task.LastRunResult = "Error: CSV file is empty";
                            _logAction?.Invoke($"[Scheduled Task] {task.LastRunResult}");
                            return;
                        }
                    }
                    catch (Exception readEx)
                    {
                        _logAction?.Invoke($"[Scheduled Task] Warning: Could not read CSV content: {readEx.Message}");
                        task.LastRunSuccessful = false;
                        task.LastRunResult = $"Error: Failed to read CSV file: {readEx.Message}";
                        return;
                    }

                    // Use the task's CustomOption field as default account number if specified
                    string defaultAccountNumber = string.IsNullOrEmpty(task.CustomOption) ? "" : task.CustomOption;
                    _logAction?.Invoke($"[Scheduled Task] Using default account number: {(string.IsNullOrEmpty(defaultAccountNumber) ? "(none)" : defaultAccountNumber)}");

                    // Log detailed information for the API call
                    _logAction?.Invoke($"[Scheduled Task] Detailed parameters for batch processing:");
                    _logAction?.Invoke($"[Scheduled Task] - BillerGUID: {task.BillerGUID}");
                    _logAction?.Invoke($"[Scheduled Task] - WebServiceKey: {task.WebServiceKey.Substring(0, Math.Min(5, task.WebServiceKey.Length))}... (truncated)");
                    _logAction?.Invoke($"[Scheduled Task] - CSV File: {csvFullPath}");
                    _logAction?.Invoke($"[Scheduled Task] - Has Account Numbers: {task.HasAccountNumbers}");
                    _logAction?.Invoke($"[Scheduled Task] - Default Account Number: {defaultAccountNumber}");

                    // Create a special, direct task processor that avoids UI dependencies
                    _logAction?.Invoke($"[Scheduled Task] Creating direct task processor...");

                    // Create a standalone batch processor for this specific task
                    var tempProcessor = new BatchProcessingHelper(
                        new InvoiceCloudApiService((level, message) =>
                            _logAction?.Invoke($"[API] {message}")),
                        (level, message) => _logAction?.Invoke($"[Batch] {message}")
                    );

                    _logAction?.Invoke($"[Scheduled Task] About to process batch via direct processor...");
                    bool success = await tempProcessor.ProcessBatchFile(
                        task.BillerGUID,
                        task.WebServiceKey,
                        csvFullPath,
                        task.HasAccountNumbers,
                        defaultAccountNumber);

                    _logAction?.Invoke($"[Scheduled Task] Direct processor returned: {success}");

                    // Update task with results
                    task.LastRunTime = DateTime.Now;
                    task.LastRunSuccessful = success;

                    // Provide more detailed information about success/failure
                    if (success)
                    {
                        task.LastRunResult = "Success: Batch processing completed";
                    }
                    else
                    {
                        task.LastRunResult =
                            "Failed to process batch - Check API credentials, CSV format, and network connectivity";
                    }

                    _logAction?.Invoke($"[Scheduled Task] Task {task.Name} completed with result: {task.LastRunResult}");
                }
                catch (Exception ex)
                {
                    task.LastRunSuccessful = false;
                    task.LastRunResult = $"Error: {ex.Message}";
                    _logAction?.Invoke($"[Scheduled Task] Error executing task {task.Name}: {ex.Message}");
                    _logAction?.Invoke($"[Scheduled Task] Exception details: {ex}");

                    if (ex.InnerException != null)
                    {
                        _logAction?.Invoke($"[Scheduled Task] Inner exception: {ex.InnerException.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                task.LastRunSuccessful = false;
                task.LastRunResult = $"Error: {ex.Message}";
                _logAction?.Invoke($"[Scheduled Task] Critical error executing task {task.Name}: {ex.Message}");
                _logAction?.Invoke($"[Scheduled Task] Exception details: {ex}");

                if (ex.InnerException != null)
                {
                    _logAction?.Invoke($"[Scheduled Task] Inner exception: {ex.InnerException.Message}");
                }
            }
            finally
            {
                // Restore original directory
                try
                {
                    Directory.SetCurrentDirectory(originalDirectory);
                    _logAction?.Invoke($"[Scheduled Task] Restored working directory to: {originalDirectory}");
                }
                catch (Exception ex)
                {
                    _logAction?.Invoke($"[Scheduled Task] Warning: Could not restore working directory: {ex.Message}");
                }

                // Update the next run time and save the schedule
                task.UpdateNextRunTime();
                SaveSchedules();

                // Notify subscribers that task data has changed
                OnTasksChanged();
            }
        }




        public async Task<bool> RunTaskNow(Guid taskId)
        {
            var task = ScheduledTasks.FirstOrDefault(t => t.Id == taskId);
            if (task == null)
            {
                _logAction?.Invoke($"Task with ID {taskId} not found");
                return false;
            }

            _logAction?.Invoke($"Manually executing task: {task.Name}");
            var originalDirectory = Environment.CurrentDirectory;

            try
            {
                // Validate parameters before processing
                if (string.IsNullOrWhiteSpace(task.BillerGUID) || string.IsNullOrWhiteSpace(task.WebServiceKey))
                {
                    _logAction?.Invoke("[Manual Run] Error: Invalid BillerGUID or WebServiceKey (empty)");
                    task.LastRunSuccessful = false;
                    task.LastRunResult = "Error: Invalid BillerGUID or WebServiceKey (empty)";
                    SaveSchedules();
                    OnTasksChanged();
                    return false;
                }

                // Always use absolute file paths
                string csvFullPath = Path.IsPathRooted(task.CsvFilePath)
                    ? task.CsvFilePath
                    : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, task.CsvFilePath));

                _logAction?.Invoke($"[Manual Run] Using absolute CSV path: {csvFullPath}");

                // Set working directory
                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
                _logAction?.Invoke($"[Manual Run] Set working directory to: {AppDomain.CurrentDomain.BaseDirectory}");

                if (!File.Exists(csvFullPath))
                {
                    _logAction?.Invoke($"[Manual Run] Error: CSV file not found at {csvFullPath}");
                    task.LastRunSuccessful = false;
                    task.LastRunResult = $"Error: CSV file not found at {csvFullPath}";
                    SaveSchedules();
                    OnTasksChanged();
                    return false;
                }

                // Check file content
                try
                {
                    var fileContent = File.ReadAllText(csvFullPath);
                    _logAction?.Invoke($"[Manual Run] CSV file content (first 100 chars): {fileContent.Substring(0, Math.Min(fileContent.Length, 100))}");
                    _logAction?.Invoke($"[Manual Run] CSV file size: {new FileInfo(csvFullPath).Length} bytes");

                    if (string.IsNullOrWhiteSpace(fileContent))
                    {
                        _logAction?.Invoke("[Manual Run] Error: CSV file is empty");
                        task.LastRunSuccessful = false;
                        task.LastRunResult = "Error: CSV file is empty";
                        SaveSchedules();
                        OnTasksChanged();
                        return false;
                    }
                }
                catch (Exception readEx)
                {
                    _logAction?.Invoke($"[Manual Run] Warning: Could not read CSV content: {readEx.Message}");
                }

                // Use the task's CustomOption field as default account number if specified
                string defaultAccountNumber = string.IsNullOrEmpty(task.CustomOption) ? "" : task.CustomOption;
                _logAction?.Invoke($"[Manual Run] Using default account number: {(string.IsNullOrEmpty(defaultAccountNumber) ? "(none)" : defaultAccountNumber)}");

                // Log detailed information for the API call
                _logAction?.Invoke($"[Manual Run] Detailed parameters for batch processing:");
                _logAction?.Invoke($"[Manual Run] - BillerGUID: {task.BillerGUID}");
                _logAction?.Invoke($"[Manual Run] - WebServiceKey: {task.WebServiceKey.Substring(0, Math.Min(5, task.WebServiceKey.Length))}... (truncated)");
                _logAction?.Invoke($"[Manual Run] - CSV File: {csvFullPath}");
                _logAction?.Invoke($"[Manual Run] - Has Account Numbers: {task.HasAccountNumbers}");
                _logAction?.Invoke($"[Manual Run] - Default Account Number: {defaultAccountNumber}");

                // Process the batch
                _logAction?.Invoke("[Manual Run] About to invoke _processBatchAction...");
                bool success = await _processBatchAction.Invoke(
                    task.BillerGUID,
                    task.WebServiceKey,
                    csvFullPath, // Use absolute path
                    task.HasAccountNumbers,
                    defaultAccountNumber); // Pass default account number

                _logAction?.Invoke($"[Manual Run] _processBatchAction returned: {success}");

                // Update with new results
                task.LastRunTime = DateTime.Now;
                task.LastRunSuccessful = success;

                // Provide more detailed information about success/failure
                if (success)
                {
                    task.LastRunResult = "Success (Manual Run): Batch processing completed";
                }
                else
                {
                    task.LastRunResult =
                        "Failed (Manual Run): Check API credentials, CSV format, and network connectivity";
                }

                // Save changes
                SaveSchedules();

                // Notify subscribers that task data has changed
                OnTasksChanged();

                _logAction?.Invoke($"Manual execution of task {task.Name} completed with result: {task.LastRunResult}");
                return success;
            }
            catch (Exception ex)
            {
                task.LastRunSuccessful = false;
                task.LastRunResult = $"Error (Manual Run): {ex.Message}";
                _logAction?.Invoke($"Error executing task {task.Name} manually: {ex.Message}");
                _logAction?.Invoke($"Exception details: {ex}");

                if (ex.InnerException != null)
                {
                    _logAction?.Invoke($"Inner exception: {ex.InnerException.Message}");
                }

                SaveSchedules();

                // Notify subscribers even on failure
                OnTasksChanged();

                return false;
            }
            finally
            {
                // Restore original directory
                try
                {
                    Directory.SetCurrentDirectory(originalDirectory);
                    _logAction?.Invoke($"[Manual Run] Restored working directory to: {originalDirectory}");
                }
                catch (Exception ex)
                {
                    _logAction?.Invoke($"[Manual Run] Warning: Could not restore working directory: {ex.Message}");
                }
            }
        }

        public void AddSchedule(ScheduledTask task)
        {
            ScheduledTasks.Add(task);
            SaveSchedules();
            _logAction?.Invoke($"Added new schedule: {task.Name}, next run at {task.NextRunTime}");

            // Notify subscribers about the new task
            OnTasksChanged();
        }

        public void UpdateSchedule(ScheduledTask task)
        {
            var existingTask = ScheduledTasks.FirstOrDefault(t => t.Id == task.Id);
            if (existingTask != null)
            {
                // Update task in list
                int index = ScheduledTasks.IndexOf(existingTask);
                ScheduledTasks[index] = task;
                SaveSchedules();
                _logAction?.Invoke($"Updated schedule: {task.Name}, next run at {task.NextRunTime}");

                // Notify subscribers about the update
                OnTasksChanged();
            }
        }

        public void RemoveSchedule(Guid taskId)
        {
            var task = ScheduledTasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                ScheduledTasks.Remove(task);
                SaveSchedules();
                _logAction?.Invoke($"Removed schedule: {task.Name}");

                // Notify subscribers about the removal
                OnTasksChanged();
            }
        }

        public void EnableSchedule(Guid taskId, bool enable)
        {
            var task = ScheduledTasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                task.IsEnabled = enable;
                SaveSchedules();
                _logAction?.Invoke($"{(enable ? "Enabled" : "Disabled")} schedule: {task.Name}");

                // Notify subscribers about the change
                OnTasksChanged();
            }
        }

        private void LoadSchedules()
        {
            try
            {
                if (File.Exists(_schedulesFilePath))
                {
                    using (var reader = new StreamReader(_schedulesFilePath))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(List<ScheduledTask>));
                        var deserializedTasks = serializer.Deserialize(reader) as List<ScheduledTask>;
                        ScheduledTasks = new ObservableCollection<ScheduledTask>(deserializedTasks ?? new List<ScheduledTask>());
                        _logAction?.Invoke($"Loaded {ScheduledTasks.Count} scheduled tasks from {_schedulesFilePath}");
                    }
                }
                else
                {
                    ScheduledTasks = new ObservableCollection<ScheduledTask>();
                    _logAction?.Invoke("No saved schedules found. Starting with empty schedule list.");
                }

                // Notify subscribers that tasks have been loaded
                OnTasksChanged();
            }
            catch (Exception ex)
            {
                ScheduledTasks = new ObservableCollection<ScheduledTask>();
                _logAction?.Invoke($"Error loading schedules: {ex.Message}");
            }
        }

        private void SaveSchedules()
        {
            try
            {
                // Ensure the directory exists
                string? directory = Path.GetDirectoryName(_schedulesFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var writer = new StreamWriter(_schedulesFilePath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<ScheduledTask>));
                    // Convert ObservableCollection to List before serializing
                    serializer.Serialize(writer, ScheduledTasks.ToList());
                }
                _logAction?.Invoke($"Saved {ScheduledTasks.Count} scheduled tasks to {_schedulesFilePath}");
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"Error saving schedules: {ex.Message}");
            }
        }

        public void ExportTasks(string filePath)
        {
            try
            {
                // Ensure the directory exists
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var writer = new StreamWriter(filePath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<ScheduledTask>));
                    // Convert ObservableCollection to List before serializing
                    serializer.Serialize(writer, ScheduledTasks.ToList());
                }
                _logAction?.Invoke($"Exported {ScheduledTasks.Count} scheduled tasks to {filePath}");
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"Error exporting schedules: {ex.Message}");
            }
        }

        public void ImportTasks(string filePath, bool replaceExisting)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logAction?.Invoke($"Import file not found: {filePath}");
                    return;
                }

                List<ScheduledTask> importedTasks;

                using (var reader = new StreamReader(filePath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<ScheduledTask>));
                    importedTasks = serializer.Deserialize(reader) as List<ScheduledTask> ?? new List<ScheduledTask>();
                }

                if (replaceExisting)
                {
                    ScheduledTasks = new ObservableCollection<ScheduledTask>(importedTasks);
                }
                else
                {
                    foreach (var importedTask in importedTasks)
                    {
                        importedTask.Id = Guid.NewGuid();
                        importedTask.Name = $"{importedTask.Name} (Imported)";
                        ScheduledTasks.Add(importedTask);
                    }
                }

                SaveSchedules();
                _logAction?.Invoke($"Imported {importedTasks.Count} scheduled tasks from {filePath}");

                // Notify subscribers about the imported tasks
                OnTasksChanged();
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"Error importing schedules: {ex.Message}");
            }
        }
    }
}
