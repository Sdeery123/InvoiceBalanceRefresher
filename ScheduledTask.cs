using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace InvoiceBalanceRefresher
{
    [Serializable]
    public class ScheduledTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public string Description { get; set; }
        public string CsvFilePath { get; set; }
        public string BillerGUID { get; set; }
        public string WebServiceKey { get; set; }
        public bool HasAccountNumbers { get; set; }
        public DateTime NextRunTime { get; set; }
        public ScheduleFrequency Frequency { get; set; }
        public TimeSpan RunTime { get; set; }  // Time of day to run
        public bool IsEnabled { get; set; } = true;
        public DateTime LastRunTime { get; set; }
        public bool LastRunSuccessful { get; set; }
        public string LastRunResult { get; set; }
        // Add to ScheduledTask.cs
        public string CustomOption { get; set; } = string.Empty;
        // Add to ScheduledTask.cs
        public bool AddToWindowsTaskScheduler { get; set; } = true;



        public ScheduledTask()
        {
            // Default to running daily at current time
            RunTime = DateTime.Now.TimeOfDay;
            NextRunTime = DateTime.Today.Add(RunTime);
            if (NextRunTime < DateTime.Now)
            {
                NextRunTime = NextRunTime.AddDays(1);
            }
        }

        public void UpdateNextRunTime()
        {
            DateTime baseTime = DateTime.Today.Add(RunTime);
            
            // If today's scheduled time has already passed, start from tomorrow
            if (baseTime < DateTime.Now)
            {
                baseTime = baseTime.AddDays(1);
            }
            
            switch (Frequency)
            {
                case ScheduleFrequency.Daily:
                    NextRunTime = baseTime;
                    break;
                    
                case ScheduleFrequency.Weekly:
                    // Calculate days until next day of week
                    int daysUntilNextRun = ((int)DateTime.Now.DayOfWeek - (int)DateTime.Now.DayOfWeek + 7) % 7;
                    if (daysUntilNextRun == 0) daysUntilNextRun = 7; // If today, then next week
                    NextRunTime = baseTime.AddDays(daysUntilNextRun);
                    break;
                    
                case ScheduleFrequency.Monthly:
                    // First day of next month at the specified time
                    NextRunTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1).Add(RunTime);
                    break;
                    
                case ScheduleFrequency.Once:
                    // Leave as is - it's a one-time task
                    break;
            }
        }
    }

    public enum ScheduleFrequency
    {
        Once,
        Daily,
        Weekly,
        Monthly
    }
}
