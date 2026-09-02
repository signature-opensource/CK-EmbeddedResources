# CK.EmbeddedResources.Assets

Implements _assets resources_: their definition and their composition accross multiple sources in a
Direct Acyclic Graph structure.

> ℹ️ Read [CK.EmbeddedResources](../CK.EmbeddedResources/README.md) first: assets are loaded from an
> `IResourceContainer`.

## Definitions and finals

Two families of types, and the distinction between them carries the whole design.

A **definition** is what one package says. A
[`ResourceAssetDefinition`](ResourceAssetDefinition.cs) is a `ResourceLocator` plus a
`ResourceOverrideKind`, and a [`ResourceAssetDefinitionSet`](ResourceAssetDefinitionSet.cs) indexes them
by their **target** path - where the resource wants to land, which is not where it lives.

A **final** is what the whole graph agreed on. A [`FinalResourceAsset`](FinalResourceAsset.cs) is *"a
resource and a potential set of ambiguities"*: an `Origin`, plus the other locators that claimed the
same target path. A [`FinalResourceAssetSet`](FinalResourceAssetSet.cs) is `IsAmbiguous` when any of its
assets carries such a list.

Ambiguity is recorded, not resolved on the spot. Two independent packages both writing `logos/logo.png`
is not an error where it happens - neither of them is wrong - so the conflict is carried forward until
something upstream settles it. The rule is stated once: **the ultimate final set of a Direct Acyclic
Graph must not be ambiguous.**

## Three ways to get a final set

`FinalResourceAssetSet` lists them in its own comment:

1. **From a definition set with no dependency.** `ResourceAssetDefinitionSet.ToInitialFinalSet` produces
   a *"terminal, dependency-less"* set. It fails - logs and returns null - if any definition is a
   `Regular` override, because there is nothing to override. `Optional` overrides are silently dropped;
   only `None` and `Always` survive. By construction, no ambiguity.
2. **By aggregating two final sets.** `Aggregate` merges them, and a target path mapped to different
   resources on each side becomes an ambiguity even though neither input was ambiguous. This is the
   operation that walks up the graph, and the class summary describes it as *"commutative, associative
   and idempotent"* - with one nuance worth knowing, below.
3. **By combining a definition set into a final set.** `ResourceAssetDefinitionSet.Combine` is where
   overrides do their work: *"the override definitions can resolve ambiguities from the final set."*

Note the asymmetry between 2 and 3. `Combine` is explicitly **not** idempotent:

> This operation is not idempotent. When applied twice on a set, false ambiguities will be created.

So a definition set is applied to its base exactly once.

### What `Aggregate` guarantees, and what it does not

On a collision, one asset keeps its `Origin` and the other - its resource *and* its own ambiguities - is
folded in as an ambiguity:

```csharp
var f = f1.AddAmbiguity( f2 );
```

Which of the two survives is not the caller's choice. `Aggregate` iterates the *smaller* set into a copy
of the larger one, so `f1` above is the smaller set's asset and its `Origin` is the one kept. When both
sets have the same count no swap happens and the receiver wins by default. Do not read anything into it:
a set holding a conflict is rejected as a whole, so the surviving `Origin` never reaches an installer.

What *is* dependable is the union: whichever direction you aggregate in, the result holds the same target
paths and knows about the same resources for each, because `AddAmbiguity` accumulates both sides'
`Origin` and `Ambiguities`.

The same resource reached through two routes is not a conflict: `AddAmbiguity` returns the asset
unchanged when the locator already equals its `Origin`.

One known gap remains, and it is shared with the Globalization sibling rather than specific to this
package: `isAmbiguous` is seeded from the larger set only and is never updated in the branch that adds
an asset present in the smaller set alone. An ambiguity carried by such an asset reaches the result while
the flag stays false, contradicting `IsAmbiguous`'s own definition. Aggregating an already-ambiguous
smaller set into a larger clean one is the case to watch.

That gap is also the one thing that can make `IsAmbiguous` depend on the direction of the call: with two
sets of equal count there is no smaller/larger swap, so the flag is seeded from whichever set was passed
as the argument. Until the gap is closed, treat `IsAmbiguous` as reliable only when at least one asset
collided.

`FinalResourceAssetSet.Empty` is the singleton to start from.

## The `assets/` folder and its manifest

[`ResourceContainerAssetsExtension.LoadAssets`](ResourceContainerAssetsExtension.cs) reads a folder -
`assets` by default - from any container. Without a manifest, the folder structure is reproduced under
the `defaultTargetPath` given by the component that owns the resources. Missing folder is not an error:
it returns true with a null set.

```
assets/
  logo.png
  some-data/
    data1.json
    data2.jsonc
  other-data/
    data1.json
```

An `assets.jsonc` file takes over the whole folder. Every property is optional (this is the example from
the XML comment, with its JSON separators repaired):

```jsonc
{
    // Defines the default mapping for resources that have no defined mapping.
    // When defined this replaces the provided defaultTargetPath (that is
    // typically the path from the component that holds the resources).
    // It can be the empty string to target the root of the final asset folder.
    "targetPath": "my/component/target",

    // Optional mappings from locally define resources to a target path in the final asset folder.
    "mappings": {
        // The logo.png will be in the final "/logos/" asset folder.
        "logo.png": "logos",
        // The 2 data files will be in the final "/data/core/" asset folder.
        "some-data/data1.json": "data/core",
        "some-data/data2.json": "data/core"
    },

    // Regular override: logo.png overrides an existing resource. The /logos/logo.png must already
    //                   exist or a warning will be emitted.
    "O": [ "logos/logo.png" ],

    // Optional override: data/core/data1.json will be updated only if it already exists.
    //                    If the resource doesn't exits, nothing is done (and no warning is emitted).
    "?O": [ "data/core/data1.json" ],

    // Always override: the /other-data/data1.json will always be updated whether it exists or not.
    //                  This is a risky behavior.
    "!O": [ "other-data/data1.json" ]
}
```

Two rules keep the manifest unambiguous:

- A mapping may target a folder (`"some-data": "data/core"`), but **mapping a resource more than once -
  explicitly or through a folder - is an error**.
- Overrides are processed **after** the mappings, so their paths are target paths, and a resource may
  appear in at most one of the three override sections.

## Requires.

- `CK.EmbeddedResources`, and through it
  [`ResourceOverrideKind`](../CK.EmbeddedResources.Abstractions/ResourceOverrideKind.cs) whose four
  values are exactly the manifest's `"O"`, `"?O"`, `"!O"` and the absence of any of them.
