using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Diagnostics;
using Microsoft.Win32.TaskScheduler;

namespace InvoiceBalanceRefresher
{
    public class ScheduleManager
    {
        private static ScheduleManager _instance;
        private static readonly object _lock = new object();

        public List<ScheduledTask> ScheduledTasks { get; private set; }
        private string _schedulesFilePath;
        private DispatcherTimer _checkTimer;
        private Action<string> _logAction;
        private Func<string, string, string, bool, Task<bool>> _processBatchAction;

        private ScheduleManager(Action<string> logAction, Func<string, string, string, bool, Task<bool>> processBatchAction)
        {
            _logAction = logAction;
            _processBatchAction = processBatchAction;
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

        private void CheckScheduledTasks(object sender, EventArgs e)
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


        public void AddSchedule(ScheduledTask task)
        {
            ScheduledTasks.Add(task);
            SaveSchedules();
            SetupWindowsTask(task);
            _logAction?.Invoke($"Added new schedule: {task.Name}, next run at {task.NextRunTime}");
        }

        public void UpdateSchedule(ScheduledTask task)
        {
            var existingTask = ScheduledTasks.FirstOrDefault(t => t.Id == task.Id);
            if (existingTask != null)
            {
                int index = ScheduledTasks.IndexOf(existingTask);
                ScheduledTasks[index] = task;
                SaveSchedules();
                UpdateWindowsTask(task);
                _logAction?.Invoke($"Updated schedule: {task.Name}, next run at {task.NextRunTime}");
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

                if (enable)
                {
                    SetupWindowsTask(task);
                    _logAction?.Invoke($"Enabled schedule: {task.Name}");
                }
                else
                {
                    DisableWindowsTask(task);
                    _logAction?.Invoke($"Disabled schedule: {task.Name}");
                }
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
                        ScheduledTasks = (List<ScheduledTask>)serializer.Deserialize(reader);
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

        // Methods for Windows Task Scheduler integration
        private void SetupWindowsTask(ScheduledTask task)
        {
            try
            {
                // Skip if task is not enabled
                if (!task.IsEnabled)
                    return;

                // Get the path to the current executable
                string exePath = Process.GetCurrentProcess().MainModule.FileName;

                // Create a new TaskDefinition
                using (TaskService ts = new TaskService())
                {
                    // Create a new task definition
                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = task.Description;

                    // Set up triggers based on frequency
                    switch (task.Frequency)
                    {
                        case ScheduleFrequency.Once:
                            td.Triggers.Add(new TimeTrigger(task.NextRunTime));
                            break;
                        case ScheduleFrequency.Daily:
                            DailyTrigger dailyTrigger = new DailyTrigger();
                            dailyTrigger.StartBoundary = DateTime.Today.Add(task.RunTime);
                            td.Triggers.Add(dailyTrigger);
                            break;
                        case ScheduleFrequency.Weekly:
                            WeeklyTrigger weeklyTrigger = new WeeklyTrigger();
                            weeklyTrigger.StartBoundary = DateTime.Today.Add(task.RunTime);
                            weeklyTrigger.DaysOfWeek = DaysOfTheWeek.Monday; // You might want to make this configurable
                            td.Triggers.Add(weeklyTrigger);
                            break;
                        case ScheduleFrequency.Monthly:
                            MonthlyTrigger monthlyTrigger = new MonthlyTrigger();
                            monthlyTrigger.StartBoundary = DateTime.Today.Add(task.RunTime);
                            monthlyTrigger.DaysOfMonth = new int[] { 1 }; // You might want to make this configurable
                            td.Triggers.Add(monthlyTrigger);
                            break;
                    }

                    // Create an action that launches the application with the proper task ID
                    td.Actions.Add(new ExecAction(exePath, $"--schedule {task.Id}", null));

                    // Additional settings
                    td.Settings.DisallowStartIfOnBatteries = false;
                    td.Settings.StopIfGoingOnBatteries = false;
                    td.Settings.ExecutionTimeLimit = TimeSpan.Zero; // No time limit

                    // Register the task in the root folder
                    string taskName = $"InvoiceBalanceRefresher_{task.Id}";
                    ts.RootFolder.RegisterTaskDefinition(taskName, td);

                    _logAction?.Invoke($"Created Windows scheduled task '{taskName}' for: {task.Name}");
                }
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"Error setting up Windows Task for {task.Name}: {ex.Message}");
            }
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
                string taskName = $"InvoiceBalanceRefresher_{task.Id}";

                using (TaskService ts = new TaskService())
                {
                    // Check if the task exists first
                    if (ts.RootFolder.Tasks.Exists(taskName))
                    {
                        ts.RootFolder.DeleteTask(taskName);
                        _logAction?.Invoke($"Removed Windows scheduled task '{taskName}' for: {task.Name}");
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
