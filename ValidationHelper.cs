using System;

namespace InvoiceBalanceRefresher
{
    public static class ValidationHelper
    {
        /// <summary>
        /// Validates if a string is a valid GUID
        /// </summary>
        public static bool ValidateGUID(string guid, Action<MainWindow.LogLevel, string>? logAction = null)
        {
            // If input is empty, return false
            if (string.IsNullOrWhiteSpace(guid))
                return false;

            // Check if it's a valid GUID
            bool isValid = Guid.TryParse(guid, out _);

            // If validation fails and logging is provided, log details
            if (!isValid && logAction != null)
            {
                logAction(MainWindow.LogLevel.Debug, $"GUID validation failed for: '{guid}'");
            }

            return isValid;
        }

    }
}
