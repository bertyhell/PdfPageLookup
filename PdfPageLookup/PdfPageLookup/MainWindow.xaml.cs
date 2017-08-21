using System;
using System.Collections.Generic;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Microsoft.Win32;
using System.Linq;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;

namespace PdfPageLookup
{
    public partial class MainWindow
    {
        private const string PDF_FILTER = "Pdf files|*.pdf";

        public MainWindow()
        {
            InitializeComponent();

            string[] commonWords = File
                .ReadAllText("noisewords.txt")
                .Split(new string[] { "\r\n", "\n", " ", "\t" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Distinct()
                .ToArray();

            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = PDF_FILTER, Title = "Select the PDF file" };
            bool? userClickedOk = openFileDialog.ShowDialog();
            if (userClickedOk == true)
            {
                List<WordOccurence> blocks = GetTextBlocsFromPdf(openFileDialog.FileName, out int numberOfPages);
                // TODO output to txt
                Dictionary<string, HashSet<int>> pageOccurencesPerWord = new Dictionary<string, HashSet<int>>();
                foreach (WordOccurence wordOccurence in blocks)
                {
                    foreach (string word in wordOccurence.Words)
                    {
                        if (!commonWords.Contains(word.ToLower()))
                        {
                            if (!pageOccurencesPerWord.ContainsKey(word))
                            {
                                pageOccurencesPerWord[word] = new HashSet<int>();
                            }
                            pageOccurencesPerWord[word].Add(wordOccurence.PageNumber);
                        }
                    }
                }

                string lookupTable = "";
                foreach (string word in pageOccurencesPerWord.Keys.OrderBy(k => k))
                {
                    HashSet<int> pageNumbers = pageOccurencesPerWord[word];
                    if (numberOfPages < 4 || pageNumbers.Count < numberOfPages * 0.5) // Less than 50% of the pages contain the word
                    {
                        List<int> orderedPageNumbers = pageNumbers.OrderBy(p => p).ToList();
                        string pageNumberString = string.Join(", ", CompactNumbersIntoIntervals(orderedPageNumbers));
                        lookupTable += word + "\t" + pageNumberString + "\n";
                    }
                }

                Clipboard.SetText(lookupTable);
                MessageBox.Show("Lookup table copied to clipboard", "Success", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
            }
            Close();
        }

        private List<string> CompactNumbersIntoIntervals(List<int> numbers)
        {
            return numbers
              .Select((n, i) => new { number = n, group = n - i })
              .GroupBy(n => n.group)
              .Select(g =>
                  g.Count() >= 3 ?
                  g.First().number + "-" + g.Last().number
                  :
                  String.Join(", ", g.Select(x => x.number))
              )
              .ToList();
        }

        public List<WordOccurence> GetTextBlocsFromPdf(string fileName, out int numberOfPages)
        {
            var blocks = new List<WordOccurence>();
            var reader = new PdfReader(fileName);
            numberOfPages = reader.NumberOfPages;
            for (int page = 1; page <= reader.NumberOfPages; page++)
            {
                string text = PdfTextExtractor.GetTextFromPage(reader, page);
                string[] words = text
                    .Split(new string[] { "\r\n", "\n", " ", "\t" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => Regex.Replace(Regex.Replace(w.Trim(), "^[^a-zA-Z0-9]+", ""), "[^a-zA-Z0-9]+$", "").ToLower())
                    .Distinct()
                    .Where(w => w.Length > 1)
                    .ToArray();
                blocks.Add(new WordOccurence(page, words.ToList()));

            }
            reader.Close();
            return blocks;
        }

        public class WordOccurence
        {
            public int PageNumber;
            public List<string> Words;

            public WordOccurence(int pageNumber, List<string> words)
            {
                PageNumber = pageNumber;
                Words = words;
            }
        }
    }
}
