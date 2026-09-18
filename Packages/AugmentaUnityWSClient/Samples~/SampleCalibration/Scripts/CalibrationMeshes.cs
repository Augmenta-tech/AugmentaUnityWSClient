using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the procedural meshes the calibration debug objects are drawn with.
/// </summary>
public static class CalibrationMeshes
{
    public static Mesh WireCube()
    {
        var mesh = new Mesh { name = "CalibrationWireCube" };
        mesh.SetVertices(new[]
        {
            new Vector3(-.5f, -.5f, -.5f), new Vector3(.5f, -.5f, -.5f), new Vector3(.5f, -.5f, .5f), new Vector3(-.5f, -.5f, .5f),
            new Vector3(-.5f, .5f, -.5f), new Vector3(.5f, .5f, -.5f), new Vector3(.5f, .5f, .5f), new Vector3(-.5f, .5f, .5f)
        });
        mesh.SetIndices(new[]
        {
            0, 1, 1, 2, 2, 3, 3, 0,
            4, 5, 5, 6, 6, 7, 7, 4,
            0, 4, 1, 5, 2, 6, 3, 7
        }, MeshTopology.Lines, 0);
        return mesh;
    }

    public static Mesh Quad()
    {
        var mesh = new Mesh { name = "CalibrationQuad" };
        mesh.SetVertices(new[]
        {
            new Vector3(-.5f, 0, -.5f), new Vector3(.5f, 0, -.5f), new Vector3(.5f, 0, .5f), new Vector3(-.5f, 0, .5f)
        });
        mesh.SetUVs(0, new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
        mesh.SetIndices(new[] { 0, 2, 1, 0, 3, 2 }, MeshTopology.Triangles, 0);
        return mesh;
    }

    public static void UpdatePoints(Mesh mesh, ArraySegment<Vector3> points)
    {
        if (points.Array == null || points.Count == 0)
        {
            mesh.Clear();
            return;
        }

        if (mesh.indexFormat != IndexFormat.UInt32)
        {
            mesh.indexFormat = IndexFormat.UInt32;
        }

        var indices = new int[points.Count];
        for (int i = 0; i < indices.Length; ++i)
        {
            indices[i] = i;
        }

        mesh.Clear();
        mesh.SetVertices(points.Array, points.Offset, points.Count);
        mesh.SetIndices(indices, MeshTopology.Points, 0);
        mesh.RecalculateBounds();
    }
}
