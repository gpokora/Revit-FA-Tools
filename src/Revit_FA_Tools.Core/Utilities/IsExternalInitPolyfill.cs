using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill for IsExternalInit to support init-only properties in .NET Framework
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}