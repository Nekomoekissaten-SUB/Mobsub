using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutomationBridgeProtocolGen;

internal static class Program
{
    private const string DefaultSpecPath = "src/SimpleTools/AutomationBridge/Protocol/bridge_calls_spec.json";

    private const string DefaultScriptCatalogPath = "src/SimpleTools/AutomationBridge/Scripts/BridgeScriptCatalog.Generated.cs";
    private const string DefaultCallUnionPath = "src/SimpleTools/AutomationBridge/Protocol/BridgeCallUnion.Generated.cs";

    private const string AutomationBridgeProjectPath = "src/SimpleTools/AutomationBridge/Mobsub.AutomationBridge.csproj";
    private const string AutomationBridgeAssemblyName = "Mobsub.AutomationBridge";

    public static int Main(string[] args)
    {
        try
        {
            string repoRoot = FindRepoRoot(Environment.CurrentDirectory)
                ?? throw new InvalidOperationException("Repo root not found (expected .git or Mobsub.slnx).");

            string specPath = args.Length >= 1 && !string.IsNullOrWhiteSpace(args[0])
                ? args[0]
                : DefaultSpecPath;

            string specFullPath = Path.GetFullPath(Path.Combine(repoRoot, specPath));
            if (!File.Exists(specFullPath))
                throw new FileNotFoundException("Spec file not found.", specFullPath);

            var specJson = File.ReadAllText(specFullPath, Encoding.UTF8);
            var spec = JsonSerializer.Deserialize<BridgeCallsSpec>(specJson, JsonOptions)
                ?? throw new InvalidOperationException("Failed to parse spec (null).");

            ValidateSpec(spec);

            WriteGeneratedFile(repoRoot, DefaultCallUnionPath, CsharpCallsEmitter.EmitCallUnion(spec));
            WriteGeneratedFile(repoRoot, DefaultScriptCatalogPath, CsharpScriptsEmitter.EmitBridgeScriptCatalog(spec));

            if (spec.Lua is not null)
            {
                string bridgeProjPath = Path.GetFullPath(Path.Combine(repoRoot, AutomationBridgeProjectPath));
                BuildProject(repoRoot, bridgeProjPath, configuration: "Release");

                string bridgeAssemblyPath = FindBuiltAssembly(
                    projectDir: Path.GetDirectoryName(bridgeProjPath) ?? repoRoot,
                    configuration: "Release",
                    assemblyName: AutomationBridgeAssemblyName);

                var asm = Assembly.LoadFrom(bridgeAssemblyPath);

                string luaSource = LuaEmitterV2.EmitProtocolFile(spec, asm, specFileName: Path.GetFileName(specFullPath));

                string luaTargetPath = Path.GetFullPath(Path.Combine(repoRoot, spec.Lua.TargetPath));
                EnsureDirForFile(luaTargetPath);
                WriteTextFile(luaTargetPath, luaSource);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static void ValidateSpec(BridgeCallsSpec spec)
    {
        if (spec.SchemaVersion <= 0)
            throw new InvalidOperationException("schema_version must be > 0.");

        if (spec.Calls.Length == 0)
            throw new InvalidOperationException("calls must be non-empty.");

        var kindSet = new HashSet<int>();
        var methodSet = new HashSet<string>(StringComparer.Ordinal);

        foreach (var call in spec.Calls)
        {
            if (string.IsNullOrWhiteSpace(call.Method))
                throw new InvalidOperationException("call.method must be non-empty.");
            if (string.IsNullOrWhiteSpace(call.CallType))
                throw new InvalidOperationException($"call.call_type must be non-empty (method={call.Method}).");
            if (!kindSet.Add(call.Kind))
                throw new InvalidOperationException($"Duplicate call.kind: {call.Kind}.");
            if (!methodSet.Add(call.Method))
                throw new InvalidOperationException($"Duplicate call.method: {call.Method}.");
        }
    }

    private static void BuildProject(string repoRoot, string projectPath, string configuration)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{projectPath}\" -c {configuration} /p:CopyLocalLockFileAssemblies=true",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start dotnet build.");

        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();

        proc.WaitForExit();

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"dotnet build failed (exit {proc.ExitCode}).\n{stdout}\n{stderr}");
    }

    private static string FindBuiltAssembly(string projectDir, string configuration, string assemblyName)
    {
        string binDir = Path.Combine(projectDir, "bin", configuration);
        if (!Directory.Exists(binDir))
            throw new DirectoryNotFoundException($"Build output not found: {binDir}");

        string fileName = assemblyName + ".dll";
        var candidates = Directory.GetFiles(binDir, fileName, SearchOption.AllDirectories);
        if (candidates.Length == 0)
            throw new FileNotFoundException("Built assembly not found.", Path.Combine(binDir, fileName));

        static bool IsPreferred(string p)
            => p.Contains($"{Path.DirectorySeparatorChar}net10.0{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !p.Contains($"{Path.DirectorySeparatorChar}publish{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

        return candidates.FirstOrDefault(IsPreferred) ?? candidates[0];
    }

    private static void WriteGeneratedFile(string repoRoot, string relativePath, string source)
    {
        string fullPath = Path.GetFullPath(Path.Combine(repoRoot, relativePath));
        EnsureDirForFile(fullPath);
        WriteTextFile(fullPath, source);
    }

    private static void EnsureDirForFile(string fullPath)
    {
        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    private static void WriteTextFile(string fullPath, string source)
        => File.WriteAllText(fullPath, source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) || File.Exists(Path.Combine(dir.FullName, "Mobsub.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

internal sealed class BridgeCallsSpec
{
    public required int SchemaVersion { get; init; }
    public required BridgeCallSpec[] Calls { get; init; }
    public LuaSpec? Lua { get; init; }
}

internal sealed class BridgeCallSpec
{
    public required int Kind { get; init; }
    public required string Method { get; init; }
    public required string CallType { get; init; }
    public string? Description { get; init; }
}

internal sealed class LuaSpec
{
    public required string TargetPath { get; init; }
}

internal static class CsharpCallsEmitter
{
    public static string EmitCallUnion(BridgeCallsSpec spec)
    {
        var sb = new StringBuilder(capacity: 2 * 1024);
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by tools/AutomationBridgeProtocolGen from bridge_calls_spec.json");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using MessagePack;");
        sb.AppendLine();
        sb.AppendLine("namespace Mobsub.AutomationBridge.Protocol;");
        sb.AppendLine();

        foreach (var call in spec.Calls.OrderBy(static c => c.Kind))
            sb.Append("[Union(").Append(call.Kind).Append(", typeof(").Append(call.CallType).AppendLine("))]");

        sb.AppendLine("internal interface IBridgeCall;");
        return sb.ToString();
    }
}

internal static class CsharpScriptsEmitter
{
    public static string EmitBridgeScriptCatalog(BridgeCallsSpec spec)
    {
        var sb = new StringBuilder(capacity: 4 * 1024);
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by tools/AutomationBridgeProtocolGen from bridge_calls_spec.json");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Mobsub.AutomationBridge.Protocol;");
        sb.AppendLine("using Mobsub.AutomationBridge.Scripts.Abstractions;");
        sb.AppendLine();
        sb.AppendLine("namespace Mobsub.AutomationBridge.Scripts;");
        sb.AppendLine();
        sb.AppendLine("internal static partial class BridgeScriptCatalog");
        sb.AppendLine("{");
        sb.AppendLine("    private static partial IBridgeCallHandler[] CreateHandlers()");
        sb.AppendLine("        =>");
        sb.AppendLine("        [");

        foreach (var call in spec.Calls)
        {
            string handlerExpr = call.Method switch
            {
                "ping" => "HandlePing",
                "list_methods" => "HandleListMethods",
                _ => GetConventionHandlerExpression(call),
            };

            string methodLit = EscapeCsharpString(call.Method);
            string descLit = EscapeCsharpString(call.Description ?? string.Empty);

            sb.AppendLine($"            new BridgeCallHandler<{call.CallType}>(");
            sb.AppendLine($"                new BridgeMethodInfo({methodLit}, {descLit}),");
            sb.AppendLine($"                {handlerExpr}),");
            sb.AppendLine();
        }

        sb.AppendLine("        ];");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GetConventionHandlerExpression(BridgeCallSpec call)
    {
        int dot = call.Method.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0)
            throw new InvalidOperationException($"Cannot infer handler namespace from method: {call.Method}.");

        string domain = call.Method[..dot];
        string domainPascal = ToPascalCase(domain);

        string handlerType = call.CallType.EndsWith("Call", StringComparison.Ordinal)
            ? call.CallType[..^4] + "Handler"
            : call.CallType + "Handler";

        return $"global::Mobsub.AutomationBridge.Scripts.{domainPascal}.{handlerType}.Handle";
    }

    private static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        // domain is expected to be simple ascii like "motion"/"drawing"/"perspective".
        if (s.Length == 1)
            return s.ToUpperInvariant();

        return char.ToUpperInvariant(s[0]) + s[1..];
    }

    private static string EscapeCsharpString(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        sb.Append('\"');
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c < ' ')
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        sb.Append('\"');
        return sb.ToString();
    }
}
