using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CommandRefGen;

/// <summary>
/// Expands the DTO types a command takes as a single structured argument.
///
/// The package's convention: a command needing several related values declares a small type
/// implementing <c>IStructuredCommandInput</c> and takes it as one parameter, which the caller passes
/// as a JSON object. A live listing shows only the type name for such a parameter — its fields appear
/// solely in the per-command JSON schema — so reading the sources is the only way the reference can tell
/// a reader what to actually put in that object.
///
/// The members mirror what <c>JsonSchemaGenerator</c> reflects over: public instance fields and
/// read/write properties, minus <c>[JsonIgnore]</c>, named by <c>[CliArg]</c>, then by Newtonsoft's
/// <c>[JsonProperty]</c>, then by the member itself. Members carry no default value — the schema has
/// no place for one.
/// </summary>
public sealed class StructuredInputReader(
    IReadOnlyDictionary<string, List<TypeDeclarationSyntax>> types,
    IReadOnlySet<string> ambiguousTypes,
    ConstantEvaluator constants,
    Func<TypeSyntax, string> typeName,
    Action<string> warn)
{
    /// <summary>The marker interface a parameter type must implement to be expanded.</summary>
    private const string MarkerInterface = "IStructuredCommandInput";

    /// <summary>Ambiguous base names already reported, so that one unresolvable type is one message.</summary>
    private readonly HashSet<string> reportedAmbiguous = new(StringComparer.Ordinal);

    /// <summary>Collection types <c>JsonSchemaGenerator</c> treats as JSON arrays.</summary>
    private static readonly string[] CollectionTypes =
    {
        "List", "IList", "IEnumerable", "ICollection", "IReadOnlyList", "IReadOnlyCollection",
    };

    /// <summary>
    /// Members of the structured type <paramref name="type"/> denotes, or an empty list when it is a
    /// plain value. Arrays and lists are expanded through to their element type, matching how the
    /// package emits an array of object schemas.
    /// </summary>
    public IReadOnlyList<CommandArg> Expand(TypeSyntax type, string context) =>
        Expand(type, context, new HashSet<string>(StringComparer.Ordinal));

    private IReadOnlyList<CommandArg> Expand(TypeSyntax type, string context, HashSet<string> visiting)
    {
        if (ElementTypeName(type) is not { } name || !types.TryGetValue(name, out var parts))
            return Array.Empty<CommandArg>();

        // JsonSchemaGenerator expands a parameter only when its type implements the marker interface;
        // anything else schemas as a plain value, and the reference says the same. A base name this tool
        // cannot resolve makes that verdict unreliable, and the cost is the whole object rather than a
        // few inherited members, so it is reported here rather than deeper down.
        var unresolvedBases = new HashSet<string>(StringComparer.Ordinal);
        if (!ImplementsMarker(parts, new HashSet<string>(StringComparer.Ordinal), unresolvedBases))
        {
            foreach (var unresolved in unresolvedBases.Where(reportedAmbiguous.Add).OrderBy(n => n, StringComparer.Ordinal))
                warn($"{context}: '{name}' derives from '{unresolved}', which the package declares in more than one namespace — this tool cannot tell whether the argument is a structured input, so it is documented without its fields");

            return Array.Empty<CommandArg>();
        }

        // Past this point the argument really is a nested object, so an unresolvable name is a hole in
        // the documentation rather than a non-event: merging the members of two unrelated types would
        // describe an object the server does not accept, and skipping quietly would drop the fields the
        // whole expansion exists to show.
        if (ambiguousTypes.Contains(name))
        {
            warn($"{context}: the package declares '{name}' in more than one namespace and this tool cannot tell which one is meant — the argument is documented without its fields");
            return Array.Empty<CommandArg>();
        }

        // A type the caller cannot instantiate is schemad as a plain string by the package, so it carries
        // no fields to show. Every part has to be concrete: `abstract` on one part makes the type abstract.
        if (!parts.All(IsConcrete))
            return Array.Empty<CommandArg>();

        // A type that contains itself would otherwise expand forever; the package emits an open object
        // in that case, and the reference stops at the same point.
        if (!visiting.Add(name))
            return Array.Empty<CommandArg>();

        try
        {
            var members = SchemaMembers(name, new HashSet<string>(StringComparer.Ordinal))
                .SelectMany(member => ReadMember(member, name, visiting))
                .ToList();

            // A structured input with nothing in it is a contradiction: either the package declared an
            // empty object, or its members are written in a shape this tool does not read — a positional
            // record, say, whose properties exist only after compilation.
            if (members.Count == 0)
                warn($"{context}: '{name}' is a structured input with no readable members — the argument is documented without its fields");

            return members;
        }
        finally
        {
            visiting.Remove(name);
        }
    }

    /// <summary>
    /// Members the schema generator would reflect over for <paramref name="name"/>: those of every
    /// <c>partial</c> part, then those inherited from base types the package declares, because
    /// <c>GetFields</c>/<c>GetProperties</c> report inherited public members too.
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> SchemaMembers(string name, HashSet<string> seen)
    {
        if (!seen.Add(name) || !Resolvable(name, $"base type '{name}'", out var parts))
            yield break;

        foreach (var member in parts.SelectMany(part => part.Members))
            yield return member;

        foreach (var member in parts.SelectMany(BaseNames).SelectMany(baseName => SchemaMembers(baseName, seen)))
            yield return member;
    }

    /// <summary>
    /// Looks a base type up, refusing an ambiguous name. Merging the members of two unrelated types that
    /// happen to share a name would describe an object the server does not accept, and it is just as
    /// wrong one level down a base-type chain as it is at the argument itself. The argument's own type
    /// is reported separately, in <see cref="Expand(TypeSyntax, string, HashSet{string})"/>, where the
    /// consequence is bigger: there it is the whole object that goes undocumented.
    /// </summary>
    private bool Resolvable(string name, string context, out List<TypeDeclarationSyntax> parts)
    {
        if (!types.TryGetValue(name, out parts!))
            return false;

        if (!ambiguousTypes.Contains(name))
            return true;

        // Reached once from the marker probe and once while collecting members; one report is enough.
        if (reportedAmbiguous.Add(name))
            warn($"{context}: the package declares '{name}' in more than one namespace and this tool cannot tell which one is meant — the members inherited from it are missing from the reference");

        parts = null!;
        return false;
    }

    private IEnumerable<CommandArg> ReadMember(MemberDeclarationSyntax member, string owner, HashSet<string> visiting)
    {
        switch (member)
        {
            case FieldDeclarationSyntax field when IsSchemaField(field):
                foreach (var variable in field.Declaration.Variables)
                    yield return ToArg(member, variable.Identifier.ValueText, field.Declaration.Type, owner, visiting);
                break;

            case PropertyDeclarationSyntax property when IsSchemaProperty(property):
                yield return ToArg(member, property.Identifier.ValueText, property.Type, owner, visiting);
                break;
        }
    }

    private CommandArg ToArg(
        MemberDeclarationSyntax member,
        string memberName,
        TypeSyntax memberType,
        string owner,
        HashSet<string> visiting)
    {
        var cliArg = Attribute(member, "CliArg");
        var name = cliArg is null ? null : ConstructorString(cliArg, 0, "name");
        name ??= JsonPropertyName(member) ?? memberName;

        var description = cliArg is null ? string.Empty : Prose.Collapse(ConstructorString(cliArg, 1, "description") ?? string.Empty);
        var required = cliArg is not null && NamedTrue(cliArg, "Required");

        return new CommandArg(
            name,
            description,
            typeName(memberType),
            required,
            DefaultValue: null,
            Expand(memberType, $"{owner}.{memberName}", visiting));
    }

    /// <summary>
    /// The name a Newtonsoft <c>[JsonProperty]</c> gives the member, in either of its forms — positional
    /// <c>[JsonProperty("x")]</c> or named <c>[JsonProperty(PropertyName = "x")]</c>. Reflection sees the
    /// same <c>PropertyName</c> for both, so reading only one of them would document a member under a name
    /// the server does not answer to.
    /// </summary>
    private string? JsonPropertyName(MemberDeclarationSyntax member)
    {
        var attribute = Attribute(member, "JsonProperty");
        if (attribute is null)
            return null;

        return ConstructorString(attribute, 0, "propertyName") ?? NamedString(attribute, "PropertyName");
    }

    private string? NamedString(AttributeSyntax attribute, string name)
    {
        var argument = attribute.ArgumentList?.Arguments
            .FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == name);

        return argument is null ? null : constants.Evaluate(argument.Expression) as string;
    }

    /// <summary>True when the declaration can be instantiated, i.e. is neither an interface nor abstract.</summary>
    private static bool IsConcrete(TypeDeclarationSyntax declaration) =>
        declaration is not InterfaceDeclarationSyntax && !declaration.Modifiers.Any(SyntaxKind.AbstractKeyword);

    /// <summary>
    /// True when the type implements the marker interface, directly or through a base type. The cycle
    /// guard counts type names rather than declarations: the parts of one <c>partial</c> type are that
    /// same type, so a marker written on the second part has to count as much as one on the first.
    /// </summary>
    private bool ImplementsMarker(IEnumerable<TypeDeclarationSyntax> parts, HashSet<string> seen, ISet<string> unresolved) =>
        parts.SelectMany(BaseNames).Any(baseName => ImplementsMarker(baseName, seen, unresolved));

    private bool ImplementsMarker(string name, HashSet<string> seen, ISet<string> unresolved)
    {
        if (name == MarkerInterface)
            return true;

        if (!seen.Add(name))
            return false;

        // Silent: the caller decides what an unresolvable base costs, and it costs different things
        // depending on whether the marker was found by another route.
        if (ambiguousTypes.Contains(name))
        {
            unresolved.Add(name);
            return false;
        }

        return types.TryGetValue(name, out var parts) && ImplementsMarker(parts, seen, unresolved);
    }

    /// <summary>The simple names of the types a declaration derives from or implements.</summary>
    private static IEnumerable<string> BaseNames(TypeDeclarationSyntax declaration) =>
        (declaration.BaseList?.Types ?? Enumerable.Empty<BaseTypeSyntax>())
            .Select(baseType => SimpleName(baseType.Type))
            .OfType<string>();

    /// <summary>Public, writable instance fields — what reflection would report to the schema generator.</summary>
    private static bool IsSchemaField(FieldDeclarationSyntax field) =>
        field.Modifiers.Any(SyntaxKind.PublicKeyword) &&
        !field.Modifiers.Any(SyntaxKind.StaticKeyword) &&
        !field.Modifiers.Any(SyntaxKind.ConstKeyword) &&
        !field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) &&
        Attribute(field, "JsonIgnore") is null;

    /// <summary>Public instance properties with both accessors and no index parameters.</summary>
    private static bool IsSchemaProperty(PropertyDeclarationSyntax property)
    {
        if (!property.Modifiers.Any(SyntaxKind.PublicKeyword) || property.Modifiers.Any(SyntaxKind.StaticKeyword))
            return false;
        if (Attribute(property, "JsonIgnore") is not null)
            return false;

        var accessors = property.AccessorList?.Accessors;
        if (accessors is null)
            return false;

        var readable = accessors.Value.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
        var writable = accessors.Value.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration) || a.IsKind(SyntaxKind.InitAccessorDeclaration));
        return readable && writable;
    }

    /// <summary>The type whose members matter: the element type for arrays and lists, the type itself otherwise.</summary>
    private static string? ElementTypeName(TypeSyntax type)
    {
        switch (type)
        {
            case ArrayTypeSyntax array:
                return ElementTypeName(array.ElementType);

            case NullableTypeSyntax nullable:
                return ElementTypeName(nullable.ElementType);

            case GenericNameSyntax generic when generic.TypeArgumentList.Arguments.Count == 1 &&
                                                CollectionTypes.Contains(generic.Identifier.ValueText):
                return ElementTypeName(generic.TypeArgumentList.Arguments[0]);

            default:
                return SimpleName(type);
        }
    }

    private static string? SimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => SimpleName(qualified.Right),
        GenericNameSyntax generic => generic.Identifier.ValueText,
        _ => null,
    };

    private static AttributeSyntax? Attribute(MemberDeclarationSyntax member, string name) =>
        member.AttributeLists
            .SelectMany(list => list.Attributes)
            .FirstOrDefault(attribute => AttributeName(attribute) == name || AttributeName(attribute) == name + "Attribute");

    private static string AttributeName(AttributeSyntax attribute) => attribute.Name switch
    {
        QualifiedNameSyntax qualified => qualified.Right.ToString(),
        var other => other.ToString(),
    };

    /// <summary>
    /// The constructor argument at <paramref name="index"/>, whether the caller wrote it positionally or
    /// as <c>name:</c>. Reading only the position would swap a member's name and description.
    /// </summary>
    private string? ConstructorString(AttributeSyntax? attribute, int index, string parameterName)
    {
        var arguments = attribute?.ArgumentList?.Arguments;
        if (arguments is null)
            return null;

        var named = arguments.Value.FirstOrDefault(a => a.NameColon?.Name.Identifier.ValueText == parameterName);
        if (named is not null)
            return constants.Evaluate(named.Expression) as string;

        var positional = arguments.Value.Where(a => a.NameEquals is null && a.NameColon is null).ToList();
        return index < positional.Count ? constants.Evaluate(positional[index].Expression) as string : null;
    }

    private bool NamedTrue(AttributeSyntax attribute, string name)
    {
        var argument = attribute.ArgumentList?.Arguments
            .FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == name);

        return argument is not null && constants.Evaluate(argument.Expression) is true;
    }
}
