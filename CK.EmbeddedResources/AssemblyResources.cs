using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CK.EmbeddedResources;

/// <summary>
/// Captures the embedded resource names of an assembly and exposes them through a <see cref="ImmutableOrdinalSortedStrings"/>.
/// Among them, resource names that starts with "ck@" prefix are exposed by <see cref="CKResourceNames"/>.
/// <para>
/// Instances are created by <see cref="AssemblyExtensions.GetResources(Assembly)"/>.
/// </para>
/// At a higher level, the <see cref="CreateResourcesContainerForType(IActivityMonitor, Type, string?, bool)"/> provides
/// the <see cref="AssemblyResourceContainer"/> defined in the "/Res" folder of a type definition.
/// </summary>
public sealed partial class AssemblyResources
{
    readonly Assembly _assembly;
    readonly string _assemblyName;
    readonly ImmutableOrdinalSortedStrings _allResourceNames;
    readonly ReadOnlyMemory<string> _ckResourceNames;
    readonly NormalizedPath _localPath;

    internal AssemblyResources( Assembly a, string assemblyName )
    {
        _assemblyName = assemblyName;
        _assembly = a;
        _allResourceNames = ImmutableOrdinalSortedStrings.UnsafeCreate( a.GetManifestResourceNames(), mustSort: true );
        _ckResourceNames = _allResourceNames.GetPrefixedStrings( "ck@" );
        var localProjects = LocalDevSolution.LocalProjectPaths;
        if( localProjects.Count > 0 )
        {
            localProjects.TryGetValue( assemblyName, out _localPath );
        }
    }

    /// <summary>
    /// Gets the assembly.
    /// </summary>
    public Assembly Assembly => _assembly;

    /// <summary>
    /// Gets the simple assembly name. This can never be null as dynamic assemblies
    /// are not handled by <see cref="AssemblyResources"/>.
    /// </summary>
    public string AssemblyName => _assemblyName;

    /// <summary>
    /// Gets the local development folder for this assembly.
    /// To not be the <see cref="NormalizedPath.IsEmptyPath"/>, the running code must be in
    /// a git working folder, a ".sln" or ".slx" file conventionnaly named with the solution
    /// directory name must exist and contain a .csproj reference that matches the name of
    /// this assembly.
    /// </summary>
    public NormalizedPath LocalPath => _localPath;

    /// <summary>
    /// Gets all the resource names.
    /// </summary>
    public ImmutableOrdinalSortedStrings AllResourceNames => _allResourceNames;

    /// <summary>
    /// Gets the sorted resource names that start with "ck@".
    /// </summary>
    public ReadOnlyMemory<string> CKResourceNames => _ckResourceNames;

    /// <summary>
    /// Gets a resource content.
    /// <para>
    /// If a stream cannot be obtained, a detailed <see cref="IOException"/> is raised.
    /// </para>
    /// </summary>
    /// <param name="resourceName">The reource name.</param>
    /// <returns>The resource's content stream.</returns>
    public Stream OpenResourceStream( string resourceName ) => _assembly.OpenResourceStream( resourceName );

    /// <summary>
    /// Tries to load the content of a "ck@" prefixed resource as a string, logging any error and returning null on error.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="resourceName">The resource name with the "ck@" prefix.</param>
    /// <param name="logLevel">The log level when the resource cannot be found or loaded.</param>
    /// <returns>The string or null on error.</returns>
    public string? TryGetCKResourceString( IActivityMonitor monitor, string resourceName, LogLevel logLevel = LogLevel.Error )
    {
        return AssemblyExtensions.TryGetCKResourceString( _assembly, monitor, resourceName, logLevel, this );
    }

    /// <summary>
    /// Tries to load the content of a "ck@" prefixed resource, logging any error and returning null on error.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="resourceName">The resource name with the "ck@" prefix.</param>
    /// <param name="logLevel">The log level when the resource cannot be found or loaded.</param>
    /// <returns>The Stream or null on error.</returns>
    public Stream? TryOpenCKResourceStream( IActivityMonitor monitor, string resourceName, LogLevel logLevel = LogLevel.Error )
    {
        return AssemblyExtensions.TryOpenCKResourceStream( _assembly, monitor, resourceName, logLevel, this );
    }

