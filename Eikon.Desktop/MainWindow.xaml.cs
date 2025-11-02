using Markdig;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Timers;
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

   public record InputMsg(string kind, Op[] ops, int caret);
   public record Op(string type, string? text, int count);
   public record SpecialCharacters(int endIndex, int width);

   public class NoteText
   {
      public string MdText { get; set; }

      public List<SpecialCharacters> SpecialChars {get; set;}

      public NoteText()
      {
         MdText = string.Empty;
         SpecialChars = new List<SpecialCharacters>();   
      }
   }

   /// <summary>
   /// Interaction logic for MainWindow.xaml
   /// </summary>
   public partial class MainWindow : Window
    {
      private NoteText _noteText;
      private int _caret;
      private MarkdownPipeline _pipeline;
      private int _textIndex;
      private bool _isHtmlLoaded = false;

      public MainWindow()
      {
         InitializeComponent();
            
         Loaded += async (_, __) =>
         {
               await Web.EnsureCoreWebView2Async();

               // Load HTML template from embedded resource
               var htmlTemplate = ReadEmbeddedText("Markdownviewer.html");

                _noteText = new NoteText();
               // Convert markdown -> HTML
               _noteText.MdText = "# Hello\nThis is **Markdown**.";
               _textIndex = _noteText.MdText.Length;
               _pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
               var mdHtml = Markdown.ToHtml(_noteText.MdText, _pipeline);

               // Replace placeholder
               var finalHtml = htmlTemplate.Replace("<!--MARKDIG_HTML-->", mdHtml);

               // Navigate to HTML
               Web.NavigateToString(finalHtml);
               _isHtmlLoaded = true;
         };

         System.Timers.Timer timer = new System.Timers.Timer();
         timer.Interval = 14;
         timer.Elapsed += Timer_Elapsed;
         timer.Start();
      }

      private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
      {
         Task.Run(() => RenderText());
      }

      private async Task RenderText()
      {
         if (_isHtmlLoaded)
         {
            var mdHtml = Markdown.ToHtml(_noteText.MdText, _pipeline);
            var html = mdHtml
                            .Replace("\\", "\\\\")   // escape backslashes first
                            .Replace("`", "\\`");    // escape backticks


            await Dispatcher.InvokeAsync(async () =>
            {



               Debug.WriteLine($"HTML: {mdHtml}");
               await Web.ExecuteScriptAsync(
                $"window.ReadonlyCaret.setHtmlAndRestoreCaret(`{html}`, {_caret});"
            );
            });
         }
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

      private void Web_KeyDown(object sender, KeyEventArgs e)
      {
         Debug.WriteLine($"Key: {e.Key}");
      }

      private void Web_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
      {
         try
         {
            string raw = e.WebMessageAsJson;
            var msg = System.Text.Json.JsonSerializer.Deserialize<InputMsg>(raw);
            if (msg?.kind != "ops") return;


            foreach (var op in msg.ops)
            {
               if (op.type == "insert" && !string.IsNullOrEmpty(op.text)) 
               {
                  var text = op.text;
                  int specialCharIndex = -1;

                  if(text == " ")
                  {
                     text = "&nbsp;";
                     specialCharIndex = _textIndex + text.Length;

                  }
                  else if(text == "<br />")
                  {
                     text = "<br />&ZeroWidthSpace;";
                     specialCharIndex = _textIndex + text.Length;
                  }
                  _noteText.MdText = _noteText.MdText.Insert(_textIndex, text ?? ""); 
                  
                  _textIndex += text.Length;
                  _noteText.SpecialChars.Add(new(_textIndex, text.Length));
               }
               else if (op.type == "delete" && _textIndex > 0) 
               {
                  int count = op.count;
                  var specialChar = _noteText.SpecialChars.FirstOrDefault(s => s.endIndex == _textIndex);
                  if (specialChar != null)
                  {
                     count = specialChar.width;
                  }
                  _noteText.MdText = _noteText.MdText.Remove(_textIndex - count, count); 
                  _textIndex = _textIndex - count; 
               }
            }

            Debug.WriteLine($"Text: {_noteText.MdText}, Caret: {msg.caret}");
            _caret = msg.caret;
         }
         catch(Exception ex) 
         {
            Debug.WriteLine($"Excpeiton: {ex.Message}, StackTrace: {ex.StackTrace}");
         }
      }
   }
}