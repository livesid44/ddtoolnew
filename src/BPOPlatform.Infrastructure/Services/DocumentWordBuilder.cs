using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace BPOPlatform.Infrastructure.Services;

/// <summary>
/// Utility class that converts a Markdown string to a <c>.docx</c> Word document.
/// Uses the OpenXML SDK — no Office installation required.
/// </summary>
internal static class DocumentWordBuilder
{
    public static Domain.Interfaces.DocumentGenerationResult Build(string markdownContent, string processName)
    {
        using var ms = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document(BuildBody(markdownContent, processName));
            mainPart.Document.Save();
        }

        var safeName = string.Join("_", processName.Split(Path.GetInvalidFileNameChars()));
        return new Domain.Interfaces.DocumentGenerationResult(
            $"{safeName}_Report.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ms.ToArray());
    }

    private static Body BuildBody(string markdown, string processName)
    {
        var body = new Body();

        // Document title
        body.Append(CreateHeading(processName + " – Process Discovery Report", HeadingLevel.Title));

        // Parse each Markdown line and convert to OpenXML
        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.TrimEnd();
            if (trimmed.StartsWith("## "))
                body.Append(CreateHeading(trimmed[3..], HeadingLevel.H2));
            else if (trimmed.StartsWith("# "))
                body.Append(CreateHeading(trimmed[2..], HeadingLevel.H1));
            else if (trimmed.StartsWith("- "))
                body.Append(CreateBulletParagraph(trimmed[2..]));
            else if (string.IsNullOrWhiteSpace(trimmed))
                body.Append(new Paragraph());
            else
                body.Append(CreateParagraph(trimmed));
        }

        body.Append(new SectionProperties());
        return body;
    }

    private enum HeadingLevel { Title, H1, H2 }

    private static Paragraph CreateHeading(string text, HeadingLevel level)
    {
        var styleId = level switch
        {
            HeadingLevel.Title => "Title",
            HeadingLevel.H1 => "Heading1",
            _ => "Heading2"
        };
        return new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = styleId }),
            new Run(new Text(text)));
    }

    private static Paragraph CreateParagraph(string text) =>
        new(new Run(new Text(text)));

    private static Paragraph CreateBulletParagraph(string text) =>
        new(
            new ParagraphProperties(
                new NumberingProperties(
                    new NumberingLevelReference { Val = 0 },
                    new NumberingId { Val = 1 })),
            new Run(new Text("• " + text)));
}
