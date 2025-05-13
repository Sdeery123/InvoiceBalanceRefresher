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
        private readonly System.Windows.Controls.TextBox? _batchResults;
        private readonly System.Windows.Controls.ProgressBar? _batchProgress;
        private readonly TextBlock? _batchStatus;
        private readonly bool _headlessMode;

        /// <summary>
        /// Constructor for UI-based batch processing
        /// </summary>
        public BatchProcessingHelper(
            InvoiceCloudApiService apiService,
            Action<MainWindow.LogLevel, string> logAction,
            System.Windows.Controls.TextBox batchResults,
            System.Windows.Controls.ProgressBar batchProgress,
            TextBlock batchStatus)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
            _batchResults = batchResults ?? throw new ArgumentNullException(nameof(batchResults));
            _batchProgress = batchProgress ?? throw new ArgumentNullException(nameof(batchProgress));
            _batchStatus = batchStatus ?? throw new ArgumentNullException(nameof(batchStatus));
            _headlessMode = false;
        }

        /// <summary>
        /// Constructor for headless batch processing (no UI dependencies)
        /// </summary>
        public BatchProcessingHelper(
            InvoiceCloudApiService apiService,
            Action<MainWindow.LogLevel, string> logAction)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
            _headlessMode = true;

            // These UI elements won't be used in headless mode
            _batchResults = null;
            _batchProgress = null;
            _batchStatus = null;
        }

        public async Task<bool> ProcessBatchFile(string billerGUID, string webServiceKey, string filePath, bool hasAccountNumbers, string defaultAccountNumber = "")
        {
            _logAction(MainWindow.LogLevel.Info, $"Running batch process for file: {filePath}");
            _logAction(MainWindow.LogLevel.Info, $"Using Biller GUID: {billerGUID}");
            _logAction(MainWindow.LogLevel.Info, $"Using Web Service Key: {webServiceKey?.Substring(0, Math.Min(5, webServiceKey?.Length ?? 0))}...(truncated)");
            _logAction(MainWindow.LogLevel.Info, $"CSV has account numbers: {hasAccountNumbers}");
            _logAction(MainWindow.LogLevel.Info, $"Running in headless mode: {_headlessMode}");

            try
            {
                // Verify API service is initialized
                if (_apiService == null)
                {
                    _logAction(MainWindow.LogLevel.Error, "API service is not initialized.");
                    return false;
                }

                // Validate inputs more thoroughly
                if (string.IsNullOrWhiteSpace(billerGUID))
                {
                    _logAction(MainWindow.LogLevel.Error, "Biller GUID cannot be empty.");
                    return false;
                }

                // Since some Web Service Keys might not be GUIDs, just ensure it's not empty
                if (string.IsNullOrWhiteSpace(webServiceKey))
                {
                    _logAction(MainWindow.LogLevel.Error, "Web Service Key cannot be empty.");
                    return false;
                }

                if (!File.Exists(filePath))
                {
                    _logAction(MainWindow.LogLevel.Error, $"CSV file not found: {filePath}");
                    return false;
                }

                try
                {
                    // Test file readability before starting
                    string[] testLines = File.ReadAllLines(filePath);
                    if (testLines.Length == 0)
                    {
                        _logAction(MainWindow.LogLevel.Error, "CSV file is empty.");
                        return false;
                    }
                }
                catch (Exception fileEx)
                {
                    _logAction(MainWindow.LogLevel.Error, $"Error reading CSV file: {fileEx.Message}");
                    return false;
                }

                // Clear previous results if in UI mode
                if (!_headlessMode && _batchResults != null)
                {
                    _batchResults.Clear();
                }

                // Read all lines from the CSV file
                var lines = File.ReadAllLines(filePath);
                var results = new StringBuilder();

                _logAction(MainWindow.LogLevel.Info, $"Found {lines.Length} records to process");

                // Add header to results display and CSV
                if (!_headlessMode && _batchResults != null)
                {
                    _batchResults.AppendText($"PROCESSING INVOICES\n");
                    _batchResults.AppendText($"----------------------------------------\n");
                }

                // Add CSV header
                if (hasAccountNumbers)
                {
                    results.AppendLine("AccountNumber,InvoiceNumber,Status,BalanceDue,DueDate,TotalAmount");
                }
                else
                {
                    results.AppendLine("InvoiceNumber,Status,BalanceDue,DueDate,TotalAmount");
                }

                // Setup progress tracking in UI mode
                if (!_headlessMode && _batchProgress != null)
                {
                    _batchProgress.Maximum = lines.Length;
                    _batchProgress.Value = 0;
                }

                int successCount = 0;
                int errorCount = 0;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        _logAction(MainWindow.LogLevel.Warning, $"Skipping empty line {i + 1}");
                        continue;
                    }

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
                        else
                        {
                            _logAction(MainWindow.LogLevel.Warning, $"Invalid format in line {i + 1}. Expected format: AccountNumber,InvoiceNumber");
                        }
                    }
                    else
                    {
                        // Format expected: InvoiceNumber only
                        invoiceNumber = line;
                        // Use provided default account number if available
                        accountNumber = defaultAccountNumber;
                    }

                    // Update progress UI if in UI mode
                    if (!_headlessMode)
                    {
                        if (_batchProgress != null)
                        {
                            _batchProgress.Value = i + 1;
                        }

                        if (_batchStatus != null)
                        {
                            _batchStatus.Text = $"Processing {i + 1} of {lines.Length}...";
                        }

                        // Force UI update to show progress in UI mode
                        await Task.Delay(1); // Minimal delay for UI update
                    }
                    else
                    {
                        // In headless mode, log progress periodically
                        if (i % 10 == 0 || i == lines.Length - 1)
                        {
                            _logAction(MainWindow.LogLevel.Info, $"Progress: {i + 1} of {lines.Length} records processed ({(i + 1) * 100 / lines.Length}%)");
                        }
                    }

                    if (!string.IsNullOrEmpty(invoiceNumber))
                    {
                        try
                        {
                            _logAction(MainWindow.LogLevel.Debug, $"Processing invoice: {invoiceNumber}" +
                                (string.IsNullOrEmpty(accountNumber) ? "" : $" for account: {accountNumber}"));

                            // First refresh the balance if account number is provided
                            if (!string.IsNullOrEmpty(accountNumber))
                            {
                                try
                                {
                                    _logAction(MainWindow.LogLevel.Debug, $"Refreshing balance for account {accountNumber}...");
                                    string customerResult = await _apiService.GetCustomerRecord(billerGUID, webServiceKey, accountNumber);
                                    _logAction(MainWindow.LogLevel.Info, $"Balance refreshed for account {accountNumber}");
                                }
                                catch (Exception accEx)
                                {
                                    _logAction(MainWindow.LogLevel.Warning, $"Error refreshing balance for account {accountNumber}: {accEx.Message}");
                                    // Continue anyway - we'll still try to get invoice data
                                }
                            }

                            // Then get invoice data
                            _logAction(MainWindow.LogLevel.Debug, $"Retrieving data for invoice {invoiceNumber}...");
                            string resultText = await _apiService.GetInvoiceByNumber(billerGUID, webServiceKey, invoiceNumber);

                            // Skip if result is empty or indicates an error
                            if (string.IsNullOrWhiteSpace(resultText) || resultText.Contains("Error:"))
                            {
                                throw new Exception(string.IsNullOrWhiteSpace(resultText) ? "Empty result from API" : resultText);
                            }

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

                            // Update UI with full details if in UI mode
                            if (!_headlessMode && _batchResults != null)
                            {
                                _batchResults.AppendText($"INVOICE: {invoiceNumber}" +
                                                      (string.IsNullOrEmpty(accountNumber) ? "" : $" (Account: {accountNumber})") + "\n");
                                _batchResults.AppendText($"{resultText}\n");
                                _batchResults.AppendText($"----------------------------------------\n");
                                _batchResults.ScrollToEnd();
                            }

                            _logAction(MainWindow.LogLevel.Info, $"Invoice {invoiceNumber} processed successfully");
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            string errorMsg = $"Error: {ex.Message}";
                            _logAction(MainWindow.LogLevel.Error, $"Error processing invoice {invoiceNumber}: {ex.Message}");
                            if (ex.InnerException != null)
                            {
                                _logAction(MainWindow.LogLevel.Debug, $"Inner exception: {ex.InnerException.Message}");
                            }

                            // Add to CSV results
                            if (hasAccountNumbers)
                            {
                                results.AppendLine($"{accountNumber},{invoiceNumber},Error,\"{CsvHelper.EscapeCsvField(ex.Message)}\",,");
                            }
                            else
                            {
                                results.AppendLine($"{invoiceNumber},Error,\"{CsvHelper.EscapeCsvField(ex.Message)}\",,");
                            }

                            // Update UI with error if in UI mode
                            if (!_headlessMode && _batchResults != null)
                            {
                                _batchResults.AppendText($"INVOICE: {invoiceNumber}" +
                                                      (string.IsNullOrEmpty(accountNumber) ? "" : $" (Account: {accountNumber})") + "\n");
                                _batchResults.AppendText($"{errorMsg}\n");
                                _batchResults.AppendText($"----------------------------------------\n");
                                _batchResults.ScrollToEnd();
                            }
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

                        // Update UI with error if in UI mode
                        if (!_headlessMode && _batchResults != null)
                        {
                            _batchResults.AppendText($"Line {i + 1}: Error - Empty invoice number\n");
                            _batchResults.AppendText($"----------------------------------------\n");
                            _batchResults.ScrollToEnd();
                        }
                        errorCount++;
                    }
                }

                // Save results to CSV
                string resultFilePath = Path.Combine(Path.GetDirectoryName(filePath) ?? AppDomain.CurrentDomain.BaseDirectory, "InvoiceResults.csv");
                try
                {
                    File.WriteAllText(resultFilePath, results.ToString());
                    _logAction(MainWindow.LogLevel.Info, $"Results saved to {resultFilePath}");
                }
                catch (Exception saveEx)
                {
                    _logAction(MainWindow.LogLevel.Error, $"Error saving results file: {saveEx.Message}");
                    // We'll still consider the processing successful even if saving fails
                }

                // Add summary to results display if in UI mode
                if (!_headlessMode && _batchResults != null)
                {
                    _batchResults.AppendText($"\n----------------------------------------\n");
                    _batchResults.AppendText($"Processing complete! Results saved to:\n{resultFilePath}\n");
                    _batchResults.AppendText($"Summary: {successCount} successful, {errorCount} failed, {lines.Length} total\n");
                    _batchResults.ScrollToEnd();
                }

                // Update status if in UI mode
                if (!_headlessMode && _batchStatus != null)
                {
                    _batchStatus.Text = "Processing complete!";
                }

                _logAction(MainWindow.LogLevel.Info, $"Batch processing completed. Results saved to {resultFilePath}");
                _logAction(MainWindow.LogLevel.Info, $"Batch summary: {successCount} successful, {errorCount} failed, {lines.Length} total");

                // Show completion message if in UI mode
                if (!_headlessMode)
                {
                    try
                    {
                        System.Windows.MessageBox.Show($"Batch processing completed.\nResults saved to: {resultFilePath}\n\nSummary: {successCount} successful, {errorCount} failed",
                                      "Batch Complete",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Information);
                    }
                    catch (Exception msgEx)
                    {
                        // Log but don't fail if message box can't be shown
                        _logAction(MainWindow.LogLevel.Warning, $"Could not display completion message box: {msgEx.Message}");
                    }
                }

                // Returns true for successful execution (even if some records had errors)
                return true;
            }
            catch (Exception ex)
            {
                _logAction(MainWindow.LogLevel.Error, $"Batch processing failed: {ex.Message}");
                _logAction(MainWindow.LogLevel.Debug, $"Stack trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    _logAction(MainWindow.LogLevel.Debug, $"Inner exception: {ex.InnerException.Message}");
                }

                if (!_headlessMode)
                {
                    if (_batchStatus != null)
                    {
                        _batchStatus.Text = "Error during processing!";
                    }

                    // Add error to results display if in UI mode
                    if (_batchResults != null)
                    {
                        _batchResults.AppendText($"\n----------------------------------------\n");
                        _batchResults.AppendText($"ERROR: {ex.Message}");
                        _batchResults.ScrollToEnd();
                    }

                    try
                    {
                        System.Windows.MessageBox.Show($"Error during batch processing: {ex.Message}",
                                       "Processing Error",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Error);
                    }
                    catch (Exception msgEx)
                    {
                        // Log but don't fail if message box can't be shown
                        _logAction(MainWindow.LogLevel.Warning, $"Could not display error message box: {msgEx.Message}");
                    }
                }

                return false;
            }
        }

    }
}
