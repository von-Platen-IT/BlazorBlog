using Markdig;
using Microsoft.AspNetCore.Components;

namespace AspBaseProj.Presentation.Components.Shared;

public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()  // Tables, footnotes, task lists, etc.
        .UseAutoLinks()           // Auto-detect URLs
        .UseEmojiAndSmiley()      // :smile: support
        .UseSoftlineBreakAsHardlineBreak()
        .Build();

    public static MarkupString ToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return new MarkupString(string.Empty);

        var html = Markdown.ToHtml(markdown, Pipeline);
        return new MarkupString(html);
    }
}
