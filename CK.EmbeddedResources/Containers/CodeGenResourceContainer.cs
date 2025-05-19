using CK.Core;
using CommunityToolkit.HighPerformance;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace CK.EmbeddedResources;

/// <summary>
/// A resource container in which a string, an array of bytes, a reader or a writer function for a
/// resource can be registered.
/// <para>
/// Whether a string, a reader or a writer is registered for a resource is irrelevant: this container
/// adapts its behavior to always support <see cref="GetStream(ResourceLocator)"/>,
/// <see cref="WriteStream(ResourceLocator, Stream)"/> and <see cref="ReadAsText(ResourceLocator)"/>.
/// </para>
/// <para>
/// The path separator is '/', '\' are normalized to '/'. This kind of container
/// doesn't support <see cref="ResourceLocator.LocalFilePath"/> (it is always null)
/// and <see cref="IResourceContainer.HasLocalFilePathSupport"/> is false by design.
/// </para>
/// </summary>
[SerializationVersion(0)]
public sealed class CodeGenResourceContainer : IResourceContainer, ICKVersionedBinarySerializable
{
    string[] _pathStore;
    object[] _streamStore;

    ReadOnlyMemory<string> _names;
    readonly string _displayName;
    bool _closed;

    /// <summary>
    /// Builder that can create a <see cref="CodeGenResourceContainer"/> from a known
    /// count of already sorted resources.
    /// </summary>
    public sealed class Builder
    {
        string[] _pathStore;
        object[] _streamStore;

        string? _previous;
        int _count;
        bool _buildDone;

        /// <summary>
        /// Initializes a new builder.
        /// </summary>
        /// <param name="count">The total number of resources.</param>
        public Builder( int count )
        {
            Throw.CheckArgument( count > 0 );
            _pathStore = new string[count];
            _streamStore = new object[count];
        }

        /// <summary>
        /// Adds the next resource as a string content.
        /// </summary>
        /// <param name="resourcePath">The path.</param>
        /// <param name="content">The string content.</param>
        public void AddNext( string resourcePath, string content )
        {
            DoAdd( resourcePath, content );
        }

        /// <summary>
        /// Adds the next resource as a stream reader.
        /// </summary>
        /// <param name="resourcePath">The path.</param>
        /// <param name="streamFactory">The function that must provide the <see cref="ResourceLocator.GetStream()"/>.</param>
        public void AddNext( string resourcePath, Func<Stream> streamFactory )
        {
            DoAdd( resourcePath, streamFactory );
        }

        /// <summary>
        /// Adds the next resource as a byte content.
        /// </summary>
        /// <param name="resourcePath">The path.</param>
        /// <param name="bytes">The binary content.</param>
        public void AddNext( string resourcePath, byte[] bytes )
        {
            DoAdd( resourcePath, bytes );
        }

        /// <summary>
        /// Adds the next resource as a stream writer.
        /// </summary>
        /// <param name="resourcePath">The path.</param>
        /// <param name="streamWriter">The function that must write the stream content.</param>
        public void AddNext( string resourcePath, Action<Stream> streamWriter )
        {
            DoAdd( resourcePath, streamWriter );
        }

        void DoAdd( string path, object content )
        {
            Throw.CheckNotNullOrWhiteSpaceArgument( path );
            Throw.CheckNotNullArgument( content );
            if( path.Contains( '\\' )
                || path.Contains( "//", StringComparison.Ordinal )
                || path[0] is '/'
                || path[^1] == '/' )
            {
                Throw.ArgumentException( nameof( path ), $"'{path}' must not contain '\\', '//' nor starts with ends with '/'." );
            }
            if( _previous != null && StringComparer.Ordinal.Compare( _previous, path ) >= 0 )
            {
                Throw.ArgumentException( $"Added path must be sorted: previous '{_previous}' is greater than '{path}'." );
            }
            _previous = path;
            Throw.CheckState( "The planned number of resources must not be exceeded.", _count < _pathStore.Length );
            _pathStore[_count] = path;
            _streamStore[_count++] = content;
        }

        /// <summary>
        /// Creates the resource container.
        /// </summary>
        /// <param name="displayName">The container's display name.</param>
        /// <param name="closed">False to leave the container opened.</param>
        /// <returns>The container.</returns>
        public CodeGenResourceContainer Build( string displayName, bool closed = true )
        {
            Throw.CheckState( "Build must be called only once.", !_buildDone );
            Throw.CheckNotNullOrWhiteSpaceArgument( displayName );
            Throw.CheckState( "Planned number of resources must have been added.", _count == _pathStore.Length );
            _buildDone = true;
            return new CodeGenResourceContainer( displayName, _pathStore, _streamStore, closed );
        }
    }