    /// <summary>
    /// Creates a <see cref="IResourceContainer"/> on a sub path of the "ck@" embedded resources.
    /// All the "ck@" prefixed resources can be handled when <paramref name="subPath"/> is empty.
    /// <para>
    /// If the path doesn't start with "ck@", the prefix is automatically added.
    /// Path separator is '/' (using a '\' will throw).
    /// It can be empty: all the <see cref="CKResourceNames"/> will be reachable.
    /// </para>
    /// </summary>
    /// <param name="subPath">
    /// The path of the directory. May start with "ck@" or not.
    /// Should almost always end with "Res/" as it's the standard directory names that automatically
    /// embeds the "ck@" resources. 
    /// </param>
    /// <param name="displayName">The container display name.</param>
    /// <returns>A resource container. May be empty.</returns>
    public AssemblyResourceContainer CreateCKResourceContainer( string subPath, string displayName )
    {
        if( string.IsNullOrEmpty( subPath ) )
        {
            return new AssemblyResourceContainer( this, "ck@", displayName, _ckResourceNames );
        }
        Throw.CheckArgument( subPath.Contains( '\\' ) is false );

        bool needPrefix = !subPath.StartsWith( "ck@", StringComparison.Ordinal );
        bool needSuffix = subPath.Length > (needPrefix ? 3 : 0) && subPath[^1] != '/';
        if( needPrefix )
        {
            subPath = needSuffix
                        ? "ck@" + subPath + '/'
                        : "ck@" + subPath;
        }
        else if( needSuffix )
        {
            subPath = subPath + '/';
        }
        var (idx, len) = ImmutableOrdinalSortedStrings.GetPrefixedRange( subPath, _ckResourceNames.Span );
        return new AssemblyResourceContainer( this, subPath, displayName, _ckResourceNames.Slice( idx, len ) );
    }

    /// <summary>
    /// Creates a <see cref="AssemblyResourceContainer"/> for a type that must be decorated with at least
    /// one <see cref="IEmbeddedResourceTypeAttribute"/> attribute.
    /// <para>
    /// On success, the container is bound to the corresponding embedded ressources "Res/" folder.
    /// It may not be <see cref="AssemblyResourceContainer.IsValid"/> (an error has been logged).
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="type">The declaring type. Its <see cref="Type.Assembly"/> MUST be this <see cref="Assembly"/>.</param>
    /// <param name="containerDisplayName">When null, the <see cref="AssemblyResourceContainer.DisplayName"/> defaults to "resources of '...' type".</param>
    /// <param name="resAfter">True to consider the "Res[After]" folder instead of "Res".</param>
    /// <returns>The resources (IsValid may be false).</returns>
    public AssemblyResourceContainer CreateResourcesContainerForType( IActivityMonitor monitor,
                                                                      Type type,
                                                                      string? containerDisplayName = null,
                                                                      bool resAfter = false )
    {
        return GetCallerInfo( monitor, type, type.GetCustomAttributes().OfType<IEmbeddedResourceTypeAttribute>(), out var callerPath, out var callerSource )
                ? CreateResourcesContainerForType( monitor, callerPath, type, callerSource.GetType().Name, containerDisplayName, resAfter )
                : new AssemblyResourceContainer( this, AssemblyResourceContainer.MakeDisplayName( containerDisplayName, type, resAfter) );

    }

