using System.Collections.Concurrent;

namespace BuildingBlocks.Core.Types;

/// <summary>
///     Maps CLR types to short string names and back.
///     Useful for serialization-friendly message type identification.
/// </summary>
public static class TypeMapper
{
    private static readonly ConcurrentDictionary<string, Type> ShortNameToType = new();
    private static readonly ConcurrentDictionary<Type, string> TypeToShortName = new();

    static TypeMapper()
    {
        // Register common types by default using FullName
    }

    /// <summary>
    ///     Register a short name for a type.
    /// </summary>
    public static void AddShortTypeName<T>(string? shortName = null)
        where T : class
    {
        AddShortTypeName(typeof(T), shortName);
    }

    /// <summary>
    ///     Register a short name for a type.
    /// </summary>
    public static void AddShortTypeName(Type type, string? shortName = null)
    {
        var name = shortName ?? type.FullName ?? type.Name;
        ShortNameToType[name] = type;
        TypeToShortName[type] = name;
    }

    /// <summary>
    ///     Returns the short name for the given type.
    ///     Auto-registers if not already mapped, using FullName as default.
    /// </summary>
    public static string GetShortTypeName<T>()
    {
        return GetShortTypeName(typeof(T));
    }

    /// <summary>
    ///     Returns the short name for the given type.
    ///     Auto-registers if not already mapped, using FullName as default.
    /// </summary>
    public static string GetShortTypeName(Type type)
    {
        if (TypeToShortName.TryGetValue(type, out var name))
        {
            return name;
        }

        var newName = type.FullName ?? type.Name;
        AddShortTypeName(type, newName);
        return newName;
    }

    /// <summary>
    ///     Resolves a type from its short name. Returns null if not found.
    /// </summary>
    public static Type? GetType(string shortTypeName)
    {
        return ShortNameToType.TryGetValue(shortTypeName, out var type) ? type : null;
    }
}
