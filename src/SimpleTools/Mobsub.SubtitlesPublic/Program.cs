using System;
using System.IO;
using Mobsub.SubtitlesPublic.Models;
using System.Text.Json;
using Mobsub.SubtitleProcess;

namespace Mobsub.SubtitlesPublic;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length != 3)
            return;
        
        var span = File.ReadAllBytes(args[0]).AsSpan();
        var config = JsonSerializer.Deserialize(span, BaseConfigContext.Default.BaseConfig);
        
        if (config == null)
            return;
        
        var gm = new GitManage(config);
        gm.Execute(args[1], args[2]).GetAwaiter().GetResult();

    }
}