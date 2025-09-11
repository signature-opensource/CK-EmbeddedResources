using CK.Core;
using System;
using System.IO;

namespace CK.EmbeddedResources;

public sealed partial class CodeGenResourceContainer
{
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

}
