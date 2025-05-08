using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Media;
using System.Windows.Shapes;
using IOPath = System.IO.Path;
using System.Windows.Documents;

namespace InvoiceBalanceRefresher
{
    public partial class MainWindow : Window
    {
        private readonly List<LogEntry> _logEntries = new List<LogEntry>();
        private string _sessionLogPath;
        private DispatcherTimer _autoScrollTimer;
        private FlowDocument _originalDocument = new FlowDocument();
        private int _searchResultCount = 0;
        private string _originalBatchResults = string.Empty;
        private int _batchSearchResultCount = 0;
        private const string APP_VERSION = "1.0.0";

        public MainWindow()
        {
            InitializeComponent();
            InitializeLogging();

            ConsoleLog.Document = new FlowDocument();
            _originalDocument = new FlowDocument();
        }

        private void InitializeLogging()
        {
            // Create logs directory if it doesn't exist
            string logsDirectory = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(logsDirectory);

            // Create a new session log file
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _sessionLogPath = IOPath.Combine(logsDirectory, $"Session_{timestamp}.log");

            Log(LogLevel.Info, "=== Session Started ===");
            Log(LogLevel.Info, $"Log file: {_sessionLogPath}");

            // Setup auto-scroll timer
            _autoScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _autoScrollTimer.Tick += (s, e) => ConsoleLog.ScrollToEnd();
            _autoScrollTimer.Start();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.Trim().ToLower();
            PerformSearch(searchText);
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Clear();
            PerformSearch(string.Empty);
        }

        private void PerformSearch(string searchText)
        {
            // Reset search results count
            _searchResultCount = 0;

            if (string.IsNullOrEmpty(searchText))
            {
                // If search text is empty, restore original content
                ConsoleLog.Document = new FlowDocument();
                foreach (var block in _originalDocument.Blocks.ToList())
                {
                    // Create a copy instead of using Clone
                    if (block is Paragraph paragraph)
                    {
                        var newParagraph = new Paragraph();
                        foreach (var inline in paragraph.Inlines)
                        {
                            if (inline is Run run)
                            {
                                newParagraph.Inlines.Add(new Run(run.Text));
                            }
                        }
                        ConsoleLog.Document.Blocks.Add(newParagraph);
                    }
                }
                SearchResultsCount.Text = string.Empty;
                return;
            }

            // Create new document for search results
            var searchResultDocument = new FlowDocument();

            // Go through each paragraph in the original document
            foreach (Paragraph originalParagraph in _originalDocument.Blocks)
            {
                string paragraphText = new TextRange(originalParagraph.ContentStart, originalParagraph.ContentEnd).Text.ToLower();

                // Check if this paragraph contains the search text
                if (paragraphText.Contains(searchText))
                {
                    // Found a match, create a new paragraph
                    var newParagraph = new Paragraph();

                    // Copy the run content
                    foreach (var inline in originalParagraph.Inlines)
                    {
                        if (inline is Run run)
                        {
                            string runText = run.Text;

                            // Find all occurrences of search text in this run
                            int pos = 0;
                            while ((pos = runText.ToLower().IndexOf(searchText, pos, StringComparison.OrdinalIgnoreCase)) != -1)
                            {
                                // Add text before match
                                if (pos > 0)
                                    newParagraph.Inlines.Add(new Run(runText.Substring(0, pos)));

                                // Add the match with highlight
                                var highlightRun = new Run(runText.Substring(pos, searchText.Length))
                                {
                                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3D7E34")), // Highlight color
                                    Foreground = Brushes.White
                                };
                                newParagraph.Inlines.Add(highlightRun);

                                // Update for next iteration
                                runText = runText.Substring(pos + searchText.Length);
                                pos = 0;
                                _searchResultCount++;
                            }

                            // Add any remaining text
                            if (!string.IsNullOrEmpty(runText))
                                newParagraph.Inlines.Add(new Run(runText));
                        }
                        else
                        {
                            // For non-Run inlines, create a copy instead of using Clone
                            if (inline is LineBreak)
                                newParagraph.Inlines.Add(new LineBreak());
                            else if (inline is Span span)
                            {
                                var newSpan = new Span();
                                foreach (var childInline in span.Inlines)
                                {
                                    if (childInline is Run childRun)
                                        newSpan.Inlines.Add(new Run(childRun.Text));
                                }
                                newParagraph.Inlines.Add(newSpan);
                            }
                        }
                    }

                    // Add this paragraph to search results
                    searchResultDocument.Blocks.Add(newParagraph);
                }
            }

            // Update the console with search results
            ConsoleLog.Document = searchResultDocument;

            // Update search results count
            SearchResultsCount.Text = $"Found: {_searchResultCount}";
        }

        private void BatchSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = BatchSearchBox.Text.Trim().ToLower();
            PerformBatchSearch(searchText);
        }

        private void ClearBatchSearch_Click(object sender, RoutedEventArgs e)
        {
            BatchSearchBox.Clear();
            PerformBatchSearch(string.Empty);
        }

