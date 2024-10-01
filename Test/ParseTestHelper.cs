using System.Text;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse.AssUtils;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Mobsub.Test;

public partial class ParseTest
{
    private readonly string[] styles =
    [
        @"Style: Sign,Source Han Sans SC Medium,70,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0.1,0,1,3,0,2,30,30,30,1",
    ];
    
    private AssData GenerateAssData(string[] evts)
    {
        return new AssData()
        {
            ScriptInfo = GetScriptInfo(),
            Styles = ParseAssStyles(styles),
            Events = ParseAssEvents(evts),
        };
    }

    private static AssScriptInfo GetScriptInfo() => new AssScriptInfo() { ScriptType = "v4.00+" };
    private static AssStyles ParseAssStyles(string[] stylesStr)
    {
        var assStyles = new AssStyles();
        var lineNumber = 0;
        foreach (var str in stylesStr)
        {
            assStyles.Read(str, lineNumber);
            lineNumber += 1;
        }

        return assStyles;
    }
    private static AssEvents ParseAssEvents(string[] eventsStr)
    {
        var assEvents = new AssEvents();
        var lineNumber = 0;
        foreach (var str in eventsStr)
        {
            assEvents.Read(str, "v4.00++", lineNumber);
            lineNumber += 1;
        }

        return assEvents;
    }
    
    private static ILogger GetLogger(LogLevel logLevel)
    {
        using var factory = LoggerFactory.Create(logging =>
        {
            logging.SetMinimumLevel(logLevel);
            logging.AddZLoggerConsole(options =>
            {
                options.UsePlainTextFormatter(formatter =>
                {
                    formatter.SetPrefixFormatter($"{0}{1:yyyy-MM-dd'T'HH:mm:sszzz}|{2:short}|", (in MessageTemplate template, in LogInfo info) => 
                    {
                        // \u001b[31m => Red(ANSI Escape Code)
                        // \u001b[0m => Reset
                        var escapeSequence = info.LogLevel switch
                        {
                            LogLevel.Warning => "\u001b[33m",
                            > LogLevel.Warning => "\u001b[31m",
                            _ => "\u001b[0m",
                        };

                        template.Format(escapeSequence, info.Timestamp, info.LogLevel);
                    });
                });
                options.LogToStandardErrorThreshold = LogLevel.Warning;
            });
        });
        return factory.CreateLogger("Mobsub.Test");
    }
}