    /// <summary>
    /// Initializes a new empty dynamic container.
    /// </summary>
    /// <param name="displayName">The display name.</param>
    public CodeGenResourceContainer( string displayName )
    {
        Throw.CheckNotNullOrWhiteSpaceArgument( displayName );
        _displayName = displayName;
        _pathStore = new string[4];
        _streamStore = new object[4];
    }

    CodeGenResourceContainer( string displayName, string[] pathStore, object[] streamStore, bool closed )
    {
        _displayName = displayName;
        _pathStore = pathStore;
        _streamStore = streamStore;
        _names = pathStore;
        _closed = closed;
    }

    /// <summary>
    /// Initializes a new <see cref="CodeGenResourceContainer"/> previously serialized
    /// by <see cref="WriteData(ICKBinaryWriter)"/>.
    /// <para>
    /// Note that <see cref="IsOpened"/> is not written: a deserialized Code container is opened by default
    /// when deserialized.
    /// </para>
    /// </summary>
    /// <param name="r">The reader.</param>
    /// <param name="version">The serialized version.</param>
    public CodeGenResourceContainer( ICKBinaryReader r, int version )
    {
        Throw.CheckArgument( version == 0 );
        _displayName = r.ReadString();
        int count = r.ReadInt32();
        _pathStore = new string[count];
        _streamStore = new object[count];
        for( int i = 0; i < _pathStore.Length; ++i )
        {
            _pathStore[i] = r.ReadString();
            var discriminator = r.ReadByte();
            Throw.CheckData( discriminator is 0 or 1 );
            if( discriminator == 0 )
            {
                _streamStore[i] = r.ReadString();
            }
            else
            {
                _streamStore[i] = r.ReadBytes( r.ReadInt32() );
            }
        }
        _names = _pathStore.AsMemory();
    }

    /// <summary>
    /// Serializes this container. Reader and Writer functions are written as
    /// binary content.
    /// <para>
    /// Note that <see cref="IsOpened"/> is not written: a deserialized container is opened by default.
    /// </para>
    /// </summary>
    /// <param name="w">The target writer.</param>
    public void WriteData( ICKBinaryWriter w )
    {
        w.Write( _displayName );
        w.Write( _names.Length );
        for( int i = 0; i < _names.Length; ++i )
        {
            w.Write( _pathStore[i] );
            var content = _streamStore[i];
            if( content is string str )
            {
                w.Write( (byte)0 );
                w.Write( str );
            }
            else
            {
                w.Write( (byte)1 );
                if( content is byte[] bytes )
                {
                    w.Write( bytes.Length );
                    w.Write( bytes );
                }
                else
                {
                    using var b = Util.RecyclableStreamManager.GetStream();
                    if( content is Action<Stream> writer )
                    {
                        writer( b );
                    }
                    else
                    {
                        Throw.DebugAssert( content is Func<Stream> );
                        using( var s = Unsafe.As<Func<Stream>>( content )() )
                        {
                            s.CopyTo( b );
                        }
                    }
                    WriteRecyclable( w, b );
                }
            }
        }

        static void WriteRecyclable( ICKBinaryWriter w, Microsoft.IO.RecyclableMemoryStream b )
        {
            w.Write( (int)b.Position );
            foreach( var seq in b.GetReadOnlySequence() )
            {
                w.Write( seq.Span );
            }
        }
    }

    /// <summary>
    /// Gets whether this container is opened: <see cref="AddBinary(string, byte[])"/>, <see cref="AddReader(string, Func{Stream})"/>,
    /// <see cref="AddWriter(string, Action{Stream})"/> or <see cref="AddText(string, string)"/> can be called.
    /// </summary>
    public bool IsOpened => !_closed;

    /// <summary>
    /// Closes this container. No more resources can be added to it.
    /// </summary>
    public void Close() => _closed = true;

    /// <summary>
    /// Adds a new resource with a dynamic reader.
    /// <para>
    /// This throws if the resource is already associated to a reader, writer, binary content or text
    /// or if <see cref="IsOpened"/> is false.
    /// </para>
    /// </summary>
    /// <param name="resourcePath">The resource path. Must not be empty or whitespace nor contains '\'.</param>
    /// <param name="streamFactory">The function that must provide the <see cref="ResourceLocator.GetStream()"/>.</param>
    /// <returns>The resource locator.</returns>
    public ResourceLocator AddReader( string resourcePath, Func<Stream> streamFactory )
    {
        Throw.CheckNotNullArgument( streamFactory );
        return DoAdd( resourcePath, streamFactory );
    }