        private void PerformBatchSearch(string searchText)
        {
            // If we don't have the original text stored yet, store it
            if (string.IsNullOrEmpty(_originalBatchResults) && !string.IsNullOrEmpty(BatchResults.Text))
            {
                _originalBatchResults = BatchResults.Text;
            }

            // Reset search counter
            _batchSearchResultCount = 0;

            // If search is empty, restore original content
            if (string.IsNullOrEmpty(searchText))
            {
                BatchResults.Text = _originalBatchResults;
                BatchSearchResultsCount.Text = string.Empty;
                return;
            }

            // If there's no content to search
            if (string.IsNullOrEmpty(_originalBatchResults))
            {
                BatchSearchResultsCount.Text = "No results";
                return;
            }

            // Filter the lines based on search text
            StringBuilder filteredContent = new StringBuilder();
            string[] lines = _originalBatchResults.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

            bool foundInCurrentInvoice = false;
            StringBuilder currentInvoiceBlock = new StringBuilder();
            string currentInvoice = string.Empty;

            foreach (string line in lines)
            {
                // Check if this is an invoice header line
                if (line.StartsWith("INVOICE:"))
                {
                    // If we had a previous invoice and it matched, add it to results
                    if (foundInCurrentInvoice && currentInvoiceBlock.Length > 0)
                    {
                        filteredContent.Append(currentInvoiceBlock);
                        _batchSearchResultCount++;
                    }

                    // Start a new invoice block
                    currentInvoice = line;
                    currentInvoiceBlock.Clear();
                    currentInvoiceBlock.AppendLine(line);
                    foundInCurrentInvoice = line.ToLower().Contains(searchText);
                }
                else if (line.StartsWith("------"))
                {
                    // This is a separator line, add it to the current invoice block
                    currentInvoiceBlock.AppendLine(line);
                }
                else
                {
                    // This is a content line, add it to current invoice block
                    currentInvoiceBlock.AppendLine(line);

                    // If we haven't already matched this invoice, check if this line matches
                    if (!foundInCurrentInvoice && line.ToLower().Contains(searchText))
                    {
                        foundInCurrentInvoice = true;
                    }
                }
            }

            // Handle the last invoice block if it matched
            if (foundInCurrentInvoice && currentInvoiceBlock.Length > 0)
            {
                filteredContent.Append(currentInvoiceBlock);
                _batchSearchResultCount++;
            }

            // Update the display
            BatchResults.Text = filteredContent.ToString();
            BatchSearchResultsCount.Text = $"Found: {_batchSearchResultCount}";
        }


        private void Log(LogLevel level, string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logMessage = $"[{timestamp}] [{level}] {message}";

            // Add to in-memory log collection
            _logEntries.Add(new LogEntry(timestamp, level, message));

            // Update UI
            Dispatcher.Invoke(() =>
            {
                // Create a new paragraph for this log entry
                Paragraph paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run(logMessage));

                // Add to original document - create a deep copy
                Paragraph originalCopy = new Paragraph();
                originalCopy.Inlines.Add(new Run(logMessage));
                _originalDocument.Blocks.Add(originalCopy);

                // Add to console
                ConsoleLog.Document.Blocks.Add(paragraph);

                // Auto-scroll to end
                ConsoleLog.ScrollToEnd();

                // If search is active, update search results
                if (!string.IsNullOrEmpty(SearchBox.Text))
                    PerformSearch(SearchBox.Text.Trim().ToLower());
            });

            // Write to session log file
            try
            {
                File.AppendAllText(_sessionLogPath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Handle file writing errors
                Dispatcher.Invoke(() =>
                {
                    Paragraph paragraph = new Paragraph();
                    paragraph.Inlines.Add(new Run($"[ERROR] Failed to write to log file: {ex.Message}"));

                    // Add to original document - create a deep copy
                    Paragraph originalCopy = new Paragraph();
                    originalCopy.Inlines.Add(new Run($"[ERROR] Failed to write to log file: {ex.Message}"));
                    _originalDocument.Blocks.Add(originalCopy);

                    // Add to console
                    ConsoleLog.Document.Blocks.Add(paragraph);
                });
            }
        }

        private async void ProcessSingleInvoice_Click(object sender, RoutedEventArgs e)
        {
            string billerGUID = BillerGUID.Text;
            string webServiceKey = WebServiceKey.Text;
            string accountNumber = AccountNumber.Text;
            string invoiceNumber = InvoiceNumber.Text;

            Log(LogLevel.Info, $"Processing single invoice: {invoiceNumber} for account: {accountNumber}");

            if (ValidateGUID(billerGUID) && ValidateGUID(webServiceKey) &&
                !string.IsNullOrEmpty(invoiceNumber) && !string.IsNullOrEmpty(accountNumber))
            {
                try
                {
                    SingleResult.Text = "Processing...";

                    // First, call customer record service to refresh the balance
                    Log(LogLevel.Debug, $"Calling customer record service to refresh balance for account {accountNumber}...");
                    await CallCustomerRecordService(billerGUID, webServiceKey, accountNumber);
                    Log(LogLevel.Info, $"Balance refreshed for account {accountNumber}");

                    // Now call invoice service
                    Log(LogLevel.Debug, $"Calling invoice service for invoice {invoiceNumber}...");
                    string result = await CallWebService(billerGUID, webServiceKey, invoiceNumber);
                    SingleResult.Text = result;
                    Log(LogLevel.Info, $"Invoice {invoiceNumber} processed successfully");
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Error processing invoice: {ex.Message}";
                    SingleResult.Text = errorMessage;
                    Log(LogLevel.Error, errorMessage);
                }
            }
            else
            {
                string validationError = "Invalid or missing fields";
                MessageBox.Show("Please enter valid GUIDs, Account Number, and Invoice Number.");
                Log(LogLevel.Warning, validationError);
            }
        }


