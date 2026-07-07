using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Shiny.Maui.Shell.SourceGenerators;


[Generator(LanguageNames.CSharp)]
public class ShinyShellGenerator : IIncrementalGenerator
{
    static readonly DiagnosticDescriptor InvalidRouteIdentifier = new(
        "SHINY001",
        "Invalid route name",
        "The route '{0}' does not produce a valid C# identifier '{1}'. Route must contain at least one letter and cannot start with a digit after conversion.",
        "Shiny.Shell",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor NavExtensionsDisabledWithMaps = new(
        "SHINY002",
        "Navigation extensions disabled but ShellMap attributes detected",
        "ShinyMauiShell_GenerateNavExtensions is set to false but {0} ShellMap attribute(s) were detected. AddGeneratedMaps will not be generated.",
        "Shiny.Shell",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor AiExtensionsMissingPackage = new(
        "SHINY003",
        "Microsoft.Extensions.AI is required for AI extensions",
        "ShinyMauiShell_GenerateAiExtensions is enabled but Microsoft.Extensions.AI is not referenced. Install the Microsoft.Extensions.AI NuGet package or set ShinyMauiShell_GenerateAiExtensions to false.",
        "Shiny.Shell",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor AiPropertyDescriptionsWithoutRouteDescription = new(
        "SHINY004",
        "ShellProperty descriptions without ShellMap description",
        "The route '{0}' has ShellProperty attributes with descriptions but the ShellMap attribute has no description. AI tools cannot determine when to navigate to this route without a description. Add a description to the ShellMap attribute.",
        "Shiny.Shell",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find classes with ShellMapAttribute
        var shellMapClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetShellMapClass(ctx))
            .Where(static m => m is not null)
            .Collect();

        var options = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue("build_property.ShinyMauiShell_GenerateRouteConstants", out var routeValue);
                provider.GlobalOptions.TryGetValue("build_property.ShinyMauiShell_GenerateNavExtensions", out var navValue);
                provider.GlobalOptions.TryGetValue("build_property.ShinyMauiShell_GenerateAiExtensions", out var aiValue);
                provider.GlobalOptions.TryGetValue("build_property.ShinyMauiShell_AiExtensionsClassName", out var aiClassName);
                provider.GlobalOptions.TryGetValue("build_property.ShinyMauiShell_AiNavigateMethodName", out var aiNavigateMethodName);
                provider.GlobalOptions.TryGetValue("build_property.ShinyMauiShell_AiToolsClassName", out var aiToolsClassName);
                // empty or missing is considered true for route/nav, but false for ai (opt-in)
                return new GeneratorOptions(
                    GenerateRouteConstants: !string.Equals(routeValue, "false", StringComparison.OrdinalIgnoreCase),
                    GenerateNavExtensions: !string.Equals(navValue, "false", StringComparison.OrdinalIgnoreCase),
                    GenerateAiExtensions: string.Equals(aiValue, "true", StringComparison.OrdinalIgnoreCase),
                    AiExtensionsClassName: string.IsNullOrWhiteSpace(aiClassName) ? "AiExtensions" : aiClassName!.Trim(),
                    AiNavigateMethodName: string.IsNullOrWhiteSpace(aiNavigateMethodName) ? "NavigateToRoute" : aiNavigateMethodName!.Trim(),
                    AiToolsClassName: string.IsNullOrWhiteSpace(aiToolsClassName) ? "AiMauiShellTools" : aiToolsClassName!.Trim()
                );
            });

        var hasAiPackage = context.CompilationProvider.Select(static (compilation, _) =>
            compilation.GetTypeByMetadataName("Microsoft.Extensions.AI.AITool") != null);

        var combined = shellMapClasses.Combine(options).Combine(hasAiPackage);

        context.RegisterSourceOutput(combined, (spc, data) => GenerateCode(spc, data.Left.Left, data.Left.Right, data.Right));
    }

