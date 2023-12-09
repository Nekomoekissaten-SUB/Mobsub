using System.Text.Json;

namespace Mobsub.ZhConvert;

public partial class OpenCCSharpUtils
{
    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    public static List<string?[]> LoadJson(FileInfo file)
    {
        var dir = file.Directory ?? throw new FileNotFoundException(file.Name);
        ReadOnlySpan<byte> jsonData = File.ReadAllBytes(file.FullName);

        if (jsonData.StartsWith(Utf8Bom))
        {
            jsonData = jsonData[Utf8Bom.Length..];
        }

        var reader = new Utf8JsonReader(jsonData);
        var dict = false;
        List<string?[]> dictionaries = [];
        List<string?> dictFile = [];

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    if (reader.ValueTextEquals("Dictionaries"u8))
                    {
                        dict = true;
                    }
                    break;
                case JsonTokenType.StartArray:
                    if (dict)
                    {
                        reader.Read();
                        dictFile.Add(ToAbsolutepath(reader.GetString(), dir));
                    }
                    break;
                case JsonTokenType.String:
                    if (dict)
                    {
                        dictFile.Add(ToAbsolutepath(reader.GetString(), dir));
                    }
                    break;
                case JsonTokenType.EndArray:
                    if (dict)
                    {
                        dict = false;
                        dictionaries.Add(dictFile.ToArray());
                        dictFile.Clear();
                    }
                    dict = !dict && dict;
                    break;
            }
        }

        return dictionaries;
    }

    private static string? ToAbsolutepath(string? p, DirectoryInfo jsonDir)
    {
        if (p is null)
        {
            return null;
        }

        if (Path.IsPathRooted(p))
        {
            return p;
        }
        else
        {
            return Path.Combine(jsonDir.FullName, p);
        }
    }

}
