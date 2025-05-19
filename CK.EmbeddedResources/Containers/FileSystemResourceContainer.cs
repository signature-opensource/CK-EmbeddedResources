using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;

namespace CK.EmbeddedResources;

/// <summary>
/// File system implementation of a <see cref="IResourceContainer"/> uses the platform <see cref="Path.DirectorySeparatorChar"/>.
/// <para>
/// By default <see cref="HasLocalFilePathSupport"/> is true but this can be changed when
/// instantiating the container to prrvent direct access to the file system.
/// </para>
/// <para>
/// This container implementation is "unstable" by design: <see cref="ResourceFolder"/> and <see cref="ResourceLocator"/>
/// can "disappear" when deleted from the file system.
/// </para>
/// </summary>
[SerializationVersion( 0 )]
public sealed class FileSystemResourceContainer : IResourceContainer, ICKVersionedBinarySerializable
{
    readonly string _displayName;
    readonly string _root;
    readonly bool _alwaysValid;
    readonly bool _allowLocalFilePath;

    /// <summary>
    /// Initializes a new <see cref="FileSystemResourceContainer"/>.
    /// </summary>
    /// <param name="root">The root directory. This should be an absolute path.</param>
    /// <param name="displayName">The <see cref="DisplayName"/> for this container.</param>
    /// <param name="alwaysValid">
    /// By default, this is valid even if the <see cref="ResourcePrefix"/> (the root directory)
    /// doesn't exist on the file system: it is considered as being empty.
    /// <para>
    /// When set to true, <see cref="IsValid"/> is true only when the root directory exists. 
    /// </para>
    /// </param>
    /// <param name="allowLocalFilePath">
    /// Gets whether <see cref="GetLocalFilePath(ResourceLocator)"/> returns the file path.
    /// <para>
    /// When set to false, this container hides the fact that it is bound to the file system: it behaves
    /// like a purely readonly resource container.
    /// </para>
    /// </param>
    public FileSystemResourceContainer( string root,
                                        string displayName,
                                        bool alwaysValid = true,
                                        bool allowLocalFilePath = true )
    {
        Throw.CheckNotNullOrWhiteSpaceArgument( displayName );
        Throw.CheckArgument( Path.IsPathFullyQualified( root ) );
        _displayName = displayName;
        _alwaysValid = alwaysValid;
        _allowLocalFilePath = allowLocalFilePath;
        root = Path.GetFullPath( root );
        if( !Path.EndsInDirectorySeparator( root ) ) root += Path.DirectorySeparatorChar;
        _root = root;
    }

    /// <summary>
    /// Initializes a new <see cref="FileSystemResourceContainer"/> previously serialized
    /// by <see cref="WriteData(ICKBinaryWriter)"/>.
    /// </summary>
    /// <param name="r">The reader.</param>
    /// <param name="version">The serialized version.</param>
    public FileSystemResourceContainer( ICKBinaryReader r, int version )
    {
        Throw.CheckArgument( version == 0 );
        _displayName = r.ReadString();
        _root = r.ReadString();
        _alwaysValid = r.ReadBoolean();
        _allowLocalFilePath = r.ReadBoolean();
    }

    /// <summary>
    /// Serializes this container.
    /// </summary>
    /// <param name="w">The target writer.</param>
    public void WriteData( ICKBinaryWriter w )
    {
        w.Write( _displayName );
        w.Write( _root );
        w.Write( _alwaysValid );
        w.Write( _allowLocalFilePath );
    }

    /// <summary>
    /// Gets whether the <see cref="ResourcePrefix"/> that is the root directory exists on the file system.
    /// </summary>
    public bool IsValid => _alwaysValid || Directory.Exists( _root );

    /// <inheritdoc />
    /// <remarks>
    /// Because this is a FileSystemResourceContainer, it is the root directory ending with
    /// a <see cref="Path.DirectorySeparatorChar"/>.
    /// </remarks>
    public string ResourcePrefix => _root;

    /// <inheritdoc />
    public string DisplayName => _displayName;