    static ShellMapInfo? GetShellMapClass(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        
        foreach (var attributeList in classDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(attribute);
                if (symbolInfo.Symbol is IMethodSymbol attributeSymbol)
                {
                    var attributeClass = attributeSymbol.ContainingType;
                    if (attributeClass.Name == "ShellMapAttribute" && attributeClass.IsGenericType)
                    {
                        var pageType = attributeClass.TypeArguments[0];
                        var route = GetRouteFromAttribute(attribute);
                        var registerRoute = GetRegisterRouteFromAttribute(attribute);
                        var description = GetDescriptionFromAttribute(attribute);
                        var properties = GetShellProperties(classDeclaration, context.SemanticModel);

                        var viewModelSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
                        var generatedName = route ?? pageType.Name.Replace("Page", "");
                        return new ShellMapInfo(
                            classDeclaration.Identifier.ValueText,
                            viewModelSymbol?.ToDisplayString() ?? classDeclaration.Identifier.ValueText,
                            pageType.Name,
                            pageType.ToDisplayString(),
                            route ?? pageType.Name,
                            generatedName,
                            registerRoute,
                            description,
                            properties,
                            attribute.GetLocation()
                        );
                    }
                }
            }
        }
        
        return null;
    }

    static string? GetRouteFromAttribute(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList?.Arguments.Count > 0)
        {
            // Look for the route parameter specifically
            foreach (var arg in attribute.ArgumentList.Arguments)
            {
                // Check if it's a named argument for "route"
                if (arg.NameColon?.Name.Identifier.ValueText == "route")
                {
                    if (arg.Expression is LiteralExpressionSyntax literal)
                    {
                        return literal.Token.ValueText;
                    }
                }
                // If it's the first positional argument (no named colon)
                else if (arg == attribute.ArgumentList.Arguments[0] && arg.NameColon == null)
                {
                    if (arg.Expression is LiteralExpressionSyntax literal &&
                        literal.Token.IsKind(SyntaxKind.StringLiteralToken))
                    {
                        return literal.Token.ValueText;
                    }
                }
            }
        }
        return null;
    }

    static bool GetRegisterRouteFromAttribute(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList?.Arguments.Count > 0)
        {
            var arguments = attribute.ArgumentList.Arguments;
            
            // Look for the registerRoute parameter specifically
            foreach (var arg in arguments)
            {
                // Check if it's a named argument for "registerRoute"
                if (arg.NameColon?.Name.Identifier.ValueText == "registerRoute")
                {
                    if (arg.Expression is LiteralExpressionSyntax literal)
                    {
                        if (literal.Token.IsKind(SyntaxKind.FalseKeyword))
                            return false;
                        if (literal.Token.IsKind(SyntaxKind.TrueKeyword))
                            return true;
                    }
                }
            }
            
            // Check positional arguments
            for (int i = 0; i < arguments.Count; i++)
            {
                var arg = arguments[i];
                
                // If it's the second positional argument (index 1) and not a named argument
                if (i == 1 && arg.NameColon == null)
                {
                    if (arg.Expression is LiteralExpressionSyntax literal)
                    {
                        if (literal.Token.IsKind(SyntaxKind.FalseKeyword))
                            return false;
                        if (literal.Token.IsKind(SyntaxKind.TrueKeyword))
                            return true;
                    }
                }
                // Handle case where registerRoute is the first argument (when route is omitted)
                else if (i == 0 && 
                         arg.NameColon == null &&
                         arg.Expression is LiteralExpressionSyntax literal &&
                         (literal.Token.IsKind(SyntaxKind.TrueKeyword) || literal.Token.IsKind(SyntaxKind.FalseKeyword)))
                {
                    if (literal.Token.IsKind(SyntaxKind.FalseKeyword))
                        return false;
                    if (literal.Token.IsKind(SyntaxKind.TrueKeyword))
                        return true;
                }
            }
        }
        // Default value is true according to the attribute definition
        return true;
    }

    static string GetDescriptionFromAttribute(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList?.Arguments.Count > 0)
        {
            foreach (var arg in attribute.ArgumentList.Arguments)
            {
                if (arg.NameColon?.Name.Identifier.ValueText == "description")
                {
                    if (arg.Expression is LiteralExpressionSyntax literal &&
                        literal.Token.IsKind(SyntaxKind.StringLiteralToken))
                        return literal.Token.ValueText;
                    return null;
                }
            }

            // Positional: description is 3rd param (index 2) on ShellMapAttribute(route, registerRoute, description)
            int positionalIndex = 0;
            foreach (var arg in attribute.ArgumentList.Arguments)
            {
                if (arg.NameColon != null)
                    continue;

                if (positionalIndex == 2 &&
                    arg.Expression is LiteralExpressionSyntax literal &&
                    literal.Token.IsKind(SyntaxKind.StringLiteralToken))
                    return literal.Token.ValueText;

                positionalIndex++;
            }
        }
        return null;
    }

    static string GetPropertyDescriptionFromAttribute(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList?.Arguments.Count > 0)
        {
            foreach (var arg in attribute.ArgumentList.Arguments)
            {
                if (arg.NameColon?.Name.Identifier.ValueText == "description")
                {
                    if (arg.Expression is LiteralExpressionSyntax literal &&
                        literal.Token.IsKind(SyntaxKind.StringLiteralToken))
                        return literal.Token.ValueText;
                    return null;
                }
            }

            // Positional: description is 1st param on ShellPropertyAttribute
            var firstArg = attribute.ArgumentList.Arguments[0];
            if (firstArg.NameColon == null &&
                firstArg.Expression is LiteralExpressionSyntax firstLiteral &&
                firstLiteral.Token.IsKind(SyntaxKind.StringLiteralToken))
                return firstLiteral.Token.ValueText;
        }
        return null;
    }

    static ImmutableArray<ShellPropertyInfo> GetShellProperties(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
    {
        var properties = ImmutableArray.CreateBuilder<ShellPropertyInfo>();
        
        foreach (var member in classDeclaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            foreach (var attributeList in member.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(attribute);
                    if (symbolInfo.Symbol is IMethodSymbol attributeSymbol &&
                        attributeSymbol.ContainingType.Name == "ShellPropertyAttribute")
                    {
                        var isRequired = GetIsRequiredFromAttribute(attribute);
                        var propDescription = GetPropertyDescriptionFromAttribute(attribute);
                        var propertySymbol = semanticModel.GetDeclaredSymbol(member) as IPropertySymbol;

                        if (propertySymbol != null)
                        {
                            // Check if property has public get/set
                            var hasPublicGetter = propertySymbol.GetMethod?.DeclaredAccessibility == Accessibility.Public;
                            var hasPublicSetter = propertySymbol.SetMethod?.DeclaredAccessibility == Accessibility.Public;

                            if (!hasPublicGetter || !hasPublicSetter)
                            {
                                // This would ideally be a diagnostic error, but for now we'll skip
                                continue;
                            }
                            else
                            {
                                var typeSymbol = propertySymbol.Type;
                                var enumType = typeSymbol.TypeKind == TypeKind.Enum
                                    ? (INamedTypeSymbol)typeSymbol
                                    : typeSymbol is INamedTypeSymbol { IsGenericType: true, ConstructedFrom.SpecialType: SpecialType.System_Nullable_T } nullableType &&
                                      nullableType.TypeArguments[0].TypeKind == TypeKind.Enum
                                        ? (INamedTypeSymbol)nullableType.TypeArguments[0]
                                        : null;

                                var enumValues = enumType != null
                                    ? enumType.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).Select(f => f.Name).ToImmutableArray()
                                    : ImmutableArray<string>.Empty;

                                properties.Add(new ShellPropertyInfo(
                                    member.Identifier.ValueText,
                                    propertySymbol.Type.ToDisplayString(),
                                    isRequired,
                                    propDescription,
                                    enumType != null,
                                    enumValues
                                ));
                            }
                        }
                    }
                }
            }
        }
        
        return properties.ToImmutable();
    }

    static bool GetIsRequiredFromAttribute(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList?.Arguments.Count > 0)
        {
            // Check named argument first
            foreach (var arg in attribute.ArgumentList.Arguments)
            {
                if (arg.NameColon?.Name.Identifier.ValueText == "required")
                {
                    if (arg.Expression is LiteralExpressionSyntax literal)
                    {
                        if (literal.Token.IsKind(SyntaxKind.TrueKeyword))
                            return true;
                        if (literal.Token.IsKind(SyntaxKind.FalseKeyword))
                            return false;
                    }
                }
            }

            // Positional: required is 2nd param (index 1) on ShellPropertyAttribute
            for (int i = 0; i < attribute.ArgumentList.Arguments.Count; i++)
            {
                var arg = attribute.ArgumentList.Arguments[i];
                if (arg.NameColon != null)
                    continue;

                if (arg.Expression is LiteralExpressionSyntax literal &&
                    (literal.Token.IsKind(SyntaxKind.TrueKeyword) || literal.Token.IsKind(SyntaxKind.FalseKeyword)))
                {
                    return literal.Token.IsKind(SyntaxKind.TrueKeyword);
                }
            }
        }
        // Default value is true according to the attribute definition
        return true;
    }

    static void GenerateCode(SourceProductionContext context, ImmutableArray<ShellMapInfo?> classes, GeneratorOptions options, bool hasAiPackage)
    {
        var validClasses = classes.Where(c => c != null).Cast<ShellMapInfo>().ToImmutableArray();

        // Validate generated names are valid C# identifiers
        var checkedClasses = ImmutableArray.CreateBuilder<ShellMapInfo>();
        foreach (var cls in validClasses)
        {
            if (!SyntaxFacts.IsValidIdentifier(cls.GeneratedName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidRouteIdentifier,
                    cls.AttributeLocation,
                    cls.Route,
                    cls.GeneratedName
                ));
            }
            else
            {
                checkedClasses.Add(cls);
            }
        }
        var filtered = checkedClasses.ToImmutable();

        if (filtered.IsEmpty)
            return;

        // Validate AI extensions configuration
        if (options.GenerateAiExtensions && !hasAiPackage)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AiExtensionsMissingPackage,
                Location.None));
        }

        if (options.GenerateAiExtensions)
        {
            foreach (var cls in filtered)
            {
                if (cls.Description == null && cls.Properties.Any(p => p.Description != null))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        AiPropertyDescriptionsWithoutRouteDescription,
                        cls.AttributeLocation,
                        cls.Route));
                }
            }
        }

        // Generate AddGeneratedMaps and nav extensions only if enabled
        if (options.GenerateNavExtensions)
        {
            GenerateNavigationBuilderExtensions(context, filtered);
            GenerateNavigationExtensions(context, filtered);
            GenerateNavigationBuilderNavExtensions(context, filtered);
        }
        else
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NavExtensionsDisabledWithMaps,
                Location.None,
                filtered.Length
            ));
        }

        if (options.GenerateAiExtensions && hasAiPackage)
        {
            GenerateAiExtensions(context, filtered, options);
        }

        // Generate Routes class only if enabled
        if (options.GenerateRouteConstants)
            GenerateRoutesClass(context, filtered);
    }

    static void GenerateRoutesClass(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("public static class Routes");
        sb.AppendLine("{");
        
        foreach (var cls in classes)
        {
            var constantName = cls.GeneratedName;
            sb.AppendLine($"    public const string {constantName} = \"{cls.Route}\";");
        }
        
        sb.AppendLine("}");
        
        context.AddSource("Routes.g.cs", sb.ToString());
    }

    static void GenerateNavigationExtensions(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("public static class NavigationExtensions");
        sb.AppendLine("{");
        
        foreach (var cls in classes)
        {
            var methodName = $"NavigateTo{cls.GeneratedName}";
            var requiredParams = cls.Properties.Where(p => p.IsRequired).ToList();
            var optionalParams = cls.Properties.Where(p => !p.IsRequired).ToList();

            // XML doc comment
            if (cls.Description != null)
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// {EscapeXml(cls.Description)}");
                sb.AppendLine($"    /// </summary>");

                foreach (var prop in requiredParams.Concat(optionalParams))
                {
                    var paramDesc = prop.Description != null ? EscapeXml(prop.Description) : "";
                    sb.AppendLine($"    /// <param name=\"{ToCamelCase(prop.Name)}\">{paramDesc}</param>");
                }

                sb.AppendLine($"    /// <param name=\"relativeNavigation\">If true, it will navigate/stack from where the application currently is otherwise, it will reset the stack to this new route</param>");
            }

            // [Description] on method
            if (cls.Description != null)
                sb.AppendLine($"    [global::System.ComponentModel.Description(\"{EscapeString(cls.Description)}\")]");

            sb.Append($"    public static global::System.Threading.Tasks.Task {methodName}(this global::Shiny.INavigator navigator");

            // Add required parameters first
            foreach (var prop in requiredParams)
            {
                if (prop.Description != null)
                    sb.Append($", [global::System.ComponentModel.Description(\"{EscapeString(prop.Description)}\")] {prop.TypeName} {ToCamelCase(prop.Name)}");
                else
                    sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)}");
            }

            // Add optional parameters last
            foreach (var prop in optionalParams)
            {
                var defaultValue = GetDefaultValue(prop.TypeName);
                if (prop.Description != null)
                    sb.Append($", [global::System.ComponentModel.Description(\"{EscapeString(prop.Description)}\")] {prop.TypeName} {ToCamelCase(prop.Name)} = {defaultValue}");
                else
                    sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)} = {defaultValue}");
            }

            if (cls.Description != null)
                sb.Append(", [global::System.ComponentModel.Description(\"If true, it will navigate/stack from where the application currently is otherwise, it will reset the stack to this new route\")] bool relativeNavigation = true");
            else
                sb.Append(", bool relativeNavigation = true");

            // If no properties, add the params argument
            if (!cls.Properties.Any())
            {
                sb.Append(", params global::System.Collections.Generic.IEnumerable<(string Key, object Value)> args");
            }

            sb.AppendLine(")");
            sb.AppendLine("    {");

            if (cls.Properties.Any())
            {
                sb.Append($"        return navigator.NavigateTo<{cls.ViewModelFullName}>(x => ");
                sb.Append("{ ");

                var assignments = cls.Properties.Select(p => $"x.{p.Name} = {ToCamelCase(p.Name)}");
                sb.Append(string.Join("; ", assignments));
                sb.Append(";");

                sb.AppendLine($" }}, relativeNavigation);");
            }
            else
            {
                sb.AppendLine($"        return navigator.NavigateTo<{cls.ViewModelFullName}>(configure: null, relativeNavigation: relativeNavigation, args: args);");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }
        
        sb.AppendLine("}");
        
        context.AddSource("NavigationExtensions.g.cs", sb.ToString());
    }

    static void GenerateNavigationBuilderExtensions(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("public static class NavigationBuilderExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static global::Shiny.ShinyAppBuilder AddGeneratedMaps(this global::Shiny.ShinyAppBuilder builder)");
        sb.AppendLine("    {");
        
        foreach (var cls in classes)
        {
            if (cls.RegisterRoute)
            {
                sb.AppendLine($"        builder.Add<{cls.PageTypeFullName}, {cls.ViewModelFullName}>(\"{cls.Route}\");");
            }
            else
            {
                sb.AppendLine($"        builder.Add<{cls.PageTypeFullName}, {cls.ViewModelFullName}>(\"{cls.Route}\", registerRoute: false);");
            }
        }
        
        sb.AppendLine("        return builder;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        context.AddSource("NavigationBuilderExtensions.g.cs", sb.ToString());
    }

    static void GenerateNavigationBuilderNavExtensions(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("public static class NavigationBuilderNavExtensions");
        sb.AppendLine("{");

        foreach (var cls in classes)
        {
            var methodName = $"Add{cls.GeneratedName}";
            var requiredParams = cls.Properties.Where(p => p.IsRequired).ToList();
            var optionalParams = cls.Properties.Where(p => !p.IsRequired).ToList();

            if (cls.Properties.Any())
            {
                sb.Append($"    public static global::Shiny.INavigationBuilder {methodName}(this global::Shiny.INavigationBuilder builder");

                foreach (var prop in requiredParams)
                    sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)}");

                foreach (var prop in optionalParams)
                {
                    var defaultValue = GetDefaultValue(prop.TypeName);
                    sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)} = {defaultValue}");
                }

                sb.AppendLine(")");
                sb.AppendLine("    {");
                sb.Append($"        return builder.Add<{cls.ViewModelFullName}>(x => {{ ");

                var assignments = cls.Properties.Select(p => $"x.{p.Name} = {ToCamelCase(p.Name)}");
                sb.Append(string.Join("; ", assignments));
                sb.Append(";");

                sb.AppendLine(" });");
                sb.AppendLine("    }");
            }
            else
            {
                sb.AppendLine($"    public static global::Shiny.INavigationBuilder {methodName}(this global::Shiny.INavigationBuilder builder)");
                sb.AppendLine("    {");
                sb.AppendLine($"        return builder.Add<{cls.ViewModelFullName}>();");
                sb.AppendLine("    }");
            }

            sb.AppendLine();
        }

        sb.AppendLine("}");
        context.AddSource("NavigationBuilderNavExtensions.g.cs", sb.ToString());
    }

    static void GenerateAiExtensions(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes, GeneratorOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine($"public static class {options.AiExtensionsClassName}");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.ComponentModel.Description(\"This provides a list of routes throughout the application\")]");
        sb.AppendLine("    public static global::Shiny.Infrastructure.GeneratedRouteInfo[] GetGeneratedRouteInfo(this global::Shiny.INavigator navigator) =>");
        sb.AppendLine("    [");

        for (int i = 0; i < classes.Length; i++)
        {
            var cls = classes[i];
            var descriptionArg = cls.Description != null
                ? $"\"{EscapeString(cls.Description)}\""
                : "\"\"";

            sb.AppendLine($"        new global::Shiny.Infrastructure.GeneratedRouteInfo(");
            sb.AppendLine($"            \"{EscapeString(cls.Route)}\",");
            sb.AppendLine($"            {descriptionArg},");

            if (cls.Properties.Any())
            {
                sb.AppendLine("            [");
                var properties = cls.Properties.ToList();
                for (int j = 0; j < properties.Count; j++)
                {
                    var p = properties[j];
                    var requiredLiteral = p.IsRequired ? "true" : "false";
                    sb.Append($"                new global::Shiny.Infrastructure.GeneratedRouteParameter(");
                    sb.Append($"\"{EscapeString(p.Name)}\", \"{GetParameterDescription(p)}\", \"{EscapeString(GetParameterTypeName(p))}\", {requiredLiteral})");
                    if (j < properties.Count - 1)
                        sb.Append(",");
                    sb.AppendLine();
                }
                sb.Append("            ]");
            }
            else
            {
                sb.Append("            []");
            }

            sb.Append(")");
            if (i < classes.Length - 1)
                sb.Append(",");
            sb.AppendLine();
        }

        sb.AppendLine("    ];");

        var aiClasses = classes.Where(c => c.Description != null).ToList();
        GenerateAiMauiShellToolsClass(sb, aiClasses, options);

        sb.AppendLine("}");

        context.AddSource("AiExtensions.g.cs", sb.ToString());
    }

    static void GenerateAiMauiShellToolsClass(StringBuilder sb, System.Collections.Generic.List<ShellMapInfo> aiClasses, GeneratorOptions options)
    {
        sb.AppendLine();
        sb.AppendLine("}"); // close the static extensions class
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Provides AI tools and a pre-formatted prompt for route discovery and navigation.");
        sb.AppendLine("/// Register this class in DI and inject it where AI chat functionality is needed.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public class {options.AiToolsClassName}");
        sb.AppendLine("{");
        sb.AppendLine("    readonly global::Shiny.INavigator _navigator;");
        sb.AppendLine();

        // Prompt property
        var promptBuilder = new StringBuilder();
        promptBuilder.Append("Available routes:\\n");
        foreach (var cls in aiClasses)
        {
            promptBuilder.Append($"- Route \\\"{EscapeString(cls.Route)}\\\": {EscapeString(cls.Description)}\\n");
            promptBuilder.Append("  Parameters:\\n");
            foreach (var p in cls.Properties)
            {
                var desc = GetParameterDescription(p);
                var typeName = GetParameterTypeName(p);
                var req = p.IsRequired ? "required" : "optional";
                promptBuilder.Append($"    - {EscapeString(p.Name)} ({EscapeString(typeName)}, {req}): {desc}\\n");
            }
        }

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// A pre-formatted prompt string describing all AI-applicable routes, their descriptions, and parameters.");
        sb.AppendLine("    /// Designed to be included in an AI system message so the model knows which routes are available without calling a discovery tool first.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public string Prompt {{ get; }} = \"{promptBuilder}\";");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// AI tools for route discovery and navigation, ready to use with Microsoft.Extensions.AI ChatOptions.Tools.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public global::Microsoft.Extensions.AI.AITool[] Tools { get; }");
        sb.AppendLine();

        // Constructor
        sb.AppendLine($"    public {options.AiToolsClassName}(global::Shiny.INavigator navigator)");
        sb.AppendLine("    {");
        sb.AppendLine("        _navigator = navigator;");
        sb.AppendLine("        Tools =");
        sb.AppendLine("        [");
        sb.AppendLine("            global::Microsoft.Extensions.AI.AIFunctionFactory.Create(");
        sb.AppendLine("                () => GetAiToolApplicableGeneratedRoutes(),");
        sb.AppendLine("                name: \"GetRoutes\",");
        sb.AppendLine("                description: \"Returns a list of available application routes with their descriptions and parameter schemas\"),");
        sb.AppendLine("            global::Microsoft.Extensions.AI.AIFunctionFactory.Create(");
        sb.AppendLine($"                (string route, global::System.Collections.Generic.Dictionary<string, string>? args) => {options.AiNavigateMethodName}(route, args),");
        sb.AppendLine($"                name: \"{options.AiNavigateMethodName}\",");

        // Build a rich description for the navigate tool
        var navDescBuilder = new StringBuilder();
        navDescBuilder.Append("Navigate to a route in the application. The 'args' parameter is a dictionary of key-value pairs where keys are parameter names from the route schema. ");
        navDescBuilder.Append("Available routes and their parameters: ");
        foreach (var cls in aiClasses)
        {
            navDescBuilder.Append($"{cls.Route}(");
            var props = cls.Properties.ToList();
            for (int j = 0; j < props.Count; j++)
            {
                var p = props[j];
                navDescBuilder.Append(p.Name);
                if (p.IsEnum && !p.EnumValues.IsDefaultOrEmpty)
                    navDescBuilder.Append($": {string.Join("|", p.EnumValues)}");
                if (!p.IsRequired)
                    navDescBuilder.Append("?");
                if (j < props.Count - 1)
                    navDescBuilder.Append(", ");
            }
            navDescBuilder.Append(") ");
        }

        sb.AppendLine($"                description: \"{EscapeString(navDescBuilder.ToString().TrimEnd())}\")");
        sb.AppendLine("        ];");
        sb.AppendLine("    }");
        sb.AppendLine();

        // GetAiToolApplicableGeneratedRoutes
        sb.AppendLine("    [global::System.ComponentModel.Description(\"This provides a list of AI tool applicable routes - routes that have descriptions and parameters that an AI can populate from user intent\")]");
        sb.AppendLine("    public global::Shiny.Infrastructure.GeneratedRouteInfo[] GetAiToolApplicableGeneratedRoutes() =>");
        sb.AppendLine("    [");

        for (int i = 0; i < aiClasses.Count; i++)
        {
            var cls = aiClasses[i];
            sb.AppendLine($"        new global::Shiny.Infrastructure.GeneratedRouteInfo(");
            sb.AppendLine($"            \"{EscapeString(cls.Route)}\",");
            sb.AppendLine($"            \"{EscapeString(cls.Description)}\",");
            sb.AppendLine("            [");

            var properties = cls.Properties.ToList();
            for (int j = 0; j < properties.Count; j++)
            {
                var p = properties[j];
                var requiredLiteral = p.IsRequired ? "true" : "false";
                sb.Append($"                new global::Shiny.Infrastructure.GeneratedRouteParameter(");
                sb.Append($"\"{EscapeString(p.Name)}\", \"{GetParameterDescription(p)}\", \"{EscapeString(GetParameterTypeName(p))}\", {requiredLiteral})");
                if (j < properties.Count - 1)
                    sb.Append(",");
                sb.AppendLine();
            }

            sb.Append("            ]");
            sb.Append(")");
            if (i < aiClasses.Count - 1)
                sb.Append(",");
            sb.AppendLine();
        }

        sb.AppendLine("    ];");
        sb.AppendLine();

        // NavigateToRoute method
        sb.AppendLine($"    [global::System.ComponentModel.Description(\"Navigate to a route in the application, passing parameters as key-value pairs. Returns a confirmation message.\")]");
        sb.AppendLine($"    public async global::System.Threading.Tasks.Task<string> {options.AiNavigateMethodName}(");
        sb.AppendLine("        [global::System.ComponentModel.Description(\"The route name to navigate to\")] string route,");
        sb.AppendLine("        [global::System.ComponentModel.Description(\"Route parameters as key-value pairs where keys are parameter names from GetGeneratedRouteInfo\")] global::System.Collections.Generic.Dictionary<string, string>? args = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (route)");
        sb.AppendLine("        {");

        foreach (var cls in aiClasses)
        {
            sb.AppendLine($"            case \"{EscapeString(cls.Route)}\":");
            if (cls.Properties.Any())
            {
                sb.AppendLine($"                await _navigator.NavigateTo<{cls.ViewModelFullName}>(vm =>");
                sb.AppendLine("                {");
                sb.AppendLine("                    if (args != null)");
                sb.AppendLine("                    {");

                foreach (var p in cls.Properties)
                {
                    sb.AppendLine($"                        if (args.TryGetValue(\"{EscapeString(p.Name)}\", out var _{ToCamelCase(p.Name)}))");
                    sb.AppendLine($"                            vm.{p.Name} = {GenerateConversion(p, $"_{ToCamelCase(p.Name)}")};");
                }

                sb.AppendLine("                    }");
                sb.AppendLine("                });");
            }
            else
            {
                sb.AppendLine($"                await _navigator.NavigateTo<{cls.ViewModelFullName}>();");
            }
            sb.AppendLine($"                return $\"Successfully navigated to {EscapeString(cls.Route)}\";");
        }

        sb.AppendLine("            default:");
        sb.AppendLine("                return $\"Unknown route: {route}\";");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}"); // close AiMauiShellTools class
        sb.AppendLine();

        // Generate AddAiTools extension method on ShinyAppBuilder
        sb.AppendLine($"public static class {options.AiToolsClassName}Extensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Registers <see cref=\"{options.AiToolsClassName}\"/> as a singleton in the service collection.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static global::Shiny.ShinyAppBuilder AddAiTools(this global::Shiny.ShinyAppBuilder builder)");
        sb.AppendLine("    {");
        sb.AppendLine($"        builder.MauiBuilder.Services.AddSingleton<{options.AiToolsClassName}>();");
        sb.AppendLine("        return builder;");
        sb.AppendLine("    }");

        // The caller adds the final "}" to close this class
    }

    static string ToCamelCase(string text)
    {
        if (string.IsNullOrEmpty(text) || char.IsLower(text[0]))
            return text;
        return char.ToLower(text[0]) + text.Substring(1);
    }

    static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    static string EscapeString(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    static string GetDefaultValue(string typeName)
    {
        return typeName.EndsWith("?") || typeName == "string" ? "null" : "default";
    }

    static string GetParameterTypeName(ShellPropertyInfo p)
    {
        if (p.IsEnum)
            return "string";
        return p.TypeName;
    }

    static string GetParameterDescription(ShellPropertyInfo p)
    {
        var desc = p.Description != null ? EscapeString(p.Description) : "";
        if (p.IsEnum && !p.EnumValues.IsDefaultOrEmpty)
        {
            var values = string.Join(", ", p.EnumValues);
            desc = string.IsNullOrEmpty(desc)
                ? $"Must be one of: {values}"
                : $"{desc}. Must be one of: {values}";
        }
        return desc;
    }

    static string GenerateConversion(ShellPropertyInfo prop, string varName)
    {
        var typeName = prop.TypeName;

        // Strip nullable wrapper for conversion logic
        var baseType = typeName.EndsWith("?") ? typeName.Substring(0, typeName.Length - 1) : typeName;

        if (prop.IsEnum)
            return $"(global::{baseType})global::System.Enum.Parse(typeof(global::{baseType}), {varName}, true)";

        return baseType switch
        {
            "string" => varName,
            "int" or "System.Int32" => $"int.Parse({varName})",
            "long" or "System.Int64" => $"long.Parse({varName})",
            "short" or "System.Int16" => $"short.Parse({varName})",
            "byte" or "System.Byte" => $"byte.Parse({varName})",
            "float" or "System.Single" => $"float.Parse({varName})",
            "double" or "System.Double" => $"double.Parse({varName})",
            "decimal" or "System.Decimal" => $"decimal.Parse({varName})",
            "bool" or "System.Boolean" => $"bool.Parse({varName})",
            "System.Guid" => $"global::System.Guid.Parse({varName})",
            "System.DateTime" => $"global::System.DateTime.Parse({varName})",
            "System.DateTimeOffset" => $"global::System.DateTimeOffset.Parse({varName})",
            "System.TimeSpan" => $"global::System.TimeSpan.Parse({varName})",
            "System.Uri" => $"new global::System.Uri({varName})",
            _ => $"({typeName})global::System.Convert.ChangeType({varName}, typeof({baseType}))"
        };
    }
}

record GeneratorOptions(
    bool GenerateRouteConstants,
    bool GenerateNavExtensions,
    bool GenerateAiExtensions,
    string AiExtensionsClassName,
    string AiNavigateMethodName,
    string AiToolsClassName
);

record ShellMapInfo(
    string ViewModelName,
    string ViewModelFullName,
    string PageTypeName,
    string PageTypeFullName,
    string Route,
    string GeneratedName,
    bool RegisterRoute,
    string Description,
    ImmutableArray<ShellPropertyInfo> Properties,
    Location? AttributeLocation
);

record ShellPropertyInfo(
    string Name,
    string TypeName,
    bool IsRequired,
    string Description,
    bool IsEnum = false,
    ImmutableArray<string> EnumValues = default
);