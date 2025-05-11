using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Diagnostics;
using Microsoft.Win32.TaskScheduler;
using System.Security;

namespace InvoiceBalanceRefresher
{
    public class ScheduleManager
    {
        private static ScheduleManager _instance = null!;
        private static readonly object _lock = new object();

        public List<ScheduledTask> ScheduledTasks { get; private set; }
        private string _schedulesFilePath;
        private DispatcherTimer _checkTimer;
        private readonly Action<string> _logAction = _ => { }; // Default to a no-op action
        private readonly Func<string, string, string, bool, System.Threading.Tasks.Task<bool>> _processBatchAction =
            (_, _, _, _) => System.Threading.Tasks.Task.FromResult(false); // Default to a no-op function

        // The name of the folder to create in Windows Task Scheduler
        private const string TASK_FOLDER_NAME = "InvoiceBalanceRefresher";

        private ScheduleManager(Action<string> logAction, Func<string, string, string, bool, Task<bool>> processBatchAction)
        {
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
            _processBatchAction = processBatchAction ?? throw new ArgumentNullException(nameof(processBatchAction));
            ScheduledTasks = new List<ScheduledTask>();
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

            // Ensure our task folder exists in Windows Task Scheduler
            EnsureTaskFolderExists();
        }

        private void EnsureTaskFolderExists()
        {
            try
            {
                using (TaskService ts = new TaskService())
                {
                    // Create the folder if it doesn't exist
                    if (!ts.RootFolder.SubFolders.Exists(TASK_FOLDER_NAME))
                    {
                        ts.RootFolder.CreateFolder(TASK_FOLDER_NAME);
                        _logAction?.Invoke($"Created Windows Task Scheduler folder: {TASK_FOLDER_NAME}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"Warning: Could not create Task Scheduler folder: {ex.Message}");
            }
        }

        public static ScheduleManager GetInstance(Action<string> logAction, Func<string, string, string, bool, Task<bool>> processBatchAction)
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

        private void CheckScheduledTasks(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            var tasksToRun = ScheduledTasks.Where(t => t.IsEnabled && t.NextRunTime <= now).ToList();

            if (tasksToRun.Any())
            {
                _logAction?.Invoke($"Found {tasksToRun.Count} scheduled tasks to run.");

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
                }

                // Save the updated schedules
                SaveSchedules();
            }
        }

        private async void ExecuteTask(ScheduledTask task)
        {
            try
            {
                bool success = false;

                if (_processBatchAction != null)
                {
                    success = await _processBatchAction.Invoke(
                        task.BillerGUID,
                        task.WebServiceKey,
                        task.CsvFilePath,
                        task.HasAccountNumbers);
                }

                task.LastRunSuccessful = success;
                task.LastRunResult = success ? "Success" : "Failed to process batch";

                _logAction?.Invoke($"Task {task.Name} completed with result: {task.LastRunResult}");
            }
            catch (Exception ex)
            {
                task.LastRunSuccessful = false;
                task.LastRunResult = $"Error: {ex.Message}";
                _logAction?.Invoke($"Error executing task {task.Name}: {ex.Message}");
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

            try
            {
                bool success = await _processBatchAction.Invoke(
                    task.BillerGUID,
                    task.WebServiceKey,
                    task.CsvFilePath,
                    task.HasAccountNumbers);

                // Update task stats but don't change schedule
                DateTime previousRunTime = task.LastRunTime;
                bool previousSuccess = task.LastRunSuccessful;
                string previousResult = task.LastRunResult;

                // Update with new results
                task.LastRunTime = DateTime.Now;
                task.LastRunSuccessful = success;
                task.LastRunResult = success ? "Success (Manual Run)" : "Failed (Manual Run)";

                // Save changes
                SaveSchedules();

                _logAction?.Invoke($"Manual execution of task {task.Name} completed with result: {task.LastRunResult}");
                return success;
            }
            catch (Exception ex)
            {
                task.LastRunSuccessful = false;
                task.LastRunResult = $"Error (Manual Run): {ex.Message}";
                SaveSchedules();
                _logAction?.Invoke($"Error executing task {task.Name} manually: {ex.Message}");
                return false;
            }
        }

        public void AddSchedule(ScheduledTask task)
        {
            ScheduledTasks.Add(task);
            SaveSchedules();

            // Only set up Windows Task if enabled
            if (task.AddToWindowsTaskScheduler)
            {
                SetupWindowsTask(task);
                _logAction?.Invoke($"Added new schedule: {task.Name} to Windows Task Scheduler, next run at {task.NextRunTime}");
            }
            else
            {
                _logAction?.Invoke($"Added new schedule: {task.Name}, next run at {task.NextRunTime} (Windows Task Scheduler integration disabled)");
            }
        }

        public void UpdateSchedule(ScheduledTask task)
        {
            var existingTask = ScheduledTasks.FirstOrDefault(t => t.Id == task.Id);
            if (existingTask != null)
            {
                // Check if Windows Task Scheduler setting changed
                bool previousSetting = existingTask.AddToWindowsTaskScheduler;

                // Update task in list
                int index = ScheduledTasks.IndexOf(existingTask);
                ScheduledTasks[index] = task;
                SaveSchedules();

                // Handle Windows Task Scheduler changes
                if (task.AddToWindowsTaskScheduler)
                {
                    // If setting was turned on or was already on, update the task
                    UpdateWindowsTask(task);
                    _logAction?.Invoke($"Updated schedule: {task.Name}, next run at {task.NextRunTime}");
                }
                else if (previousSetting && !task.AddToWindowsTaskScheduler)
                {
                    // If setting was turned off, remove from Windows Task Scheduler
                    RemoveWindowsTask(task);
                    _logAction?.Invoke($"Updated schedule: {task.Name}, removed from Windows Task Scheduler");
                }
                else
                {
                    _logAction?.Invoke($"Updated schedule: {task.Name}, next run at {task.NextRunTime}");
                }
            }
        }

        public void RemoveSchedule(Guid taskId)
        {
            var task = ScheduledTasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                ScheduledTasks.Remove(task);
                SaveSchedules();
                RemoveWindowsTask(task);
                _logAction?.Invoke($"Removed schedule: {task.Name}");
            }
        }

        public void EnableSchedule(Guid taskId, bool enable)
        {
            var task = ScheduledTasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                task.IsEnabled = enable;
                SaveSchedules();

                if (enable && task.AddToWindowsTaskScheduler)
                {
                    SetupWindowsTask(task);
                    _logAction?.Invoke($"Enabled schedule: {task.Name}");
                }
                else if (!enable && task.AddToWindowsTaskScheduler)
                {
                    DisableWindowsTask(task);
                    _logAction?.Invoke($"Disabled schedule: {task.Name}");
                }
                else
                {
                    _logAction?.Invoke($"Updated schedule state: {task.Name} (in-app only)");
                }
            }
        }

        public List<string> GetRegisteredWindowsTasks()
        {
            List<string> registeredTasks = new List<string>();

            try
            {
                using (TaskService ts = new TaskService())
                {
                    TaskFolder taskFolder;
                    if (ts.RootFolder.SubFolders.Exists(TASK_FOLDER_NAME))
                    {
                        taskFolder = ts.RootFolder.SubFolders[TASK_FOLDER_NAME];
                    }
                    else
                    {
                        taskFolder = ts.RootFolder;
                    }

                    foreach (var registeredTask in taskFolder.Tasks)
                    {
                        if (registeredTask.Name.StartsWith("InvoiceBalanceRefresher_"))
                        {
                            registeredTasks.Add(registeredTask.Name);
                        }
                    }
                }

                _logAction?.Invoke($"Found {registeredTasks.Count} registered Windows tasks for this application");
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"Error retrieving Windows tasks: {ex.Message}");
            }

            return registeredTasks;
        }