    /// <summary>
    /// Separator is <see cref="Path.DirectorySeparatorChar"/>.
    /// </summary>
    public char DirectorySeparatorChar => Path.DirectorySeparatorChar;
    /// <inheritdoc />
    public IEnumerable<ResourceLocator> AllResources
    {
        get
        {
            if( Directory.Exists( _root ) )
            {
                foreach( var f in Directory.EnumerateFiles( _root, "*", SearchOption.AllDirectories ) )
                {
                    yield return new ResourceLocator( this, f );
                }
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<ResourceLocator> GetAllResource( ResourceFolder folder )
    {
        folder.CheckContainer( this );
        if( Directory.Exists( _root ) )
        {
            foreach( var f in Directory.EnumerateFiles( folder.FullFolderName, "*", SearchOption.AllDirectories ) )
            {
                yield return new ResourceLocator( this, f );
            }
        }
    }

    /// <inheritdoc />
    public bool HasLocalFilePathSupport => _allowLocalFilePath;

    /// <inheritdoc />
    public string? GetLocalFilePath( ResourceLocator resource )
    {
        resource.CheckContainer( this );
        return _allowLocalFilePath
                    ? resource.FullResourceName
                    : null;
    }

    /// <inheritdoc />
    public Stream GetStream( ResourceLocator resource )
    {
        resource.CheckContainer( this );
        return File.OpenRead( resource.FullResourceName );
    }

    /// <inheritdoc />
    public void WriteStream( ResourceLocator resource, Stream target )
    {
        resource.CheckContainer( this );
        using var source = File.OpenRead( resource.FullResourceName );
        source.CopyTo( target );
    }

    /// <inheritdoc />
    public string ReadAsText( ResourceLocator resource )
    {
        resource.CheckContainer( this );
        return File.ReadAllText( resource.FullResourceName );
    }

    /// <inheritdoc />
    public ResourceLocator GetResource( ReadOnlySpan<char> resourceName ) => DoGetResource( _root, resourceName );

    /// <inheritdoc />
    public ResourceLocator GetResource( ResourceFolder folder, ReadOnlySpan<char> resourceName )
    {
        folder.CheckContainer( this );
        return DoGetResource( folder.FullFolderName, resourceName );
    }

    /// <summary>
    /// Gets a <see cref="ResourceLocator"/> from its local file path.
    /// Returns an invalid resource if the file doesn't exist on the file system.
    /// </summary>
    /// <param name="localFilePath">The local file path that must start with <see cref="ResourcePrefix"/>.</param>
    /// <returns>The resource. <see cref="ResourceLocator.IsValid"/> is false if the file doesn't exist.</returns>
    public ResourceLocator GetResourceFromLocalPath( string localFilePath )
    {
        Throw.CheckArgument( localFilePath.StartsWith( ResourcePrefix ) );
        localFilePath = Path.GetFullPath( localFilePath );
        if( File.Exists( localFilePath ) )
        {
            return new ResourceLocator( this, localFilePath );
        }
        return default;
    }

    ResourceLocator DoGetResource( string prefix, ReadOnlySpan<char> resourceName )
    {
        if( resourceName.Length > 0 && (resourceName[0] == '/' || resourceName[0] == '\\') )
        {
            resourceName = resourceName.Slice( 1 );
        }
        // This normalizes the Path.DirectorySeparatorChar.
        // File.Exists calls it anyway.
        var p = Path.GetFullPath( String.Concat( prefix, resourceName ) );
        if( File.Exists( p ) )
        {
            return new ResourceLocator( this, p );
        }
        return default;
    }

    /// <inheritdoc />
    public ResourceFolder GetFolder( ReadOnlySpan<char> folderName ) => DoGetFolder( _root, folderName );

    /// <inheritdoc />
    public ResourceFolder GetFolder( ResourceFolder folder, ReadOnlySpan<char> folderName )
    {
        folder.CheckContainer( this );
        return DoGetFolder( folder.FullFolderName, folderName );
    }

    ResourceFolder DoGetFolder( string prefix, ReadOnlySpan<char> folderName )
    {
        if( folderName.Length > 0 && (folderName[0] == '/' || folderName[0] == '\\') )
        {
            folderName = folderName.Slice( 1 );
        }
        var p = Path.GetFullPath( String.Concat( prefix, folderName ) );
        if( Directory.Exists( p ) )
        {
            return new ResourceFolder( this, Path.EndsInDirectorySeparator( p )
                                                ? p
                                                : p + Path.DirectorySeparatorChar );
        }
        return default;
    }

    /// <inheritdoc />
    public IEnumerable<ResourceLocator> GetAllResources( ResourceFolder folder )
    {
        folder.CheckContainer( this );
        foreach( var f in Directory.EnumerateFiles( folder.FullFolderName, "*", SearchOption.AllDirectories ) )
        {
            yield return new ResourceLocator( this, f );
        }
    }

    /// <inheritdoc />
    public IEnumerable<ResourceLocator> GetResources( ResourceFolder folder )
    {
        folder.CheckContainer( this );
        foreach( var f in Directory.EnumerateFiles( folder.FullFolderName ) )
        {
            yield return new ResourceLocator( this, f );
        }
    }

    /// <inheritdoc />
    public IEnumerable<ResourceFolder> GetFolders( ResourceFolder folder )
    {
        folder.CheckContainer( this );
        foreach( var f in Directory.EnumerateDirectories( folder.FullFolderName ) )
        {
            Throw.DebugAssert( f[^1] != Path.DirectorySeparatorChar );
            yield return new ResourceFolder( this, f + Path.DirectorySeparatorChar );
        }
    }

    /// <inheritdoc />
    public override string ToString() => _displayName;
}
