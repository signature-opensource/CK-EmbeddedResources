using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CK.EmbeddedResources;

/// <summary>
/// Stable wrapper container around an inner container that can be changed.
/// All <see cref="ResourceFolder"/> and <see cref="ResourceLocator"/> that
/// flow accross this container are bound to this container: the inner one is
/// totally hidden.
/// <para>
/// This should almost always be used to transition from an empty container
/// to a real one. When used with non empty containers, resources and folders
/// can "disappear" (and that is not the implicit contract of resource containers). 
/// </para>
/// <para>
/// The <see cref="IResourceContainer"/> is explicitly implemented: the point of this
/// container is to be manipulated as its contract.
/// </para>
/// <para>
/// This container is not directly serializable (because it references another container this
/// requires a graph serialization and this library only relies on simple serialization).
/// Its <see cref="InnerContainer"/> must be serialized and, if needed, a wrapper can be recreated.
/// </para>
/// </summary>
public sealed class ResourceContainerWrapper : IResourceContainer
{
    IResourceContainer _container;

    /// <summary>
    /// Initializes a new <see cref="ResourceContainerWrapper"/> on a container.
    /// </summary>
    /// <param name="container">The inner container. Cannot be a <see cref="ResourceContainerWrapper"/>.</param>
    public ResourceContainerWrapper( IResourceContainer container )
    {
        Throw.CheckNotNullArgument( container );
        Throw.CheckArgument( container is not ResourceContainerWrapper );
        _container = container;
    }

    /// <summary>
    /// Gets or sets the inner container.
    /// Setting a <see cref="ResourceContainerWrapper"/> currently raises a <see cref="ArgumentException"/>.
    /// This prevents any cycles to be created.
    /// </summary>
    public IResourceContainer InnerContainer
    {
        get => _container;
        set
        {
            Throw.CheckNotNullArgument( value );
            Throw.CheckArgument( value is not ResourceContainerWrapper );
            _container = value;
        }
    }

    bool IResourceContainer.IsValid => _container.IsValid;

    string IResourceContainer.DisplayName => _container.DisplayName;

    string IResourceContainer.ResourcePrefix => _container.ResourcePrefix;

    char IResourceContainer.DirectorySeparatorChar => _container.DirectorySeparatorChar;

    IEnumerable<ResourceLocator> IResourceContainer.AllResources => _container.AllResources.Select( r => ResourceLocator.UnsafeCreate( this, r.FullResourceName ) );

    bool IResourceContainer.HasLocalFilePathSupport => _container.HasLocalFilePathSupport;

    IEnumerable<ResourceLocator> IResourceContainer.GetAllResources( ResourceFolder folder )
    {
        folder.CheckContainer( this );
        return _container.GetAllResources( ResourceFolder.UnsafeCreate( _container, folder.FullFolderName ) )
                         .Select( r => ResourceLocator.UnsafeCreate( this, r.FullResourceName ) );
    }

    ResourceFolder IResourceContainer.GetFolder( ReadOnlySpan<char> folderName )
    {
        var f = _container.GetFolder( folderName );
        return f.IsValid ? ResourceFolder.UnsafeCreate( this, f.FullFolderName ) : f;
    }

    ResourceFolder IResourceContainer.GetFolder( ResourceFolder folder, ReadOnlySpan<char> folderName )
    {
        folder.CheckContainer( this );
        var f = _container.GetFolder( ResourceFolder.UnsafeCreate( _container, folder.FullFolderName ), folderName );
        return f.IsValid ? ResourceFolder.UnsafeCreate( this, f.FullFolderName ) : f;
    }

    IEnumerable<ResourceFolder> IResourceContainer.GetFolders( ResourceFolder folder )
    {
        folder.CheckContainer( this );
        return _container.GetFolders( ResourceFolder.UnsafeCreate(_container, folder.FullFolderName) )
                         .Select( f => ResourceFolder.UnsafeCreate( this, f.FullFolderName ) );
    }

    string? IResourceContainer.GetLocalFilePath( ResourceLocator resource )
    {
        resource.CheckContainer( this );
        return _container.GetLocalFilePath( ResourceLocator.UnsafeCreate( _container, resource.FullResourceName ) );
    }

    ResourceLocator IResourceContainer.GetResource( ReadOnlySpan<char> resourceName )
    {
        var r = _container.GetResource( resourceName );
        return r.IsValid ? ResourceLocator.UnsafeCreate( this, r.FullResourceName ) : r;
    }

    ResourceLocator IResourceContainer.GetResource( ResourceFolder folder, ReadOnlySpan<char> resourceName )
    {
        folder.CheckContainer( this );
        var r = _container.GetResource( ResourceFolder.UnsafeCreate( _container, folder.FullFolderName ), resourceName );
        return r.IsValid ? ResourceLocator.UnsafeCreate( this, r.FullResourceName ) : r;
    }

    IEnumerable<ResourceLocator> IResourceContainer.GetResources( ResourceFolder folder )
    {
        folder.CheckContainer( this );
        return _container.GetResources( ResourceFolder.UnsafeCreate( _container, folder.FullFolderName ) )
                         .Select( r => ResourceLocator.UnsafeCreate( this, r.FullResourceName ) );
    }

    Stream IResourceContainer.GetStream( ResourceLocator resource )
    {
        resource.CheckContainer( this );
        return _container.GetStream( ResourceLocator.UnsafeCreate( _container, resource.FullResourceName ) );
    }

    string IResourceContainer.ReadAsText( ResourceLocator resource )
    {
        resource.CheckContainer( this );
        return _container.ReadAsText( ResourceLocator.UnsafeCreate( _container, resource.FullResourceName ) );
    }

    void IResourceContainer.WriteStream( ResourceLocator resource, Stream target )
    {
        resource.CheckContainer( this );
        _container.WriteStream( ResourceLocator.UnsafeCreate( _container, resource.FullResourceName ), target );
    }

    /// <inheritdoc />
    public override string ToString() => _container.ToString();
}
