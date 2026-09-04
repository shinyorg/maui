namespace Shiny.Infrastructure;


/// <summary>
/// A single compiled app link template.
/// </summary>
/// <param name="Template">The template as written on the attribute.</param>
/// <param name="Route">The Shell route it targets.</param>
/// <param name="ViewModelType">The ViewModel mapped to that route.</param>
/// <param name="RegisterRoute">
/// False when the route is declared as a ShellContent in AppShell XAML, which decides whether an
/// inbound link resets the stack or pushes onto it.
/// </param>
/// <param name="Apply">
/// Source-generated binder that assigns the extracted values onto the ViewModel, returning false
/// when a required value is absent or unparseable.
/// </param>
public record RegisteredAppLink(
    string Template,
    string Route,
    Type ViewModelType,
    bool RegisterRoute,
    Func<object, IReadOnlyDictionary<string, string>, bool> Apply
)
{
    internal string[] Segments { get; } = SplitTemplate(Template);

    /// <summary>
    /// Literal segments beat tokens, so "product/featured" is tried before "product/{id}".
    /// </summary>
    internal int Specificity { get; } = SplitTemplate(Template).Count(x => !IsToken(x));

    internal static bool IsToken(string segment)
        => segment.Length > 1 && segment[0] == '{' && segment[segment.Length - 1] == '}';

    internal static string TokenName(string segment)
        => segment.Substring(1, segment.Length - 2);

    static string[] SplitTemplate(string template)
        => template.Trim('/').Split(['/'], StringSplitOptions.RemoveEmptyEntries);
}


/// <summary>
/// Holds the app link templates declared through <see cref="ShellMapAttribute{TPage}"/> and
/// matches inbound URIs against them. Registration order does not matter - candidates are always
/// returned most-specific first.
/// </summary>
public class AppLinkRegistry
{
    readonly List<RegisteredAppLink> links = new();

    public IReadOnlyList<RegisteredAppLink> Links => this.links;

    public void Add(RegisteredAppLink link)
    {
        this.links.Add(link);

        // Sort on write - matching happens far more often than registration, and registration
        // is a one-time startup cost.
        this.links.Sort(static (a, b) =>
        {
            var bySpecificity = b.Specificity.CompareTo(a.Specificity);
            if (bySpecificity != 0)
                return bySpecificity;

            var byLength = b.Segments.Length.CompareTo(a.Segments.Length);
            return byLength != 0 ? byLength : string.CompareOrdinal(a.Template, b.Template);
        });
    }


    /// <summary>
    /// Every template whose path shape matches, most specific first. The router walks these in
    /// order because a later candidate can still win if the more specific one fails to bind.
    /// </summary>
    public IEnumerable<AppLinkMatch> GetMatches(Uri uri)
    {
        var segments = GetPathSegments(uri);
        var query = ParseQuery(uri);

        foreach (var link in this.links)
        {
            if (link.Segments.Length != segments.Length)
                continue;

            var values = new Dictionary<string, string>(query, StringComparer.OrdinalIgnoreCase);
            var matched = true;

            for (var i = 0; i < link.Segments.Length; i++)
            {
                var templateSegment = link.Segments[i];
                if (RegisteredAppLink.IsToken(templateSegment))
                {
                    // Path wins over a query value of the same name.
                    values[RegisteredAppLink.TokenName(templateSegment)] = segments[i];
                }
                else if (!string.Equals(templateSegment, segments[i], StringComparison.OrdinalIgnoreCase))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
                yield return new AppLinkMatch(link.Route, link.Template, link.ViewModelType, values, uri);
        }
    }


    public RegisteredAppLink? Find(string template)
    {
        foreach (var link in this.links)
        {
            if (link.Template == template)
                return link;
        }
        return null;
    }


    /// <summary>
    /// A custom scheme puts the first path segment in the authority - "myapp://product/123" has
    /// Host "product" and AbsolutePath "/123". For http(s) the host is a real domain and only the
    /// path counts.
    /// </summary>
    internal static string[] GetPathSegments(Uri uri)
    {
        var path = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.ToString();

        if (uri.IsAbsoluteUri &&
            !uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(uri.Host))
        {
            path = uri.Host + "/" + path.TrimStart('/');
        }

        var raw = path.Trim('/').Split(['/'], StringSplitOptions.RemoveEmptyEntries);
        var segments = new string[raw.Length];
        for (var i = 0; i < raw.Length; i++)
            segments[i] = Uri.UnescapeDataString(raw[i]);

        return segments;
    }


    internal static Dictionary<string, string> ParseQuery(Uri uri)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!uri.IsAbsoluteUri)
            return result;

        var query = uri.Query;
        if (string.IsNullOrEmpty(query))
            return result;

        foreach (var pair in query.TrimStart('?').Split(['&'], StringSplitOptions.RemoveEmptyEntries))
        {
            var index = pair.IndexOf('=');
            if (index < 0)
            {
                result[Uri.UnescapeDataString(pair)] = string.Empty;
            }
            else
            {
                var key = Uri.UnescapeDataString(pair.Substring(0, index));
                // '+' is a legal space encoding in a query string but not in a path.
                var value = Uri.UnescapeDataString(pair.Substring(index + 1).Replace("+", "%20"));
                result[key] = value;
            }
        }
        return result;
    }
}
