using Markdig;
using Microsoft.Win32;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Eikon.Desktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Loaded += async (_, __) =>
            {
                await Web.EnsureCoreWebView2Async();

                // Load HTML template from embedded resource
                var htmlTemplate = ReadEmbeddedText("Markdownviewer.html");

                // Convert markdown -> HTML
                var md = "# Hello\nThis is **Markdown**.";
                var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                var mdHtml = Markdown.ToHtml(md, pipeline);

                // Replace placeholder
                var finalHtml = htmlTemplate.Replace("<!--MARKDIG_HTML-->", mdHtml);

                // Navigate to HTML
                Web.NavigateToString(finalHtml);
            };
        }

      private async void Pen_Click(object sender, RoutedEventArgs e)
      {
         await Web.ExecuteScriptAsync("window.ink?.enable()");
         await Web.ExecuteScriptAsync("window.ink?.setMode('draw')");
         await Web.ExecuteScriptAsync("window.ink.setColor('#8B0000')");
      }

      private string ReadEmbeddedText(string resourcePath)
      {
            return File.ReadAllText(resourcePath);
      }

      private async void ExportInkJson(object sender, RoutedEventArgs e)
      {
         var result = await Web.ExecuteScriptAsync("window.ink?.exportStrokes()");
         // ExecuteScriptAsync returns a JSON-encoded string literal → decode it:
         var strokesJson = JsonSerializer.Deserialize<string>(result) ?? "[]";

         Console.WriteLine($"Strokes: {strokesJson}");
      }
   }
}