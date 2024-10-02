using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mobsub.SubtitleParse.SourceGen;

[Generator]
public class OverrideTagsSourceGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new AssTypesSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not AssTypesSyntaxReceiver receiver) { return; }
        if (receiver.Declaration is null) { return; }
        Generate(receiver.Declaration, out var def, out var parse, out var constants, out var defTrans);
        
        context.AddSource("AssTextStyle.g.cs", SourceText.From(def, Encoding.UTF8));
        context.AddSource("AssTagParse.g.cs", SourceText.From(parse, Encoding.UTF8));
        context.AddSource("AssConstants.g.cs", SourceText.From(constants, Encoding.UTF8));
        context.AddSource("AssTextStyleTransform.g.cs", SourceText.From(defTrans, Encoding.UTF8));
    }

    private void Generate(ClassDeclarationSyntax syntax, out string def, out string parse, out string constants, out string defTrans)
    {
        List<string> propertyNames = [];
        var sbDef = new StringBuilder();
        sbDef.AppendLine("#nullable enable");
        sbDef.AppendLine();
        sbDef.AppendLine("namespace Mobsub.SubtitleParse.AssTypes;");
        sbDef.AppendLine();
        sbDef.AppendLine("public partial class AssTextStyle");
        sbDef.AppendLine("{");

        var sbDefTrans = new StringBuilder("""
                                           #nullable enable
                                           
                                           namespace Mobsub.SubtitleParse.AssTypes;
                                           
                                           public struct AssTextStyleTransform(AssTextStyle style)
                                           {
                                           """);
        sbDefTrans.AppendLine();
        HashSet<string> animateableProps = [];

        var sbParse = new StringBuilder();
        sbParse.AppendLine("""
                           using Microsoft.Extensions.Logging;
                           using ZLogger;
                           using Mobsub.SubtitleParse.AssTypes;

                           namespace Mobsub.SubtitleParse.AssUtils;

                           public partial class AssTagParse
                           {
                               internal void ParseTag(ReadOnlySpan<char> span)
                               {
                           """);
        var sbGeneralParse = new StringBuilder();

        var sbConstants = new StringBuilder();
        sbConstants.AppendLine("namespace Mobsub.SubtitleParse.AssTypes;");
        sbConstants.AppendLine();
        sbConstants.AppendLine("public static partial class AssConstants");
        sbConstants.AppendLine("{");
        var sbTagsLineOnlyRenderFirst = new StringBuilder("    public static readonly HashSet<string> OverrideTagsLineOnlyRenderFirst = [");
        sbTagsLineOnlyRenderFirst.AppendLine();
        var sbTagsLineOnlyRenderLatest = new StringBuilder("    public static readonly HashSet<string> OverrideTagsLineOnlyRenderLatest = [");
        sbTagsLineOnlyRenderLatest.AppendLine();
        var sbTagsAnimateable = new StringBuilder("    public static readonly HashSet<string> OverrideTagsAnimateable = [");
        sbTagsAnimateable.AppendLine();
        var sbTagsShouldBeFunction = new StringBuilder("    public static readonly HashSet<string> OverrideTagsShouldBeFunction = [");
        sbTagsShouldBeFunction.AppendLine();
        
        foreach (var member in syntax.Members)
        {
            if (member is not FieldDeclarationSyntax field) { continue; }
            
            var tagAttrs = field.AttributeLists
                .SelectMany(a => a.Attributes).ToArray();

            var tagAttr = tagAttrs.FirstOrDefault(a => a.Name.ToString().AsSpan().StartsWith("AssOverrideTag".AsSpan()));
            var generalParseFuncAttr = tagAttrs.FirstOrDefault(a => a.Name.ToString().AsSpan().StartsWith("AssTagGeneralParse".AsSpan()));
            var contListAttr = tagAttrs.FirstOrDefault(a => a.Name.ToString().AsSpan().StartsWith("AssTagKind".AsSpan()));
            if (tagAttr is null) { continue; }
            
            var args = tagAttr.ArgumentList!.Arguments;
            var propertyVariable = field.Declaration.Variables.First();
            
            var propertyName = propertyVariable.Identifier.Text;
            var propertyType = args[0].ToString() == "null" ? null : args[0].ToString().AsSpan(7).TrimEnd(')').ToString();
            var parseMethod = args[1].ToString().Trim('"');
            var parseMethodParams = args[2].ToString().Trim('\"') == string.Empty ? "" : $", {args[2].ToString().Trim('\"')}";
            var mapPropName = args.Count > 3 ? args[3].ToString() == "null" ? null : args[3].ToString().Trim('"') : null;
            // var isVsfMod = args.Count > 4 && args[4].ToString().Contains("true");
            var literal = (LiteralExpressionSyntax)propertyVariable.Initializer!.Value;
            var propertyValueLength = literal.Token.ValueText.Length;

            var firstField = propertyNames.Count == 0;
            // define
            var propertyNameLast = mapPropName ?? propertyName;
            if (propertyType is not null & !propertyNames.Contains(propertyNameLast))
            {
                sbDef.AppendLine($"    public {propertyType}? {propertyNameLast} {{ get; set; }}");
                propertyNames.Add(propertyNameLast);
            }
            
            // parse
            sbParse.Append((char)0x20, 8);
            sbParse.Append(firstField ? "if " : $"else if ");
            sbParse.AppendLine($"(span.StartsWith(AssConstants.OverrideTags.{propertyName}))");
            sbParse.Append((char)0x20, 8);
            sbParse.AppendLine("{");
            sbParse.Append((char)0x20, 12);
            sbParse.AppendLine($"if (CheckDuplicateFlag(span, CheckDuplicateTag(AssConstants.OverrideTags.{propertyName}))) {{ return; }}");
            sbParse.Append((char)0x20, 12);
            sbParse.AppendLine($"{parseMethod}(TrimValueBlockSep(span[{propertyValueLength}..], AssConstants.OverrideTags.{propertyName}){parseMethodParams});");
            sbParse.Append((char)0x20, 12);
            sbParse.AppendLine($"if (inTransformation) {{ curTransTags.Add(AssConstants.OverrideTags.{propertyName}); }}");
            sbParse.Append((char)0x20, 12);
            sbParse.AppendLine($"else {{ curBlockTags.Add(AssConstants.OverrideTags.{propertyName}); }}");
            sbParse.Append((char)0x20, 8);
            sbParse.AppendLine("}");

            var isAnimateable = false;
            var isVsfMod = false;
            if (contListAttr != null)
            {
                var kinds = contListAttr.ArgumentList!.Arguments[0].ToString().AsSpan();
                if (kinds.Contains("LineOnlyRenderFirst".AsSpan(), StringComparison.Ordinal))
                {
                    sbTagsLineOnlyRenderFirst.AppendLine($"        OverrideTags.{propertyName},");
                }
                if (kinds.Contains("Animateable".AsSpan(), StringComparison.Ordinal))
                {
                    sbTagsAnimateable.AppendLine($"        OverrideTags.{propertyName},");
                    isAnimateable = true;
                }
                if (kinds.Contains("ShouldBeFunction".AsSpan(), StringComparison.Ordinal))
                {
                    sbTagsShouldBeFunction.AppendLine($"        OverrideTags.{propertyName},");
                }
                if (kinds.Contains("LineOnlyRenderLatest".AsSpan(), StringComparison.Ordinal))
                {
                    sbTagsLineOnlyRenderLatest.AppendLine($"        OverrideTags.{propertyName},");
                }
            }
            
            if (generalParseFuncAttr != null)
            {
                if (propertyType is null) { continue; }
                var gFuncArgs = generalParseFuncAttr.ArgumentList!.Arguments;
                var stylePropName = gFuncArgs[0].ToString().Trim('"');
                var isLimit = gFuncArgs.Count > 1 && gFuncArgs[1].ToString() == "true";
                
                switch (propertyType)
                {
                    case "bool":
                        sbGeneralParse.AppendLine($$"""
                                                        private void {{parseMethod}}(ReadOnlySpan<char> span)
                                                        {
                                                            if (IsEmptyOrWhiteSpace(span))
                                                            {
                                                               if (span.Length > 0)
                                                               {
                                                                   logger?.ZLogWarning($"Extra whitespace: {AssConstants.OverrideTags.{{propertyName}}}{span.ToString()}");
                                                               }
                                                               curTextStyle!.{{propertyNameLast}} = curTextStyle.BaseStyle.{{stylePropName}};
                                                            }
                                                            else
                                                            {
                                                                if (!int.TryParse(span, out var v))
                                                                {
                                                                    logger?.ZLogWarning($"Invalid value: {AssConstants.OverrideTags.{{propertyName}}}{span.ToString()}");
                                                                }
                                                                curTextStyle!.{{propertyNameLast}} = v != 0 && (v == 1 || curTextStyle.BaseStyle.{{stylePropName}});
                                                            }
                                                        }
                                                    """);
                        break;
                    
                    case "int":
                        var defaultValue = stylePropName == "null" ? "0" : $"curTextStyle.BaseStyle.{stylePropName}";
                        sbGeneralParse.AppendLine($$"""
                                                        private void {{parseMethod}}(ReadOnlySpan<char> span)
                                                        {
                                                            if (IsEmptyOrWhiteSpace(span))
                                                            {
                                                               if (span.Length > 0)
                                                               {
                                                                   logger?.ZLogWarning($"Extra whitespace: {AssConstants.OverrideTags.{{propertyName}}}{span.ToString()}");
                                                               }
                                                               curTextStyle!.{{propertyNameLast}} = {{defaultValue}};
                                                            }
                                                            else
                                                            {
                                                                if (!int.TryParse(span, out var v))
                                                                {
                                                                    logger?.ZLogWarning($"Invalid value: {AssConstants.OverrideTags.{{propertyName}}}{span.ToString()}");
                                                                }
                                                                curTextStyle!.{{propertyNameLast}} = v;
                                                            }
                                                        }
                                                    """);
                        break;
                    
                    case "double":

                        defaultValue = stylePropName == "null" ? "0" : $"curTextStyle.BaseStyle.{stylePropName}";
                        var assignValue = isLimit ? $"v < 0 ? {defaultValue} : v" : "v";
                        
                        if (!isAnimateable)
                        {
                            sbGeneralParse.AppendLine($$"""
                                                            private void {{parseMethod}}(ReadOnlySpan<char> span)
                                                            {
                                                                if (IsEmptyOrWhiteSpace(span))
                                                                {
                                                                   if (span.Length > 0)
                                                                   {
                                                                       logger?.ZLogWarning($"Extra whitespace: {AssConstants.OverrideTags.{{propertyName}}}{span.ToString()}");
                                                                   }
                                                                   curTextStyle!.{{propertyNameLast}} = {{defaultValue}};
                                                                }
                                                                else
                                                                {
                                                                    if (!double.TryParse(span, out var v))
                                                                    {
                                                                        logger?.ZLogWarning($"Invalid value: {AssConstants.OverrideTags.{{propertyName}}}{span.ToString()}");
                                                                    }
                                                                    curTextStyle!.{{propertyNameLast}} = {{assignValue}};
                                                                }
                                                            }
                                                        """);
                        }
                        else
                        {
                            sbGeneralParse.AppendLine($$"""
                                                            private void {{parseMethod}}(ReadOnlySpan<char> span)
                                                            {
                                                                if (IsEmptyOrWhiteSpace(span))
                                                                {
                                                                   if (span.Length > 0)
                                                                   {
                                                                       logger?.ZLogWarning($"Extra whitespace: {AssConstants.OverrideTags.{{propertyName}}}{span.ToString()}");
                                                                   }
                                                                   if (inTransformation)
                                                                   {
                                                                       curTextStyleTrans!.TransTextStyle!.{{propertyNameLast}} = {{defaultValue}};
                                                                   }
                                                                   else
                                                                   {
                                                                       curTextStyle!.{{propertyNameLast}} = {{defaultValue}};
                                                                   }
                                                                }
                                                                else
                                                                {
                                                                    if (!double.TryParse(span, out var v))
                                                                    {
                                                                        logger?.ZLogWarning($"Invalid value: {AssConstants.OverrideTags.{{propertyName}}}{span.ToString()}");
                                                                    }
                                                                    if (inTransformation)
                                                                    {
                                                                        curTextStyleTrans!.TransTextStyle!.{{propertyNameLast}} = {{assignValue}};
                                                                    }
                                                                    else
                                                                    {
                                                                        curTextStyle!.{{propertyNameLast}} = {{assignValue}};
                                                                    }
                                                                }
                                                            }
                                                        """);
                        }
                        
                        break;
                    
                    case "AssTextBorder":
                    case "AssTextShadow":
                    case "AssTextScale":

                        var stylePropNameX = propertyType == "AssTextScale" ? $"{stylePropName}X" : stylePropName;
                        var stylePropNameY = propertyType == "AssTextScale" ? $"{stylePropName}Y" : stylePropName;
                        var isBase = !(propertyName.AsSpan().EndsWith("X".AsSpan()) ||
                                      propertyName.AsSpan().EndsWith("Y".AsSpan()));
                        
                        sbGeneralParse.AppendLine($$"""
                                                        private void {{parseMethod}}(ReadOnlySpan<char> span, int index)
                                                        {
                                                            var value = inTransformation switch
                                                            {
                                                                true when curTextStyleTrans!.TransTextStyle.{{propertyNameLast}} is not null => ({{propertyType}})curTextStyleTrans.TransTextStyle.{{propertyNameLast}},
                                                                false when curTextStyle!.{{propertyNameLast}} is not null => ({{propertyType}})curTextStyle.{{propertyNameLast}},
                                                                _ => new {{propertyType}}()
                                                                {
                                                                    X = curTextStyle!.BaseStyle.{{stylePropNameX}},
                                                                    Y = curTextStyle!.BaseStyle.{{stylePropNameY}},
                                                                }
                                                            };
                                                            
                                                            var tag = index switch
                                                            {
                                                                {{(isBase ? $"0 => AssConstants.OverrideTags.{propertyName}," : "")}}
                                                                1 => AssConstants.OverrideTags.{{(isBase ? propertyName : propertyName.TrimEnd('X'))}}X,
                                                                2 => AssConstants.OverrideTags.{{(isBase ? propertyName : propertyName.TrimEnd('X'))}}Y,
                                                                _ => string.Empty
                                                            };
                                                            
                                                            if (IsEmptyOrWhiteSpace(span))
                                                            {
                                                                if (span.Length > 0)
                                                                {
                                                                    logger?.ZLogWarning($"Extra whitespace: {tag}{span.ToString()}");
                                                                }
                                                            }
                                                            else
                                                            {
                                                                if (!double.TryParse(span, out var v))
                                                                {
                                                                    logger?.ZLogWarning($"Invalid value: {tag}{span.ToString()}");
                                                                }
                                                                v = v < 0 ? 0 : v;
                                                                
                                                                switch (index)
                                                                {
                                                                    case 0:
                                                                        value.X = v;
                                                                        value.Y = v;
                                                                        break;
                                                                    case 1:
                                                                        value.X = v;
                                                                        break;
                                                                    case 2:
                                                                        value.Y = v;
                                                                        break;
                                                                }
                                                            }
                                                            
                                                            if (inTransformation)
                                                            {
                                                                curTextStyleTrans!.TransTextStyle.{{propertyNameLast}} = value;
                                                            }
                                                            else
                                                            {
                                                                curTextStyle!.{{propertyNameLast}} = value;
                                                            }
                                                        }
                                                    """);
                        break;
                }
            }

            if (isAnimateable && animateableProps.Add(propertyNameLast))
            {
                sbDefTrans.AppendLine($"    public {propertyType}? {propertyNameLast} = style.{propertyNameLast};");
            }
        }

        sbDef.AppendLine("}");
        def = sbDef.ToString();

        sbParse.Append((char)0x20, 8);
        sbParse.AppendLine("else");
        sbParse.Append((char)0x20, 8);
        sbParse.AppendLine("{");
        sbParse.AppendLine("        }");
        sbParse.AppendLine("    }");
        sbParse.AppendLine("");
        sbParse.Append(sbGeneralParse);
        sbParse.AppendLine("}");
        parse = sbParse.ToString();

        sbTagsLineOnlyRenderFirst.AppendLine("    ];");
        sbTagsAnimateable.AppendLine("    ];");
        sbTagsShouldBeFunction.AppendLine("    ];");
        sbTagsLineOnlyRenderLatest.AppendLine("    ];");
        sbConstants.Append(sbTagsLineOnlyRenderFirst);
        sbConstants.AppendLine();
        sbConstants.Append(sbTagsAnimateable);
        sbConstants.AppendLine();
        sbConstants.Append(sbTagsShouldBeFunction);
        sbConstants.AppendLine();
        sbConstants.Append(sbTagsLineOnlyRenderLatest);
        sbConstants.AppendLine("}");
        constants = sbConstants.ToString();

        sbDefTrans.AppendLine("}");
        defTrans = sbDefTrans.ToString();
    }
}

internal class AssTypesSyntaxReceiver : ISyntaxReceiver
{
    public ClassDeclarationSyntax? Declaration;

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is ClassDeclarationSyntax declaration
            && declaration.IsKind(SyntaxKind.ClassDeclaration)
            && declaration.Parent is ClassDeclarationSyntax parentDeclaration
            && parentDeclaration.Identifier.Text.AsSpan().SequenceEqual("AssConstants".AsSpan())
            && parentDeclaration.Parent is FileScopedNamespaceDeclarationSyntax nsDeclaration
            && nsDeclaration.Name.ToString().AsSpan().SequenceEqual("Mobsub.SubtitleParse.AssTypes".AsSpan())
            && declaration.Identifier.Text.AsSpan().SequenceEqual("OverrideTags".AsSpan())
           )
        {
            Declaration = declaration;
        }
    }
}