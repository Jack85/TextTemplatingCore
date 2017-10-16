using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using TextTemplating.Infrastructure;
using TextTemplating.T4.Parsing;

namespace TextTemplating.T4.Preprocessing
{
    /// <summary>
    /// Transform .tt to .cs
    /// </summary>
    internal class PreprocessTextTransformation : TextTransformationBase
    {
        private readonly string _className;
        private readonly string _classNamespace;
        private readonly ParseResult _result;

        public PreprocessTextTransformation(string className, string classNamespace, ParseResult result, ITextTemplatingEngineHost host)
        {
            _className = className;
            _classNamespace = classNamespace;
            _result = result;
            Host = host;
        }

        public override string TransformText()
        {
            foreach (var import in _result.Imports.Distinct())
            {
                WriteLine($"using {import};");
            }

            WriteLine($"{Environment.NewLine}namespace {_classNamespace}");
            WriteLine("{");
            PushIndent("    ");
            WriteLine($"{_result.Visibility} partial class {_className} : TextTransformationBase");
            WriteLine("{");
            PushIndent("    ");
            WriteLine("public override string TransformText()");
            WriteLine("{");
            PushIndent("    ");

            foreach (var block in _result.ContentBlocks)
            {
                Write(Render(block));
            }
            WriteLine();
            WriteLine();
            WriteLine("return GenerationEnvironment.ToString();");
            PopIndent();
            WriteLine("}");

            foreach (var block in _result.FeatureBlocks)
            {
                Write(Render(block));
            }

            PopIndent();
            WriteLine("}");
            PopIndent();
            WriteLine("}");

            // format output
            return FormatCode(GenerationEnvironment.ToString());
        }

        private string FormatCode(string code)
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var formattedRoot = Formatter.Format(tree.GetRoot(), new AdhocWorkspace());
            return formattedRoot.SyntaxTree.ToString();
        }

        private string Render(Block block)
        {
            switch (block.BlockType)
            {
                case BlockType.TextBlock:
                    var textLines = block.Content
                        .Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None)
                        .Select(line => string.IsNullOrEmpty(line)
                            ? ""
                            : $"Write(\"{line.Replace("\\", "\\\\").Replace("\"", "\\\"")}\");{Environment.NewLine}");
                    return string.Join($"WriteLine();{Environment.NewLine}", textLines);
                //return $"Write(\"{block.Content.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n")}\");";

                case BlockType.StandardControlBlock:
                case BlockType.ClassFeatureControlBlock:
                    return block.Content;

                case BlockType.ExpressionControlBlock:
                    return $"Write(({block.Content}).ToString());";

                default:
                    throw new InvalidOperationException("Unexpected block type.");
            }
        }
    }
}
