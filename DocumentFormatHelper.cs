using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace InvoiceBalanceRefresher
{
    /// <summary>
    /// Provides helper methods for document formatting and text display
    /// </summary>
    public static class DocumentFormatHelper
    {
        /// <summary>
        /// Adds a paragraph of text to a StackPanel
        /// </summary>
        public static void AddParagraph(StackPanel panel, string text, bool isIntro = false)
        {
            var paragraph = new TextBlock
            {
                Text = text,
                Foreground = (SolidColorBrush)Application.Current.Resources["ForegroundBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };

            if (isIntro)
            {
                paragraph.FontSize += 2;
                paragraph.FontStyle = FontStyles.Italic;
            }

            // Store original font weight as a string
            paragraph.Tag = paragraph.FontWeight.ToString();
            panel.Children.Add(paragraph);
        }

        /// <summary>
        /// Adds a bullet point to a StackPanel
        /// </summary>
        public static void AddBulletPoint(StackPanel panel, string text)
        {
            var bulletPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 5) };

            var bullet = new TextBlock
            {
                Text = "•",
                Foreground = (SolidColorBrush)Application.Current.Resources["ForegroundBrush"],
                Margin = new Thickness(0, 0, 5, 0),
                FontWeight = FontWeights.Bold
            };

            var content = new TextBlock
            {
                Text = text,
                Foreground = (SolidColorBrush)Application.Current.Resources["ForegroundBrush"],
                TextWrapping = TextWrapping.Wrap
            };

            // Store original font weights as strings
            bullet.Tag = bullet.FontWeight.ToString();
            content.Tag = content.FontWeight.ToString();

            bulletPanel.Children.Add(bullet);
            bulletPanel.Children.Add(content);
            panel.Children.Add(bulletPanel);
        }

        /// <summary>
        /// Adds a subheading to a StackPanel
        /// </summary>
        public static void AddSubheading(StackPanel panel, string text)
        {
            var subheading = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 10, 0, 5)
            };

            // Store original font weight as a string
            subheading.Tag = subheading.FontWeight.ToString();
            panel.Children.Add(subheading);
        }

        /// <summary>
        /// Adds a code block to a StackPanel
        /// </summary>
        public static void AddCodeBlock(StackPanel panel, string code)
        {
            var codeBorder = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#124050")),
                Background = new SolidColorBrush(Color.FromArgb(30, 18, 83, 106)),
                Padding = new Thickness(10),
                Margin = new Thickness(10, 5, 10, 15),
                CornerRadius = new CornerRadius(4)
            };

            var codeText = new TextBlock
            {
                Text = code,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E55555")),
                FontFamily = new FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap
            };

            // Store original font weight as a string
            codeText.Tag = codeText.FontWeight.ToString();

            codeBorder.Child = codeText;
            panel.Children.Add(codeBorder);
        }

        /// <summary>
        /// Adds a FAQ item to a StackPanel
        /// </summary>
        public static void AddFAQItem(StackPanel panel, string question, string answer, Window? owner = null)
        {
            var faqBorder = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#124050")),
                Background = new SolidColorBrush(Color.FromArgb(20, 24, 180, 233)),
                Padding = new Thickness(15, 10, 15, 15),
                Margin = new Thickness(0, 5, 0, 15),
                CornerRadius = new CornerRadius(4)
            };

            var faqPanel = new StackPanel();

            var questionText = new TextBlock
            {
                Text = "Q: " + question,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18B4E9")),
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var answerText = new TextBlock
            {
                Text = "A: " + answer,
                Foreground = owner != null ? (SolidColorBrush)owner.FindResource("ForegroundBrush") : Brushes.White,
                TextWrapping = TextWrapping.Wrap
            };

            // Store original font weights as strings
            questionText.Tag = questionText.FontWeight.ToString();
            answerText.Tag = answerText.FontWeight.ToString();

            faqPanel.Children.Add(questionText);
            faqPanel.Children.Add(answerText);
            faqBorder.Child = faqPanel;
            panel.Children.Add(faqBorder);
        }

        /// <summary>
        /// Adds a note to a StackPanel
        /// </summary>
        public static void AddNote(StackPanel panel, string text)
        {
            var noteBorder = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0A030")),
                Background = new SolidColorBrush(Color.FromArgb(20, 240, 160, 48)),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 5, 0, 15),
                CornerRadius = new CornerRadius(4)
            };

            var notePanel = new StackPanel { Orientation = Orientation.Horizontal };

            var noteIcon = new TextBlock
            {
                Text = "??",
                FontSize = 14,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            var noteText = new TextBlock
            {
                Text = "Note: " + text,
                Foreground = (SolidColorBrush)Application.Current.Resources["ForegroundBrush"],
                TextWrapping = TextWrapping.Wrap
            };

            notePanel.Children.Add(noteIcon);
            notePanel.Children.Add(noteText);
            noteBorder.Child = notePanel;
            panel.Children.Add(noteBorder);
        }

        /// <summary>
        /// Adds steps with numbers to a StackPanel
        /// </summary>
        public static void AddSteps(StackPanel panel, string introText, string[] steps)
        {
            // Add intro text
            var intro = new TextBlock
            {
                Text = introText,
                Foreground = (SolidColorBrush)Application.Current.Resources["ForegroundBrush"],
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 5, 0, 5)
            };
            panel.Children.Add(intro);

            // Add steps
            for (int i = 0; i < steps.Length; i++)
            {
                var stepPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(15, 2, 0, 2)
                };

                var stepNumber = new TextBlock
                {
                    Text = $"{i + 1}.",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0A030")),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };

                var stepContent = new TextBlock
                {
                    Text = steps[i],
                    Foreground = (SolidColorBrush)Application.Current.Resources["ForegroundBrush"],
                    TextWrapping = TextWrapping.Wrap
                };

                stepPanel.Children.Add(stepNumber);
                stepPanel.Children.Add(stepContent);
                panel.Children.Add(stepPanel);
            }

            // Add a little space after the steps
            panel.Children.Add(new Rectangle { Height = 5 });
        }

        /// <summary>
        /// Highlights search text in a paragraph
        /// </summary>
        public static void HighlightSearchText(Paragraph newParagraph, string runText, string searchText, ref int pos, ref int searchResultCount)
        {
            // Find all occurrences of search text in this run
            while ((pos = runText.ToLower().IndexOf(searchText, pos, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                // Add text before match
                if (pos > 0)
                    newParagraph.Inlines.Add(new Run(runText.Substring(0, pos)));

                // Choose highlight color based on theme
                Color highlightColor;
                if (Application.Current.Resources["BackgroundBrush"] == Application.Current.Resources["LightBackgroundBrush"])
                {
                    // Light mode highlight
                    highlightColor = (Color)ColorConverter.ConvertFromString("#085368"); // Deep Teal
                }
                else
                {
                    // Dark mode highlight
                    highlightColor = (Color)ColorConverter.ConvertFromString("#18B4E9"); // Sky Blue
                }

                // Add the match with highlight
                var highlightRun = new Run(runText.Substring(pos, searchText.Length))
                {
                    Background = new SolidColorBrush(highlightColor),
                    Foreground = Brushes.White
                };
                newParagraph.Inlines.Add(highlightRun);

                // Update for next iteration
                runText = runText.Substring(pos + searchText.Length);
                pos = 0;
                searchResultCount++;
            }

            // Add any remaining text
            if (!string.IsNullOrEmpty(runText))
                newParagraph.Inlines.Add(new Run(runText));
        }

        /// <summary>
        /// Helper method to properly escape CSV fields
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
    }
}