    /// <summary>
    /// Adds a new resource with a binary content.
    /// <para>
    /// This throws if the resource is already associated to a reader, writer, binary content or text
    /// or if <see cref="IsOpened"/> is false.
    /// </para>
    /// </summary>
    /// <param name="resourcePath">The resource path. Must not be empty or whitespace nor contains '\'.</param>
    /// <param name="bytes">The binary content.</param>
    /// <returns>The resource locator.</returns>
    public ResourceLocator AddBinary( string resourcePath, byte[] bytes )
    {
        Throw.CheckNotNullArgument( bytes );
        return DoAdd( resourcePath, bytes );
    }

    /// <summary>
    /// Adds a new resource with a dynamic writer.
    /// <para>
    /// This throws if the resource is already associated to a reader, writer, binary content or text
    /// or if <see cref="IsOpened"/> is false.
    /// </para>
    /// </summary>
    /// <param name="resourcePath">
    /// The resource path. '\' are normalized to '/'.
    /// Must not be empty or whitespace, contains '//' nor ends with a '/'.
    /// </param>
    /// <param name="streamWriter">The function that must write the stream content.</param>
    /// <returns>The resource locator.</returns>
    public ResourceLocator AddWriter( string resourcePath, Action<Stream> streamWriter )
    {
        Throw.CheckNotNullArgument( streamWriter );
        return DoAdd( resourcePath, streamWriter );
    }

    /// <summary>
    /// Adds a new textual resource.
    /// <para>
    /// This throws if the resource is already associated to a reader, writer, binary content or text
    /// or if <see cref="IsOpened"/> is false.
    /// </para>
    /// </summary>
    /// <param name="resourcePath">
    /// The resource path. '\' are normalized ro '/'.
    /// Must not be empty or whitespace, contains '//' nor ends with a '/'.
    /// </param>
    /// <param name="text">The text content.</param>
    /// <returns>The resource locator.</returns>
    public ResourceLocator AddText( string resourcePath, string text )
    {
        Throw.CheckNotNullArgument( text );
        return DoAdd( resourcePath, text );
    }

    ResourceLocator DoAdd( string resourcePath, object content )
    {
        Throw.CheckArgument( resourcePath != null && !String.IsNullOrWhiteSpace( resourcePath ) );
        resourcePath = resourcePath.Replace( '\\', '/' );
        if( resourcePath.Contains( "//" ) || resourcePath[^1] == '/' )
        {
            Throw.ArgumentException( nameof( resourcePath ), $"'{resourcePath}' must not contain '//' nor ends with '/'." );
        }
        Throw.CheckState( IsOpened );
        if( resourcePath[0] == '/' )
        {
            Throw.CheckArgument( resourcePath != "/" );
            resourcePath = resourcePath.Substring( 1 );
        }
        int idx = ImmutableOrdinalSortedStrings.IndexOf( resourcePath, _names.Span );
        if( idx >= 0 )
        {
            Throw.InvalidOperationException( $"Resource '{resourcePath}' already exists." );
        }
        idx = ~idx;
        if( _names.Length == _pathStore.Length )
        {
            Grow( idx );
        }
        else if( idx < _names.Length )
        {
            Array.Copy( _pathStore, idx, _pathStore, idx + 1, _names.Length - idx );
            Array.Copy( _streamStore, idx, _streamStore, idx + 1, _names.Length - idx );
        }
        _pathStore[idx] = resourcePath;
        _streamStore[idx] = content;
        _names = _pathStore.AsMemory( 0, _names.Length + 1 );
        return new ResourceLocator( this, resourcePath );
    }

    void Grow( int indexToInsert )
    {
        int capacity = checked(_names.Length + 1);
        Throw.DebugAssert( _pathStore.Length == _streamStore.Length && _pathStore.Length < capacity );
        int newCapacity = _pathStore.Length == 0 ? 4 : 2 * _pathStore.Length;
        if( (uint)newCapacity > Array.MaxLength ) newCapacity = Array.MaxLength;
        Throw.DebugAssert( newCapacity >= capacity );

        string[] newPStore = new string[newCapacity];
        object[] newSStore = new object[newCapacity];
        if( indexToInsert != 0 )
        {
            Array.Copy( _pathStore, newPStore, length: indexToInsert );
            Array.Copy( _streamStore, newSStore, length: indexToInsert );
        }
        if( _names.Length != indexToInsert )
        {
            Array.Copy( _pathStore, indexToInsert, newPStore, indexToInsert + 1, _names.Length - indexToInsert );
            Array.Copy( _streamStore, indexToInsert, newSStore, indexToInsert + 1, _names.Length - indexToInsert );
        }
        _pathStore = newPStore;
        _streamStore = newSStore;
    }

