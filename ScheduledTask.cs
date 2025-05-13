using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace InvoiceBalanceRefresher
{
    [Serializable]
    public class ScheduledTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [XmlElement("n")]
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CsvFilePath { get; set; } = string.Empty;
        public string BillerGUID { get; set; } = string.Empty;
        public string WebServiceKey { get; set; } = string.Empty;
        public bool HasAccountNumbers { get; set; }
        public DateTime NextRunTime { get; set; }
        public ScheduleFrequency Frequency { get; set; }
        public TimeSpan RunTime { get; set; }  // Time of day to run (for Daily, Weekly, Monthly)
        public bool IsEnabled { get; set; } = true;
        public DateTime LastRunTime { get; set; }
        public bool LastRunSuccessful { get; set; }
        public string LastRunResult { get; set; } = string.Empty;
        public string CustomOption { get; set; } = string.Empty;

        // Enhanced scheduling options

        // Hours interval for Hourly frequency (every X hours)
        public int HoursInterval { get; set; } = 1;

        // Minutes interval for Custom frequency (every X minutes)
        public int MinutesInterval { get; set; } = 30;

        // Day options for Weekly schedule
        public DaysOfTheWeek SelectedDaysOfWeek { get; set; } = DaysOfTheWeek.Monday;

        // Day options for Monthly schedule (1-31)
        public int[] SelectedDaysOfMonth { get; set; } = new int[] { 1 };

        // Repeat interval for Daily schedule (every X days)
        public short DaysInterval { get; set; } = 1;

        // Month options for Monthly schedule
        public MonthsOfTheYear SelectedMonths { get; set; } = MonthsOfTheYear.AllMonths;

        // Retry options
        public short MaxRetryCount { get; set; } = 0;
        public int RetryIntervalMinutes { get; set; } = 5;

        // Advanced scheduling options

        // For quarterly schedule: which months in each quarter (1=first month, 2=second, 3=third)
        public int[] QuarterlyMonths { get; set; } = new int[] { 1 };  // Default to first month of quarter

        // For biweekly schedule: week of schedule (0=even weeks, 1=odd weeks)
        public int BiweeklyWeek { get; set; } = 0;  // Default to even weeks

        // For workday schedule: include/exclude weekends
        public bool WorkdaysOnly { get; set; } = true;

        // Time ranges for specific scheduling (multiple times in a day)
        [XmlArray("TimeRanges")]
        public List<TimeRange> TimeRanges { get; set; } = new List<TimeRange>();

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
            // For some frequencies, we use current time as the base
            DateTime now = DateTime.Now;

            // For others, we use today's date with the configured time
            DateTime baseTime = DateTime.Today.Add(RunTime);

            // If today's scheduled time has already passed, calculate from now for frequent schedules,
            // or from tomorrow for daily+ schedules
            if (baseTime < now)
            {
                // For hourly and custom frequencies, we calculate from now
                if (Frequency == ScheduleFrequency.Hourly || Frequency == ScheduleFrequency.Custom)
                {
                    // We're using now as the base
                }
                else
                {
                    // For daily and less frequent, start from tomorrow
                    baseTime = baseTime.AddDays(1);
                }
            }

            switch (Frequency)
            {
                case ScheduleFrequency.Custom:
                    // Run every X minutes
                    if (MinutesInterval <= 0) MinutesInterval = 1; // Safeguard
                    NextRunTime = now.AddMinutes(MinutesInterval);
                    break;

                case ScheduleFrequency.Hourly:
                    // Run every X hours
                    if (HoursInterval <= 0) HoursInterval = 1; // Safeguard
                    NextRunTime = now.AddHours(HoursInterval);
                    break;

                case ScheduleFrequency.Daily:
                    // Apply days interval
                    if (DaysInterval > 1)
                    {
                        // Calculate days until next run based on interval
                        int daysToAdd = DaysInterval;
                        NextRunTime = baseTime.AddDays(daysToAdd);
                    }
                    else if (WorkdaysOnly)
                    {
                        // Skip weekends for workday schedule
                        NextRunTime = baseTime;
                        while (NextRunTime.DayOfWeek == DayOfWeek.Saturday || NextRunTime.DayOfWeek == DayOfWeek.Sunday)
                        {
                            NextRunTime = NextRunTime.AddDays(1);
                        }
                    }
                    else
                    {
                        NextRunTime = baseTime;
                    }
                    break;

                case ScheduleFrequency.BiWeekly:
                    // Run every other week on specified days
                    NextRunTime = CalculateBiWeeklyRunTime(baseTime);
                    break;

                case ScheduleFrequency.Weekly:
                    NextRunTime = CalculateNextWeeklyRunTime(baseTime);
                    break;

                case ScheduleFrequency.Quarterly:
                    NextRunTime = CalculateNextQuarterlyRunTime();
                    break;

                case ScheduleFrequency.Monthly:
                    NextRunTime = CalculateNextMonthlyRunTime();
                    break;

                case ScheduleFrequency.Once:
                    // Leave as is - it's a one-time task
                    break;

                case ScheduleFrequency.MultipleTimesDaily:
                    // Find the next time range after now
                    NextRunTime = CalculateNextTimeRangeRunTime();
                    break;
            }
        }

        private DateTime CalculateNextTimeRangeRunTime()
        {
            if (TimeRanges == null || TimeRanges.Count == 0)
            {
                // Default to once daily if no time ranges specified
                return DateTime.Today.Add(RunTime);
            }

            DateTime now = DateTime.Now;
            DateTime tomorrow = DateTime.Today.AddDays(1);

            // Sort time ranges by hour and minute
            TimeRanges.Sort((a, b) => a.CompareTo(b));

            // Check today's remaining times first
            foreach (var timeRange in TimeRanges)
            {
                DateTime rangeTime = DateTime.Today.Add(new TimeSpan(timeRange.Hour, timeRange.Minute, 0));
                if (rangeTime > now)
                {
                    return rangeTime;
                }
            }

            // If no times left today, use first time tomorrow
            return tomorrow.Add(new TimeSpan(TimeRanges[0].Hour, TimeRanges[0].Minute, 0));
        }

        private DateTime CalculateBiWeeklyRunTime(DateTime baseTime)
        {
            // First, calculate like a weekly schedule
            DateTime weeklyTime = CalculateNextWeeklyRunTime(baseTime);

            // Now check if this week is the right one (even/odd)
            int weekNumber = GetIso8601WeekOfYear(weeklyTime);
            bool isEvenWeek = weekNumber % 2 == 0;

            // If we're on wrong week parity, add 7 days
            if ((BiweeklyWeek == 0 && !isEvenWeek) || (BiweeklyWeek == 1 && isEvenWeek))
            {
                return weeklyTime.AddDays(7);
            }

            return weeklyTime;
        }

        private DateTime CalculateNextQuarterlyRunTime()
        {
            // If no months selected, default to first month
            if (QuarterlyMonths == null || QuarterlyMonths.Length == 0)
            {
                QuarterlyMonths = new int[] { 1 };
            }

            // Sort for easier processing
            Array.Sort(QuarterlyMonths);

            DateTime now = DateTime.Now;
            int currentMonth = now.Month;
            int currentYear = now.Year;

            // Determine current quarter (1-4)
            int currentQuarter = (currentMonth - 1) / 3 + 1;

            // Look ahead up to 4 quarters
            for (int quarterOffset = 0; quarterOffset < 4; quarterOffset++)
            {
                // Calculate target quarter & year
                int targetQuarter = ((currentQuarter - 1 + quarterOffset) % 4) + 1;
                int targetYear = currentYear + ((currentQuarter + quarterOffset - 1) / 4);

                // For each month in the target quarter
                foreach (int monthInQuarter in QuarterlyMonths)
                {
                    if (monthInQuarter < 1 || monthInQuarter > 3) continue; // Skip invalid values

                    // Calculate actual month number (1-12)
                    int actualMonth = (targetQuarter - 1) * 3 + monthInQuarter;

                    // Get selected days for this month
                    if (SelectedDaysOfMonth == null || SelectedDaysOfMonth.Length == 0)
                    {
                        SelectedDaysOfMonth = new int[] { 1 };
                    }

                    // Check each selected day
                    foreach (int day in SelectedDaysOfMonth)
                    {
                        int daysInMonth = DateTime.DaysInMonth(targetYear, actualMonth);
                        if (day <= daysInMonth)
                        {
                            DateTime potentialDate = new DateTime(targetYear, actualMonth, day).Add(RunTime);

                            // If this date is in the future, return it
                            if (potentialDate > now)
                            {
                                return potentialDate;
                            }
                        }
                    }
                }
            }

            // Fallback: first selected day of next quarter
            int nextQuarter = currentQuarter % 4 + 1;
            int nextQuarterYear = currentQuarter == 4 ? currentYear + 1 : currentYear;
            int nextQuarterMonth = (nextQuarter - 1) * 3 + QuarterlyMonths[0];

            return new DateTime(nextQuarterYear, nextQuarterMonth, SelectedDaysOfMonth[0]).Add(RunTime);
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

        // Helper method for calculating ISO week number (for biweekly scheduling)
        private int GetIso8601WeekOfYear(DateTime time)
        {
            // Return the ISO 8601 week of year
            var day = (int)System.Globalization.CultureInfo.CurrentCulture.Calendar.GetDayOfWeek(time);
            if (day == 0) day = 7; // Sunday should be 7, not 0

            // Add days to get to Thursday (ISO weeks are defined by Thursday)
            time = time.AddDays(4 - day);

            // Return the week of year
            return System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                time,
                System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
        }
    }

    [Serializable]
    public class TimeRange : IComparable<TimeRange>
    {
        public int Hour { get; set; }
        public int Minute { get; set; }

        public TimeRange() { }

        public TimeRange(int hour, int minute)
        {
            Hour = hour;
            Minute = minute;
        }

        public int CompareTo(TimeRange? other)
        {
            if (other == null) return 1;

            if (Hour != other.Hour)
                return Hour.CompareTo(other.Hour);

            return Minute.CompareTo(other.Minute);
        }

        public override string ToString()
        {
            return $"{Hour:D2}:{Minute:D2}";
        }
    }

    public enum ScheduleFrequency
    {
        Once,           // Run one time only
        Custom,         // Run every X minutes
        Hourly,         // Run every X hours
        Daily,          // Run every X days
        WorkDays,       // Run on weekdays only (Mon-Fri)
        Weekly,         // Run on specified days of the week
        BiWeekly,       // Run every other week
        Monthly,        // Run on specified days of specified months
        Quarterly,      // Run every quarter on specified months
        MultipleTimesDaily // Run multiple times per day at specified times
    }
}
