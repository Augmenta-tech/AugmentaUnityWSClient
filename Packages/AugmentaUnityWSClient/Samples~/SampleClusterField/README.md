# Cluster Field

One particle field covering the whole space, deformed by every tracked cluster at once.

`ClusterFieldManager` uploads all the clusters of the scene into a single `GraphicsBuffer`
(position, velocity, size, plus an influence that fades in and out), and `AugmentaClusterField.hlsl`
turns that buffer into values the graph can use. Copy both files as is into your own project: the
HLSL functions take a `StructuredBuffer<AugmentaClusterData>`, and that struct is declared in the
generated shaders by the Visual Effect Graph itself, from `ClusterFieldManager.AugmentaClusterData`
being marked `[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]`. Without that attribute — or with the
C# type renamed — the graph compiles to `syntax error: unexpected token 'AugmentaClusterData'`.

## Using the HLSL functions

Add a **Custom HLSL** operator in the graph, set its mode to **Shader File**, pick
`HLSL/AugmentaClusterField.hlsl`, and type the name of the function you want. The `Clusters` and
`ClusterCount` exposed properties go into every one of them.

| Function | Returns |
|---|---|
| `ClosestClusterPosition` | Position of the nearest cluster, or the given position when there is none |
| `ClosestClusterDirection` | Normalized direction towards the nearest cluster, for `Orient : Look At Position` |
| `ClosestCluster` | Position, velocity, size, distance and influence of the nearest cluster |
| `ClusterAttractionForce` | Pull towards every cluster within `FieldRadius`, summed. Multiply by a negative value to repel |
| `ClusterSwirlForce` | Rotation around every cluster within `FieldRadius`. Multiply by a negative value to turn the other way |
| `ClusterFieldDensity` | 0 to 1 proximity to the clusters, to drive color, size or alpha |

A Custom HLSL node accepts at most **4 inputs**, so the two force functions take no strength: scale
their output with a `Multiply` node in the graph.

## Graph wiring

The effect must run in **world space**: the cluster data is uploaded in world space and the graph
applies no transform to it.

Exposed properties expected by `ClusterFieldManager`: `Clusters` (GraphicsBuffer), `ClusterCount`
(uint), `AttractStrength`, `SwirlStrength`, `FieldRadius` (floats).

- **Initialize**: spread the particles over the interactive volume (`Set Position (Shape: AABox)`),
  with a random lifetime.
- **Update**: `ClusterAttractionForce` and `ClusterSwirlForce`, both fed the `position` attribute and
  multiplied by `AttractStrength` / `SwirlStrength`, added together into an `Add Velocity` block,
  followed by a `Linear Drag` block so the field settles instead of building up speed.
- **Output**: additive quads, so overlapping particles glow where people are.

The client does not need the cluster points option for this sample: only the cluster boxes are used.
