using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Microsoft.Win32.TaskScheduler;

namespace InvoiceBalanceRefresher
{
    [Serializable]
    public class ScheduledTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CsvFilePath { get; set; } = string.Empty;
        public string BillerGUID { get; set; } = string.Empty;
        public string WebServiceKey { get; set; } = string.Empty;
        public bool HasAccountNumbers { get; set; }
        public DateTime NextRunTime { get; set; }
        public ScheduleFrequency Frequency { get; set; }
        public TimeSpan RunTime { get; set; }  // Time of day to run
        public bool IsEnabled { get; set; } = true;
        public DateTime LastRunTime { get; set; }
        public bool LastRunSuccessful { get; set; }
        public string LastRunResult { get; set; } = string.Empty;
        public string CustomOption { get; set; } = string.Empty;
        public bool AddToWindowsTaskScheduler { get; set; } = true;

        // Enhanced Windows Task Scheduler options
        [XmlIgnore] // Don't serialize sensitive data
        public string WindowsTaskUsername { get; set; } = string.Empty;

        [XmlIgnore] // Don't serialize sensitive data
        public string WindowsTaskPassword { get; set; } = string.Empty;

        // Day options for Weekly schedule
        public DaysOfTheWeek SelectedDaysOfWeek { get; set; } = DaysOfTheWeek.Monday;

        // Day options for Monthly schedule (1-31)
        public int[] SelectedDaysOfMonth { get; set; } = new int[] { 1 };

        // Repeat interval for Daily schedule (every X days)
        public short DaysInterval { get; set; } = 1;

        // Month options for Monthly schedule
        public MonthsOfTheYear SelectedMonths { get; set; } = MonthsOfTheYear.AllMonths;

        // Task execution options
        public bool RunWithHighestPrivileges { get; set; } = false;
        public bool WakeToRun { get; set; } = false;
        public bool RunOnlyIfNetworkAvailable { get; set; } = false;
        public bool AllowRunOnBattery { get; set; } = true;

        // Retry options
        public short MaxRetryCount { get; set; } = 0;
        public int RetryIntervalMinutes { get; set; } = 5;

        // Runtime limit (in minutes, 0 = no limit)
        public int ExecutionTimeLimitMinutes { get; set; } = 0;

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
                    // Apply days interval
                    if (DaysInterval > 1)
                    {
                        // Calculate days until next run based on interval
                        int daysToAdd = DaysInterval;
                        NextRunTime = baseTime.AddDays(daysToAdd);
                    }
                    else
                    {
                        NextRunTime = baseTime;
                    }
                    break;

                case ScheduleFrequency.Weekly:
                    NextRunTime = CalculateNextWeeklyRunTime(baseTime);
                    break;

                case ScheduleFrequency.Monthly:
                    NextRunTime = CalculateNextMonthlyRunTime();
                    break;

                case ScheduleFrequency.Once:
                    // Leave as is - it's a one-time task
                    break;
            }
        }

        private DateTime CalculateNextWeeklyRunTime(DateTime baseTime)
        {
            // If no days selected, default to Monday
            if (SelectedDaysOfWeek == 0) // Assuming 0 represents no days selected
            {
                SelectedDaysOfWeek = DaysOfTheWeek.Monday;
            }

            // Start looking from tomorrow
            DateTime startDate = DateTime.Today.AddDays(1);

            // Find the next day that matches our selected days
            for (int i = 0; i < 7; i++)
            {
                DateTime checkDate = startDate.AddDays(i);
                DaysOfTheWeek dayOfWeek = MapDayOfWeekToDaysOfTheWeek(checkDate.DayOfWeek);

                if ((SelectedDaysOfWeek & dayOfWeek) != 0)
                {
                    // This day is selected, return this date with the scheduled time
                    return new DateTime(checkDate.Year, checkDate.Month, checkDate.Day)
                        .Add(RunTime);
                }
            }

            // If no day was found (should not happen), default to next week same day
            return baseTime.AddDays(7);
        }

        private DaysOfTheWeek MapDayOfWeekToDaysOfTheWeek(DayOfWeek dayOfWeek)
        {
            switch (dayOfWeek)
            {
                case DayOfWeek.Sunday: return DaysOfTheWeek.Sunday;
                case DayOfWeek.Monday: return DaysOfTheWeek.Monday;
                case DayOfWeek.Tuesday: return DaysOfTheWeek.Tuesday;
                case DayOfWeek.Wednesday: return DaysOfTheWeek.Wednesday;
                case DayOfWeek.Thursday: return DaysOfTheWeek.Thursday;
                case DayOfWeek.Friday: return DaysOfTheWeek.Friday;
                case DayOfWeek.Saturday: return DaysOfTheWeek.Saturday;
                default: return DaysOfTheWeek.Monday;
            }
        }

        private DateTime CalculateNextMonthlyRunTime()
        {
            // Ensure we have days selected
            if (SelectedDaysOfMonth == null || SelectedDaysOfMonth.Length == 0)
            {
                SelectedDaysOfMonth = new int[] { 1 };
            }

            // Sort days for processing
            Array.Sort(SelectedDaysOfMonth);

            // Start with current month
            int currentMonth = DateTime.Now.Month;
            int currentYear = DateTime.Now.Year;

            // Look ahead up to 12 months to find the next scheduled run
            for (int monthOffset = 0; monthOffset < 12; monthOffset++)
            {
                // Calculate target month and year
                int targetMonth = ((currentMonth - 1 + monthOffset) % 12) + 1;
                int targetYear = currentYear + ((currentMonth + monthOffset - 1) / 12);

                // Check if this month is selected
                MonthsOfTheYear monthFlag = GetMonthFlag(targetMonth);

                if ((SelectedMonths & monthFlag) != 0)
                {
                    // This month is selected, check for days
                    int daysInMonth = DateTime.DaysInMonth(targetYear, targetMonth);

                    // Try each selected day
                    foreach (int day in SelectedDaysOfMonth)
                    {
                        if (day <= daysInMonth)
                        {
                            DateTime potentialDate = new DateTime(targetYear, targetMonth, day).Add(RunTime);

                            // If this date is in the future, return it
                            if (potentialDate > DateTime.Now)
                            {
                                return potentialDate;
                            }
                        }
                    }
                }
            }

            // Fallback: first day of next month
            return new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)
                .AddMonths(1)
                .Add(RunTime);
        }

        private MonthsOfTheYear GetMonthFlag(int month)
        {
            switch (month)
            {
                case 1: return MonthsOfTheYear.January;
                case 2: return MonthsOfTheYear.February;
                case 3: return MonthsOfTheYear.March;
                case 4: return MonthsOfTheYear.April;
                case 5: return MonthsOfTheYear.May;
                case 6: return MonthsOfTheYear.June;
                case 7: return MonthsOfTheYear.July;
                case 8: return MonthsOfTheYear.August;
                case 9: return MonthsOfTheYear.September;
                case 10: return MonthsOfTheYear.October;
                case 11: return MonthsOfTheYear.November;
                case 12: return MonthsOfTheYear.December;
                default: return MonthsOfTheYear.AllMonths;
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