        public void CleanupOrphanedTasks()
        {
            try
            {
                using (TaskService ts = new TaskService())
                {
                    TaskFolder taskFolder;
                    if (ts.RootFolder.SubFolders.Exists(TASK_FOLDER_NAME))
                    {
                        taskFolder = ts.RootFolder.SubFolders[TASK_FOLDER_NAME];
                    }
                    else
                    {
                        taskFolder = ts.RootFolder;
                    }

                    var windowsTasks = taskFolder.Tasks
                        .Where(t => t.Name.StartsWith("InvoiceBalanceRefresher_"))
                        .ToList();

                    int removedCount = 0;

                    foreach (var windowsTask in windowsTasks)
                    {
                        // Extract the GUID from the task name
                        string taskIdStr = windowsTask.Name.Substring(windowsTask.Name.LastIndexOf('_') + 1);
                        if (Guid.TryParse(taskIdStr, out Guid taskId))
                        {
                            // Check if this task exists in our application
                            bool taskExists = ScheduledTasks.Any(t => t.Id == taskId);

                            if (!taskExists)
                            {
                                // This is an orphaned task, remove it
                                taskFolder.DeleteTask(windowsTask.Name);
                                removedCount++;
                            }
                        }
                    }

                    if (removedCount > 0)
                    {
                        _logAction?.Invoke($"Cleaned up {removedCount} orphaned Windows tasks");
                    }
                }
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"Error cleaning up orphaned tasks: {ex.Message}");
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
                        ScheduledTasks = deserializedTasks ?? new List<ScheduledTask>();
                        _logAction?.Invoke($"Loaded {ScheduledTasks.Count} scheduled tasks from {_schedulesFilePath}");
                    }
                }
                else
                {
                    ScheduledTasks = new List<ScheduledTask>();
                    _logAction?.Invoke("No saved schedules found. Starting with empty schedule list.");
                }
            }
            catch (Exception ex)
            {
                ScheduledTasks = new List<ScheduledTask>();
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
                    serializer.Serialize(writer, ScheduledTasks);
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
                    serializer.Serialize(writer, ScheduledTasks);
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
                    // Remove all existing Windows tasks first
                    foreach (var task in ScheduledTasks)
                    {
                        RemoveWindowsTask(task);
                    }

                    // Replace task list and recreate Windows tasks
                    ScheduledTasks = importedTasks;

                    foreach (var task in ScheduledTasks.Where(t => t.IsEnabled && t.AddToWindowsTaskScheduler))
                    {
                        SetupWindowsTask(task);
                    }
                }
                else
                {
                    // Merge imported tasks with existing ones
                    foreach (var importedTask in importedTasks)
                    {
                        // Generate a new ID to avoid conflicts
                        importedTask.Id = Guid.NewGuid();
                        importedTask.Name = $"{importedTask.Name} (Imported)";

                        ScheduledTasks.Add(importedTask);

                        if (importedTask.IsEnabled && importedTask.AddToWindowsTaskScheduler)
                        {
                            SetupWindowsTask(importedTask);
                        }
                    }
                }

                SaveSchedules();
                _logAction?.Invoke($"Imported {importedTasks.Count} scheduled tasks from {filePath}");
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"Error importing schedules: {ex.Message}");
            }
        }

        // Methods for Windows Task Scheduler integration
        private void SetupWindowsTask(ScheduledTask task)
        {
            try
            {
                // Skip if task is not enabled or Windows Task Scheduler integration is disabled
                if (!task.IsEnabled || !task.AddToWindowsTaskScheduler)
                    return;

                // Get the path to the current executable
                string exePath = Process.GetCurrentProcess()?.MainModule?.FileName
                    ?? throw new InvalidOperationException("Unable to retrieve the executable path.");

                // Create a new TaskDefinition
                using (TaskService ts = new TaskService())
                {
                    // Get or create our task folder
                    TaskFolder taskFolder;
                    if (ts.RootFolder.SubFolders.Exists(TASK_FOLDER_NAME))
                    {
                        taskFolder = ts.RootFolder.SubFolders[TASK_FOLDER_NAME];
                    }
                    else
                    {
                        taskFolder = ts.RootFolder.CreateFolder(TASK_FOLDER_NAME);
                    }

                    // Create a new task definition
                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = task.Description;

                    // Set security options
                    td.Principal.RunLevel = task.RunWithHighestPrivileges ? TaskRunLevel.Highest : TaskRunLevel.LUA;

                    // Configure triggers based on frequency
                    switch (task.Frequency)
                    {
                        case ScheduleFrequency.Once:
                            // One-time task
                            TimeTrigger timeTrigger = new TimeTrigger(task.NextRunTime);
                            td.Triggers.Add(timeTrigger);
                            break;

                        case ScheduleFrequency.Daily:
                            // Daily task with configurable interval
                            DailyTrigger dailyTrigger = new DailyTrigger();
                            dailyTrigger.StartBoundary = DateTime.Today.Add(task.RunTime);
                            dailyTrigger.DaysInterval = task.DaysInterval; // Run every X days
                            td.Triggers.Add(dailyTrigger);
                            break;

                        case ScheduleFrequency.Weekly:
                            // Weekly task with configurable days
                            WeeklyTrigger weeklyTrigger = new WeeklyTrigger();
                            weeklyTrigger.StartBoundary = DateTime.Today.Add(task.RunTime);
                            weeklyTrigger.DaysOfWeek = task.SelectedDaysOfWeek; // Run on selected days
                            td.Triggers.Add(weeklyTrigger);
                            break;

                        case ScheduleFrequency.Monthly:
                            // Monthly task with configurable days and months
                            MonthlyTrigger monthlyTrigger = new MonthlyTrigger();
                            monthlyTrigger.StartBoundary = DateTime.Today.Add(task.RunTime);
                            monthlyTrigger.DaysOfMonth = task.SelectedDaysOfMonth; // Run on selected days
                            monthlyTrigger.MonthsOfYear = task.SelectedMonths; // Run in selected months
                            td.Triggers.Add(monthlyTrigger);
                            break;
                    }

                    // Create command line with parameters
                    string parameters = $"--schedule {task.Id}";

                    // Add custom option if specified
                    if (!string.IsNullOrEmpty(task.CustomOption))
                    {
                        parameters += $" --option \"{task.CustomOption}\"";
                    }

                    // Create the action
                    td.Actions.Add(new ExecAction(exePath, parameters, null));

                    // Set advanced settings
                    td.Settings.DisallowStartIfOnBatteries = !task.AllowRunOnBattery;
                    td.Settings.StopIfGoingOnBatteries = !task.AllowRunOnBattery;
                    td.Settings.WakeToRun = task.WakeToRun;
                    td.Settings.RunOnlyIfNetworkAvailable = task.RunOnlyIfNetworkAvailable;

                    // Set execution time limit
                    if (task.ExecutionTimeLimitMinutes > 0)
                    {
                        td.Settings.ExecutionTimeLimit = TimeSpan.FromMinutes(task.ExecutionTimeLimitMinutes);
                    }
                    else
                    {
                        td.Settings.ExecutionTimeLimit = TimeSpan.Zero; // No time limit
                    }

                    // Set retry settings
                    if (task.MaxRetryCount > 0)
                    {
                        td.Settings.RestartCount = task.MaxRetryCount;
                        td.Settings.RestartInterval = TimeSpan.FromMinutes(task.RetryIntervalMinutes);
                    }

                    // Task name includes both name and ID for uniqueness and readability
                    string safeTaskName = MakeSafeTaskName(task.Name);
                    string taskName = $"InvoiceBalanceRefresher_{safeTaskName}_{task.Id}";

                    // Register the task in our folder
                    if (!string.IsNullOrEmpty(task.WindowsTaskUsername))
                    {
                        // Convert SecureString to string for use with the TaskFolder.RegisterTaskDefinition method
                        // This is necessary because the method signature requires string, not SecureString
                        string passwordStr = string.Empty;
                        if (!string.IsNullOrEmpty(task.WindowsTaskPassword))
                        {
                            passwordStr = task.WindowsTaskPassword;
                        }

                        // Register with specific credentials
                        taskFolder.RegisterTaskDefinition(
                            taskName,
                            td,
                            TaskCreation.CreateOrUpdate,
                            task.WindowsTaskUsername,
                            passwordStr,  // Use string instead of SecureString
                            TaskLogonType.Password);
                    }
                    else
                    {
                        // Register with current user
                        taskFolder.RegisterTaskDefinition(
                            taskName,
                            td,
                            TaskCreation.CreateOrUpdate,
                            null,
                            null,
                            TaskLogonType.InteractiveToken);
                    }


                    _logAction?.Invoke($"Created/updated Windows scheduled task '{taskName}' for: {task.Name}");
                }
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"Error setting up Windows Task for {task.Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logAction?.Invoke($"  Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        private string MakeSafeTaskName(string name)
        {
            // Replace invalid characters with underscores
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string safeName = new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());

            // Limit length
            if (safeName.Length > 50)
            {
                safeName = safeName.Substring(0, 50);
            }

            return safeName;
        }

        private void UpdateWindowsTask(ScheduledTask task)
        {
            // First remove the existing task
            RemoveWindowsTask(task);

            // Then create a new one if the task is enabled
            if (task.IsEnabled)
            {
                SetupWindowsTask(task);
            }
        }

        private void RemoveWindowsTask(ScheduledTask task)
        {
            try
            {
                using (TaskService ts = new TaskService())
                {
                    TaskFolder taskFolder;

                    // Try to use our custom folder first
                    if (ts.RootFolder.SubFolders.Exists(TASK_FOLDER_NAME))
                    {
                        taskFolder = ts.RootFolder.SubFolders[TASK_FOLDER_NAME];
                    }
                    else
                    {
                        taskFolder = ts.RootFolder;
                    }

                    // Try to remove by ID-based name
                    string taskIdName = $"InvoiceBalanceRefresher_{task.Id}";
                    bool removed = false;

                    // Look for any task containing our task ID
                    foreach (var registeredTask in taskFolder.Tasks)
                    {
                        if (registeredTask.Name.Contains(task.Id.ToString()))
                        {
                            taskFolder.DeleteTask(registeredTask.Name);
                            removed = true;
                            _logAction?.Invoke($"Removed Windows scheduled task '{registeredTask.Name}' for: {task.Name}");
                            break;
                        }
                    }

                    // Also try with safe name (backward compatibility)
                    if (!removed)
                    {
                        string safeTaskName = MakeSafeTaskName(task.Name);
                        string nameBasedTaskName = $"InvoiceBalanceRefresher_{safeTaskName}_{task.Id}";

                        if (taskFolder.Tasks.Exists(nameBasedTaskName))
                        {
                            taskFolder.DeleteTask(nameBasedTaskName);
                            _logAction?.Invoke($"Removed Windows scheduled task '{nameBasedTaskName}' for: {task.Name}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"Error removing Windows Task for {task.Name}: {ex.Message}");
            }
        }

        private void DisableWindowsTask(ScheduledTask task)
        {
            // Simply remove the task as Windows Task Scheduler doesn't have a simple "disable" option
            RemoveWindowsTask(task);
            _logAction?.Invoke($"Disabled Windows scheduled task for: {task.Name}");
        }
    }
}
