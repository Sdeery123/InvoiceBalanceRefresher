using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace InvoiceBalanceRefresher
{
    public class BatchProcessingHelper
    {
        private readonly InvoiceCloudApiService _apiService;
        private readonly Action<MainWindow.LogLevel, string> _logAction;
        private readonly TextBox _batchResults;
        private readonly ProgressBar _batchProgress;
        private readonly TextBlock _batchStatus;

        public BatchProcessingHelper(
            InvoiceCloudApiService apiService, 
            Action<MainWindow.LogLevel, string> logAction,
            TextBox batchResults,
            ProgressBar batchProgress,
            TextBlock batchStatus)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
            _batchResults = batchResults ?? throw new ArgumentNullException(nameof(batchResults));
            _batchProgress = batchProgress ?? throw new ArgumentNullException(nameof(batchProgress));
            _batchStatus = batchStatus ?? throw new ArgumentNullException(nameof(batchStatus));
        }

        public async Task<bool> ProcessBatchFile(string billerGUID, string webServiceKey, string filePath, bool hasAccountNumbers, string defaultAccountNumber = "")
        {
            _logAction(MainWindow.LogLevel.Info, $"Running batch process for file: {filePath}");
            _logAction(MainWindow.LogLevel.Info, $"Using Biller GUID: {billerGUID}");
            _logAction(MainWindow.LogLevel.Info, $"Using Web Service Key: {webServiceKey}");
            _logAction(MainWindow.LogLevel.Info, $"CSV has account numbers: {hasAccountNumbers}");

            try
            {
                // Clear previous results
                _batchResults.Clear();
                
                // Read all lines from the CSV file
                var lines = File.ReadAllLines(filePath);
                var results = new StringBuilder();

                _logAction(MainWindow.LogLevel.Info, $"Found {lines.Length} records to process");

                // Add header to results display and CSV
                _batchResults.AppendText($"PROCESSING INVOICES\n");
                _batchResults.AppendText($"----------------------------------------\n");

                // Add CSV header
                if (hasAccountNumbers)
                {
                    results.AppendLine("AccountNumber,InvoiceNumber,Status,BalanceDue,DueDate,TotalAmount");
                }
                else
                {
                    results.AppendLine("InvoiceNumber,Status,BalanceDue,DueDate,TotalAmount");
                }

                // Setup progress tracking
                _batchProgress.Maximum = lines.Length;
                _batchProgress.Value = 0;
                
                int successCount = 0;
                int errorCount = 0;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    string accountNumber = string.Empty;
                    string invoiceNumber = string.Empty;

                    // Process CSV line
                    if (hasAccountNumbers)
                    {
                        // Format expected: AccountNumber,InvoiceNumber
                        var parts = line.Split(',');
                        if (parts.Length >= 2)
                        {
                            accountNumber = parts[0].Trim();
                            invoiceNumber = parts[1].Trim();
                        }
                    }
                    else
                    {
                        // Format expected: InvoiceNumber only
                        invoiceNumber = line;
                        // Use provided default account number if available
                        accountNumber = defaultAccountNumber;
                    }

                    // Update progress UI
                    _batchProgress.Value = i + 1;
                    _batchStatus.Text = $"Processing {i + 1} of {lines.Length}...";

                    // Force UI update to show progress
                    await Task.Delay(1); // Minimal delay for UI update

                    if (!string.IsNullOrEmpty(invoiceNumber))
                    {
                        try
                        {
                            _logAction(MainWindow.LogLevel.Debug, $"Processing invoice: {invoiceNumber}" +
                                (string.IsNullOrEmpty(accountNumber) ? "" : $" for account: {accountNumber}"));

                            // First refresh the balance if account number is provided
                            if (!string.IsNullOrEmpty(accountNumber))
                            {
                                _logAction(MainWindow.LogLevel.Debug, $"Refreshing balance for account {accountNumber}...");
                                await _apiService.GetCustomerRecord(billerGUID, webServiceKey, accountNumber);
                                _logAction(MainWindow.LogLevel.Info, $"Balance refreshed for account {accountNumber}");
                            }

                            // Then get invoice data
                            string resultText = await _apiService.GetInvoiceByNumber(billerGUID, webServiceKey, invoiceNumber);

                            // Extract basic data for CSV
                            string csvLine;
                            if (hasAccountNumbers)
                            {
                                csvLine = $"{accountNumber}," + CsvHelper.FormatInvoiceDataForCSV(invoiceNumber, resultText);
                            }
                            else
                            {
                                csvLine = CsvHelper.FormatInvoiceDataForCSV(invoiceNumber, resultText);
                            }
                            results.AppendLine(csvLine);

                            // Update UI with full details
                            _batchResults.AppendText($"INVOICE: {invoiceNumber}" +
                                                  (string.IsNullOrEmpty(accountNumber) ? "" : $" (Account: {accountNumber})") + "\n");
                            _batchResults.AppendText($"{resultText}\n");
                            _batchResults.AppendText($"----------------------------------------\n");
                            _batchResults.ScrollToEnd();

                            _logAction(MainWindow.LogLevel.Info, $"Invoice {invoiceNumber} processed successfully");
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            string errorMsg = $"Error: {ex.Message}";
                            _logAction(MainWindow.LogLevel.Error, $"Error processing invoice {invoiceNumber}: {ex.Message}");

                            // Add to CSV results
                            if (hasAccountNumbers)
                            {
                                results.AppendLine($"{accountNumber},{invoiceNumber},Error,\"{CsvHelper.EscapeCsvField(ex.Message)}\",,");
                            }
                            else
                            {
                                results.AppendLine($"{invoiceNumber},Error,\"{CsvHelper.EscapeCsvField(ex.Message)}\",,");
                            }

                            // Update UI with error
                            _batchResults.AppendText($"INVOICE: {invoiceNumber}" +
                                                  (string.IsNullOrEmpty(accountNumber) ? "" : $" (Account: {accountNumber})") + "\n");
                            _batchResults.AppendText($"{errorMsg}\n");
                            _batchResults.AppendText($"----------------------------------------\n");
                            _batchResults.ScrollToEnd();
                            errorCount++;
                        }
                    }
                    else
                    {
                        _logAction(MainWindow.LogLevel.Warning, $"Empty invoice number in line {i + 1}");

                        // Add to CSV results
                        if (hasAccountNumbers)
                        {
                            results.AppendLine($"{accountNumber},Line {i + 1},Error,\"Empty invoice number\",,");
                        }
                        else
                        {
                            results.AppendLine($"Line {i + 1},Error,\"Empty invoice number\",,");
                        }

                        // Update UI with error
                        _batchResults.AppendText($"Line {i + 1}: Error - Empty invoice number\n");
                        _batchResults.AppendText($"----------------------------------------\n");
                        _batchResults.ScrollToEnd();
                        errorCount++;
                    }
                }

                // Save results to CSV
                string resultFilePath = Path.Combine(Path.GetDirectoryName(filePath) ?? AppDomain.CurrentDomain.BaseDirectory, "InvoiceResults.csv");
                File.WriteAllText(resultFilePath, results.ToString());

                // Add summary to results display
                _batchResults.AppendText($"\n----------------------------------------\n");
                _batchResults.AppendText($"Processing complete! Results saved to:\n{resultFilePath}\n");
                _batchResults.AppendText($"Summary: {successCount} successful, {errorCount} failed, {lines.Length} total\n");
                _batchResults.ScrollToEnd();

                // Update status
                _batchStatus.Text = "Processing complete!";
                _logAction(MainWindow.LogLevel.Info, $"Batch processing completed. Results saved to {resultFilePath}");
                _logAction(MainWindow.LogLevel.Info, $"Batch summary: {successCount} successful, {errorCount} failed, {lines.Length} total");
                
                // Show completion message
                MessageBox.Show($"Batch processing completed.\nResults saved to: {resultFilePath}\n\nSummary: {successCount} successful, {errorCount} failed",
                              "Batch Complete",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
                
                return true;
            }
            catch (Exception ex)
            {
                _logAction(MainWindow.LogLevel.Error, $"Batch processing failed: {ex.Message}");
                _batchStatus.Text = "Error during processing!";

                // Add error to results display
                _batchResults.AppendText($"\n----------------------------------------\n");
                _batchResults.AppendText($"ERROR: {ex.Message}");
                _batchResults.ScrollToEnd();

                MessageBox.Show($"Error during batch processing: {ex.Message}",
                               "Processing Error",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
                
                return false;
            }
        }
    }
}
