Asset resources: their definition per package and their composition across a Direct Acyclic Graph.
Main types: `ResourceAssetDefinitionSet` (loaded by `LoadAssets` from an `assets/` folder and its optional
`assets.jsonc` manifest) and `FinalResourceAssetSet`, the composed result in which unresolved target path
conflicts are carried as ambiguities.
