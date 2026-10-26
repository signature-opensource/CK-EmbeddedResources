Automatic embedded resources for the `Res/` and `Res[After]/` folders of a project: MSBuild props and
targets, flowing transitively across dependencies, embed every file under them with a "ck@" prefixed name.
Main types: `IEmbeddedResourceTypeAttribute` (and its default `EmbeddedResourceTypeAttribute`), which maps
a type to its resource folder, and `ResourceOverrideKind`, the ways a package can redefine a resource.