    /// <inheritdoc />
    public bool IsValid => true;

    /// <inheritdoc />
    public string DisplayName => _displayName;

    /// <inheritdoc />
    public string ResourcePrefix => String.Empty;

    /// <summary>
    /// Separator is '/'.
    /// </summary>
    public char DirectorySeparatorChar => '/';
    /// <inheritdoc />
    public IEnumerable<ResourceLocator> AllResources => MemoryMarshal.ToEnumerable( _names ).Select( p => new ResourceLocator( this, p ) );

    bool IResourceContainer.HasLocalFilePathSupport => false;

    string? IResourceContainer.GetLocalFilePath( ResourceLocator resource ) => null;

    /// <inheritdoc />
    public IEnumerable<ResourceLocator> GetAllResources( ResourceFolder folder ) => DoGetAllResources( folder, this, _names );

    /// <inheritdoc />
    public ResourceFolder GetFolder( ReadOnlySpan<char> localFolderName ) => DoGetFolder( string.Empty, this, _names.Span, localFolderName );

    /// <inheritdoc />
    public ResourceFolder GetFolder( ResourceFolder folder, ReadOnlySpan<char> localFolderName )
    {
        folder.CheckContainer( this );
        return DoGetFolder( folder.FullFolderName, this, _names.Span, localFolderName );
    }

    /// <inheritdoc />
    public IEnumerable<ResourceFolder> GetFolders( ResourceFolder folder ) => DoGetFolders( folder, this, _names );

    /// <inheritdoc />
    public ResourceLocator GetResource( ReadOnlySpan<char> localResourceName ) => DoGetResource( string.Empty, this, _names.Span, localResourceName );

    /// <inheritdoc />
    public ResourceLocator GetResource( ResourceFolder folder, ReadOnlySpan<char> localResourceName )
    {
        folder.CheckContainer( this );
        return DoGetResource( folder.FullFolderName, this, _names.Span, localResourceName );
    }

    /// <inheritdoc />
    public IEnumerable<ResourceLocator> GetResources( ResourceFolder folder ) => DoGetResources( folder, this, _names );

    /// <inheritdoc />
    public Stream GetStream( ResourceLocator resource )
    {
        resource.CheckContainer( this );
        int idx = ImmutableOrdinalSortedStrings.IndexOf( resource.FullResourceName, _names.Span );
        Throw.DebugAssert( idx >= 0 );
        var s = _streamStore[idx];
        if( s is Func<Stream> reader )
        {
            return reader();
        }
        if( s is string str )
        {
            return Util.RecyclableStreamManager.GetStream( Encoding.UTF8.GetBytes( str ) );
        }
        if( s is byte[] bytes )
        {
            return Util.RecyclableStreamManager.GetStream( bytes );
        }
        var writer = (Action<Stream>)s;
        var memory = Util.RecyclableStreamManager.GetStream();
        writer( memory );
        memory.Position = 0;
        return memory;
    }

    /// <inheritdoc />
    public void WriteStream( ResourceLocator resource, Stream target )
    {
        resource.CheckContainer( this );
        int idx = ImmutableOrdinalSortedStrings.IndexOf( resource.FullResourceName, _names.Span );
        Throw.DebugAssert( idx >= 0 );
        var s = _streamStore[idx];
        if( s is Action<Stream> writer )
        {
            writer( target );
        }
        else if( s is string str )
        {
            var len = Encoding.UTF8.GetMaxByteCount( str.Length );
            var buffer = ArrayPool<byte>.Shared.Rent( len );
            len = Encoding.UTF8.GetBytes( str, buffer.AsSpan() );
            target.Write( buffer.AsSpan( 0, len ) );
            ArrayPool<byte>.Shared.Return( buffer );
        }
        else if( s is byte[] bytes )
        {
            target.Write( bytes );
        }
        else
        {
            using var source = ((Func<Stream>)s)();
            source.CopyTo( target );
        }
    }