    /// <summary>
    /// Creates a <see cref="AssemblyResourceContainer"/> for a type based on a CallerFilePath (<see cref="CallerFilePathAttribute"/>)
    /// captured by an attribute on the <paramref name="type"/> (typically a <see cref="IEmbeddedResourceTypeAttribute"/>).
    /// <para>
    /// On success, the container is bound to the corresponding embedded ressources "Res/" folder.
    /// It may not be <see cref="AssemblyResourceContainer.IsValid"/> (an error has been logged).
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="callerFilePath">The caller file path. This is required: when null or empty, an error is logged and an invalid container is returned.</param>
    /// <param name="type">The declaring type. Its <see cref="Type.Assembly"/> MUST be this <see cref="Assembly"/>. (This is used for logging).</param>
    /// <param name="attributeName">The attribute name that declares the resource (used for logging).</param>
    /// <param name="containerDisplayName">When null, the <see cref="AssemblyResourceContainer.DisplayName"/> defaults to "resources of '...' type".</param>
    /// <param name="resAfter">True to consider the "Res[After]" folder instead of "Res".</param>
    /// <returns>The resources (IsValid may be false).</returns>
    public AssemblyResourceContainer CreateResourcesContainerForType( IActivityMonitor monitor,
                                                                      string? callerFilePath,
                                                                      Type type,
                                                                      string attributeName,
                                                                      string? containerDisplayName = null,
                                                                      bool resAfter = false )
    {
        containerDisplayName = AssemblyResourceContainer.MakeDisplayName( containerDisplayName, type, resAfter );
        if( ValidateContainerForType( monitor, Assembly, _assemblyName, callerFilePath, type, attributeName, out var subPath ) )
        {
            var prefix = resAfter
                            ? (subPath.IsEmptyPath
                                ? "ck@Res[After]/"
                                : $"ck@{subPath.Path}/Res[After]/")
                            : (subPath.IsEmptyPath
                                ? "ck@Res/"
                                : $"ck@{subPath.Path}/Res/");

            return new AssemblyResourceContainer( this,
                                                  prefix,
                                                  containerDisplayName,
                                                  ImmutableOrdinalSortedStrings.GetPrefixedStrings( prefix, _ckResourceNames ) );
        }
        return new AssemblyResourceContainer( this, containerDisplayName );
    }

    internal static bool GetCallerInfo( IActivityMonitor monitor,
                                        Type type,
                                        IEnumerable<IEmbeddedResourceTypeAttribute> attributes,
                                        [NotNullWhen( true )] out string? callerPath,
                                        [NotNullWhen( true )] out IEmbeddedResourceTypeAttribute? callerSource )
    {
        var eA = attributes.GetEnumerator();
        callerPath = null;
        callerSource = null;
        if( !eA.MoveNext() )
        {
            monitor.Error( $"Type '{type:N}' has no IEmbeddedResourceTypeAttribute. Unable to resolve a resource container." );
            return false;
        }
        callerSource = eA.Current;
        callerPath = callerSource.CallerFilePath ?? "";
        while( eA.MoveNext() )
        {
            var otherSource = eA.Current;
            var otherPath = otherSource.CallerFilePath ?? "";
            if( callerPath.Length == 0 )
            {
                callerPath = otherPath;
                callerSource = otherSource;
            }
            else if( otherPath.Length != 0 && callerPath != otherPath )
            {
                monitor.Error( $"""
                                Type '{type:N}' has more than one IEmbeddedResourceTypeAttribute with different CallerFilePath.
                                Attribute '{callerSource.GetType().Name}' provides '{callerPath}' and '{otherSource.GetType().Name}' provides '{otherPath}'.
                                """ );
                return false;
            }
        }
        if( callerPath.Length == 0 )
        {
            monitor.Error( $"Type '{type:N}' has IEmbeddedResourceTypeAttribute but with a null or empty CallerFilePath. Unable to resolve a resource container." );
            return false;
        }
        return true;
    }

    internal static bool ValidateContainerForType( IActivityMonitor monitor,
                                                   Assembly assembly,
                                                   string assemblyName,
                                                   string? callerFilePath,
                                                   Type type,
                                                   string attributeName,
                                                   out NormalizedPath subPath )
    {
        Throw.CheckNotNullArgument( type );
        Throw.CheckNotNullArgument( attributeName );
        Throw.CheckArgument( type.Assembly == assembly );
        subPath = callerFilePath;
        if( subPath.IsEmptyPath )
        {
            monitor.Error( $"[{attributeName}] on '{type:N}' has no CallerFilePath. Unable to resolve relative embedded resource paths." );
            return false;
        }
        int idx;
        for( idx = subPath.Parts.Count - 2; idx >= 0; --idx )
        {
            if( subPath.Parts[idx] == assemblyName ) break;
        }
        if( idx < 0 )
        {
            monitor.Error( $"Unable to resolve relative embedded resource paths: the assembly name '{assemblyName}' parent folder of type '{type:N}' not found in caller file path '{subPath}'." );
            return false;
        }
        subPath = subPath.RemoveFirstPart( idx + 1 ).With( NormalizedPathRootKind.None ).RemoveLastPart();
        return true;
    }

}
