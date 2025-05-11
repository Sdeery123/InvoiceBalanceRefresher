using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace InvoiceBalanceRefresher
{
    public class SearchHelper
    {
        private readonly RichTextBox _consoleLog;
        private readonly TextBlock _searchResultsCount;
        private FlowDocument _originalDocument;
        private int _searchResultCount = 0;
        
        // For batch search
        private readonly TextBox _batchResults;
        private readonly TextBlock _batchSearchResultsCount;
        private string _originalBatchResults = string.Empty;
        private int _batchSearchResultCount = 0;
        
        public SearchHelper(RichTextBox consoleLog, TextBlock searchResultsCount, TextBox batchResults, TextBlock batchSearchResultsCount)
        {
            _consoleLog = consoleLog ?? throw new ArgumentNullException(nameof(consoleLog));
            _searchResultsCount = searchResultsCount ?? throw new ArgumentNullException(nameof(searchResultsCount));
            _batchResults = batchResults ?? throw new ArgumentNullException(nameof(batchResults));
            _batchSearchResultsCount = batchSearchResultsCount ?? throw new ArgumentNullException(nameof(batchSearchResultsCount));
            
            // Initialize original document
            _originalDocument = new FlowDocument();
        }
        
        public void SetOriginalDocument(FlowDocument document)
        {
            _originalDocument = document ?? new FlowDocument();
        }
        
        public void PerformSearch(string searchText)
        {
            // Reset search results count
            _searchResultCount = 0;

            if (string.IsNullOrEmpty(searchText))
            {
                // If search text is empty, restore original content
                _consoleLog.Document = new FlowDocument();
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
                        _consoleLog.Document.Blocks.Add(newParagraph);
                    }
                }
                _searchResultsCount.Text = string.Empty;
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
                            int pos = 0;

                            // Use helper method for highlighting text based on theme
                            DocumentFormatHelper.HighlightSearchText(newParagraph, runText, searchText, ref pos, ref _searchResultCount);
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
            _consoleLog.Document = searchResultDocument;

            // Update search results count
            _searchResultsCount.Text = $"Found: {_searchResultCount}";
        }
        
        public void PerformBatchSearch(string searchText)
        {
            // If we don't have the original text stored yet, store it
            if (string.IsNullOrEmpty(_originalBatchResults) && !string.IsNullOrEmpty(_batchResults.Text))
            {
                _originalBatchResults = _batchResults.Text;
            }

            // Reset search counter
            _batchSearchResultCount = 0;

            // If search is empty, restore original content
            if (string.IsNullOrEmpty(searchText))
            {
                _batchResults.Text = _originalBatchResults;
                _batchSearchResultsCount.Text = string.Empty;
                return;
            }

            // If there's no content to search
            if (string.IsNullOrEmpty(_originalBatchResults))
            {
                _batchSearchResultsCount.Text = "No results";
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
            _batchResults.Text = filteredContent.ToString();
            _batchSearchResultsCount.Text = $"Found: {_batchSearchResultCount}";
        }
    }
}
