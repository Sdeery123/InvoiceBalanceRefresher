using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Documents;

namespace InvoiceBalanceRefresher
{
    /// <summary>
    /// Provides helper methods for working with UI elements and visual tree operations
    /// </summary>
    public static class UIHelper
    {
        /// <summary>
        /// Recursively finds all visual children of a specified type in a dependency object
        /// </summary>
        /// <typeparam name="T">Type of child objects to find</typeparam>
        /// <param name="depObj">The dependency object to search</param>
        /// <returns>An enumerable collection of found child objects</returns>
        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
                yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T)
                    yield return (T)child;

                foreach (T childOfChild in FindVisualChildren<T>(child!))
                    yield return childOfChild;
            }
        }

        /// <summary>
        /// Creates a deep clone of a UI element by serializing and deserializing it
        /// </summary>
        /// <param name="element">The element to clone</param>
        /// <returns>A cloned UIElement or null if cloning failed</returns>
        public static UIElement? CloneElement(UIElement element)
        {
            if (element == null) return null;

            try
            {
                // Serialize to XAML
                string xaml = System.Windows.Markup.XamlWriter.Save(element);

                // Deserialize from XAML
                using (System.IO.StringReader stringReader = new System.IO.StringReader(xaml))
                using (System.Xml.XmlReader xmlReader = System.Xml.XmlReader.Create(stringReader))
                {
                    return System.Windows.Markup.XamlReader.Load(xmlReader) as UIElement;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Finds a console header grid in the visual tree of the specified content
        /// </summary>
        /// <param name="content">The content to search</param>
        /// <returns>The found grid or null if not found</returns>
        public static Grid? FindConsoleHeaderGrid(object content)
        {
            // The console header grid is in Grid.Row="2" of the main grid
            if (content is Grid mainGrid)
            {
                foreach (UIElement element in mainGrid.Children)
                {
                    if (element is Grid grid && Grid.GetRow(grid) == 2)
                    {
                        return grid;
                    }
                }
            }
            return null; // Return null if no matching grid is found
        }

        /// <summary>
        /// Helper method to parse FontWeight from string
        /// </summary>
        public static class FontWeightConverter
        {
            public static bool TryParse(string weightString, out FontWeight fontWeight)
            {
                fontWeight = FontWeights.Normal;

                try
                {
                    // Handle common FontWeight predefined values
                    switch (weightString)
                    {
                        case "Thin": fontWeight = FontWeights.Thin; return true;
                        case "ExtraLight": fontWeight = FontWeights.ExtraLight; return true;
                        case "Light": fontWeight = FontWeights.Light; return true;
                        case "Normal": fontWeight = FontWeights.Normal; return true;
                        case "Medium": fontWeight = FontWeights.Medium; return true;
                        case "SemiBold": fontWeight = FontWeights.SemiBold; return true;
                        case "Bold": fontWeight = FontWeights.Bold; return true;
                        case "ExtraBold": fontWeight = FontWeights.ExtraBold; return true;
                        case "Black": fontWeight = FontWeights.Black; return true;
                        case "ExtraBlack": fontWeight = FontWeights.ExtraBlack; return true;
                        default:
                            // Try to parse as numeric weight
                            if (int.TryParse(weightString, out int numericWeight))
                            {
                                fontWeight = FontWeight.FromOpenTypeWeight(numericWeight);
                                return true;
                            }
                            return false;
                    }
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
