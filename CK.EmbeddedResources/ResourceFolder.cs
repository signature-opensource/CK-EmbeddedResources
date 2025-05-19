using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;

namespace CK.EmbeddedResources;

/// <summary>
/// Resource folder in a <see cref="IResourceContainer"/>.
/// <para>
/// The <c>default</c> value is <see cref="IsValid"/> false: this makes <see cref="Nullable{T}"/> useless for this type.
/// </para>
/// </summary>
public readonly struct ResourceFolder : IEquatable<ResourceFolder>
{
    readonly IResourceContainer _container;
    readonly string _fullName;

    /// <summary>
    /// Initializes a new resource folder.
    /// </summary>
    /// <param name="container">The resources that contains this folder.</param>
    /// <param name="fullFolderName">
    /// The full folder name in the <paramref name="container"/>, including the <see cref="IResourceContainer.ResourcePrefix"/>.
    /// When not equal to the ResourcePrefix (that is the root folder), it must always ends with
    /// the <see cref="IResourceContainer.DirectorySeparatorChar"/>.
    /// </param>
    public ResourceFolder( IResourceContainer container, string fullFolderName )
    {
        Throw.CheckNotNullArgument( container );
        Throw.CheckNotNullArgument( fullFolderName );
        Throw.CheckArgument( fullFolderName.StartsWith( container.ResourcePrefix ) );
        Throw.CheckArgument( (fullFolderName.Length - container.ResourcePrefix.Length) <= IResourceContainer.MaxNameLength );
        Throw.CheckArgument( "If there is a folder name, then it must end with the container's separator.",
                             fullFolderName.Length == container.ResourcePrefix.Length || fullFolderName.EndsWith( container.DirectorySeparatorChar ) );
        _container = container;
        _fullName = fullFolderName;
    }

    ResourceFolder( string fullFolderName, IResourceContainer container )
    {
        _container = container;
        _fullName = fullFolderName;
    }

    /// <summary>
    /// Unsafe initialization of a <see cref="ResourceFolder"/>: no check are done.
    /// </summary>
    /// <param name="container">The container.</param>
    /// <param name="fullFolderName">The full folder name.</param>
    /// <returns>A resource folder.</returns>
    public static ResourceFolder UnsafeCreate( IResourceContainer container, string fullFolderName ) => new ResourceFolder( fullFolderName, container );

    /// <summary>
    /// Gets whether this folder is valid: the <see cref="Container"/> is not null
    /// and the <see cref="FullFolderName"/> is not null, not empty nor whitespace.
    /// <para>
    /// Whether the resource actuallly exists or not is not known.
    /// </para>
    /// Only the <c>default</c> of this type is invalid.
    /// </summary>
    public bool IsValid => _container != null;

    /// <summary>
    /// Gets the container of this resource.
    /// </summary>
    public IResourceContainer Container => _container;

    /// <summary>
    /// Gets the folder name in the <see cref="Container"/>.
    /// <para>
    /// This is the full name that includes the <see cref="IResourceContainer.ResourcePrefix"/>.
    /// </para>
    /// </summary>
    public string FullFolderName => _fullName;

    /// <summary>
    /// Gets the folder name without the <see cref="IResourceContainer.ResourcePrefix"/>.
    /// </summary>
    public ReadOnlySpan<char> FolderName
    {
        get
        {
            Throw.CheckState( IsValid );
            return _fullName.AsSpan( _container.ResourcePrefix.Length );
        }
    }

    /// <summary>
    /// Gets the name of this folder (without parent folder names).
    /// </summary>
    public ReadOnlySpan<char> Name
    {
        get
        {
            var s = FolderName;
            return s.Length != 0 ? Path.GetFileName( s.Slice( 0, s.Length - 1 ) ) : s;
        }
    }

    /// <summary>
    /// Gets an existing resource or a locator with <see cref="ResourceLocator.IsValid"/> false
    /// if the resource doesn't exist.
    /// </summary>
    /// <param name="localResourceName">The local resource name (can contain any folder prefix).</param>
    /// <returns>The resource locator that may not be valid.</returns>
    public ResourceLocator GetResource( ReadOnlySpan<char> localResourceName ) => _container.GetResource( this, localResourceName );

    /// <summary>
    /// Gets an existing folder or a ResourceFolder with <see cref="ResourceLocator.IsValid"/> false
    /// if the folder doesn't exist.
    /// </summary>
    /// <param name="localFolderName">The local resource folder name (can contain any folder prefix).</param>
    /// <returns>The resource folder that may not be valid.</returns>
    public ResourceFolder GetFolder( ReadOnlySpan<char> localFolderName ) => _container.GetFolder( this, localFolderName );

    /// <summary>
    /// Gets the resources contained in this folder.
    /// <para>
    /// This is a simple relay to <see cref="IResourceContainer.GetResources(ResourceFolder)"/>.
    /// </para>
    /// </summary>
    public IEnumerable<ResourceLocator> Resources => _container.GetResources( this );

    /// <summary>
    /// Gets all the resources contained in this folder, regardless of any subordinated folders.
    /// <para>
    /// This is a simple relay to <see cref="IResourceContainer.GetAllResources(ResourceFolder)"/>.
    /// </para>
    /// </summary>
    public IEnumerable<ResourceLocator> AllResources => _container.GetAllResources( this );

    /// <summary>
    /// Gets the direct children folders contained in this folder.
    /// <para>
    /// This is a simple relay to <see cref="IResourceContainer.GetFolders(ResourceFolder)"/>.
    /// </para>
    /// </summary>
    public IEnumerable<ResourceFolder> Folders => _container.GetFolders( this );

    /// <summary>
    /// To be equal the <see cref="Container"/> must be the same and <see cref="FullFolderName"/>
    /// must be equal (<see cref="StringComparison.Ordinal"/>).
    /// </summary>
    /// <param name="other">The other locator.</param>
    /// <returns>True if the resources are the same.</returns>
    public bool Equals( ResourceFolder other )
    {
        return _container == other._container
               && (_container == null || _fullName == other._fullName);
    }

    /// <summary>
    /// Gets "Folder "Folder 'LocalFolderName' in 'Container'" when <see cref="IsValid"/> is true
    /// or the empty string when IsValid is false.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return IsValid
                ? $"Folder '{FolderName}' in {Container.DisplayName}"
                : "";
    }

    /// <summary>
    /// Checks that <see cref="Container"/> is the same as <paramref name="expectedContainer"/> or
    /// throws a <see cref="ArgumentException"/>.
    /// </summary>
    /// <param name="expectedContainer">The expected container.</param>
    public void CheckContainer( IResourceContainer expectedContainer )
    {
        if( _container != expectedContainer )
        {
            if( _container == null )
            {
                throw new ArgumentException( $"The folder is invalid." );
            }
            throw new ArgumentException( $"'{ToString()}' doesn't belong to this '{expectedContainer.DisplayName}'." );
        }
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public override bool Equals( object? obj ) => obj is ResourceFolder locator && Equals( locator );

    public static bool operator ==( ResourceFolder left, ResourceFolder right ) => left.Equals( right );

    public static bool operator !=( ResourceFolder left, ResourceFolder right ) => !(left == right);

    public override int GetHashCode() => IsValid
                                            ? HashCode.Combine( Container.GetHashCode(), FullFolderName.GetHashCode() )
                                            : 0;


}
