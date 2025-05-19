using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.RegularExpressions;

namespace CK.Core;

/// <summary>
/// Tries to detect a "under development" solution and locates the local projects.
/// </summary>
public static class LocalDevSolution
{
    /// <summary>
    /// Gets the solution root folder based on <see cref="AppContext.BaseDirectory"/>
    /// and the existence of a /.git folder.
    /// </summary>
    public static NormalizedPath SolutionFolder;

    /// <summary>
    /// Gets the local ".csproj" projects folders found in the ".sln" or ".slnx"
    /// file in the solution folder indexed by their name.
    /// <para>
    /// given a <c>"{SolutionFolder}MyProject/MySuperProject.csproj"</c> file, the dictionary entry is
    /// <c>"MySuperProject"</c>, <c>"{SolutionFolder}MyProject"</c>.
    /// </para>
    /// <para>
    /// This is empty if <see cref="SolutionFolder"/> is <see cref="NormalizedPath.IsEmptyPath"/> (no
    /// <c>/.git</c> folder has been found above <see cref="AppContext.BaseDirectory"/>).
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<string,NormalizedPath> LocalProjectPaths;

    static LocalDevSolution()
    {
        var p = GetSolutionFolder();
        Dictionary<string, NormalizedPath>? projectsPath = null;
        if( !p.IsEmptyPath )
        {
            var slnText = ReadSlnFile( p );
            if( slnText != null )
            {
                // One time regex. Don't cache.
                var projects = Regex.Matches( slnText, @"(?<="")[^""]*\.csproj(?="")" );
                foreach( Match project in projects )
                {
                    var path = p.Combine( project.Value );
                    if( !File.Exists( path ) )
                    {
                        ActivityMonitor.StaticLogger.Warn( $"Project file '{path}' declared in solution file not found. Ignoring project." );
                    }
                    else
                    {
                        projectsPath ??= new Dictionary<string, NormalizedPath>();
                        var name = path.LastPart;
                        Throw.DebugAssert( name.EndsWith( ".csproj" ) && ".csproj".Length == 7 );
                        name = name.Substring( 0, name.Length - 7 );
                        if( !projectsPath.TryAdd( name, path.RemoveLastPart() ) )
                        {
                            ActivityMonitor.StaticLogger.Warn( $"Found duplicate project '{name}' in solution file. Ignoring project '{path}'." );
                        }
                    }
                }
                if( projectsPath == null )
                {
                    ActivityMonitor.StaticLogger.Warn( $"No project found in solution file:{Environment.NewLine}{slnText}." );
                }
            }
        }
        SolutionFolder = p;
        LocalProjectPaths = (IReadOnlyDictionary<string, NormalizedPath>?)projectsPath
                            ?? ImmutableDictionary<string, NormalizedPath>.Empty;

        static NormalizedPath GetSolutionFolder()
        {
            var p = AppContext.BaseDirectory;
            while( !string.IsNullOrEmpty( p ) )
            {
                if( Directory.Exists( Path.Combine( p, ".git" ) ) )
                {
                    return p;
                }
                p = Path.GetDirectoryName( p );
            }
            return default;
        }

        static string? ReadSlnFile( NormalizedPath solutionFolder )
        {
            var slnPath = solutionFolder.AppendPart( solutionFolder.LastPart + ".sln" );
            if( File.Exists( slnPath ) )
            {
                return File.ReadAllText( slnPath );
            }
            var slnxPath = slnPath.Path + 'x';
            if( File.Exists( slnxPath ) )
            {
                return File.ReadAllText( slnxPath );
            }
            ActivityMonitor.StaticLogger.Warn( $"Unable to find expected '{slnPath}' file. No local projects can be handled." );
            return null;
        }
    }
}
