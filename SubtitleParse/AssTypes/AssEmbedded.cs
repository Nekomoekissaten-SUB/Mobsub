using System.Text;
using Mobsub.SubtitleParse;

namespace Mobsub.AssTypes;

public class AssEmbeddedFont
{
    public string OriginalName = string.Empty;  // lowercase
    public bool Bold = false;
    public bool Italic = false;
    public int CharacterEncoding = 0;
    public readonly string Suffix = ".ttf";
    public List<string> Data = [];
    public int DataLength = 0;

    // public void EncodeFont(FileInfo file)
    // {
    //     if (!file.Exists)
    //     {
    //         throw new IOException($"{file.FullName} not exists");
    //     }
        
    //     // record font name?
    // }

    public void Write(StreamWriter sw, char[] newline)
    {
        var sb = new StringBuilder($"filename: {OriginalName}_");

        var info = new List<char>();
        if (Bold)
        {
            info.Add('B');
        }
        if (Italic)
        {
            info.Add('I');
        }
        info.Add((char)(CharacterEncoding + '0'));
        sb.Append(info);
        sw.Write($"filename: {sb}");
        sw.Write(newline);
        for (int i = 0; i < Data.Count; i++)
        {
            sw.Write(Data.ToArray()[i]);
            sw.Write(newline);
        }
    }
}

public class AssEmbeddedGraphic
{
    public string Name = string.Empty;  // lowercase
    public List<string> Data = [];
    public int DataLength = 0;

    public void EncodeGraphic(FileInfo file)
    {
        if (!file.Exists)
        {
            throw new IOException($"{file.FullName} not exists");
        }

        Name = file.Name;
        using var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
        var br = new BinaryReader(fs);
        AssEmbededParse.UUEncode(br, Data, ref DataLength);
        fs.Close();
    }

    public void Write(StreamWriter sw, char[] newline)
    {
        sw.Write($"filename: {Name}");
        sw.Write(newline);
        for (int i = 0; i < Data.Count; i++)
        {
            sw.Write(Data.ToArray()[i]);
            sw.Write(newline);
        }
    }
}
