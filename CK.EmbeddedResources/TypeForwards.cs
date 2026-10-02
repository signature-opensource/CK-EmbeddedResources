using CK.Core;
using System.Runtime.CompilerServices;

// LocalDevSolution now lives in CK.ActivityMonitor. This forward keeps assemblies
// that were compiled against CK.EmbeddedResources working.
[assembly: TypeForwardedTo( typeof( LocalDevSolution ) )]
