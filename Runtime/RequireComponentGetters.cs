using System;

namespace ComponentUtils {
/// <summary>
/// Add to a MonoBehaviour class to generate lazy-loaded properties for each of its RequireComponent types.
/// The generated field names will be the same as the type names inside RequireComponent(typeof(...)).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class RequireComponentGettersAttribute : Attribute {
    /// <summary>
    /// The visibility of the generated properties. Must be a literal string.
    /// Default is 'public'.
    /// </summary>
    public string Visibility { get; }

    public RequireComponentGettersAttribute(string visibility = "public") {
        Visibility = visibility;
    }
}

/// <summary>
/// Add to a MonoBehaviour class to generate a lazy-loaded property and a corresponding RequireComponent attribute.
/// Can be used to customize getter name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class RequireComponentGetterAttribute : Attribute {
    /// <summary>
    /// The required component type.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// The name of the generated property getter.
    /// Usual C# property naming restrictions apply.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The visibility of the generated properties. Must be a literal string.
    /// Default is 'public'.
    /// </summary>
    public string Visibility { get; }

    public RequireComponentGetterAttribute(Type type, string name, string visibility = "public") {
        Type = type;
        Name = name;
        Visibility = visibility;
    }
}
}
