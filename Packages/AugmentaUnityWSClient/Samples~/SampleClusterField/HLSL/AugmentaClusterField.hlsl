// Generic Custom HLSL library turning a buffer of Augmenta clusters into values a Visual Effect
// Graph can consume: the closest cluster, an attraction force, a swirl force, a proximity density.
//
// Use it from a Custom HLSL *Operator* in "Shader File" mode, picking one function per node. Every
// function takes the cluster buffer and its count, so it works from any context that can read the
// particle position.
//
// All cluster data is in world space, so the effect must run in world space too.
//
// A Custom HLSL node builds one expression per input, and an expression takes at most 4 parents: a
// function exposed here must therefore never have more than 4 parameters.

#ifndef AUGMENTA_CLUSTER_FIELD_INCLUDED
#define AUGMENTA_CLUSTER_FIELD_INCLUDED

// Smooth 1 to 0 falloff over [0, radius], flat at both ends so a particle crossing the radius does
// not jerk. Returns 0 outside the radius, which is what makes summing over all clusters cheap to
// reason about.
/// Hidden
float AugmentaClusterFalloff(float distance, float radius)
{
    return radius > 0.0f ? smoothstep(1.0f, 0.0f, saturate(distance / radius)) : 0.0f;
}

/// Index of the cluster closest to the given position, or -1 when the buffer is empty.
/// Compares squared distances, so no square root is taken inside the loop.
int ClosestClusterIndex(StructuredBuffer<AugmentaClusterData> clusters, uint count, float3 position)
{
    int closest = -1;
    float closestSqrDistance = 3.402823466e+38f;

    for (uint i = 0; i < count; i++)
    {
        float3 delta = clusters[i].position - position;
        float sqrDistance = dot(delta, delta);

        if (sqrDistance < closestSqrDistance)
        {
            closestSqrDistance = sqrDistance;
            closest = (int)i;
        }
    }

    return closest;
}

/// Position of the cluster closest to the given position.
/// Falls back to the position itself when no cluster is present, so anything derived from it
/// (a direction, a force, a look at target) comes out neutral instead of pointing at the origin.
float3 ClosestClusterPosition(StructuredBuffer<AugmentaClusterData> clusters, uint count, float3 position)
{
    int closest = ClosestClusterIndex(clusters, count, position);
    return closest < 0 ? position : clusters[closest].position;
}

/// Everything known about the cluster closest to the given position, in one node.
/// With no cluster present, clusterPosition is the position itself, distance is 0 and influence is
/// 0: test influence rather than distance to know whether the query found anything.
void ClosestCluster(StructuredBuffer<AugmentaClusterData> clusters, uint count, float3 position,
                    out float3 clusterPosition, out float3 clusterVelocity, out float3 clusterSize,
                    out float distance, out float influence)
{
    int closest = ClosestClusterIndex(clusters, count, position);

    if (closest < 0)
    {
        clusterPosition = position;
        clusterVelocity = float3(0.0f, 0.0f, 0.0f);
        clusterSize = float3(0.0f, 0.0f, 0.0f);
        distance = 0.0f;
        influence = 0.0f;
        return;
    }

    AugmentaClusterData cluster = clusters[closest];

    clusterPosition = cluster.position;
    clusterVelocity = cluster.velocity;
    clusterSize = cluster.size;
    distance = length(cluster.position - position);
    influence = cluster.influence;
}

/// Force pulling the given position towards every cluster within radius, faded by their influence.
/// Contributions are summed, so a particle between two people is pulled by both.
/// Multiply the result by the strength you want; a negative value repels instead.
float3 ClusterAttractionForce(StructuredBuffer<AugmentaClusterData> clusters, uint count, float3 position,
                              float radius)
{
    float3 force = float3(0.0f, 0.0f, 0.0f);

    for (uint i = 0; i < count; i++)
    {
        float3 delta = clusters[i].position - position;
        float distance = length(delta);

        float weight = AugmentaClusterFalloff(distance, radius) * clusters[i].influence;
        if (weight <= 0.0f || distance <= 1e-5f)
        {
            continue;
        }

        force += (delta / distance) * weight;
    }

    return force;
}

/// Force turning the given position around the vertical axis of every cluster within radius, faded
/// by their influence. Combined with a small attraction, particles spiral in instead of falling
/// straight to the center.
/// Multiply the result by the strength you want; a negative value turns the other way.
float3 ClusterSwirlForce(StructuredBuffer<AugmentaClusterData> clusters, uint count, float3 position,
                         float radius)
{
    float3 force = float3(0.0f, 0.0f, 0.0f);

    for (uint i = 0; i < count; i++)
    {
        float3 delta = clusters[i].position - position;
        float distance = length(delta);

        float weight = AugmentaClusterFalloff(distance, radius) * clusters[i].influence;
        if (weight <= 0.0f || distance <= 1e-5f)
        {
            continue;
        }

        // Tangent to the horizontal circle around the cluster. Degenerates right above and below
        // the cluster, where the cross product goes to zero, which is the behaviour we want.
        float3 tangent = cross(delta / distance, float3(0.0f, 1.0f, 0.0f));

        force += tangent * weight;
    }

    return force;
}

/// How close the given position is to the clusters, as a 0 to 1 scalar: 0 away from everyone, 1 on
/// a cluster, more than 0 but clamped to 1 where several clusters overlap.
/// Meant to drive color, size or alpha from stock nodes.
float ClusterFieldDensity(StructuredBuffer<AugmentaClusterData> clusters, uint count, float3 position,
                          float radius)
{
    float density = 0.0f;

    for (uint i = 0; i < count; i++)
    {
        float distance = length(clusters[i].position - position);
        density += AugmentaClusterFalloff(distance, radius) * clusters[i].influence;
    }

    return saturate(density);
}

/// Direction the given position should face to look at the closest cluster, already normalized.
/// Returns the world forward axis when no cluster is present, so particles keep a stable orientation
/// instead of collapsing.
float3 ClosestClusterDirection(StructuredBuffer<AugmentaClusterData> clusters, uint count, float3 position)
{
    int closest = ClosestClusterIndex(clusters, count, position);
    if (closest < 0)
    {
        return float3(0.0f, 0.0f, 1.0f);
    }

    float3 delta = clusters[closest].position - position;
    float distance = length(delta);

    return distance <= 1e-5f ? float3(0.0f, 0.0f, 1.0f) : delta / distance;
}

#endif // AUGMENTA_CLUSTER_FIELD_INCLUDED
