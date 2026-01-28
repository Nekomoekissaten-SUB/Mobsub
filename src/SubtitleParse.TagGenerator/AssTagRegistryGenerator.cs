using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Mobsub.SubtitleParse.TagGenerator;

[Generator(LanguageNames.CSharp)]
public sealed class AssTagRegistryGenerator : IIncrementalGenerator
{
    private const string TagEnumFullName = "Mobsub.SubtitleParse.AssTypes.AssTag";
    private const string TagSpecAttributeFullName = "Mobsub.SubtitleParse.AssTypes.AssTagSpecAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var tagEnumProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is EnumDeclarationSyntax,
                transform: static (ctx, _) =>
                {
                    var enumDecl = (EnumDeclarationSyntax)ctx.Node;
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(enumDecl) as INamedTypeSymbol;
                    if (symbol is null)
                        return null;
                    return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::" + TagEnumFullName
                        ? symbol
                        : null;
                })
            .Where(static s => s is not null)!;

        var combined = context.CompilationProvider.Combine(tagEnumProvider.Collect());

        context.RegisterSourceOutput(combined, static (spc, pair) =>
        {
            var (compilation, enums) = pair;
            if (enums.IsDefaultOrEmpty)
                return;

            var tagEnum = enums[0];
            if (tagEnum is null)
                return;
            var attrSymbol = compilation.GetTypeByMetadataName(TagSpecAttributeFullName);
            if (attrSymbol is null)
                return;

            var specs = ReadSpecs(tagEnum, attrSymbol, spc);
            if (specs is null)
                return;

            var source = GenerateRegistrySource(specs.Value);
            spc.AddSource("AssTagRegistry.Generated.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private readonly struct TagSpec
    {
        public TagSpec(int tagValue, string name, ITypeSymbol valueType, int tagKindValue, int functionKindValue)
        {
            TagValue = tagValue;
            Name = name;
            ValueType = valueType;
            TagKindValue = tagKindValue;
            FunctionKindValue = functionKindValue;
        }

        public int TagValue { get; }
        public string Name { get; }
        public ITypeSymbol ValueType { get; }
        public int TagKindValue { get; }
        public int FunctionKindValue { get; }
    }

    private static ImmutableArray<TagSpec>? ReadSpecs(INamedTypeSymbol tagEnum, INamedTypeSymbol attrSymbol, SourceProductionContext spc)
    {
        var members = tagEnum.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).ToArray();
        var specs = ImmutableArray.CreateBuilder<TagSpec>(members.Length);

        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in members)
        {
            var attrs = member.GetAttributes();
            var specAttr = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrSymbol));
            if (specAttr is null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "MSP001",
                        title: "Missing AssTagSpecAttribute",
                        messageFormat: $"AssTag member '{{0}}' is missing [AssTagSpec(...)]",
                        category: "Mobsub.SubtitleParse.TagGenerator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    member.Locations.FirstOrDefault(),
                    member.Name));
                return null;
            }

            if (specAttr.ConstructorArguments.Length < 3)
                return null;

            string? name = specAttr.ConstructorArguments[0].Value as string;
            var valueType = specAttr.ConstructorArguments[1].Value as ITypeSymbol;
            if (name is null || valueType is null)
                return null;

            if (!IsAscii(name))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "MSP002",
                        title: "Non-ASCII tag name",
                        messageFormat: $"AssTag member '{{0}}' has non-ASCII tag name '{{1}}'",
                        category: "Mobsub.SubtitleParse.TagGenerator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    member.Locations.FirstOrDefault(),
                    member.Name,
                    name));
                return null;
            }

            if (!usedNames.Add(name))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "MSP003",
                        title: "Duplicate tag name",
                        messageFormat: $"Duplicate tag name '{{0}}' across AssTag members",
                        category: "Mobsub.SubtitleParse.TagGenerator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    member.Locations.FirstOrDefault(),
                    name));
                return null;
            }

            int tagValue = Convert.ToInt32(member.ConstantValue);
            int tagKindValue = Convert.ToInt32(specAttr.ConstructorArguments[2].Value);
            int functionKindValue = 0;
            if (specAttr.ConstructorArguments.Length >= 4 && specAttr.ConstructorArguments[3].Value is not null)
                functionKindValue = Convert.ToInt32(specAttr.ConstructorArguments[3].Value);

            specs.Add(new TagSpec(tagValue, name, valueType, tagKindValue, functionKindValue));
        }

        var arr = specs.ToArray();
        Array.Sort(arr, static (a, b) => a.TagValue.CompareTo(b.TagValue));
        return arr.ToImmutableArray();
    }

    private static bool IsAscii(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] > 0x7F)
                return false;
        }
        return true;
    }

    private sealed class TrieNode
    {
        public readonly Dictionary<byte, TrieNode> Children = new();
        public int TerminalTagValue = -1;
    }

    private static string GenerateRegistrySource(ImmutableArray<TagSpec> specs)
    {
        var trie = new TrieNode();
        foreach (var spec in specs)
        {
            var bytes = Encoding.ASCII.GetBytes(spec.Name);
            var node = trie;
            for (int i = 0; i < bytes.Length; i++)
            {
                var b = bytes[i];
                if (!node.Children.TryGetValue(b, out var next))
                {
                    next = new TrieNode();
                    node.Children.Add(b, next);
                }
                node = next;
            }
            node.TerminalTagValue = spec.TagValue;
        }

        var nodes = new List<TrieNode>(capacity: 128) { trie };
        var nodeIndex = new Dictionary<TrieNode, int>(ReferenceEqualityComparer<TrieNode>.Instance) { [trie] = 0 };

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            foreach (var kv in node.Children.OrderBy(k => k.Key))
            {
                if (nodeIndex.ContainsKey(kv.Value))
                    continue;
                nodeIndex[kv.Value] = nodes.Count;
                nodes.Add(kv.Value);
            }
        }

        var edgeChars = new List<byte>(capacity: 256);
        var edgeNext = new List<ushort>(capacity: 256);
        var nodeEdgeStart = new int[nodes.Count];
        var nodeEdgeCount = new byte[nodes.Count];
        var terminalTag = new int[nodes.Count];

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            terminalTag[i] = node.TerminalTagValue;

            nodeEdgeStart[i] = edgeChars.Count;
            var edges = node.Children.OrderBy(k => k.Key).ToArray();
            nodeEdgeCount[i] = checked((byte)edges.Length);
            foreach (var kv in edges)
            {
                edgeChars.Add(kv.Key);
                edgeNext.Add(checked((ushort)nodeIndex[kv.Value]));
            }
        }

        int maxTagValue = specs.Length == 0 ? 0 : specs.Max(s => s.TagValue);
        var specByValue = new Dictionary<int, TagSpec>(capacity: specs.Length);
        for (int i = 0; i < specs.Length; i++)
            specByValue[specs[i].TagValue] = specs[i];

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("namespace Mobsub.SubtitleParse.AssTypes;");
        sb.AppendLine();
        sb.AppendLine("public static partial class AssTagRegistry");
        sb.AppendLine("{");

        sb.AppendLine("    private static readonly AssTagDescriptor?[] s_descByTag = new AssTagDescriptor?[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec))
            {
                sb.AppendLine("        null,");
                continue;
            }

            sb.Append("        new AssTagDescriptor(");
            sb.Append(EmitAsciiByteArray(spec.Name));
            sb.Append(", typeof(");
            sb.Append(spec.ValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            sb.Append("), unchecked((AssTagKind)");
            sb.Append(spec.TagKindValue);
            sb.Append(")),");
            sb.AppendLine();
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly byte[] s_functionKindByTag = new byte[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec))
            {
                sb.AppendLine("        0,");
                continue;
            }
            sb.AppendLine($"        {spec.FunctionKindValue},");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly int[] s_nodeEdgeStart = new int[]");
        sb.AppendLine("    {");
        for (int i = 0; i < nodeEdgeStart.Length; i++)
            sb.AppendLine($"        {nodeEdgeStart[i]},");
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly byte[] s_nodeEdgeCount = new byte[]");
        sb.AppendLine("    {");
        for (int i = 0; i < nodeEdgeCount.Length; i++)
            sb.AppendLine($"        {nodeEdgeCount[i]},");
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly byte[] s_edgeChar = new byte[]");
        sb.AppendLine("    {");
        for (int i = 0; i < edgeChars.Count; i++)
            sb.AppendLine($"        {edgeChars[i]},");
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly ushort[] s_edgeNext = new ushort[]");
        sb.AppendLine("    {");
        for (int i = 0; i < edgeNext.Count; i++)
            sb.AppendLine($"        {edgeNext[i]},");
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly int[] s_terminalTag = new int[]");
        sb.AppendLine("    {");
        for (int i = 0; i < terminalTag.Length; i++)
            sb.AppendLine($"        {terminalTag[i]},");
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static int FindNext(int node, byte b)");
        sb.AppendLine("    {");
        sb.AppendLine("        int start = s_nodeEdgeStart[node];");
        sb.AppendLine("        int count = s_nodeEdgeCount[node];");
        sb.AppendLine("        for (int i = 0; i < count; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (s_edgeChar[start + i] == b)");
        sb.AppendLine("                return s_edgeNext[start + i];");
        sb.AppendLine("        }");
        sb.AppendLine("        return -1;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string EmitAsciiByteArray(string s)
    {
        var bytes = Encoding.ASCII.GetBytes(s);
        var sb = new StringBuilder();
        sb.Append("new byte[] { ");
        for (int i = 0; i < bytes.Length; i++)
        {
            if (i != 0) sb.Append(", ");
            sb.Append(bytes[i]);
        }
        sb.Append(" }");
        return sb.ToString();
    }

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Instance = new();
        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
