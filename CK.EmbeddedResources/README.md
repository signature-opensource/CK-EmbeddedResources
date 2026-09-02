# CK.EmbeddedResources

Provides an abstraction for "resources" identified by a string that can exist as embedded resources in
assembly, be files on the local file system or dynamically created by code.

Is a bigger package that implements resource containers. Resource containers are a "File System" with
resources in folders identified by a hierarchical path.

> ℹ️ The `Res/` folders whose content this package reads are produced by the MSBuild logic of
> [CK.EmbeddedResources.Abstractions](../CK.EmbeddedResources.Abstractions/README.md).

## The container abstraction

The [`IResourceContainer`](IResourceContainer.cs) is the common abstraction to different concrete
containers:

- The [`AssemblyResourceContainer`](Containers/AssemblyResourceContainer.cs) exposes embedded resources
  in assembly.
- The [`FileSystemResourceContainer`](Containers/FileSystemResourceContainer.cs) exposes local file
  system folders and files as resources.
- The [`CodeGenResourceContainer`](Containers/CodeGenResourceContainer.cs) exposes code generated
  content as resources.
- [`EmptyResourceContainer`](Containers/EmptyResourceContainer.cs) and
  [`ResourceContainerWrapper`](Containers/ResourceContainerWrapper.cs) are rather classical helpers for
  such abstractions.

Resources are identified by [`ResourceLocator`](ResourceLocator.cs) and
[`ResourceFolder`](ResourceFolder.cs) that are 2 small structs managed by their owning container.

_Note:_ This abstraction is not perfect. The "folder separator" should be abstracted more than it
currenly is: today, only '/' or '\' is really supported, but this is enough for our needs.

### Invalid instead of null

Both structs make the same choice, and state it in their own comment: *"The `default` value is `IsValid`
false: this makes `Nullable<T>` useless for this type."* So `GetResource` and `GetFolder` return a
locator that is simply not valid rather than a null, and there is no `ResourceLocator?` anywhere.
`IsValid` on a locator says the *container reference* is there - **not** that the resource exists.

The containers themselves have an `IsValid` too, with a different meaning: an `AssemblyResourceContainer`
is valid even when empty, and *"it is false only when an error prevented a correct instantiation"*. An
invalid container is how a failure travels back from a *construction* that logged an error instead of
throwing. Reading from one is another matter: `EmptyResourceContainer.GetStream`, `WriteStream` and
`ReadAsText` all throw `InvalidOperationException`, as `IResourceContainer` documents.

Sizes are bounded: `MaxNameLength` is 512 characters and `MaxFolderCount` is `512 / 2 - 1`.

### What each container gives up

The four implementations differ on two axes, and the differences are the interesting part:

| Container | Separator | `HasLocalFilePathSupport` | Contents |
|-----------|-----------|---------------------------|----------|
| `AssemblyResourceContainer` | `/` | via the `LocalDevSolution` detection | fixed at construction |
| `FileSystemResourceContainer` | platform `Path.DirectorySeparatorChar` | true by default, can be disabled at construction | *"unstable" by design: `ResourceFolder` and `ResourceLocator` can "disappear" when deleted from the file system* |
| `CodeGenResourceContainer` | `/` (`\` is normalized to it) | false by design | grows via `AddText`/`AddBinary`/`AddReader`/`AddWriter` while `IsOpened`, until `Close()` |
| `EmptyResourceContainer` | `/`, and *"this is meaningless"* | *"always false"* | empty, and can be constructed *disabled* or *invalid* on purpose |

`HasLocalFilePathSupport` being true is not a promise that every resource has a file path, and a
resource that has one may still not match its stream: *"the resource stream is a projection (a
transformation) of the file content"*, or the file was edited after capture.

`ResourceContainerWrapper` is the odd one out. It hides its inner container completely - every locator
and folder flowing through it is bound to the wrapper - so the inner one can be swapped without
invalidating anything held by callers. Its comment restricts the intended use sharply: *"This should
almost always be used to transition from an empty container to a real one."* Swapping between two
non-empty containers makes resources disappear, which is *"not the implicit contract of resource
containers"*. It is also the only container that is not serializable, because a graph reference is
beyond what this library's simple serialization does - serialize `InnerContainer` and rebuild the
wrapper.

The other four - including `EmptyResourceContainer` - are `ICKVersionedBinarySerializable` at
`[SerializationVersion(0)]`.

## From an assembly to a container

[`AssemblyResources`](AssemblyResources.cs) is deliberately *not* an `IResourceContainer` - *"because we
cannot know the folder separator to use for the .Net resources"*. It captures the manifest resource
names of an assembly into an `ImmutableOrdinalSortedStrings` and splits out the ones prefixed `ck@`,
which do have a known separator. From there:

```csharp
AssemblyResources r = typeof( Something ).Assembly.GetResources();
IResourceContainer c = typeof( Something ).CreateResourcesContainer( monitor );
```

[`TypeExtensions.CreateResourcesContainer`](TypeExtensions.cs) is the path most code takes. It reads the
`IEmbeddedResourceTypeAttribute` on the type, uses its captured `CallerFilePath` to work out which
`Res/` folder belongs to it, and returns either an `AssemblyResourceContainer` or - when the assembly is
recognized as locally built - a `FileSystemResourceContainer` on the real source folder. Passing
`resAfter: true` targets `Res[After]/` instead, and `ignoreLocal: true` forces the embedded form
(*"this is mainly for tests"*).

When the type carries no such attribute the call still returns a container: an `EmptyResourceContainer`
with `IsValid` false, after logging the error. Check `IsValid` on what comes back.

That local-source detection is [`LocalDevSolution`](CK.Core/LocalDevSolution.cs), and it happens in two
stages. `HasLocalProjects` is decided once, in a static constructor, and needs three things:

1. a `/.git` folder found by walking up from `AppContext.BaseDirectory`;
2. next to it, a `.sln` or `.slnx` named after that folder;
3. **at least one `.csproj` listed in that solution file that actually exists on disk** - the ones that
   do not are individually warned about and skipped.

Then, per assembly, `FindLocalProjectPath` reads the `SolutionRelativeProjectPath` metadata that the
build target wrote and checks it against that set. So the metadata does *not* gate `HasLocalProjects`;
it is what maps one assembly to one project once the solution has been recognized. For a published
package neither stage succeeds and everything falls back to embedded resources.

The sorted-strings structure is not incidental:
[`ImmutableOrdinalSortedStrings`](CK.Core/ImmutableOrdinalSortedStrings.cs) supports
`GetPrefixedRange`, so scoping a container to a folder is a binary search returning a slice of the
shared array, not a copy.

## Requires.

- `CK.EmbeddedResources.Abstractions` and `CK.ActivityMonitor.SimpleSender` - errors here are logged to
  a monitor and surface as an invalid container rather than as exceptions.
