using CommunityToolkit.HighPerformance;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using CK.Core;

namespace CK.EmbeddedResources;

/// <summary>
/// Extends <see cref="Assembly"/>.
/// </summary>
public static class AssemblyExtensions
{
    static readonly ConcurrentDictionary<Assembly, AssemblyResources> _cache = new();
    static readonly object _lock = new();

    /// <summary>
    /// Gets all resource names contained in the assembly (calls <see cref="Assembly.GetManifestResourceNames"/>)
    /// as a sorted ascending (thanks to <see cref="StringComparer.Ordinal"/>) cached list of strings (see <see cref="ImmutableOrdinalSortedStrings"/>).
    /// </summary>
    /// <param name="assembly">Assembly </param>
    /// <returns>An ordered list of the resource names.</returns>
    static public AssemblyResources GetResources( this Assembly assembly )
    {
        return _cache.GetOrAdd( assembly, a =>
        {
            var assemblyName = assembly.GetName().Name;
            Throw.CheckArgument( "Cannot handle dynamic assembly.", assemblyName != null );
            // We DO care about duplicate computation.
            // "Out of lock" GetOrAdd must be protected.
            lock( _lock )
            {
                return new AssemblyResources( a, assemblyName );
            }
        } );
    }

    internal static AssemblyResources GetResources( Assembly assembly, string assemblyName )
    {
        return _cache.GetOrAdd( assembly, a =>
        {
            // We DO care about duplicate computation.
            // "Out of lock" GetOrAdd must be protected.
            lock( _lock )
            {
                return new AssemblyResources( a, assemblyName );
            }
        } );
    }

    /// <summary>
    /// Gets a resource content.
    /// <para>
    /// If a stream cannot be obtained, a detailed <see cref="IOException"/> is raised.
    /// </para>
    /// </summary>
    /// <param name="assembly">This assembly.</param>
    /// <param name="resourceName">The reource name.</param>
    /// <returns>The resource's content stream.</returns>
    public static Stream OpenResourceStream( this Assembly assembly, string resourceName )
    {
        var s = assembly.GetManifestResourceStream( resourceName );
        return s ?? ThrowDetailedError( assembly, resourceName );
    }

    [StackTraceHidden]
    static Stream ThrowDetailedError( Assembly assembly, string resourceName )
    {
        var b = new StringBuilder();
        b.AppendLine( $"Resource '{resourceName}' cannot be loaded from '{assembly.GetName().Name}'." );
        AssemblyExtensions.AppendDetailedError( b, assembly, resourceName );
        throw new IOException( b.ToString() );
    }

    /// <summary>
    /// Tries to load the content of a "ck@" prefixed resource as a string, logging any error and returning null on error.
    /// </summary>
    /// <param name="assembly">This assembly.</param>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="resourceName">The resource name with the "ck@" prefix.</param>
    /// <param name="logLevel">The log level when the resource cannot be found or loaded.</param>
    /// <returns>The string or null on error.</returns>
    public static string? TryGetCKResourceString( this Assembly assembly,
                                                  IActivityMonitor monitor,
                                                  string resourceName,
                                                  LogLevel logLevel = LogLevel.Error )
    {
        return TryGetCKResourceString( assembly, monitor, resourceName, logLevel, null );
    }

    internal static string? TryGetCKResourceString( Assembly assembly,
                                                    IActivityMonitor monitor,
                                                    string resourceName,
                                                    LogLevel logLevel,
                                                    AssemblyResources? assemblyResources )
    {
        using var s = TryOpenCKResourceStream( assembly, monitor, resourceName, logLevel, assemblyResources );
        try
        {
            return s == null
                    ? null
                    : new StreamReader( s, detectEncodingFromByteOrderMarks: true, leaveOpen: true ).ReadToEnd();
        }
        catch( Exception ex )
        {
            LogLoadError( assembly, monitor, resourceName, logLevel, ex );
            return null;
        }
    }

    /// <summary>
    /// Tries to load the content of a "ck@" prefixed resource, logging any error and returning null on error.
    /// </summary>
    /// <param name="assembly">This assembly.</param>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="resourceName">The resource name with the "ck@" prefix.</param>
    /// <param name="logLevel">The log level when the resource cannot be found or loaded.</param>
    /// <returns>The Stream or null on error.</returns>
    public static Stream? TryOpenCKResourceStream( this Assembly assembly,
                                                   IActivityMonitor monitor,
                                                   string resourceName,
                                                   LogLevel logLevel = LogLevel.Error )
    {
        return TryOpenCKResourceStream( assembly, monitor, resourceName, logLevel, null );
    }

    internal static Stream? TryOpenCKResourceStream( Assembly assembly,
                                                     IActivityMonitor monitor,
                                                     string resourceName,
                                                     LogLevel logLevel,
                                                     AssemblyResources? assemblyResources )
    {
        if( resourceName is null
            || resourceName.Length <= 3
            || !resourceName.StartsWith( "ck@" )
            || resourceName.Contains( '\\' ) )
        {
            Throw.ArgumentException( $"Invalid resource name '{resourceName}'. It must start with \"ck@\" and must not contain '\\'." );
        }
        try
        {
            var s = assembly.GetManifestResourceStream( resourceName );
            if( s == null )
            {
                var resName = resourceName.AsSpan().Slice( 3 );
                var fName = Path.GetFileName( resName );
                string? shouldBe = null;
                assemblyResources ??= assembly.GetResources();
                foreach( string c in assemblyResources.CKResourceNames.Span )
                {
                    if( c.AsSpan().EndsWith( fName, StringComparison.OrdinalIgnoreCase ) )
                    {
                        shouldBe = c;
                        if( c[c.Length - fName.Length - 1] == '/' )
                        {
                            break;
                        }
                    }
                }
                monitor.Log( logLevel, $"CK resource not found: '{resName}' in assembly '{assembly.GetName().Name}'.{(shouldBe == null ? string.Empty : $" It seems to be '{shouldBe.AsSpan().Slice( 3 )}'.")}" );
            }
            return s;
        }
        catch( Exception ex )
        {
            LogLoadError( assembly, monitor, resourceName, logLevel, ex );
            return null;
        }
    }

    static void LogLoadError( Assembly a, IActivityMonitor monitor, string resourceName, LogLevel logLevel, Exception ex )
    {
        monitor.Log( logLevel, $"While loading '{resourceName}' from '{a.GetName().Name}'.", ex );
    }

    internal static void AppendDetailedError( StringBuilder b, Assembly assembly, string resourceName )
    {
        var info = assembly.GetManifestResourceInfo( resourceName );
        if( info == null )
        {
            b.AppendLine( "No information for this resource. The ResourceName may not exist at all. Resource names are:" );
            foreach( var n in assembly.GetManifestResourceNames() )
            {
                b.AppendLine( n );
            }
        }
        else
        {
            b.AppendLine( "ManifestResourceInfo:" )
             .Append( "ReferencedAssembly = " ).Append( info.ReferencedAssembly ).AppendLine()
             .Append( "FileName = " ).Append( info.FileName ).AppendLine()
             .Append( "ResourceLocation = " ).Append( info.ResourceLocation ).AppendLine();
        }
    }
}
