using System.Net.Http.Json;
using System.Text.Json;
using System.Text;

namespace Mobsub.Helper.ZhConvert;

public class FanhuajiClient(HttpClient client)
{
    // Use ReadOnlySpan<byte> properties for zero allocation
    private static ReadOnlySpan<byte> TextProp => "text"u8;
    private static ReadOnlySpan<byte> ConverterProp => "converter"u8;
    private static ReadOnlySpan<byte> UserProtectProp => "userProtect"u8;
    private static ReadOnlySpan<byte> UserPostReplaceProp => "userPostReplace"u8;
    private static ReadOnlySpan<byte> UserPreReplaceProp => "userPreReplace"u8;
    private static ReadOnlySpan<byte> JpTextStylesProp => "jpTextStyles"u8;
    private static ReadOnlySpan<byte> JpStrategyProp => "jpStrategy"u8;
    private static ReadOnlySpan<byte> PrettifyProp => "prettify"u8;
    private static ReadOnlySpan<byte> ApiKeyProp => "apiKey"u8;

    /// <summary>
    /// Convert text lines using Fanhuaji API.
    /// Accepts string directly to avoid unnecessary byte[] encoding/decoding overhead.
    /// </summary>
    public async Task<string> ConvertAsync(IEnumerable<string> textLines, FanhuajiOptions options, CancellationToken ct = default)
    {
        // Write JSON directly to stream, avoiding intermediate string allocation for the entire payload

        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();

            // Calculate approximate total length for StringBuilder capacity
            int totalLen = 0;
            foreach (var line in textLines)
            {
                totalLen += line.Length + 1; // +1 for \n separator
            }
            if (totalLen > 0) totalLen--; // remove last \n

            // Build joined text with single StringBuilder allocation
            var sb = new StringBuilder(totalLen);
            bool first = true;
            foreach (var line in textLines)
            {
                if (!first) sb.Append('\n');
                sb.Append(line);
                first = false;
            }
            writer.WriteString(TextProp, sb.ToString());

            // Write other options
            writer.WriteString(ConverterProp, options.Converter);

            // Handle UserProtect (merge list and string)
            var protectList = options.IgnoreTextPatterns;
            var protectStr = options.UserProtect;
            if (protectList.Count > 0 || !string.IsNullOrEmpty(protectStr))
            {
                var sbProtect = new StringBuilder();
                if (!string.IsNullOrEmpty(protectStr))
                {
                    sbProtect.Append(protectStr);
                }

                if (protectList.Count > 0)
                {
                    if (sbProtect.Length > 0 && protectStr != null && !protectStr.EndsWith('\n'))
                    {
                        sbProtect.Append('\n');
                    }
                    foreach (var p in protectList)
                    {
                        sbProtect.Append(p).Append('\n');
                    }
                    // remove last newline if added from loop? Fanhuaji tolerates it? 
                    // safer to trim end

                }
                writer.WriteString(UserProtectProp, sbProtect.ToString().TrimEnd('\n'));
            }

            if (!string.IsNullOrEmpty(options.UserPostReplace))
            {
                writer.WriteString(UserPostReplaceProp, options.UserPostReplace);
            }

            if (!string.IsNullOrEmpty(options.UserPreReplace))
            {
                writer.WriteString(UserPreReplaceProp, options.UserPreReplace);
            }

            if (!string.IsNullOrEmpty(options.JpTextStyles))
            {
                writer.WriteString(JpTextStylesProp, options.JpTextStyles);
            }

            if (!string.IsNullOrEmpty(options.JpStrategy))
            {
                writer.WriteString(JpStrategyProp, options.JpStrategy);
            }

            writer.WriteNumber(PrettifyProp, 0); // Always 0 for subtitle processing fidelity

            if (!string.IsNullOrEmpty(options.ApiKey))
            {
                writer.WriteString(ApiKeyProp, options.ApiKey);
            }

            writer.WriteEndObject();
        }

        ms.Position = 0;
        var content = new StreamContent(ms);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var response = await client.PostAsync("https://api.zhconvert.org/convert", content, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(FanhuajiJsonContext.Default.FanhuajiResponse, ct);

        if (result is null) throw new Exception("Empty response from Fanhuaji API");
        if (result.Code != 0) throw new Exception($"Fanhuaji API Error {result.Code}: {result.Msg}");

        return result.Data?.Text ?? string.Empty;
    }
}
