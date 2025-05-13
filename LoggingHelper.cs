using System;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Collections.Generic;

namespace InvoiceBalanceRefresher
{
    public class LoggingHelper
    {
        private readonly List<LogEntry> _logEntries = new List<LogEntry>();
        private string _sessionLogPath = string.Empty;
        private FlowDocument _originalDocument;
        private System.Windows.Controls.RichTextBox _consoleLog;
        private Action<Paragraph> _addToConsoleAction;

        public LoggingHelper(System.Windows.Controls.RichTextBox consoleLog, Action<Paragraph> addToConsoleAction)
        {
            _consoleLog = consoleLog;
            _addToConsoleAction = addToConsoleAction;
            _originalDocument = new FlowDocument();

            // Create logs directory if it doesn't exist
            string logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(logsDirectory);

            // Create a new session log file
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _sessionLogPath = Path.Combine(logsDirectory, $"Session_{timestamp}.log");
        }

        public void Log(MainWindow.LogLevel level, string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logMessage = $"[{timestamp}] [{level}] {message}";

            // Add to in-memory log collection
            _logEntries.Add(new LogEntry(timestamp, level, message));

            // Create a new paragraph for this log entry
            Paragraph paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(logMessage));

            // Add to original document - create a deep copy
            Paragraph originalCopy = new Paragraph();
            originalCopy.Inlines.Add(new Run(logMessage));
            _originalDocument.Blocks.Add(originalCopy);

            // Add to console via the provided action
            _addToConsoleAction(paragraph);

            // Write to session log file
            try
            {
                File.AppendAllText(_sessionLogPath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Handle file writing errors
                Paragraph errorParagraph = new Paragraph();
                errorParagraph.Inlines.Add(new Run($"[ERROR] Failed to write to log file: {ex.Message}"));

                // Add to original document - create a deep copy
                Paragraph errorCopy = new Paragraph();
                errorCopy.Inlines.Add(new Run($"[ERROR] Failed to write to log file: {ex.Message}"));
                _originalDocument.Blocks.Add(errorCopy);

                // Add to console
                _addToConsoleAction(errorParagraph);
            }
        }

        public void ClearLog()
        {
            // Clear document
            _originalDocument = new FlowDocument();
            _consoleLog.Document = new FlowDocument();

            // Log the clearing action
            Log(MainWindow.LogLevel.Info, "Console cleared");
        }

        public bool SaveLogs()
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Log files (*.log)|*.log",
                FileName = $"InvoiceRefresher_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Extract text from RichTextBox
                    string consoleText = new TextRange(_consoleLog.Document.ContentStart, _consoleLog.Document.ContentEnd).Text;
                    File.WriteAllText(saveFileDialog.FileName, consoleText);
                    Log(MainWindow.LogLevel.Info, $"Logs saved to: {saveFileDialog.FileName}");
                    return true;
                }
                catch (Exception ex)
                {
                    Log(MainWindow.LogLevel.Error, $"Failed to save logs: {ex.Message}");
                    System.Windows.MessageBox.Show($"Failed to save logs: {ex.Message}");
                    return false;
                }
            }

            return false;
        }

        public FlowDocument OriginalDocument => _originalDocument;
        public string SessionLogPath => _sessionLogPath;
        public IReadOnlyList<LogEntry> LogEntries => _logEntries.AsReadOnly();

        // Define LogEntry class locally in LoggingHelper
        public class LogEntry
        {
            public string Timestamp { get; }
            public MainWindow.LogLevel Level { get; }
            public string Message { get; }

            public LogEntry(string timestamp, MainWindow.LogLevel level, string message)
            {
                Timestamp = timestamp;
                Level = level;
                Message = message;
            }
        }
    }
}
