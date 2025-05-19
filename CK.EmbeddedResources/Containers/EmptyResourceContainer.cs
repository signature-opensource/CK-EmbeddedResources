using CK.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace CK.EmbeddedResources;

/// <summary>
/// An empty resource container with no contents.
/// </summary>
[SerializationVersion(0)]
public sealed class EmptyResourceContainer : IResourceContainer, ICKVersionedBinarySerializable
{
    readonly string _displayName;
    readonly bool _isDisabled;
    readonly string _resourcePrefix;
    readonly bool _isValid;

    /// <summary>
    /// Initializes a new empty container with a display name.
    /// </summary>
    /// <param name="displayName">The name of this empty container.</param>
    /// <param name="isDisabled">
    /// True for a by design disabled container. When true, the <paramref name="displayName"/> is automatically
    /// prefixed by "disabled ".
    /// </param>
    /// <param name="resourcePrefix">Optional resource prefix. Defaults to the empty string.</param>
    /// <param name="isValid">Optionally creates an invalid container. By default, an empty container is valid.</param>
    public EmptyResourceContainer( string displayName, bool isDisabled, string? resourcePrefix = null, bool isValid = true )
    {
        Throw.CheckNotNullOrWhiteSpaceArgument( displayName );
        _displayName = isDisabled
                        ? "disabled " + displayName
                        : displayName;
        _isDisabled = isDisabled;
        _resourcePrefix = resourcePrefix ?? string.Empty;
        _isValid = isValid;
    }

    /// <summary>
    /// Initializes a new empty resource container previously serialized by <see cref="WriteData(ICKBinaryWriter)"/>.
    /// </summary>
    /// <param name="r">The reader.</param>
    /// <param name="version">The serialized version.</param>
    [EditorBrowsable( EditorBrowsableState.Never )]
    public EmptyResourceContainer( ICKBinaryReader r, int version )
    {
        Throw.CheckArgument( version == 0 );
        _displayName = r.ReadString();
        _isDisabled = r.ReadBoolean();
        _resourcePrefix = r.ReadString();
        _isValid = r.ReadBoolean();
    }

    /// <summary>
    /// Writes this empty container.
    /// </summary>
    /// <param name="w">The target writer.</param>
    public void WriteData( ICKBinaryWriter w )
    {
        w.Write( _displayName );
        w.Write( _isDisabled );
        w.Write( _resourcePrefix );
        w.Write( _isValid );
    }

    /// <inheritdoc />
    public bool IsValid => _isValid;

    /// <summary>
    /// Gets whether this container is disabled by designed.
    /// </summary>
    public bool IsDisabled => _isDisabled;

    /// <inheritdoc />
    public string DisplayName => _displayName;

    /// <summary>
    /// Separator is '/' (but this is meaningless).
    /// </summary>
    public char DirectorySeparatorChar => '/';
    /// <summary>
    /// Gets the display name regardless of the <see cref="IsDisabled"/> state.
    /// </summary>
    public ReadOnlySpan<char> NonDisabledDisplayName => _isDisabled ? _displayName.AsSpan( 9 ) : _displayName;

    /// <inheritdoc />
    public string ResourcePrefix => _resourcePrefix;

    /// <inheritdoc />
    public IEnumerable<ResourceLocator> AllResources => [];

    /// <summary>
    /// Always false.
    /// </summary>
    public bool HasLocalFilePathSupport => false;

    /// <summary>
    /// Always returns null.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <returns>A null local file path.</returns>
    public string? GetLocalFilePath( ResourceLocator resource ) => null;

    /// <inheritdoc />
    public Stream GetStream( ResourceLocator resource ) => Throw.InvalidOperationException<Stream>();

    /// <inheritdoc />
    public void WriteStream( ResourceLocator resource, Stream target ) => Throw.InvalidOperationException();

    /// <inheritdoc />
    public string ReadAsText( ResourceLocator resource ) => Throw.InvalidOperationException<string>();

    /// <inheritdoc />
    public ResourceLocator GetResource( ReadOnlySpan<char> resourceName ) => default;

    /// <inheritdoc />
    public ResourceLocator GetResource( ResourceFolder folder, ReadOnlySpan<char> resourceName ) => default;

    /// <inheritdoc />
    public ResourceFolder GetFolder( ReadOnlySpan<char> folderName ) => default;

    /// <inheritdoc />
    public ResourceFolder GetFolder( ResourceFolder folder, ReadOnlySpan<char> folderName ) => default;

    /// <inheritdoc />
    public IEnumerable<ResourceLocator> GetAllResources( ResourceFolder folder ) => [];

    /// <inheritdoc />
    public IEnumerable<ResourceLocator> GetResources( ResourceFolder folder ) => [];

    /// <inheritdoc />
    public IEnumerable<ResourceFolder> GetFolders( ResourceFolder folder ) => [];

    /// <inheritdoc />
    public override string ToString() => _displayName;

}
