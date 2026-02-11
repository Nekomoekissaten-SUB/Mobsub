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

    private const byte AssTagValueKind_None = 0;
    private const byte AssTagValueKind_Int = 1;
    private const byte AssTagValueKind_Double = 2;
    private const byte AssTagValueKind_Bool = 3;
    private const byte AssTagValueKind_Byte = 4;
    private const byte AssTagValueKind_Color = 5;
    private const byte AssTagValueKind_Bytes = 6;
    private const byte AssTagValueKind_Function = 7;

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
        public TagSpec(
            int tagValue,
            string name,
            byte valueKind,
            int tagKindValue,
            int functionKindValue,
            byte isAlphaTag,
            string? obsoleteReplacementName,
            ulong intAllowedMask,
            string? intAllowedMaskDiagnosticCode,
            string? intAllowedMaskMessage,
            int intMin,
            int intMax,
            string? intRangeDiagnosticCode,
            string? intRangeMessage,
            double doubleMin,
            double doubleMax,
            string? doubleRangeDiagnosticCode,
            string? doubleRangeMessage)
        {
            TagValue = tagValue;
            Name = name;
            ValueKind = valueKind;
            TagKindValue = tagKindValue;
            FunctionKindValue = functionKindValue;
            IsAlphaTag = isAlphaTag;

            ObsoleteReplacementName = obsoleteReplacementName;

            IntAllowedMask = intAllowedMask;
            IntAllowedMaskDiagnosticCode = intAllowedMaskDiagnosticCode;
            IntAllowedMaskMessage = intAllowedMaskMessage;

            IntMin = intMin;
            IntMax = intMax;
            IntRangeDiagnosticCode = intRangeDiagnosticCode;
            IntRangeMessage = intRangeMessage;

            DoubleMin = doubleMin;
            DoubleMax = doubleMax;
            DoubleRangeDiagnosticCode = doubleRangeDiagnosticCode;
            DoubleRangeMessage = doubleRangeMessage;
        }

        public int TagValue { get; }
        public string Name { get; }
        public byte ValueKind { get; }
        public int TagKindValue { get; }
        public int FunctionKindValue { get; }
        public byte IsAlphaTag { get; }

        public string? ObsoleteReplacementName { get; }

        public ulong IntAllowedMask { get; }
        public string? IntAllowedMaskDiagnosticCode { get; }
        public string? IntAllowedMaskMessage { get; }

        public int IntMin { get; }
        public int IntMax { get; }
        public string? IntRangeDiagnosticCode { get; }
        public string? IntRangeMessage { get; }

        public double DoubleMin { get; }
        public double DoubleMax { get; }
        public string? DoubleRangeDiagnosticCode { get; }
        public string? DoubleRangeMessage { get; }
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
            if (name is null)
                return null;

            if (specAttr.ConstructorArguments[1].Value is null)
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

            byte valueKind = checked((byte)Convert.ToInt32(specAttr.ConstructorArguments[1].Value));
            if (valueKind > AssTagValueKind_Function)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "MSP004",
                        title: "Invalid AssTag value kind",
                        messageFormat: $"AssTag member '{{0}}' has invalid value kind '{{1}}'",
                        category: "Mobsub.SubtitleParse.TagGenerator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    member.Locations.FirstOrDefault(),
                    member.Name,
                    valueKind));
                return null;
            }

            int tagKindValue = Convert.ToInt32(specAttr.ConstructorArguments[2].Value);
            int functionKindValue = 0;
            if (specAttr.ConstructorArguments.Length >= 4 && specAttr.ConstructorArguments[3].Value is not null)
                functionKindValue = Convert.ToInt32(specAttr.ConstructorArguments[3].Value);

            int intMin = int.MinValue;
            int intMax = int.MaxValue;
            string? intRangeCode = null;
            string? intRangeMessage = null;

            string? obsoleteReplacementName = null;

            ulong intAllowedMask = 0;
            string? intAllowedMaskCode = null;
            string? intAllowedMaskMessage = null;

            double doubleMin = double.NaN;
            double doubleMax = double.NaN;
            string? doubleRangeCode = null;
            string? doubleRangeMessage = null;

            foreach (var kv in specAttr.NamedArguments)
            {
                switch (kv.Key)
                {
                    case "IntMin":
                        if (kv.Value.Value is int iv) intMin = iv;
                        break;
                    case "IntMax":
                        if (kv.Value.Value is int iax) intMax = iax;
                        break;
                    case "IntRangeDiagnosticCode":
                        intRangeCode = kv.Value.Value as string;
                        break;
                    case "IntRangeMessage":
                        intRangeMessage = kv.Value.Value as string;
                        break;
                    case "ObsoleteReplacementName":
                        obsoleteReplacementName = kv.Value.Value as string;
                        break;
                    case "IntAllowedMask":
                        if (kv.Value.Value is ulong ul)
                            intAllowedMask = ul;
                        else if (kv.Value.Value is long l)
                            intAllowedMask = unchecked((ulong)l);
                        break;
                    case "IntAllowedMaskDiagnosticCode":
                        intAllowedMaskCode = kv.Value.Value as string;
                        break;
                    case "IntAllowedMaskMessage":
                        intAllowedMaskMessage = kv.Value.Value as string;
                        break;
                    case "DoubleMin":
                        if (kv.Value.Value is double dv) doubleMin = dv;
                        break;
                    case "DoubleMax":
                        if (kv.Value.Value is double dax) doubleMax = dax;
                        break;
                    case "DoubleRangeDiagnosticCode":
                        doubleRangeCode = kv.Value.Value as string;
                        break;
                    case "DoubleRangeMessage":
                        doubleRangeMessage = kv.Value.Value as string;
                        break;
                }
            }

            if (functionKindValue != 0 && valueKind != AssTagValueKind_Function)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "MSP005",
                        title: "Function kind requires Function value kind",
                        messageFormat: $"AssTag member '{{0}}' has FunctionKind but ValueKind is not Function",
                        category: "Mobsub.SubtitleParse.TagGenerator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    member.Locations.FirstOrDefault(),
                    member.Name));
                return null;
            }

            if (intAllowedMask != 0 && valueKind is not (AssTagValueKind_Int or AssTagValueKind_Byte))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "MSP006",
                        title: "Allowed int mask requires Int or Byte value kind",
                        messageFormat: $"AssTag member '{{0}}' has IntAllowedMask but ValueKind is not Int/Byte",
                        category: "Mobsub.SubtitleParse.TagGenerator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    member.Locations.FirstOrDefault(),
                    member.Name));
                return null;
            }

            if (obsoleteReplacementName != null && !IsAscii(obsoleteReplacementName))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "MSP007",
                        title: "Non-ASCII obsolete replacement name",
                        messageFormat: $"AssTag member '{{0}}' has non-ASCII obsolete replacement name '{{1}}'",
                        category: "Mobsub.SubtitleParse.TagGenerator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    member.Locations.FirstOrDefault(),
                    member.Name,
                    obsoleteReplacementName));
                return null;
            }

            byte isAlphaTag = IsAlphaTagName(name) ? (byte)1 : (byte)0;

            specs.Add(new TagSpec(
                tagValue,
                name,
                valueKind,
                tagKindValue,
                functionKindValue,
                isAlphaTag,
                obsoleteReplacementName,
                intAllowedMask,
                intAllowedMaskCode,
                intAllowedMaskMessage,
                intMin,
                intMax,
                intRangeCode,
                intRangeMessage,
                doubleMin,
                doubleMax,
                doubleRangeCode,
                doubleRangeMessage));
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

    private static bool IsAlphaTagName(string name)
        => name is "alpha" or "1a" or "2a" or "3a" or "4a";

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

        var nameBytes = new List<byte>(capacity: specs.Length * 4);
        var nameStartByTag = new int[maxTagValue + 1];
        var nameLenByTag = new byte[maxTagValue + 1];
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec))
            {
                nameStartByTag[i] = 0;
                nameLenByTag[i] = 0;
                continue;
            }

            var bytes = Encoding.ASCII.GetBytes(spec.Name);
            nameStartByTag[i] = nameBytes.Count;
            nameLenByTag[i] = checked((byte)bytes.Length);
            nameBytes.AddRange(bytes);
        }

        sb.AppendLine("    private static readonly byte[] s_nameBytes = new byte[]");
        sb.AppendLine("    {");
        for (int i = 0; i < nameBytes.Count; i++)
            sb.AppendLine($"        {nameBytes[i]},");
        sb.AppendLine("    };");
        sb.AppendLine();

        var obsoleteBytes = new List<byte>(capacity: 32);
        var obsoleteStartByTag = new int[maxTagValue + 1];
        var obsoleteLenByTag = new byte[maxTagValue + 1];
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec) || spec.ObsoleteReplacementName == null)
            {
                obsoleteStartByTag[i] = 0;
                obsoleteLenByTag[i] = 0;
                continue;
            }

            var bytes = Encoding.ASCII.GetBytes(spec.ObsoleteReplacementName);
            obsoleteStartByTag[i] = obsoleteBytes.Count;
            obsoleteLenByTag[i] = checked((byte)bytes.Length);
            obsoleteBytes.AddRange(bytes);
        }

        sb.AppendLine("    private static readonly byte[] s_obsoleteReplacementNameBytes = new byte[]");
        sb.AppendLine("    {");
        for (int i = 0; i < obsoleteBytes.Count; i++)
            sb.AppendLine($"        {obsoleteBytes[i]},");
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly int[] s_obsoleteReplacementNameStartByTag = new int[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
            sb.AppendLine($"        {obsoleteStartByTag[i]},");
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly byte[] s_obsoleteReplacementNameLenByTag = new byte[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
            sb.AppendLine($"        {obsoleteLenByTag[i]},");
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly int[] s_nameStartByTag = new int[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
            sb.AppendLine($"        {nameStartByTag[i]},");
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly byte[] s_nameLenByTag = new byte[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
            sb.AppendLine($"        {nameLenByTag[i]},");
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly byte[] s_tagKindByTag = new byte[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec))
            {
                sb.AppendLine("        0,");
                continue;
            }
            sb.AppendLine($"        {spec.TagKindValue},");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly ulong[] s_intAllowedMaskByTag = new ulong[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec))
            {
                sb.AppendLine("        0UL,");
                continue;
            }
            sb.AppendLine($"        {spec.IntAllowedMask}UL,");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly string?[] s_intAllowedMaskCodeByTag = new string?[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec))
            {
                sb.AppendLine("        null,");
                continue;
            }
            sb.AppendLine($"        {EmitNullableStringLiteral(spec.IntAllowedMaskDiagnosticCode)},");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly string?[] s_intAllowedMaskMessageByTag = new string?[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec))
            {
                sb.AppendLine("        null,");
                continue;
            }
            sb.AppendLine($"        {EmitNullableStringLiteral(spec.IntAllowedMaskMessage)},");
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

        sb.AppendLine("    private static readonly byte[] s_valueKindByTag = new byte[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec))
            {
                sb.AppendLine($"        {AssTagValueKind_None},");
                continue;
            }
            sb.AppendLine($"        {spec.ValueKind},");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly byte[] s_isAlphaTagByTag = new byte[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec))
            {
                sb.AppendLine("        0,");
                continue;
            }
            sb.AppendLine($"        {spec.IsAlphaTag},");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly int[] s_intMinByTag = new int[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec))
            {
                sb.AppendLine($"        {int.MinValue},");
                continue;
            }
            sb.AppendLine($"        {spec.IntMin},");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly int[] s_intMaxByTag = new int[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec))
            {
                sb.AppendLine($"        {int.MaxValue},");
                continue;
            }
            sb.AppendLine($"        {spec.IntMax},");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly string?[] s_intRangeCodeByTag = new string?[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec))
            {
                sb.AppendLine("        null,");
                continue;
            }
            sb.AppendLine($"        {EmitNullableStringLiteral(spec.IntRangeDiagnosticCode)},");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly string?[] s_intRangeMessageByTag = new string?[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec))
            {
                sb.AppendLine("        null,");
                continue;
            }
            sb.AppendLine($"        {EmitNullableStringLiteral(spec.IntRangeMessage)},");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly double[] s_doubleMinByTag = new double[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec) || double.IsNaN(spec.DoubleMin))
            {
                sb.AppendLine("        double.NaN,");
                continue;
            }
            sb.AppendLine($"        {spec.DoubleMin.ToString("R", System.Globalization.CultureInfo.InvariantCulture)},");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly double[] s_doubleMaxByTag = new double[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec) || double.IsNaN(spec.DoubleMax))
            {
                sb.AppendLine("        double.NaN,");
                continue;
            }
            sb.AppendLine($"        {spec.DoubleMax.ToString("R", System.Globalization.CultureInfo.InvariantCulture)},");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly string?[] s_doubleRangeCodeByTag = new string?[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec))
            {
                sb.AppendLine("        null,");
                continue;
            }
            sb.AppendLine($"        {EmitNullableStringLiteral(spec.DoubleRangeDiagnosticCode)},");
        }
        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly string?[] s_doubleRangeMessageByTag = new string?[]");
        sb.AppendLine("    {");
        for (int i = 0; i <= maxTagValue; i++)
        {
            if (!specByValue.TryGetValue(i, out var spec))
            {
                sb.AppendLine("        null,");
                continue;
            }
            sb.AppendLine($"        {EmitNullableStringLiteral(spec.DoubleRangeMessage)},");
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

    private static string EmitNullableStringLiteral(string? s)
        => s == null ? "null" : EmitStringLiteral(s);

    private static string EmitStringLiteral(string s)
    {
        var sb = new StringBuilder(capacity: s.Length + 16);
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

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Instance = new();
        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
