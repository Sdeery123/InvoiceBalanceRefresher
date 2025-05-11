using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceBalanceRefresher
{
    public enum MaintenanceFrequency
    {
        EveryStartup,    // Run maintenance on every application startup
        Daily,           // Run once per day (based on last run date)
        Weekly,          // Run once per week
        Monthly          // Run once per month
    }

    public class MaintenanceConfig
    {
        public int LogRetentionDays { get; set; } = 30;
        public string LogDirectory { get; set; } = "Logs";
        public int MaxSessionFilesPerDay { get; set; } = 10;
        public bool EnableLogCleanup { get; set; } = true;
        public bool EnableOrphanedTaskCleanup { get; set; } = true;
        public bool EnablePeriodicMaintenance { get; set; } = false;
        public MaintenanceFrequency MaintenanceFrequency { get; set; } = MaintenanceFrequency.EveryStartup;
        public DateTime LastMaintenanceRun { get; set; } = DateTime.MinValue;

        private static readonly string ConfigFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "MaintenanceConfig.json");

        public static MaintenanceConfig Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<MaintenanceConfig>(json);
                    return config ?? new MaintenanceConfig();
                }
            }
            catch (Exception ex)
            {
                // If there's an error reading the config, log it and return default
                Console.WriteLine($"Error loading maintenance config: {ex.Message}");
            }

            return new MaintenanceConfig();
        }

        public bool Save()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(ConfigFilePath, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving maintenance config: {ex.Message}");
                return false;
            }
        }
    }
}
