using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace CK.EmbeddedResources;

/// <summary>
/// Resource container for embedded resources. <see cref="IsValid"/> is true even if no
/// resources exist under the <see cref="ResourcePrefix"/> (it is false only when an error
/// prevented a correct instantiation).
/// <para>
/// This can be created from a <see cref="TypeExtensions.CreateResourcesContainer(Type, IActivityMonitor, string?, bool, bool)">Type</see>
/// or a <see cref="AssemblyResources.CreateCKResourceContainer(string, string)">AssemblyResources</see>.
/// </para>
/// </summary>
[SerializationVersion(0)]
public sealed class AssemblyResourceContainer : IResourceContainer, ICKVersionedBinarySerializable
{
    readonly ReadOnlyMemory<string> _names;
    readonly AssemblyResources _assemblyResources;
    readonly string _prefix;
    readonly string _displayName;

    internal AssemblyResourceContainer( AssemblyResources assemblyResources,
                                        string prefix,
                                        string displayName,
                                        ReadOnlyMemory<string> resourceNames )
    {
        Throw.DebugAssert( prefix.Length == 0
                           || (prefix.StartsWith( "ck@" )
                                && (prefix.Length == 3 || prefix.EndsWith( '/' ))) );
        _assemblyResources = assemblyResources;
        _prefix = prefix;
        _displayName = displayName;
        _names = resourceNames;
    }

    // Invalid constructor.
    internal AssemblyResourceContainer( AssemblyResources assemblyResources,
                                        string displayName )
    {
        _assemblyResources = assemblyResources;
        _prefix = string.Empty;
        _displayName = displayName;
        _names = ReadOnlyMemory<string>.Empty;
    }

    /// <summary>
    /// Initializes a new <see cref="CodeGenResourceContainer"/> previously serialized
    /// by <see cref="WriteData(ICKBinaryWriter)"/>.
    /// </summary>
    /// <param name="r">The reader.</param>
    /// <param name="version">The serialized version.</param>
    public AssemblyResourceContainer( ICKBinaryReader r, int version )
    {
        Throw.CheckArgument( version == 0 );
        _displayName = r.ReadString();
        _prefix = r.ReadString();
        var a = System.Reflection.Assembly.Load( r.ReadString() );
        _assemblyResources = a.GetResources();
        _names = _prefix.Length > 0
                    ? _assemblyResources.AllResourceNames.GetPrefixedStrings( _prefix )
                    : ReadOnlyMemory<string>.Empty;
    }

    /// <summary>
    /// Serializes this container. The <see cref="AssemblyResources.AssemblyName"/> is serialized
    /// and deserialization loads the assembly in the default assembly load context.
    /// </summary>
    /// <param name="w">The target writer.</param>
    public void WriteData( ICKBinaryWriter w )
    {
        w.Write( _displayName );
        w.Write( _prefix );
        w.Write( _assemblyResources.AssemblyName );
    }

    internal static string MakeDisplayName( string? displayName, Type type, bool resAfter )
    {
        return displayName
                ?? (resAfter
                        ? $"resources [After] of '{type.ToCSharpName()}' type"
                        : $"resources of '{type.ToCSharpName()}' type");
    }

    /// <summary>
    /// Gets the assembly that contains this container.
    /// </summary>
    public AssemblyResources Assembly => _assemblyResources;

    /// <inheritdoc />
    public bool IsValid => _prefix.Length > 0;

    /// <inheritdoc />
    public string DisplayName => _displayName;

    /// <summary>
    /// Separator is '/'.
    /// </summary>
    public char DirectorySeparatorChar => '/';
    /// <inheritdoc />
    public IEnumerable<ResourceLocator> AllResources => MemoryMarshal.ToEnumerable( _names ).Select( p => new ResourceLocator( this, p ) );

    /// <inheritdoc />
    public string ResourcePrefix => _prefix;

    /// <inheritdoc />
    public bool HasLocalFilePathSupport => !_assemblyResources.LocalPath.IsEmptyPath;

    /// <inheritdoc />
    public string? GetLocalFilePath( ResourceLocator resource )
    {
        var p = _assemblyResources.LocalPath;
        if( p.IsEmptyPath ) return null;
        resource.CheckContainer( this );
        Throw.DebugAssert( resource.FullResourceName.StartsWith( "ck@" ) );
        var s = string.Concat( p.Path.AsSpan(), "/", resource.FullResourceName.AsSpan( 3 ) );
        if( Path.DirectorySeparatorChar != NormalizedPath.DirectorySeparatorChar )
        {
            s = s.Replace( NormalizedPath.DirectorySeparatorChar, Path.DirectorySeparatorChar );
        }
        return s;
    }

    /// <inheritdoc />
    public Stream GetStream( ResourceLocator resource )
    {
        resource.CheckContainer( this );
        return _assemblyResources.OpenResourceStream( resource.FullResourceName ); 
    }

    /// <inheritdoc />
    public void WriteStream( ResourceLocator resource, Stream target )
    {
        resource.CheckContainer( this );
        using var source = _assemblyResources.OpenResourceStream( resource.FullResourceName );
        source.CopyTo( target );
    }

    /// <inheritdoc />
    public string ReadAsText( ResourceLocator resource )
    {
        using( var source = GetStream( resource ) )
        using( var r = new StreamReader( source ) )
        {
            return r.ReadToEnd();
        }
    }

    /// <inheritdoc />
    public ResourceLocator GetResource( ReadOnlySpan<char> localResourceName ) => CodeGenResourceContainer.DoGetResource( _prefix, this, _names.Span, localResourceName );

    /// <inheritdoc />
    public ResourceLocator GetResource( ResourceFolder folder, ReadOnlySpan<char> resourceName )
    {
        folder.CheckContainer( this );
        return CodeGenResourceContainer.DoGetResource( folder.FullFolderName, this, _names.Span, resourceName );
    }

    /// <inheritdoc />
    public ResourceFolder GetFolder( ReadOnlySpan<char> localFolderName ) => CodeGenResourceContainer.DoGetFolder( _prefix, this, _names.Span, localFolderName );

    /// <inheritdoc />
    public ResourceFolder GetFolder( ResourceFolder folder, ReadOnlySpan<char> folderName )
    {
        folder.CheckContainer( this );
        return CodeGenResourceContainer.DoGetFolder( folder.FullFolderName, this, _names.Span, folderName );
    }

    /// <inheritdoc />
    public IEnumerable<ResourceLocator> GetAllResources( ResourceFolder folder ) => CodeGenResourceContainer.DoGetAllResources( folder, this, _names );

    /// <inheritdoc />
    public IEnumerable<ResourceLocator> GetResources( ResourceFolder folder ) => CodeGenResourceContainer.DoGetResources( folder, this, _names );

    /// <inheritdoc />
    public IEnumerable<ResourceFolder> GetFolders( ResourceFolder folder ) => CodeGenResourceContainer.DoGetFolders( folder, this, _names );

    /// <inheritdoc />
    public override string ToString() => _displayName;
}
