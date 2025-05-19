using System.Runtime.CompilerServices;

namespace CK.Core;

/// <summary>
/// Interface for attributes that captures the source file path where the attribute
/// decorates a type.
/// </summary>
/// <see cref="CallerFilePathAttribute"/>
public interface IEmbeddedResourceTypeAttribute
{
    /// <summary>
    /// Gets the source file path that where the attribute
    /// decorates a type.
    /// <para>
    /// When null, resources associated to the type cannot be located.
    /// </para>
    /// </summary>
    string? CallerFilePath { get; }
}
