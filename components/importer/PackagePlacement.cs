namespace EAJsonModelImporter;

// Package ownership is independent of domain/diagram membership. A null path
// means preserve placement; an empty path explicitly means the model root.
internal static class PackagePlacement
{
    public static string Normalize(string path)
    {
        if (path.Length == 0) return path;
        var parts = path.Split('/');
        if (parts.Any(p => p.Length == 0 || p != p.Trim() || p is "." or ".."
            || p.Contains('\\') || p.Any(char.IsControl)))
            throw new InvalidDataException("ea_package_path must be a relative, slash-separated package path without empty, '.' or '..' segments: " + path);
        return string.Join('/', parts);
    }

    public static Dictionary<string, TElement> Index<TPackage, TElement>(TPackage root,
        Func<TPackage, IEnumerable<TPackage>> children,
        Func<TPackage, IEnumerable<TElement>> elements,
        Func<TPackage, string> packageName, Func<TElement, string> elementName)
    {
        var result = new Dictionary<string, TElement>(StringComparer.OrdinalIgnoreCase);
        void Visit(TPackage package)
        {
            foreach (var element in elements(package))
                if (!result.TryAdd(elementName(element), element))
                    throw new InvalidDataException("Ambiguous element name '" + elementName(element)
                        + "' within the selected model. LinkML class/enum keys must be unique; no update was applied.");
            var nested = children(package).ToList();
            if (nested.GroupBy(packageName, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new InvalidDataException("Ambiguous sibling package names within '" + packageName(package) + "'. No update was applied.");
            foreach (var child in nested) Visit(child);
        }
        Visit(root);
        return result;
    }

    public static TPackage Resolve<TPackage>(TPackage root, string path,
        Func<TPackage, IEnumerable<TPackage>> children, Func<TPackage, string> name,
        Func<TPackage, string, TPackage> create) where TPackage : class
    {
        Normalize(path);
        var current = root;
        foreach (string segment in path.Length == 0 ? [] : path.Split('/'))
        {
            var matches = children(current).Where(p => name(p).Equals(segment, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 1) throw new InvalidDataException("Ambiguous package path: " + path);
            current = matches.SingleOrDefault() ?? create(current, segment);
        }
        return current;
    }
}
