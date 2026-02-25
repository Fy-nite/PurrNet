using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using purrnet.Pages.Shared;
using purrnet.Documentation;
namespace purrnet.Pages.Documentation
{
    public class IndexModel : PageModel
    {
        public class DocEntry
        {
            public string Title { get; set; }
            public string Category { get; set; }
            public string Content { get; set; }
            public string Url { get; set; }
        }

        public List<DocEntry> DocumentationPages { get; set; } = new();
        public string SelectedContent { get; set; }
        public string SelectedTitle { get; set; }

        public void OnGet()
        {
            var page = Request.Query["page"].ToString();
            var docs = new List<DocEntry>();
            var methods = Assembly.GetExecutingAssembly()
                .GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(m => m.GetCustomAttribute<DocumentationPageAttribute>() != null);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<DocumentationPageAttribute>();
                var content = method.Invoke(null, null) as string;
                docs.Add(new DocEntry
                {
                    Title = attr.Title,
                    Category = attr.Category,
                    Content = content,
                    Url = "?page=" + System.Net.WebUtility.UrlEncode(attr.Title)
                });
            }
            DocumentationPages = docs.OrderBy(d => d.Category).ThenBy(d => d.Title).ToList();
            // Also include any physical Razor pages under Pages/Documentation so file-based docs (like SubmitPackage.cshtml) appear
            try
            {
                var contentRoot = Directory.GetCurrentDirectory();
                var docsDir = Path.Combine(contentRoot, "Pages", "Documentation");
                if (Directory.Exists(docsDir))
                {
                    var csfiles = Directory.GetFiles(docsDir, "*.cshtml", SearchOption.TopDirectoryOnly);
                    foreach (var f in csfiles)
                    {
                        var fname = Path.GetFileName(f);
                        if (string.Equals(fname, "Index.cshtml", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (fname.StartsWith("_") || fname.StartsWith("Shared", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string fileContent = System.IO.File.ReadAllText(f);
                        // Try to extract ViewData["Title"] = "..." if present
                        string title = Path.GetFileNameWithoutExtension(f);
                        try
                        {
                            var marker = "ViewData[\"Title\"]";
                            var idx = fileContent.IndexOf(marker);
                            if (idx >= 0)
                            {
                                var sub = fileContent.Substring(idx, Math.Min(200, fileContent.Length - idx));
                                var start = sub.IndexOf('=');
                                if (start >= 0)
                                {
                                    var quote = sub.IndexOf('"', start);
                                    var quote2 = sub.IndexOf('"', quote + 1);
                                    if (quote >= 0 && quote2 > quote)
                                    {
                                        var t = sub.Substring(quote + 1, quote2 - quote - 1).Trim();
                                        if (!string.IsNullOrEmpty(t)) title = t;
                                    }
                                }
                            }
                        }
                        catch { }

                        // Create a doc entry; use category 'Pages' for file-based docs and link directly to the page route
                        var nameNoExt = Path.GetFileNameWithoutExtension(f);
                        var pageRoute = "/Documentation/" + nameNoExt;
                        docs.Add(new DocEntry { Title = title, Category = "Pages", Content = string.Empty, Url = pageRoute });
                    }

                    DocumentationPages = docs.OrderBy(d => d.Category).ThenBy(d => d.Title).ToList();
                }
            }
            catch { /* ignore filesystem errors */ }
            var selected = DocumentationPages
                .FirstOrDefault(d => string.Equals(d.Title?.Trim(), page?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? DocumentationPages.FirstOrDefault();
            SelectedContent = selected?.Content;
            SelectedTitle = selected?.Title;
        }
    }
}
