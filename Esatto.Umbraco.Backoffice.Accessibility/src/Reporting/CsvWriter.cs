using System.Text;

namespace Esatto.Umbraco.Backoffice.Accessibility.Reporting;

/// <summary>The Issues list as CSV, one row per affected element. RFC 4180 quoting; UTF-8 BOM so Excel reads å/ä/ö.</summary>
public static class CsvWriter
{
    public static string Write(IEnumerable<IssueGroup> groups)
    {
        var csv = new StringBuilder("﻿");
        Row(csv, "Rule", "Criteria", "Level", "Owner", "Impact", "Kind", "Page", "Url", "Selector", "Html");
        foreach (var g in groups)
        foreach (var page in g.Pages)
        foreach (var node in page.Nodes)
        {
            Row(csv, g.RuleId, string.Join(' ', g.Criteria), g.Level?.ToString() ?? string.Empty, g.Owner.ToString(),
                g.Impact ?? string.Empty, g.Kind, page.Name, page.Url, node.Selector, node.Html);
        }

        return csv.ToString();
    }

    private static void Row(StringBuilder csv, params string[] fields)
    {
        csv.Append(string.Join(',', fields.Select(Field))).Append("\r\n");
    }

    private static string Field(string value)
    {
        // Page names and HTML come from editors and scanned pages: a leading = + - @ (or tab/CR) would make
        // Excel run it as a formula, so it's prefixed with ' (OWASP "CSV injection").
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
