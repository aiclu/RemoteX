using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace _1RM.View.Host
{
    /// <summary>
    /// A read-only snapshot of the non-sensitive information available for a running session.
    /// </summary>
    public sealed class ConnectionInfoSnapshot
    {
        public ConnectionInfoSnapshot(
            string windowTitle,
            string summary,
            DateTimeOffset capturedAtUtc,
            IEnumerable<ConnectionInfoSection> sections)
        {
            WindowTitle = windowTitle;
            Summary = summary;
            CapturedAtUtc = capturedAtUtc;
            Sections = (sections ?? throw new ArgumentNullException(nameof(sections))).ToArray();
            CopyText = BuildCopyText();
        }

        public string WindowTitle { get; }
        public string Summary { get; }
        public DateTimeOffset CapturedAtUtc { get; }
        public IReadOnlyList<ConnectionInfoSection> Sections { get; }
        public string CopyText { get; }

        private string BuildCopyText()
        {
            var builder = new StringBuilder();
            builder.AppendLine(WindowTitle);
            if (!string.IsNullOrWhiteSpace(Summary))
            {
                builder.AppendLine(Summary);
                builder.AppendLine();
            }

            foreach (var section in Sections)
            {
                builder.AppendLine($"[{section.Title}]");
                foreach (var row in section.Rows)
                {
                    builder.AppendLine($"{row.Label}: {row.Value}");
                }

                builder.AppendLine();
            }

            return builder.ToString().TrimEnd();
        }
    }

    public sealed class ConnectionInfoSection
    {
        public ConnectionInfoSection(string title, IEnumerable<ConnectionInfoRow> rows)
        {
            Title = title;
            Rows = (rows ?? throw new ArgumentNullException(nameof(rows))).ToArray();
        }

        public string Title { get; }
        public IReadOnlyList<ConnectionInfoRow> Rows { get; }
    }

    public sealed class ConnectionInfoRow
    {
        public ConnectionInfoRow(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }
        public string Value { get; }
    }
}
