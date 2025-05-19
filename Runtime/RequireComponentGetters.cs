using System;

namespace ComponentUtils {
    /// <summary>
    /// Add to a MonoBehaviour class to generate lazy-loaded properties for each of its RequireComponent types.
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
}
