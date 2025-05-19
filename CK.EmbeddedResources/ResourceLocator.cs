using CK.Core;
using System;
using System.IO;

namespace CK.EmbeddedResources;

/// <summary>
/// Locator for a resource in a <see cref="IResourceContainer"/>.
/// <para>
/// The <c>default</c> value is <see cref="IsValid"/> false: this makes <see cref="Nullable{T}"/> useless for this type.
/// </para>
/// </summary>
public readonly struct ResourceLocator : IEquatable<ResourceLocator>
{
    readonly IResourceContainer _container;
    readonly string _fullName;

    /// <summary>
    /// Initializes a new resource locator.
    /// </summary>
    /// <param name="container">The type resources that contains this resource.</param>
    /// <param name="fullResourceName">the resource name in the <paramref name="container"/>.</param>
    public ResourceLocator( IResourceContainer container, string fullResourceName )
    {
        Throw.CheckNotNullArgument( container );
        Throw.CheckNotNullArgument( fullResourceName );
        Throw.CheckArgument( fullResourceName.StartsWith( container.ResourcePrefix ) );
        Throw.CheckArgument( (fullResourceName.Length - container.ResourcePrefix.Length) is >= 1 and <= IResourceContainer.MaxNameLength );
        Throw.CheckArgument( fullResourceName[^1] is not '/' and not '\\' );
        _container = container;
        _fullName = fullResourceName;
    }

    ResourceLocator( string fullResourceName, IResourceContainer container )
    {
        _container = container;
        _fullName = fullResourceName;
    }

    /// <summary>
    /// Unsafe initialization of a <see cref="ResourceLocator"/>: no check are done.
    /// </summary>
    /// <param name="container">The container.</param>
    /// <param name="fullResourceName">The full resource name.</param>
    /// <returns>A resource locator.</returns>
    public static ResourceLocator UnsafeCreate( IResourceContainer container, string fullResourceName ) => new ResourceLocator( fullResourceName, container );

    /// <summary>
    /// Gets whether this locator is valid: the <see cref="Container"/> is not null
    /// and the <see cref="FullResourceName"/> is not null, not empty nor whitespace.
    /// <para>
    /// Whether the resource actuallly exists or not is not known.
    /// </para>
    /// Only the <c>default</c> of this type is invalid.
    /// </summary>
    public bool IsValid => _container != null;

    /// <summary>
    /// Gets the type resources that contains this resource.
    /// </summary>
    public IResourceContainer Container => _container;

    /// <summary>
    /// Gets the resource name in the <see cref="Container"/>.
    /// <para>
    /// This is the full resource name that includes the <see cref="IResourceContainer.ResourcePrefix"/>.
    /// </para>
    /// </summary>
    public string FullResourceName => _fullName;

    /// <summary>
    /// Gets the resource name without the <see cref="IResourceContainer.ResourcePrefix"/>.
    /// </summary>
    public ReadOnlySpan<char> ResourceName
    {
        get
        {
            Throw.CheckState( IsValid );
            return _fullName.AsSpan( _container.ResourcePrefix.Length );
        }
    }

    /// <summary>
    /// Gets the name of this resource without any folder related information.
    /// </summary>
    public ReadOnlySpan<char> Name => Path.GetFileName( _fullName.AsSpan() );

    /// <summary>
    /// Gets the local file path bound to this resource. See <see cref="IResourceContainer.HasLocalFilePathSupport"/>.
    /// <para>
    /// This is a simple relay to <see cref="IResourceContainer.GetLocalFilePath(ResourceLocator)"/>.
    /// </para>
    /// </summary>
    public string? LocalFilePath => _container.GetLocalFilePath( this );

    /// <summary>
    /// Gets the resource content.
    /// See <see cref="IResourceContainer.GetStream(ResourceLocator)"/>.
    /// </summary>
    /// <returns>The resource's content stream.</returns>
    public Stream GetStream() => _container.GetStream( this );

    /// <summary>
    /// Writes the content of this resource to a stream.
    /// See <see cref="IResourceContainer.WriteStream(ResourceLocator, Stream)"/>.
    /// </summary>
    /// <param name="target">The target stream.</param>
    public void WriteStream( Stream target ) => _container.WriteStream( this, target );

    /// <summary>
    /// Reads the resource content as a text.
    /// </summary>
    /// <returns>The content as a text.</returns>
    public string ReadAsText() => _container.ReadAsText( this );

    /// <summary>
    /// To be equal the <see cref="Container"/> must be the same and <see cref="FullResourceName"/>
    /// must be equal (<see cref="StringComparison.Ordinal"/>).
    /// </summary>
    /// <param name="other">The other locator.</param>
    /// <returns>True if the resources are the same.</returns>
    public bool Equals( ResourceLocator other )
    {
        return _container == other._container
               && (!IsValid || _fullName == other._fullName);
    }

    /// <summary>
    /// Gets "'LocalResourceName' in 'Container'" when <see cref="IsValid"/> is true
    /// or the empty string when IsValid is false.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return IsValid
                ? $"'{ResourceName}' in {_container.DisplayName}"
                : "";
    }

    /// <inheritdoc cref="Equals(ResourceLocator)"/>.
    public override bool Equals( object? obj ) => obj is ResourceLocator locator && Equals( locator );

    /// <inheritdoc cref="Equals(ResourceLocator)"/>.
    public static bool operator ==( ResourceLocator left, ResourceLocator right ) => left.Equals( right );

    /// <summary>
    /// To be equal the <see cref="Container"/> must be the same and <see cref="FullResourceName"/>
    /// must be equal (<see cref="StringComparison.Ordinal"/>).
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>Whether the two resource locators are different.</returns>
    public static bool operator !=( ResourceLocator left, ResourceLocator right ) => !(left == right);

    /// <summary>
    /// Hash code is based on <see cref="Container"/> and <see cref="FullResourceName"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => IsValid
                                            ? HashCode.Combine( Container.GetHashCode(), FullResourceName.GetHashCode() )
                                            : 0;

    /// <summary>
    /// Checks that <see cref="Container"/> is the same as <paramref name="expectedContainer"/> or
    /// throws a <see cref="ArgumentException"/>.
    /// </summary>
    /// <param name="expectedContainer">The expected container.</param>
    public void CheckContainer( IResourceContainer expectedContainer )
    {
        if( _container != expectedContainer )
        {
            if( !IsValid )
            {
                throw new ArgumentException( $"The resource is invalid." );
            }
            throw new ArgumentException( $"The resource {ToString()} doesn't belong to this '{expectedContainer.DisplayName}'." );
        }
    }
}
