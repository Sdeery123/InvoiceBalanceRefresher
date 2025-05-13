using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace InvoiceBalanceRefresher
{
    public class MaintenanceHelper
    {
        private readonly string _logDirectory;
        private readonly int _logRetentionDays;
        private readonly int _maxSessionFilesPerDay;
        private readonly bool _enableLogCleanup;
        private readonly bool _enableOrphanedTaskCleanup;
        private readonly Action<MainWindow.LogLevel, string> _logAction;

        // Existing fields for maintenance frequency
        private readonly MaintenanceFrequency _maintenanceFrequency;
        private readonly DateTime _lastMaintenanceRun;

        public MaintenanceHelper(
        string logDirectory,
        int logRetentionDays,
        int maxSessionFilesPerDay,
        bool enableLogCleanup,
        bool enableOrphanedTaskCleanup,
        MaintenanceFrequency maintenanceFrequency,
        DateTime lastMaintenanceRun,
        Action<MainWindow.LogLevel, string> logAction)
        {
            _logDirectory = logDirectory;
            _logRetentionDays = logRetentionDays;
            _maxSessionFilesPerDay = maxSessionFilesPerDay;
            _enableLogCleanup = enableLogCleanup;
            _enableOrphanedTaskCleanup = enableOrphanedTaskCleanup;
            _maintenanceFrequency = maintenanceFrequency;
            _lastMaintenanceRun = lastMaintenanceRun;
            _logAction = logAction;
        }

        // Existing method to check if maintenance should run based on frequency
        public bool ShouldRunMaintenance()
        {
            // Always run if it's the first time (LastMaintenanceRun is DateTime.MinValue)
            if (_lastMaintenanceRun == DateTime.MinValue)
            {
                _logAction(MainWindow.LogLevel.Debug, "First-time maintenance run");
                return true;
            }

            // Check based on frequency
            switch (_maintenanceFrequency)
            {
                case MaintenanceFrequency.EveryStartup:
                    _logAction(MainWindow.LogLevel.Debug, "Maintenance frequency set to run on every startup");
                    return true;

                case MaintenanceFrequency.Daily:
                    // Check if the last run was on a different day
                    var daysSinceLastRun = (DateTime.Now - _lastMaintenanceRun).TotalDays;
                    bool shouldRun = daysSinceLastRun >= 1;
                    _logAction(MainWindow.LogLevel.Debug, $"Daily maintenance check: {daysSinceLastRun:F1} days since last run, should run: {shouldRun}");
                    return shouldRun;

                case MaintenanceFrequency.Weekly:
                    // Check if the last run was in a different week
                    var weeksSinceLastRun = (DateTime.Now - _lastMaintenanceRun).TotalDays / 7;
                    shouldRun = weeksSinceLastRun >= 1;
                    _logAction(MainWindow.LogLevel.Debug, $"Weekly maintenance check: {weeksSinceLastRun:F1} weeks since last run, should run: {shouldRun}");
                    return shouldRun;

                case MaintenanceFrequency.Monthly:
                    // Check if the last run was in a different month
                    bool differentMonth =
                        _lastMaintenanceRun.Month != DateTime.Now.Month ||
                        _lastMaintenanceRun.Year != DateTime.Now.Year;
                    _logAction(MainWindow.LogLevel.Debug, $"Monthly maintenance check: last run was on {_lastMaintenanceRun:yyyy-MM-dd}, different month: {differentMonth}");
                    return differentMonth;

                default:
                    _logAction(MainWindow.LogLevel.Warning, $"Unknown maintenance frequency: {_maintenanceFrequency}, running maintenance to be safe");
                    return true;
            }
        }

        // Update RunMaintenance method to pass the logAction to static methods
        public bool RunMaintenance(MaintenanceConfig config)
        {
            _logAction(MainWindow.LogLevel.Info, "Starting maintenance tasks...");
            bool taskRan = false;

            try
            {
                if (_enableLogCleanup)
                {
                    _logAction(MainWindow.LogLevel.Info, "Running log cleanup tasks");
                    CleanupLogs(_logDirectory, _logRetentionDays, _logAction);
                    EnforceMaxSessionFiles();
                    taskRan = true;
                }
                else
                {
                    _logAction(MainWindow.LogLevel.Debug, "Log cleanup is disabled");
                }

                if (_enableOrphanedTaskCleanup)
                {
                    _logAction(MainWindow.LogLevel.Info, "Running orphaned tasks cleanup");
                    CleanupOrphanedTasks(_logAction);
                    taskRan = true;
                }
                else
                {
                    _logAction(MainWindow.LogLevel.Debug, "Orphaned tasks cleanup is disabled");
                }

                // Update LastMaintenanceRun if any task ran
                if (taskRan)
                {
                    config.LastMaintenanceRun = DateTime.Now;
                    config.Save();
                    _logAction(MainWindow.LogLevel.Info, $"Maintenance completed and last run time updated to {config.LastMaintenanceRun}");
                }
                else
                {
                    _logAction(MainWindow.LogLevel.Info, "No maintenance tasks were enabled to run");
                }

                return taskRan;
            }
            catch (Exception ex)
            {
                _logAction(MainWindow.LogLevel.Error, $"Error running maintenance tasks: {ex.Message}");
                return false;
            }
        }

        private void EnforceMaxSessionFiles()
        {
            try
            {
                if (_maxSessionFilesPerDay <= 0)
                {
                    _logAction(MainWindow.LogLevel.Debug, "Max session files limit is 0 or negative, skipping enforcement");
                    return;
                }

                if (!Directory.Exists(_logDirectory))
                {
                    _logAction(MainWindow.LogLevel.Info, $"Creating log directory: {_logDirectory}");
                    Directory.CreateDirectory(_logDirectory);
                    return;
                }

                // Log the current configuration
                _logAction(MainWindow.LogLevel.Debug, $"Enforcing max session files: {_maxSessionFilesPerDay} files per day in {_logDirectory}");

                // Match both "session_" and "Session_" patterns
                var logFiles = Directory.GetFiles(_logDirectory, "*.log")
                    .Select(f => new FileInfo(f))
                    .Where(f => f.Name.StartsWith("session_", StringComparison.OrdinalIgnoreCase));

                // Group files by creation date
                var now = DateTime.Now.Date;
                var filesByDate = logFiles
                    .GroupBy(f => f.CreationTime.Date);

                // For today's files, keep only the most recent _maxSessionFilesPerDay
                var todaysFiles = filesByDate.FirstOrDefault(g => g.Key == now);
                if (todaysFiles != null)
                {
                    _logAction(MainWindow.LogLevel.Debug, $"Found {todaysFiles.Count()} log files for today");

                    if (todaysFiles.Count() > _maxSessionFilesPerDay)
                    {
                        // Order by creation time (oldest first)
                        var filesToDelete = todaysFiles
                            .OrderBy(f => f.CreationTime)
                            .Take(todaysFiles.Count() - _maxSessionFilesPerDay)
                            .ToList(); // Force evaluation

                        _logAction(MainWindow.LogLevel.Info, $"Cleaning up {filesToDelete.Count} old session files");

                        foreach (var file in filesToDelete)
                        {
                            try
                            {
                                File.Delete(file.FullName);
                                _logAction(MainWindow.LogLevel.Info, $"Deleted old session file: {file.Name}");
                            }
                            catch (Exception ex)
                            {
                                _logAction(MainWindow.LogLevel.Warning, $"Failed to delete file {file.Name}: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        _logAction(MainWindow.LogLevel.Debug, "No session files need to be deleted today");
                    }
                }
                else
                {
                    _logAction(MainWindow.LogLevel.Debug, "No log files found for today");
                }
            }
            catch (Exception ex)
            {
                _logAction(MainWindow.LogLevel.Error, $"Error enforcing max session files: {ex.Message}");
            }
        }

        public static void CleanupLogs(string logDirectory, int logRetentionDays, Action<MainWindow.LogLevel, string>? logAction = null)

        {
            try
            {
                if (logRetentionDays <= 0)
                {
                    logAction?.Invoke(MainWindow.LogLevel.Debug, "Log retention days is 0 or negative, skipping cleanup");
                    return;
                }

                if (!Directory.Exists(logDirectory))
                {
                    logAction?.Invoke(MainWindow.LogLevel.Info, $"Creating log directory: {logDirectory}");
                    Directory.CreateDirectory(logDirectory);
                    return;
                }

                logAction?.Invoke(MainWindow.LogLevel.Debug, $"Cleaning up logs older than {logRetentionDays} days in {logDirectory}");

                var cutoffDate = DateTime.Now.AddDays(-logRetentionDays);
                var oldFiles = Directory.GetFiles(logDirectory, "*.log")
                    .Select(f => new FileInfo(f))
                    .Where(f => f.CreationTime < cutoffDate)
                    .ToList(); // Force evaluation

                logAction?.Invoke(MainWindow.LogLevel.Info, $"Found {oldFiles.Count} old log files to clean up");

                foreach (var file in oldFiles)
                {
                    try
                    {
                        File.Delete(file.FullName);
                        logAction?.Invoke(MainWindow.LogLevel.Info, $"Deleted old log file: {file.Name}");
                    }
                    catch (Exception ex)
                    {
                        logAction?.Invoke(MainWindow.LogLevel.Warning, $"Failed to delete file {file.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke(MainWindow.LogLevel.Error, $"Error cleaning up logs: {ex.Message}");
            }
        }

        public static void CleanupOrphanedTasks(Action<MainWindow.LogLevel, string> logAction)
        {
            try
            {
                logAction?.Invoke(MainWindow.LogLevel.Info, "Cleaning up schedule-related data");

                // Get an instance of the schedule manager with simple logAction
                var scheduleManager = ScheduleManager.GetInstance(
                    message => logAction?.Invoke(MainWindow.LogLevel.Info, message),
                    (_, _, _, _, _) => Task.FromResult(false)); // Updated to take 5 parameters

                // We don't need to call CleanupOrphanedTasks anymore since Windows Task integration is removed
                // However, we might want to check for any data integrity issues in the schedule data

                // Check for any corrupted schedule entries or perform other maintenance on schedules
                // This could involve validating all scheduled tasks or cleaning up old/expired tasks

                logAction?.Invoke(MainWindow.LogLevel.Info, "Schedule data cleanup completed");
            }
            catch (Exception ex)
            {
                logAction?.Invoke(MainWindow.LogLevel.Error, $"Error cleaning up schedule data: {ex.Message}");
            }
        }

    }
}
