using System;
using System.IO;
using System.Text;
using System.Windows;

namespace InvoiceBalanceRefresher
{
    public static class CsvHelper
    {
        /// <summary>
        /// Properly escapes fields for CSV formatting
        /// </summary>
        public static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            // If the field contains quotes, commas, or newlines, escape quotes by doubling them
            // and surround the whole field with quotes
            if (field.Contains("\"") || field.Contains(",") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }

        /// <summary>
        /// Extracts invoice data from result text and formats it for CSV
        /// </summary>
        public static string FormatInvoiceDataForCSV(string invoiceNumber, string resultText)
        {
            // Extract just the key fields for CSV
            string status = "Unknown";
            string balanceDue = "";
            string dueDate = "";
            string totalAmount = "";

            string[] lines = resultText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                if (line.StartsWith("Status:"))
                {
                    status = line.Substring("Status:".Length).Trim();
                }
                else if (line.StartsWith("Balance Due:"))
                {
                    balanceDue = line.Substring("Balance Due:".Length).Trim();
                }
                else if (line.StartsWith("Due Date:"))
                {
                    dueDate = line.Substring("Due Date:".Length).Trim();
                }
                else if (line.StartsWith("Total Amount Due:"))
                {
                    totalAmount = line.Substring("Total Amount Due:".Length).Trim();
                }
            }

            // Escape any commas in fields
            return $"{invoiceNumber},{status},\"{balanceDue}\",\"{dueDate}\",\"{totalAmount}\"";
        }

        /// <summary>
        /// Generates a sample CSV file with invoice numbers
        /// </summary>
        public static bool GenerateSampleCSV(Action<LogLevel, string> logAction)
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = "SampleInvoices.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var sampleContent = new StringBuilder();
                    sampleContent.AppendLine("INV0001");
                    sampleContent.AppendLine("INV0002");
                    sampleContent.AppendLine("INV0003");

                    File.WriteAllText(saveFileDialog.FileName, sampleContent.ToString());

                    logAction(LogLevel.Info, $"Sample CSV file generated: {saveFileDialog.FileName}");

                    System.Windows.MessageBox.Show(
                        $"Sample CSV file created at:\n{saveFileDialog.FileName}\n\nFormat:\nOne invoice number per line\n\nThe Biller GUID and Web Service Key from the Single Invoice section will be used for processing.",
                        "Sample File Created",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                logAction(LogLevel.Error, $"Failed to create sample file: {ex.Message}");
                System.Windows.MessageBox.Show($"Error creating sample file: {ex.Message}");
                return false;
            }
        }
    }
}
