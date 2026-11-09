using System.Text;
using Esatto.Umbraco.Backoffice.Seo.Knowledge;

namespace Esatto.Umbraco.Backoffice.Seo.Reporting;

/// <summary>The Issues list as CSV, one row per affected page. RFC 4180 quoting; UTF-8 BOM so Excel reads å/ä/ö.</summary>
public static class CsvWriter
{
    public static string Write(IEnumerable<IssueGroup> groups, KnowledgeBase knowledge)
    {
        var csv = new StringBuilder("﻿");
        Row(csv, "Check", "Severity", "Owner", "Page", "Language", "URL", "Value");
        foreach (var g in groups)
        foreach (var o in g.Occurrences)
        {
            Row(csv, knowledge.Get(g.Summary.CheckId).Title, g.Summary.Severity.ToString(), g.Summary.Owner.ToString(),
                o.Name, o.Culture, o.Url ?? string.Empty, o.Value ?? string.Empty);
        }

        return csv.ToString();
    }

    private static void Row(StringBuilder csv, params string[] fields) => csv.Append(string.Join(',', fields.Select(Field))).Append("\r\n");

    private static string Field(string value)
    {
        // Page names and values come from editors and scanned pages: a leading = + - @ (or tab/CR) would make
        // Excel run it as a formula, so it's prefixed with ' (OWASP "CSV injection").
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
