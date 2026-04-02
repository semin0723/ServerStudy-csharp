using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Generator
{
    [Generator]
    public class ServerReceiveGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var methodDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (s, ct) => s is MethodDeclarationSyntax m && m.AttributeLists.Count > 0,
                    transform: (ctx, _) => GetMethodSymbol(ctx))
                .Where(m => m != null);

            var collectedMethods = methodDeclarations.Collect();

            context.RegisterSourceOutput(collectedMethods, (ctx, methods) =>
            {
                if (methods.IsDefaultOrEmpty)
                {
                    return;
                }

                var groupbyclass = methods.Where(m => m != null).GroupBy(m => m.ContainingType, SymbolEqualityComparer.Default);
                foreach (var group in groupbyclass)
                {
                    var methodSymbols = group.ToList();
                    GenerateCode(ctx, methodSymbols);
                }
            });
        }

        private static IMethodSymbol GetMethodSymbol(GeneratorSyntaxContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            var symbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

            if (symbol == null)
            {
                return null;
            }

            if (symbol is IMethodSymbol methodSymbol &&
                methodSymbol.GetAttributes().Any(attr => attr.AttributeClass.Name == "ServerReceiveAttribute" || attr.AttributeClass.Name == "ServerReceive"))
            {
                return methodSymbol;
            }
            return null;
        }

        private static void GenerateCode(SourceProductionContext context, List<IMethodSymbol> methods)
        {
            var methodNamespace = methods[0].ContainingNamespace.ToDisplayString();
            var className = methods[0].ContainingType.Name;

            var stringbuilder = new StringBuilder();
            stringbuilder.AppendLine($@"using System;
using System.Buffers;
using Network.NetworkUtility;
using Network.DataObject;

namespace {methodNamespace}
{{
    public partial class {className}
    {{");
            foreach (var method in methods)
            {
                var methodName = method.Name;
                var parameters = string.Join(", ", method.Parameters.Select(p =>
                {
                    string parameterTypeString = p.Type.ToDisplayString();
                    if (p.NullableAnnotation == NullableAnnotation.Annotated)
                    {
                        parameterTypeString += "?";
                    }
                    return $"{parameterTypeString} {p.Name}";
                }));
                var parameterValues = string.Join(", ", method.Parameters.Select(p => p.Name));
                var returnType = method.ReturnType.ToDisplayString();
                string typeParameterKind = string.Empty;
                if (method.IsGenericMethod)
                {
                    var genericArgs = string.Join(", ", method.TypeParameters.Select(tp => tp.Name));
                    methodName += $"<{genericArgs}>";
                    typeParameterKind = string.Join(" ", method.TypeParameters.Select(tp => $"where {tp.Name} : {GetConstraintType(tp)}"));
                }

                string byteParameter = string.Empty;
                string deserializeCall = string.Empty;
                if (parameters.Length > 0)
                {
                    byteParameter = "byte[]? data = null";

                    deserializeCall = $@"
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);";
                    foreach (var parameter in method.Parameters)
                    {
                        if (parameter.Type is INamedTypeSymbol namedType && namedType.TypeKind == TypeKind.Class && namedType.SpecialType == SpecialType.None)
                        {
                            var members = namedType.GetMembers().OfType<IPropertySymbol>();
                            deserializeCall += $@"
            {parameter.Type.Name} {parameter.Name}_value = new {parameter.Type.Name}
            {{";
                            foreach (var member in members)
                            {
                                deserializeCall += $@"
                {member.Name} = reader.Read{member.Type.SpecialType.ToString().Replace("System_", "")}(),";
                            }
                            deserializeCall += $"\n\t\t\t}};";
                        }
                        else
                        {
                            deserializeCall += $@"
            var {parameter.Name}_value = reader.Read{parameter.Type.SpecialType.ToString().Replace("System_", "")}();";
                        }
                    }
                    deserializeCall += $@"
            {methodName}({string.Join(", ", method.Parameters.Select(p => $"{p.Name}_value"))});";
                }
                else
                {
                    deserializeCall = $@"
            {methodName}();";
                }

                stringbuilder.AppendLine($"\t\tpublic void {methodName}_RemoteCall({byteParameter}) {typeParameterKind}\n\t\t{{");
                // if has parameters
                stringbuilder.AppendLine(deserializeCall);



                if (returnType != "void")
                {
                    stringbuilder.AppendLine(
$"\t\t\treturn default({returnType});"
);
                }
                stringbuilder.AppendLine($"\t\t}}");
            }
            stringbuilder.AppendLine($"\t}}\n" + 
$"}}");
            context.AddSource($"{className}_Generated.cs", SourceText.From(stringbuilder.ToString(), Encoding.UTF8));
        }

        private static string GetConstraintType(ITypeParameterSymbol symbol)
        {
            List<string> constraintTypes = new List<string>();
            if (symbol.HasUnmanagedTypeConstraint)
            {
                constraintTypes.Add("unmanaged");
            }
            if (symbol.HasValueTypeConstraint)
            {
                constraintTypes.Add("struct");
            }
            if (symbol.HasReferenceTypeConstraint)
            {
                constraintTypes.Add(symbol.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
            }
            if (symbol.HasNotNullConstraint)
            {
                constraintTypes.Add("notnull");
            }
            foreach (var constraintType in symbol.ConstraintTypes)
            {
                constraintTypes.Add(constraintType.ToDisplayString());
            }
            if (symbol.HasConstructorConstraint && !symbol.HasValueTypeConstraint)
            {
                constraintTypes.Add("new()");
            }

            return string.Join(", ", constraintTypes);
        }
    }
}
