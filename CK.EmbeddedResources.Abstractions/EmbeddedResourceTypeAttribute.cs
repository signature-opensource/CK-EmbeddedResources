using System;
using System.Runtime.CompilerServices;

namespace CK.Core;

/// <summary>
/// Default and specializable implementation of <see cref="IEmbeddedResourceTypeAttribute"/>.
/// </summary>
[AttributeUsage( AttributeTargets.Class|AttributeTargets.Struct|AttributeTargets.Enum, AllowMultiple = false, Inherited = false )]
public class EmbeddedResourceTypeAttribute : Attribute, IEmbeddedResourceTypeAttribute
{
    /// <summary>
    /// Initializes a new <see cref="EmbeddedResourceTypeAttribute"/>.
    /// </summary>
    /// <param name="callerFilePath">Automatically set by the Roslyn comiler.</param>
    public EmbeddedResourceTypeAttribute( [CallerFilePath]string? callerFilePath = null )
    {
        CallerFilePath = callerFilePath;
    }

    /// <inheritdoc />
    public string? CallerFilePath { get; }
}
