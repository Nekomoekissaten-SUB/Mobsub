using Mobsub.SubtitleParse.AssTypes;
using Mobsub.Helper.ZhConvert;

namespace Mobsub.SubtitleProcess;

public class ConvertFanhuaji(FanhuajiOptions options)
{
    private readonly FanhuajiOptions _options = options;

    public async Task ConvertEventsAsync(IList<AssEvent> events, FanhuajiClient client, CancellationToken ct = default)
    {
        // 1. Filter events and prepare payload
        var eventsToConvert = new List<int>(); // Store indices
        var tagStorage = new List<List<ReadOnlyMemory<char>>>();

        // Direct string list - avoid unnecessary byte[] encoding/decoding
        var pureTextPayloads = new List<string>();

        // Track which events have empty text (need special handling)
        var emptyTextIndices = new HashSet<int>();

        for (int i = 0; i < events.Count; i++)
        {
            var evt = events[i];
            if (ConvertSimplifiedChinese.NotZhConvert(evt)) continue;

            eventsToConvert.Add(i);

            // Replace tags with placeholders
            var (textWithPlaceholders, tags) = ReplaceTags(evt.Text);

            // Store tags for later restoration
            tagStorage.Add(tags);

            // IMPORTANT: If the pure text is empty (only contained tags), 
            // use a special placeholder to prevent Fanhuaji from merging consecutive empty lines.
            // This can happen when a line only has override tags like {\fad(100,100)}
            if (string.IsNullOrEmpty(textWithPlaceholders))
            {
                emptyTextIndices.Add(eventsToConvert.Count - 1);
                pureTextPayloads.Add("{{EMPTY}}");
            }
            else
            {
                pureTextPayloads.Add(textWithPlaceholders);
            }
        }

        if (eventsToConvert.Count == 0) return;

        // 2. Call API with string list directly
        var convertedText = await client.ConvertAsync(pureTextPayloads, _options, ct);

        // 3. Process result using Span-based splitting to minimize allocations
        var textSpan = convertedText.AsSpan();

        // Count lines and validate
        int lineCount = 1;
        foreach (var c in textSpan)
        {
            if (c == '\n') lineCount++;
        }

        // Handle potential trailing newline
        if (textSpan.Length > 0 && textSpan[^1] == '\n')
        {
            lineCount--;
        }

        // Normalize \r\n in span if present
        bool hasCarriageReturn = textSpan.Contains('\r');

        if (lineCount != eventsToConvert.Count)
        {
            Console.Error.WriteLine($"[Error] Event count mismatch! Sent {eventsToConvert.Count}, received {lineCount}. Skipped update to prevent data corruption.");
            return;
        }

        // Process lines using span enumeration
        int lineIndex = 0;
        int start = 0;
        for (int pos = 0; pos <= textSpan.Length; pos++)
        {
            bool isEnd = pos == textSpan.Length;
            bool isNewline = !isEnd && textSpan[pos] == '\n';

            if (isEnd || isNewline)
            {
                int end = pos;
                // Handle \r\n
                if (hasCarriageReturn && end > start && textSpan[end - 1] == '\r')
                {
                    end--;
                }

                var lineSpan = textSpan[start..end];

                if (lineIndex < eventsToConvert.Count)
                {
                    int index = eventsToConvert[lineIndex];
                    var evt = events[index];
                    var tags = tagStorage[lineIndex];

                    string lineStr;
                    // Check if this was an empty text line - restore empty string
                    if (emptyTextIndices.Contains(lineIndex))
                    {
                        // Original was empty, just use empty string (tags will be restored)
                        lineStr = string.Empty;
                    }
                    else
                    {
                        lineStr = lineSpan.ToString();
                    }

                    // Restore tags from placeholders
                    var restored = RestoreTags(lineStr, tags);

                    evt.Text = restored;

                    // Apply style conversion (CHS->CHT etc)
                    ConvertSimplifiedChinese.StyleZhConvert(ref evt);

                    events[index] = evt;
                }

                lineIndex++;
                start = pos + 1;
            }
        }
    }

    private static (string TextWithPlaceholders, List<ReadOnlyMemory<char>> Tags) ReplaceTags(string text)
    {
        // Optimization: Use ReadOnlyMemory<char> to avoid allocating new strings for tags.
        // The original 'text' string must be kept alive during tag restoration.

        var sb = new System.Text.StringBuilder(text.Length);
        var tags = new List<ReadOnlyMemory<char>>();
        var span = text.AsSpan();
        int i = 0;
        int tagCounter = 0;

        while (i < span.Length)
        {
            // Check for ASS tag { ... }
            if (span[i] == '{')
            {
                // Use span.Slice().IndexOf() instead of string.IndexOf()
                int relativeEnd = span[(i + 1)..].IndexOf('}');
                if (relativeEnd != -1)
                {
                    int end = i + 1 + relativeEnd;
                    // Found a tag. Store as slice.
                    tags.Add(text.AsMemory(i, end - i + 1));

                    // Append placeholder {{Tx}}
                    sb.Append("{{T");
                    sb.Append(tagCounter);
                    sb.Append("}}");

                    tagCounter++;
                    i = end + 1;
                    continue;
                }
            }

            // Check for \N, \n, \h
            if (span[i] == '\\' && i + 1 < span.Length)
            {
                char next = span[i + 1];
                if (next == 'N' || next == 'n' || next == 'h')
                {
                    tags.Add(text.AsMemory(i, 2));

                    sb.Append("{{T");
                    sb.Append(tagCounter);
                    sb.Append("}}");

                    tagCounter++;
                    i += 2;
                    continue;
                }
            }

            sb.Append(span[i]);
            i++;
        }

        return (sb.ToString(), tags);
    }

    private static string RestoreTags(string convertedText, List<ReadOnlyMemory<char>> tags)
    {
        var sb = new System.Text.StringBuilder(convertedText.Length + tags.Count * 10);
        var span = convertedText.AsSpan();
        int i = 0;

        // Use span for search to avoid substring allocs
        while (i < span.Length)
        {
            // Look for placeholder {{T...}}
            // Check starts with {{T
            if (i + 3 < span.Length && span[i] == '{' && span[i + 1] == '{' && span[i + 2] == 'T')
            {
                // Use span to find "}}" instead of string.IndexOf
                int end = -1;
                for (int j = i + 3; j < span.Length - 1; j++)
                {
                    if (span[j] == '}' && span[j + 1] == '}')
                    {
                        end = j;
                        break;
                    }
                }

                if (end != -1)
                {
                    // Parse index from span slice: i+3 to end
                    var numSlice = span[(i + 3)..end];

                    if (int.TryParse(numSlice, out int tagIdx))
                    {
                        if (tagIdx >= 0 && tagIdx < tags.Count)
                        {
                            var tagMem = tags[tagIdx];
                            // Append the tag content
                            sb.Append(tagMem.Span);
                        }
                        else
                        {
                            // Index out of range? Keep placeholder
                            sb.Append("{{T");
                            sb.Append(tagIdx);
                            sb.Append("}}");
                        }
                    }
                    else
                    {
                        // Not a number? Append raw chars
                        sb.Append(span.Slice(i, end - i + 2));
                    }
                    i = end + 2;
                    continue;
                }
            }
            sb.Append(span[i]);
            i++;
        }
        return sb.ToString();
    }
}
