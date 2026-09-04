Asset resources: their definition per package and their composition across a Direct Acyclic Graph.

Each package declares where its assets should land and whether they override what another package
already put there. Those definition sets are combined up the graph into a final set; when two packages
claim the same target path the conflict is carried along as an ambiguity that an override can resolve,
and the final set of the whole graph must have none left.

An optional assets.jsonc manifest remaps files or folders to target paths and declares the override
behavior of each one.
