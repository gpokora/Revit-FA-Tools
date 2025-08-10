using System.Collections.Generic;

namespace Revit_FA_Tools
{
    /// <summary>
    /// Extension methods for Dictionary to provide .NET Core compatibility in .NET Framework 4.8
    /// </summary>
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Gets the value associated with the specified key, or returns the default value if the key is not found.
        /// This provides .NET Core GetValueOrDefault functionality for .NET Framework 4.8
        /// </summary>
        /// <typeparam name="TKey">The type of the keys in the dictionary</typeparam>
        /// <typeparam name="TValue">The type of the values in the dictionary</typeparam>
        /// <param name="dictionary">The dictionary to search</param>
        /// <param name="key">The key to search for</param>
        /// <param name="defaultValue">The default value to return if the key is not found</param>
        /// <returns>The value associated with the key, or the default value if not found</returns>
        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            if (dictionary == null)
                return defaultValue;

            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : defaultValue;
        }

        /// <summary>
        /// Gets the value associated with the specified key, or returns the default value for the type if the key is not found.
        /// </summary>
        /// <typeparam name="TKey">The type of the keys in the dictionary</typeparam>
        /// <typeparam name="TValue">The type of the values in the dictionary</typeparam>
        /// <param name="dictionary">The dictionary to search</param>
        /// <param name="key">The key to search for</param>
        /// <returns>The value associated with the key, or the default value for TValue if not found</returns>
        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
        {
            return GetValueOrDefault(dictionary, key, default(TValue));
        }
    }
}