        private void BrowseCSV_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                CSVFilePath.Text = openFileDialog.FileName;
                Log(LogLevel.Info, $"Selected CSV file: {openFileDialog.FileName}");
            }
        }

        private async void ProcessCSV_Click(object sender, RoutedEventArgs e)
{
            string filePath = CSVFilePath.Text;
            string billerGUID = BillerGUID.Text;
            string webServiceKey = WebServiceKey.Text;
            bool hasAccountNumbers = AccountInvoiceFormat.IsChecked ?? false;

            // Validate BillerGUID and WebServiceKey from the single invoice fields
            if (!ValidateGUID(billerGUID) || !ValidateGUID(webServiceKey))
    {
        MessageBox.Show("Please enter valid Biller GUID and Web Service Key in the Single Invoice section before processing batch.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
        Log(LogLevel.Warning, "Batch processing cancelled: Invalid Biller GUID or Web Service Key");
        return;
    }

    if (File.Exists(filePath))
    {
        // Clear previous results and search state
        BatchResults.Clear();
        _originalBatchResults = string.Empty;
        BatchSearchBox.Clear();
        BatchSearchResultsCount.Text = string.Empty;

        Log(LogLevel.Info, $"Starting batch processing of file: {filePath}");
        Log(LogLevel.Info, $"Using Biller GUID: {billerGUID}");
        Log(LogLevel.Info, $"Using Web Service Key: {webServiceKey}");
        Log(LogLevel.Info, $"CSV has account numbers: {hasAccountNumbers}");

        try
        {
            // Read all lines from the CSV file
            var lines = File.ReadAllLines(filePath);
            var results = new StringBuilder();
            BatchProgress.Maximum = lines.Length;
            BatchProgress.Value = 0;

            Log(LogLevel.Info, $"Found {lines.Length} records to process");

            // Add header to results display and CSV
            BatchResults.AppendText($"PROCESSING INVOICES\n");
            BatchResults.AppendText($"----------------------------------------\n");
            
            if (hasAccountNumbers)
            {
                results.AppendLine("AccountNumber,InvoiceNumber,Status,BalanceDue,DueDate,TotalAmount");
            }
            else
            {
                results.AppendLine("InvoiceNumber,Status,BalanceDue,DueDate,TotalAmount");
            }

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
                    // Use the account number from the single invoice section if available
                    accountNumber = AccountNumber.Text;
                }

                BatchProgress.Value = i + 1;
                BatchStatus.Text = $"Processing {i + 1} of {lines.Length}...";

                if (!string.IsNullOrEmpty(invoiceNumber))
                {
                    try
                    {
                        Log(LogLevel.Debug, $"Processing invoice: {invoiceNumber}" + 
                            (string.IsNullOrEmpty(accountNumber) ? "" : $" for account: {accountNumber}"));
                        
                        // First refresh the balance if account number is provided
                        if (!string.IsNullOrEmpty(accountNumber))
                        {
                            Log(LogLevel.Debug, $"Refreshing balance for account {accountNumber}...");
                            await CallCustomerRecordService(billerGUID, webServiceKey, accountNumber);
                            Log(LogLevel.Info, $"Balance refreshed for account {accountNumber}");
                        }
                        
                        // Then get invoice data
                        string resultText = await CallWebService(billerGUID, webServiceKey, invoiceNumber);

                        // Extract basic data for CSV
                        string csvLine;
                        if (hasAccountNumbers)
                        {
                            csvLine = $"{accountNumber}," + FormatInvoiceDataForCSV(invoiceNumber, resultText);
                        }
                        else
                        {
                            csvLine = FormatInvoiceDataForCSV(invoiceNumber, resultText);
                        }
                        results.AppendLine(csvLine);

                        // Update UI with full details
                        BatchResults.AppendText($"INVOICE: {invoiceNumber}" + 
                                              (string.IsNullOrEmpty(accountNumber) ? "" : $" (Account: {accountNumber})") + "\n");
                        BatchResults.AppendText($"{resultText}\n");
                        BatchResults.AppendText($"----------------------------------------\n");
                        BatchResults.ScrollToEnd();

                        Log(LogLevel.Info, $"Invoice {invoiceNumber} processed successfully");
                    }
                    catch (Exception ex)
                    {
                        string errorMsg = $"Error: {ex.Message}";
                        Log(LogLevel.Error, $"Error processing invoice {invoiceNumber}: {ex.Message}");

                        // Add to CSV results
                        if (hasAccountNumbers)
                        {
                            results.AppendLine($"{accountNumber},{invoiceNumber},Error,\"{ex.Message}\",,");
                        }
                        else
                        {
                            results.AppendLine($"{invoiceNumber},Error,\"{ex.Message}\",,");
                        }

                        // Update UI with error
                        BatchResults.AppendText($"INVOICE: {invoiceNumber}" + 
                                              (string.IsNullOrEmpty(accountNumber) ? "" : $" (Account: {accountNumber})") + "\n");
                        BatchResults.AppendText($"{errorMsg}\n");
                        BatchResults.AppendText($"----------------------------------------\n");
                        BatchResults.ScrollToEnd();
                    }
                }
                else
                {
                    Log(LogLevel.Warning, $"Empty invoice number in line {i + 1}");

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
                    BatchResults.AppendText($"Line {i + 1}: Error - Empty invoice number\n");
                    BatchResults.AppendText($"----------------------------------------\n");
                    BatchResults.ScrollToEnd();
                }
            }

            string resultFilePath = IOPath.Combine(IOPath.GetDirectoryName(filePath), "InvoiceResults.csv");
            File.WriteAllText(resultFilePath, results.ToString());

            // Add summary to results display
            BatchResults.AppendText($"\n----------------------------------------\n");
            BatchResults.AppendText($"Processing complete! Results saved to:\n{resultFilePath}");
            BatchResults.ScrollToEnd();

            BatchStatus.Text = "Processing complete!";
            Log(LogLevel.Info, $"Batch processing completed. Results saved to {resultFilePath}");
            MessageBox.Show($"Batch processing completed. Results saved to {resultFilePath}");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Batch processing failed: {ex.Message}");
            BatchStatus.Text = "Error during processing!";

            // Add error to results display
            BatchResults.AppendText($"\n----------------------------------------\n");
            BatchResults.AppendText($"ERROR: {ex.Message}");
            BatchResults.ScrollToEnd();

            MessageBox.Show($"Error during batch processing: {ex.Message}");
        }
    }
    else
    {
        Log(LogLevel.Warning, "Invalid CSV file selected");
        MessageBox.Show("Please select a valid CSV file.");
    }
}


        private string FormatInvoiceDataForCSV(string invoiceNumber, string resultText)
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

        private async void ProcessCustomerRecord_Click(object sender, RoutedEventArgs e)
        {
            string billerGUID = BillerGUID.Text;
            string webServiceKey = WebServiceKey.Text;
            string accountNumber = CustomerAccountNumber.Text;

            Log(LogLevel.Info, $"Looking up customer record for account: {accountNumber}");

            if (ValidateGUID(billerGUID) && ValidateGUID(webServiceKey) && !string.IsNullOrEmpty(accountNumber))
            {
                try
                {
                    Log(LogLevel.Debug, "Calling customer record web service...");
                    CustomerResult.Text = "Processing...";

                    // Add debug logging to see what's happening
                    Log(LogLevel.Debug, $"Using Biller GUID: {billerGUID}");
                    Log(LogLevel.Debug, $"Using Web Service Key: {webServiceKey}");

                    string result = await CallCustomerRecordService(billerGUID, webServiceKey, accountNumber);

                    // Add debug logging to see what result we got back
                    Log(LogLevel.Debug, $"Raw customer record result length: {result?.Length ?? 0}");
                    Log(LogLevel.Debug, $"First 100 chars of result: {result?.Substring(0, Math.Min(100, result?.Length ?? 0))}");

                    CustomerResult.Text = result;

                    // Force UI update
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

                    Log(LogLevel.Info, $"Customer record for account {accountNumber} processed successfully");
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Error processing customer record: {ex.Message}";
                    CustomerResult.Text = errorMessage;
                    Log(LogLevel.Error, errorMessage);
                }
            }
            else
            {
                string validationError = "Invalid GUIDs or Account Number";
                MessageBox.Show("Please enter valid GUIDs and Account Number.");
                Log(LogLevel.Warning, validationError);
            }
        }




        private async Task<string> CallWebService(string billerGUID, string webServiceKey, string invoiceNumber)
        {
            string soapRequest = $@"
        <soap12:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap12='http://www.w3.org/2003/05/soap-envelope'>
            <soap12:Body>
                <ViewInvoiceByInvoiceNumber xmlns='https://www.invoicecloud.com/portal/webservices/CloudInvoicing/'>
                    <Req>
                        <BillerGUID>{billerGUID}</BillerGUID>
                        <WebServiceKey>{webServiceKey}</WebServiceKey>
                        <InvoiceNumber>{invoiceNumber}</InvoiceNumber>
                    </Req>
                </ViewInvoiceByInvoiceNumber>
            </soap12:Body>
        </soap12:Envelope>";

            // Configure the HttpClient with appropriate timeout
            using (HttpClient client = new HttpClient())
            {
                // Set reasonable timeout and headers
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.ExpectContinue = false; // Can help with some SOAP services

                // Use retry pattern to handle intermittent failures
                int maxRetries = 3;
                int currentRetry = 0;
                int retryDelayMs = 1000; // Start with 1 second delay

                while (true)
                {
                    try
                    {
                        Log(LogLevel.Debug, $"Sending request for invoice {invoiceNumber} (Attempt {currentRetry + 1} of {maxRetries})");

                        var content = new StringContent(soapRequest, Encoding.UTF8, "application/soap+xml");

                        // Add important SOAP action header if needed
                        // client.DefaultRequestHeaders.Add("SOAPAction", "ViewInvoiceByInvoiceNumber");

                        var response = await client.PostAsync("https://www.invoicecloud.com/portal/webservices/CloudInvoicing.asmx", content);

                        // Check HTTP status code
                        if (!response.IsSuccessStatusCode)
                        {
                            Log(LogLevel.Warning, $"HTTP error: {(int)response.StatusCode} {response.StatusCode} for invoice {invoiceNumber}");

                            // For specific error codes that might be temporary, we'll retry
                            if ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                            {
                                throw new HttpRequestException($"Server error: {response.StatusCode}");
                            }
                        }

                        var responseText = await response.Content.ReadAsStringAsync();

                        // Verify the response actually contains XML data
                        if (string.IsNullOrWhiteSpace(responseText) || !responseText.Contains("<"))
                        {
                            Log(LogLevel.Warning, $"Empty or non-XML response received for invoice {invoiceNumber}");
                            throw new FormatException("Invalid response format received");
                        }

                        Log(LogLevel.Debug, $"Received response for invoice {invoiceNumber}");
                        return ParseResponse(responseText);
                    }
                    catch (Exception ex) when (ex is HttpRequestException ||
                                               ex is TaskCanceledException ||
                                               ex is TimeoutException ||
                                               ex is FormatException)
                    {
                        currentRetry++;
                        Log(LogLevel.Warning, $"API call attempt {currentRetry} failed: {ex.Message}");

                        if (currentRetry >= maxRetries)
                        {
                            Log(LogLevel.Error, $"All retry attempts failed for invoice {invoiceNumber}. Last error: {ex.Message}");
                            throw new Exception($"Failed to retrieve invoice data after {maxRetries} attempts. Last error: {ex.Message}", ex);
                        }

                        // Exponential backoff for retries
                        await Task.Delay(retryDelayMs);
                        retryDelayMs *= 2; // Double the delay for each retry
                    }
                }
            }
        }

        private async Task<string> CallCustomerRecordService(string billerGUID, string webServiceKey, string accountNumber)
        {
            string soapRequest = $@"
<soap12:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap12='http://www.w3.org/2003/05/soap-envelope'>
  <soap12:Body>
    <ViewCustomerRecord xmlns='https://www.invoicecloud.com/portal/webservices/CloudManagement/'>
      <BillerGUID>{billerGUID}</BillerGUID>
      <WebServiceKey>{webServiceKey}</WebServiceKey>
      <AccountNumber>{accountNumber}</AccountNumber>
    </ViewCustomerRecord>
  </soap12:Body>
</soap12:Envelope>";

            // Configure the HttpClient with appropriate timeout
            using (HttpClient client = new HttpClient())
            {
                // Set reasonable timeout and headers
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.ExpectContinue = false;

                // Use retry pattern to handle intermittent failures
                int maxRetries = 3;
                int currentRetry = 0;
                int retryDelayMs = 1000; // Start with 1 second delay

                while (true)
                {
                    try
                    {
                        Log(LogLevel.Debug, $"Sending customer record request for account {accountNumber} (Attempt {currentRetry + 1} of {maxRetries})");

                        var content = new StringContent(soapRequest, Encoding.UTF8, "application/soap+xml");

                        var response = await client.PostAsync("https://www.invoicecloud.com/portal/webservices/CloudManagement.asmx", content);

                        // Check HTTP status code
                        if (!response.IsSuccessStatusCode)
                        {
                            Log(LogLevel.Warning, $"HTTP error: {(int)response.StatusCode} {response.StatusCode} for account {accountNumber}");

                            // For specific error codes that might be temporary, we'll retry
                            if ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                            {
                                throw new HttpRequestException($"Server error: {response.StatusCode}");
                            }
                        }

                        var responseText = await response.Content.ReadAsStringAsync();

                        // Verify the response actually contains XML data
                        if (string.IsNullOrWhiteSpace(responseText) || !responseText.Contains("<"))
                        {
                            Log(LogLevel.Warning, $"Empty or non-XML response received for account {accountNumber}");
                            throw new FormatException("Invalid response format received");
                        }

                        Log(LogLevel.Debug, $"Received customer record response for account {accountNumber}");
                        return ParseCustomerRecordResponse(responseText);
                    }
                    catch (Exception ex) when (ex is HttpRequestException ||
                                               ex is TaskCanceledException ||
                                               ex is TimeoutException ||
                                               ex is FormatException)
                    {
                        currentRetry++;
                        Log(LogLevel.Warning, $"API call attempt {currentRetry} failed: {ex.Message}");

                        if (currentRetry >= maxRetries)
                        {
                            Log(LogLevel.Error, $"All retry attempts failed for account {accountNumber}. Last error: {ex.Message}");
                            throw new Exception($"Failed to retrieve customer data after {maxRetries} attempts. Last error: {ex.Message}", ex);
                        }

                        // Exponential backoff for retries
                        await Task.Delay(retryDelayMs);
                        retryDelayMs *= 2; // Double the delay for each retry
                    }
                }
            }
        }

        //
        private string ParseCustomerRecordResponse(string responseText)
        {
            try
            {
                // Add debug logging to see the raw response
                Log(LogLevel.Debug, $"Raw customer record XML starts with: {responseText.Substring(0, Math.Min(200, responseText.Length))}");

                // Check for top-level SOAP fault first
                if (responseText.Contains("<soap12:Fault") || responseText.Contains("<Fault"))
                {
                    Log(LogLevel.Warning, "SOAP Fault detected in customer record response");

                    // Try to extract fault reason
                    string faultReason = "Unknown fault";
                    int reasonStart = responseText.IndexOf("<faultstring>") + "<faultstring>".Length;
                    int reasonEnd = responseText.IndexOf("</faultstring>");

                    if (reasonStart > 0 && reasonEnd > reasonStart)
                    {
                        faultReason = responseText.Substring(reasonStart, reasonEnd - reasonStart);
                        Log(LogLevel.Warning, $"Fault reason: {faultReason}");
                    }

                    return $"Error: {faultReason}";
                }

                // Look for customer data in the response
                if (responseText.Contains("<CustomerID>") || responseText.Contains("<AccountNumber>"))
                {
                    try
                    {
                        StringBuilder result = new StringBuilder();
                        result.AppendLine("CUSTOMER RECORD:");
                        result.AppendLine("---------------");

                        // Extract all customer fields
                        string[] fields = new[] {
                    "CustomerID", "AccountNumber", "CustomerName", "Address1", "Address2",
                    "City", "State", "Zip", "Phone", "EmailAddress", "AutoPay",
                    "PaperInvoices", "Registered", "RegistrationDate", "LoginName"
                };

                        foreach (var field in fields)
                        {
                            string value = ExtractValue(responseText, field);
                            if (!string.IsNullOrEmpty(value))
                            {
                                // Format boolean values as Yes/No
                                if (field == "AutoPay" || field == "PaperInvoices" || field == "Registered" || field == "Active")
                                {
                                    value = value.ToLower() == "true" ? "Yes" : "No";
                                }
                                // Format dates
                                else if (field.Contains("Date") && DateTime.TryParse(value, out DateTime date) && date != DateTime.MinValue)
                                {
                                    value = date.ToString("MM/dd/yyyy");
                                }

                                result.AppendLine($"{FormatFieldName(field)}: {value}");
                            }
                        }

                        string finalResult = result.ToString();
                        Log(LogLevel.Debug, $"Formatted customer record result: {finalResult}");
                        return finalResult;
                    }
                    catch (Exception ex)
                    {
                        Log(LogLevel.Error, $"Error parsing customer data: {ex.Message}");
                        return "Error parsing customer data: " + ex.Message;
                    }
                }
                else if (responseText.Contains("ViewCustomerRecordResult") && !responseText.Contains("<CustomerID>"))
                {
                    Log(LogLevel.Warning, "No customer record found for this account number");
                    return "No customer record found for this account number.";
                }
                else
                {
                    Log(LogLevel.Warning, "Unexpected response format for customer record");
                    return "Unknown status: Response did not contain expected customer data format";
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error parsing customer response: {ex.Message}");
                return $"Error parsing customer response: {ex.Message}";
            }
        }


        private string FormatFieldName(string fieldName)
        {
            // Insert spaces before capital letters in the middle of the string
            string result = string.Empty;
            for (int i = 0; i < fieldName.Length; i++)
            {
                if (i > 0 && char.IsUpper(fieldName[i]))
                {
                    result += " ";
                }
                result += fieldName[i];
            }
            return result;
        }



        private string ParseResponse(string responseText)
        {
            try
            {
                // Check for top-level SOAP fault first
                if (responseText.Contains("<soap12:Fault") || responseText.Contains("<Fault"))
                {
                    Log(LogLevel.Warning, "SOAP Fault detected in response");

                    // Try to extract fault reason
                    string faultReason = "Unknown fault";
                    int reasonStart = responseText.IndexOf("<faultstring>") + "<faultstring>".Length;
                    int reasonEnd = responseText.IndexOf("</faultstring>");

                    if (reasonStart > 0 && reasonEnd > reasonStart)
                    {
                        faultReason = responseText.Substring(reasonStart, reasonEnd - reasonStart);
                        Log(LogLevel.Warning, $"Fault reason: {faultReason}");
                    }

                    return $"Error: {faultReason}";
                }

                // Check for success response
                if (responseText.Contains("<Success>true</Success>"))
                {
                    try
                    {
                        // Extract all available information from the response
                        StringBuilder result = new StringBuilder();

                        // Status
                        result.AppendLine("Status: Success");

                        // Invoice Number
                        string invoiceNumber = ExtractValue(responseText, "InvoiceNumber");
                        if (!string.IsNullOrEmpty(invoiceNumber))
                            result.AppendLine($"Invoice Number: {invoiceNumber}");

                        // Invoice Date
                        string invoiceDate = ExtractValue(responseText, "InvoiceDate");
                        if (!string.IsNullOrEmpty(invoiceDate))
                        {
                            if (DateTime.TryParse(invoiceDate, out DateTime parsedDate))
                                result.AppendLine($"Invoice Date: {parsedDate:MM/dd/yyyy}");
                            else
                                result.AppendLine($"Invoice Date: {invoiceDate}");
                        }

                        // Due Date
                        string dueDate = ExtractValue(responseText, "InvoiceDueDate");
                        if (!string.IsNullOrEmpty(dueDate))
                        {
                            if (DateTime.TryParse(dueDate, out DateTime parsedDate))
                                result.AppendLine($"Due Date: {parsedDate:MM/dd/yyyy}");
                            else
                                result.AppendLine($"Due Date: {dueDate}");
                        }

                        // AutoPay Collection Date
                        string autoPayDate = ExtractValue(responseText, "AutoPayCollectionDate");
                        if (!string.IsNullOrEmpty(autoPayDate) && autoPayDate != "1/1/0001")
                        {
                            if (DateTime.TryParse(autoPayDate, out DateTime parsedDate))
                                result.AppendLine($"AutoPay Date: {parsedDate:MM/dd/yyyy}");
                            else
                                result.AppendLine($"AutoPay Date: {autoPayDate}");
                        }

                        // Amount fields
                        string totalAmountDue = ExtractValue(responseText, "TotalAmountDue");
                        if (!string.IsNullOrEmpty(totalAmountDue))
                        {
                            if (decimal.TryParse(totalAmountDue, out decimal amount))
                                result.AppendLine($"Total Amount Due: {amount:C}");
                            else
                                result.AppendLine($"Total Amount Due: {totalAmountDue}");
                        }

                        string balanceDue = ExtractValue(responseText, "BalanceDue");
                        if (!string.IsNullOrEmpty(balanceDue))
                        {
                            if (decimal.TryParse(balanceDue, out decimal amount))
                                result.AppendLine($"Balance Due: {amount:C}");
                            else
                                result.AppendLine($"Balance Due: {balanceDue}");
                        }

                        string minimumAmountDue = ExtractValue(responseText, "MinimumAmountDue");
                        if (!string.IsNullOrEmpty(minimumAmountDue))
                        {
                            if (decimal.TryParse(minimumAmountDue, out decimal amount))
                                result.AppendLine($"Minimum Amount Due: {amount:C}");
                            else
                                result.AppendLine($"Minimum Amount Due: {minimumAmountDue}");
                        }

                        // Boolean fields
                        string allowPartialPayments = ExtractValue(responseText, "AllowPartialPayments");
                        if (!string.IsNullOrEmpty(allowPartialPayments))
                        {
                            string displayValue = allowPartialPayments.ToLower() == "true" ? "Yes" : "No";
                            result.AppendLine($"Allow Partial Payments: {displayValue}");
                        }

                        // Notification dates
                        string[] notificationFields = new[]
                        {
                            "FirstNotifyRequestedDate",
                            "SecondNotifyRequestedDate",
                            "ThirdNotifyRequestedDate"
                        };

                        foreach (string field in notificationFields)
                        {
                            string notifyDate = ExtractValue(responseText, field);
                            if (!string.IsNullOrEmpty(notifyDate) && notifyDate != "1/1/0001")
                            {
                                if (DateTime.TryParse(notifyDate, out DateTime parsedDate))
                                {
                                    string displayName = field.Replace("RequestedDate", "");
                                    result.AppendLine($"{displayName}: {parsedDate:MM/dd/yyyy}");
                                }
                            }
                        }

                        return result.ToString();
                    }
                    catch (Exception ex)
                    {
                        Log(LogLevel.Error, $"Error parsing data from success response: {ex.Message}");
                        return "Success: true, Error parsing detailed invoice data";
                    }
                }
                else if (responseText.Contains("<Success>false</Success>"))
                {
                    // Try to get error message if available
                    string errorMessage = "No details provided";
                    int messageStart = responseText.IndexOf("<ErrorMessage>") + "<ErrorMessage>".Length;
                    int messageEnd = responseText.IndexOf("</ErrorMessage>");

                    if (messageStart > 0 && messageEnd > messageStart)
                    {
                        errorMessage = responseText.Substring(messageStart, messageEnd - messageStart);
                    }

                    Log(LogLevel.Warning, $"API returned Success: false with message: {errorMessage}");
                    return $"Status: Failed\nReason: {errorMessage}";
                }
                else
                {
                    // Unexpected response format
                    Log(LogLevel.Warning, "Unexpected response format - could not determine success status");
                    return "Status: Unknown\nResponse did not contain expected format";
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error parsing response: {ex.Message}");
                return $"Error parsing response: {ex.Message}";
            }
        }

        private string ExtractValue(string xml, string tagName)
        {
            string openTag = $"<{tagName}>";
            string closeTag = $"</{tagName}>";

            int startIndex = xml.IndexOf(openTag);
            if (startIndex < 0)
                return string.Empty;

            startIndex += openTag.Length;
            int endIndex = xml.IndexOf(closeTag, startIndex);

            if (endIndex < 0)
                return string.Empty;

            return xml.Substring(startIndex, endIndex - startIndex).Trim();
        }

        private bool ValidateGUID(string guid)
        {
            // If input is empty, return false
            if (string.IsNullOrWhiteSpace(guid))
                return false;

            // Check if it's a valid GUID
            bool isValid = Guid.TryParse(guid, out _);

            // If validation fails, log more details for debugging
            if (!isValid)
            {
                Log(LogLevel.Debug, $"GUID validation failed for: '{guid}'");
            }

            return isValid;
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            // Clear RichTextBox content by creating a new document
            ConsoleLog.Document = new FlowDocument();
            _originalDocument = new FlowDocument();
            Log(LogLevel.Info, "Console cleared");
        }

        // Fixed SaveLogs_Click method
        private void SaveLogs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Log files (*.log)|*.log",
                FileName = $"InvoiceRefresher_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Extract text from RichTextBox
                    string consoleText = new TextRange(ConsoleLog.Document.ContentStart, ConsoleLog.Document.ContentEnd).Text;
                    File.WriteAllText(saveFileDialog.FileName, consoleText);
                    Log(LogLevel.Info, $"Logs saved to: {saveFileDialog.FileName}");
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Error, $"Failed to save logs: {ex.Message}");
                    MessageBox.Show($"Failed to save logs: {ex.Message}");
                }
            }
        }

        #region Menu Actions

        private void GenerateSampleCSV_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = "SampleInvoices.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var sampleContent = new StringBuilder();
                    sampleContent.AppendLine("INV0001");
                    sampleContent.AppendLine("INV0002");
                    sampleContent.AppendLine("INV0003");

                    File.WriteAllText(saveFileDialog.FileName, sampleContent.ToString());

                    Log(LogLevel.Info, $"Sample CSV file generated: {saveFileDialog.FileName}");

                    MessageBox.Show(
                        $"Sample CSV file created at:\n{saveFileDialog.FileName}\n\nFormat:\nOne invoice number per line\n\nThe Biller GUID and Web Service Key from the Single Invoice section will be used for processing.",
                        "Sample File Created",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Error, $"Failed to create sample file: {ex.Message}");
                    MessageBox.Show($"Error creating sample file: {ex.Message}");
                }
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Log(LogLevel.Info, "Application exit requested from menu");
            Application.Current.Shutdown();
        }

        private void Documentation_Click(object sender, RoutedEventArgs e)
        {
            Log(LogLevel.Info, "Documentation requested");
            ShowDocumentationWindow();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            Log(LogLevel.Info, "About dialog requested");
            ShowAboutDialog();
        }

        private void ShowDocumentationWindow()
        {
            // Create documentation window with terminal-like styling to match the app
            var docWindow = new Window
            {
                Title = "Documentation",
                Width = 700,
                Height = 500,
                Background = (System.Windows.Media.SolidColorBrush)FindResource("BackgroundBrush"),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var scrollViewer = new ScrollViewer
            {
                Margin = new Thickness(10),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var docContent = new TextBlock
            {
                Foreground = (System.Windows.Media.SolidColorBrush)FindResource("ForegroundBrush"),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10),
                Text = GetDocumentationText()
            };

            scrollViewer.Content = docContent;
            docWindow.Content = scrollViewer;
            docWindow.ShowDialog();
        }

        private string GetDocumentationText()
        {
            return @"
[INVOICE BALANCE REFRESHER DOCUMENTATION]
========================================

1. OVERVIEW
-----------
This application allows you to refresh invoice balances from the Invoice Cloud service.
You can process single invoices manually or batch process multiple invoices using a CSV file.

2. SINGLE INVOICE PROCESSING
---------------------------
To process a single invoice:
1. Enter the Biller GUID
2. Enter the Web Service Key
3. Enter the Invoice Number
4. Click [PROCESS INVOICE]

The result will appear in the output box below the button.

3. BATCH PROCESSING
------------------
To process multiple invoices:
1. Enter the Biller GUID and Web Service Key in the SINGLE INVOICE section
2. Create a CSV file with one invoice number per line
   
   Example:
   INV0001
   INV0002
   INV0003
   
3. Click [BROWSE] to select your CSV file
4. Click [PROCESS CSV] to begin processing

The application will use the Biller GUID and Web Service Key from the Single Invoice section
for all invoices in the batch. Results will be saved to a file named 'InvoiceResults.csv' 
in the same directory as your input file.

4. LOGGING
---------
All operations are logged in the console at the bottom of the application.
- Session logs are automatically saved to the 'Logs' directory
- You can manually save the current console log by clicking [SAVE LOGS]
- Clear the console display by clicking [CLEAR]

5. SAMPLE CSV GENERATION
----------------------
You can generate a sample CSV file by:
1. Click on [FILE] > Generate Sample CSV
2. Choose where to save the file

The generated sample will contain example invoice numbers, one per line.
Remember that the Biller GUID and Web Service Key from the Single Invoice section
will be used when processing this file.

6. API FORMAT
-----------
The application communicates with the Invoice Cloud SOAP API using the following request format:

<soap12:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema' xmlns:soap12='http://www.w3.org/2003/05/soap-envelope'>
    <soap12:Body>
        <ViewInvoiceByInvoiceNumber xmlns='https://www.invoicecloud.com/portal/webservices/CloudInvoicing/'>
            <Req>
                <BillerGUID>{billerGUID}</BillerGUID>
                <WebServiceKey>{webServiceKey}</WebServiceKey>
                <InvoiceNumber>{invoiceNumber}</InvoiceNumber>
            </Req>
        </ViewInvoiceByInvoiceNumber>
    </soap12:Body>
</soap12:Envelope>
";
        }

        private void ShowAboutDialog()
        {
            Log(LogLevel.Info, "About dialog requested");

            // Create custom About window with Fallout/Borderlands styling
            var aboutWindow = new Window
            {
                Title = "About Invoice Balance Refresher",
                Width = 600,
                Height = 500,
                Background = (System.Windows.Media.SolidColorBrush)FindResource("BackgroundBrush"),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };

            // Create a Grid as the main container
            var mainGrid = new Grid();
            aboutWindow.Content = mainGrid;

            // Add scanlines overlay (same as main window but a bit more visible)
            var scanlinesGrid = new Grid();
            scanlinesGrid.SetValue(Panel.ZIndexProperty, -1);
            scanlinesGrid.Background = new DrawingBrush
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 2, 2),
                ViewportUnits = BrushMappingMode.Absolute,
                Opacity = 0.07,
                Drawing = new DrawingGroup
                {
                    Children =
                    {
                        new GeometryDrawing
                        {
                            Brush = System.Windows.Media.Brushes.Transparent,
                            Geometry = new RectangleGeometry(new Rect(0, 0, 2, 2))
                        },
                        new GeometryDrawing
                        {
                            Brush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#00FF00")),
                            Geometry = new RectangleGeometry(new Rect(0, 0, 2, 1))
                        }
                    }
                }
            };
            mainGrid.Children.Add(scanlinesGrid);

            // Create a ScrollViewer to contain all content
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(20)
            };
            mainGrid.Children.Add(scrollViewer);

            // Create a StackPanel for the content
            var contentPanel = new StackPanel
            {
                Margin = new Thickness(0)
            };
            scrollViewer.Content = contentPanel;

            // Add logo/title with glow effect
            var titleBlock = new TextBlock
            {
                Text = ">> INVOICE BALANCE REFRESHER <<",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = (System.Windows.Media.SolidColorBrush)FindResource("AccentBrush"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5),
                Effect = new DropShadowEffect
                {
                    Color = ((System.Windows.Media.SolidColorBrush)FindResource("AccentBrush")).Color,
                    ShadowDepth = 0,
                    BlurRadius = 15,
                    Opacity = 0.7
                }
            };
            contentPanel.Children.Add(titleBlock);

            // Add version info
            var versionBlock = new TextBlock
            {
                Text = $"Version {APP_VERSION}",
                FontSize = 14,
                Foreground = (System.Windows.Media.SolidColorBrush)FindResource("AccentBrush2"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };
            contentPanel.Children.Add(versionBlock);

            // Add divider
            var divider1 = new Rectangle
            {
                Height = 2,
                Fill = (System.Windows.Media.SolidColorBrush)FindResource("BorderBrush"),
                Margin = new Thickness(50, 0, 50, 15),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            contentPanel.Children.Add(divider1);

            // Build company info with terminal-style
            var infoPanel = new StackPanel
            {
                Margin = new Thickness(10, 0, 10, 20)
            };
            contentPanel.Children.Add(infoPanel);

            // Add description section with terminal styling
            var descriptionBlock = new TextBlock
            {
                Text = "SYSTEM INFORMATION:",
                Foreground = (System.Windows.Media.SolidColorBrush)FindResource("AccentBrush"),
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            infoPanel.Children.Add(descriptionBlock);

            // Application description with typewriter-style text
            var appDescriptionBlock = new TextBlock
            {
                Text = "A terminal-style application for refreshing invoice balances from the Invoice Cloud service.",
                Foreground = (System.Windows.Media.SolidColorBrush)FindResource("ForegroundBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 0, 10, 10)
            };
            infoPanel.Children.Add(appDescriptionBlock);

            // Add copyright info
            var copyrightBlock = new TextBlock
            {
                Text = $"© {DateTime.Now.Year} Invoice Cloud",
                Foreground = (System.Windows.Media.SolidColorBrush)FindResource("ForegroundBrush"),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };
            infoPanel.Children.Add(copyrightBlock);

            // Add second divider
            var divider2 = new Rectangle
            {
                Height = 2,
                Fill = (System.Windows.Media.SolidColorBrush)FindResource("BorderBrush"),
                Margin = new Thickness(50, 0, 50, 15),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            contentPanel.Children.Add(divider2);

            // Features border with terminal styling
            var featuresBorder = new Border
            {
                BorderBrush = (System.Windows.Media.SolidColorBrush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Background = (System.Windows.Media.SolidColorBrush)FindResource("GroupBackgroundBrush"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(15),
                Margin = new Thickness(10, 0, 10, 15)
            };
            contentPanel.Children.Add(featuresBorder);

            // Features stack panel
            var featuresPanel = new StackPanel();
            featuresBorder.Child = featuresPanel;

            // Features header
            var featuresHeader = new TextBlock
            {
                Text = "FEATURES:",
                Foreground = (System.Windows.Media.SolidColorBrush)FindResource("AccentBrush"),
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            featuresPanel.Children.Add(featuresHeader);

            // Add feature list with bullet points
            string[] features = new string[]
            {
                "Single invoice processing with real-time feedback",
                "Batch processing of multiple invoices via CSV files",
                "Comprehensive logging system with session history",
                "Terminal-inspired UI with Fallout/Borderlands aesthetic",
                "Invoice balance checking via SOAP API integration",
                "CSV sample generation for easier batch processing setup",
                "Detailed error handling and validation"
            };

            foreach (var feature in features)
            {
                var featureBlock = new TextBlock
                {
                    Text = $"• {feature}",
                    Foreground = (System.Windows.Media.SolidColorBrush)FindResource("ForegroundBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 0, 0, 5)
                };
                featuresPanel.Children.Add(featureBlock);
            }

            // Technical info
            var techInfoBorder = new Border
            {
                BorderBrush = (System.Windows.Media.SolidColorBrush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Background = (System.Windows.Media.SolidColorBrush)FindResource("ConsoleBackgroundBrush"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(15),
                Margin = new Thickness(10, 0, 10, 15)
            };
            contentPanel.Children.Add(techInfoBorder);

            // Technical info stack panel
            var techInfoPanel = new StackPanel();
            techInfoBorder.Child = techInfoPanel;

            // Technical info header
            var techInfoHeader = new TextBlock
            {
                Text = "TECHNICAL INFORMATION:",
                Foreground = (System.Windows.Media.SolidColorBrush)FindResource("AccentBrush"),
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            techInfoPanel.Children.Add(techInfoHeader);

            // Technical details
            var techDetails = new TextBlock
            {
                Text = $"Framework: .NET 8\nLanguage: C# 12.0\nUI Framework: WPF\nAPI: SOAP XML\nCreated: May 2025\nBuild: {APP_VERSION}.0",
                Foreground = (System.Windows.Media.SolidColorBrush)FindResource("ForegroundBrush"),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Margin = new Thickness(10, 0, 0, 0)
            };
            techInfoPanel.Children.Add(techDetails);

            // Add close button at bottom
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };
            contentPanel.Children.Add(buttonPanel);

            var closeButton = new Button
            {
                Content = "[CLOSE]",
                Width = 150,
                Height = 30,
                Margin = new Thickness(5)
            };
            closeButton.Click += (s, e) => aboutWindow.Close();
            buttonPanel.Children.Add(closeButton);

            // Show the about window
            aboutWindow.ShowDialog();
        }

        #endregion

        // Log entry model
        public class LogEntry
        {
            public string Timestamp { get; }
            public LogLevel Level { get; }
            public string Message { get; }

            public LogEntry(string timestamp, LogLevel level, string message)
            {
                Timestamp = timestamp;
                Level = level;
                Message = message;
            }
        }

        // Log level enum
        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }
    }
}