    /// <inheritdoc />
    public string ReadAsText( ResourceLocator resource )
    {
        resource.CheckContainer( this );
        int idx = ImmutableOrdinalSortedStrings.IndexOf( resource.FullResourceName, _names.Span );
        Throw.DebugAssert( idx >= 0 );
        var s = _streamStore[idx];
        if( s is string str )
        {
            return str;
        }
        if( s is byte[] bytes )
        {
            return Encoding.UTF8.GetString( bytes );
        }
        if( s is Action<Stream> writer )
        {
            using var memory = Util.RecyclableStreamManager.GetStream();
            writer( memory );
            return Encoding.UTF8.GetString( memory.GetReadOnlySequence() );
        }
        using( var source = ((Func<Stream>)s)() )
        using( var r = new StreamReader( source ) )
        {
            return r.ReadToEnd();
        }
    }

    /// <inheritdoc />
    public override string ToString() => _displayName;

    internal static ResourceLocator DoGetResource( string fullPrefix, IResourceContainer container, ReadOnlySpan<string> names, ReadOnlySpan<char> localResourceName )
    {
        Throw.CheckArgument( localResourceName.Contains( '\\' ) is false );
        if( localResourceName.Length > 0 && localResourceName[0] == '/' )
        {
            localResourceName = localResourceName.Slice( 1 );
        }
        var name = String.Concat( fullPrefix.AsSpan(), localResourceName );
        int idx = ImmutableOrdinalSortedStrings.IndexOf( name, names );
        if( idx >= 0 )
        {
            return new ResourceLocator( container, name );
        }
        return default;
    }

    internal static ResourceFolder DoGetFolder( string prefix, IResourceContainer container, ReadOnlySpan<string> names, ReadOnlySpan<char> localFolderName )
    {
        Throw.CheckArgument( localFolderName.Contains( '\\' ) is false );
        if( localFolderName.Length > 0 && localFolderName[0] == '/' )
        {
            localFolderName = localFolderName.Slice( 1 );
        }
        var name = String.Concat( prefix.AsSpan(), localFolderName );
        if( name[^1] != '/' ) name += '/';
        if( ImmutableOrdinalSortedStrings.IsPrefix( name, names ) )
        {
            return new ResourceFolder( container, name );
        }
        return default;
    }

    internal static IEnumerable<ResourceLocator> DoGetAllResources( ResourceFolder folder, IResourceContainer container, ReadOnlyMemory<string> names )
    {
        folder.CheckContainer( container );
        var (idx, len) = ImmutableOrdinalSortedStrings.GetPrefixedRange( folder.FullFolderName, names.Span );
        if( len > 0 )
        {
            for( int i = 0; i < len; i++ )
            {
                yield return new ResourceLocator( container, names.Span[idx + i] );
            }
        }
    }

    internal static IEnumerable<ResourceLocator> DoGetResources( ResourceFolder folder, IResourceContainer container, ReadOnlyMemory<string> names )
    {
        folder.CheckContainer( container );
        var (idx, len) = ImmutableOrdinalSortedStrings.GetPrefixedRange( folder.FullFolderName, names.Span );
        if( len > 0 )
        {
            for( int i = 0; i < len; i++ )
            {
                var s = names.Span[idx + i];
                if( !s.AsSpan( folder.FullFolderName.Length ).Contains( '/' ) )
                {
                    yield return new ResourceLocator( container, s );
                }
            }
        }
    }

    internal static IEnumerable<ResourceFolder> DoGetFolders( ResourceFolder folder, IResourceContainer container, ReadOnlyMemory<string> names )
    {
        folder.CheckContainer( container );
        var (idx, len) = ImmutableOrdinalSortedStrings.GetPrefixedRange( folder.FullFolderName, names.Span );
        for( int i = 0; i < len; ++i )
        {
            var currentIdx = idx + i;
            var p = names.Span[currentIdx];
            int idxSub = p.AsSpan( folder.FullFolderName.Length ).IndexOf( '/' );
            if( idxSub != -1 )
            {
                p = p.Remove( folder.FullFolderName.Length + idxSub + 1 );
                var lenSub = ImmutableOrdinalSortedStrings.GetEndIndex( p, names.Span.Slice( currentIdx ) );
                Throw.DebugAssert( lenSub > 0 );
                Throw.DebugAssert( (currentIdx, lenSub) == ImmutableOrdinalSortedStrings.GetPrefixedRange( p, names.Span ) );
                yield return new ResourceFolder( container, p );
                i += (lenSub - 1);
            }
        }
    }

}
