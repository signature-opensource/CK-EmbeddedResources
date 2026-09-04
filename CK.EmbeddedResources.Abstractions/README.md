# CK.EmbeddedResources.Abstractions

Provides automatic embedded resources support for `Res/` and `Res[After]/` folders thanks to the
[MSBuild](MSBuild/) props and targets files that transitively flow accross dependencies.

It is a micro-package that only defines 3 types - and the build logic is arguably the larger half of it.

## The three types

- [`IEmbeddedResourceTypeAttribute`](IEmbeddedResourceTypeAttribute.cs) is an interface that can be
  implemented by attributes.
- [`EmbeddedResourceTypeAttribute`](EmbeddedResourceTypeAttribute.cs) is the default implementation of
  this interface.
- [`ResourceOverrideKind`](ResourceOverrideKind.cs) is an enum that models all possible behaviors when
  dealing with overrides of a resource accross packages.

### Locating a type's resources

The whole point of the attribute interface is one property:

```csharp
public interface IEmbeddedResourceTypeAttribute
{
    /// <summary>
    /// Gets the source file path that where the attribute decorates a type.
    /// <para>
    /// When null, resources associated to the type cannot be located.
    /// </para>
    /// </summary>
    string? CallerFilePath { get; }
}
```

`EmbeddedResourceTypeAttribute` fills it from a `[CallerFilePath]` default parameter, so the compiler
bakes the source path of the *decoration site* into the assembly. That path is what later maps a type
back to the `Res/` folder that sits next to its source file - a link that nothing else in .NET provides.

An attribute that carries this interface is therefore doing double duty: whatever else it means, it also
declares "this type owns the resources beside it".

In practice a decorated type is the whole declaration:

```csharp
using CK.Core;

namespace Namespace.Does.Not.Matter;

[EmbeddedResourceType]
public class SomeType
{
}
```

with the resources beside it, and the logical names the build gives them:

```
SomeType/SomeType.cs
SomeType/Res/data.json        ->  ck@SomeType/Res/data.json
```

The namespace in that fixture is called `Namespace.Does.Not.Matter` on purpose, and it is the clearest
statement of what this package does. Classic .NET embedded-resource names are built from the namespace;
these are built from the **project-relative source path**. Move the file and the resource name follows
it; rename the namespace and nothing changes.

Two mechanisms meet on that path, and they are worth keeping apart:

- **At build time**, the `.targets` file names the resource from the item's own location -
  `ck@$([System.String]::new('%(RelativeDir)').Replace('\','/'))%(FileName)%(Extension)`. No attribute
  is involved; a `Res/` file is embedded under its project-relative path whether or not any type
  is decorated.
- **At run time**, `[CallerFilePath]` gives the *absolute* path of the decoration site. That is more
  than is wanted, so the consumer walks it back to the folder named after the assembly and keeps the
  remainder - recovering the same project-relative sub-path the build used, and with it the type's own
  resource prefix.

So the attribute does not produce the name; it lets a type compute the name the build already chose.
The consequence is worth stating plainly: the folder layout is the API. A `Res/` folder is bound to the
type whose source file sits next to it, so moving a `.cs` file without its `Res/` folder silently
detaches its resources.

From [`SomeType`](../Tests/CK.EmbeddedResources.Tests/SomeType/SomeType.cs).

### Overriding across packages

Resources coming from different packages land on the same target paths, so a definition has to say what
it expects to find there. `ResourceOverrideKind` names four possibilities; three of them have a string
form used in manifests, as a value (`"O"`) or as a prefix (`"O:"`), and the fourth is the default that
needs no marker:

| Value | String | Behaviour |
|-------|--------|-----------|
| `None` | *(none)* | *"the defined resource must not already exist. This default mode prevents any unattended rewrite of existing resources."* |
| `Regular` | `O` | *"the safest one: the resource must already exist"* - so removing it upstream raises an error or a warning instead of silently changing meaning |
| `Optional` | `?O` | overrides only if it exists; *"no warning of any kind must be emitted"* otherwise |
| `Always` | `!O` | *"adds the resource whether it exists or not. This is the most risky mode to consider."* |

The ordering is deliberate: the default refuses to overwrite, and each step away from it trades a check
for convenience. `Regular` is called the safest *override* because it is the only one that notices when
the thing being overridden disappears.

## What the build does

The package ships its props and targets under `buildTransitive/`, which is what makes them apply to
every project that depends on it, directly or not.

[`CK.EmbeddedResources.Abstractions.props`](MSBuild/CK.EmbeddedResources.Abstractions.props) declares
the item group:

```xml
<CKEmbeddedResource Include="**/Res/**;**/Res[After]/**" Exclude="obj/**;bin/**" />
```

[`CK.EmbeddedResources.Abstractions.targets`](MSBuild/CK.EmbeddedResources.Abstractions.targets) then
folds them into the standard `EmbeddedResource` group before `BeforeResGen`, rewriting each logical name
to `ck@` followed by the project-relative path with `/` separators:

```xml
<LogicalName>ck@$([System.String]::new('%(RelativeDir)').Replace('\','/'))%(FileName)%(Extension)</LogicalName>
```

Two properties of that name are load-bearing, and a reader will not guess either. The `ck@` prefix marks
these resources as belonging to this convention, so a reader of the assembly's manifest can tell them
apart from every other embedded resource by name alone. And the separator is forced to `/` whatever the
build platform, which turns a .NET resource name - an opaque string - into a path that can be walked.

Set `EnableCKEmbeddedResource` to `false` to turn the whole target off.

The same target also writes an assembly metadata attribute:

```xml
<AssemblyMetadata Include="SolutionRelativeProjectPath" Value="$(_SolutionRelativeProjectPath)" />
```

It records where the project sits inside its solution, which is knowable at build time and nowhere else:
once the assembly is published, nothing in it says which folder its sources came from. A reader that
finds this metadata and can see the solution on disk can therefore go back to the *source* files instead
of the embedded copies - which is what lets a developer edit a resource and see the change without
rebuilding.

Its value is `$(MSBuildProjectFullPath.Substring($(SolutionDir.Length)))`, so it depends on `SolutionDir`
being defined. Visual Studio defines it; a plain `dotnet build` does not, which is why this repository's
own `Directory.Build.props` unifies it - and that file is not part of the package. Absent
`SolutionDir`, the metadata is simply not usable and the embedded resources are the only source.
