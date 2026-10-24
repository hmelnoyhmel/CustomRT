using System;
using System.Collections.Generic;
using UnityEngine;

public class RayTracedMesh : MonoBehaviour
{
    public RayTracingMaterial material;
    public MeshFilter meshFilter;
    public int triangleCount;

    private Mesh builtMesh;
    
    [SerializeField, HideInInspector] bool materialInitFlag;

    public struct Triangle
    {
        public Vector3 posA, posB, posC;
        public Vector3 normalA, normalB, normalC;
    }
    
    private Triangle[] triangles;
    public Triangle[] Triangles => triangles;

    private BVH bvh;
    public BVH Bvh => bvh;
    
#if UNITY_EDITOR
    public static bool CollectStats { get; set; } = true;
    public BVHStats? Stats { get; private set; }
#endif

    public Vector3 boundsMin;
    public Vector3 boundsMax;
    
    private void OnValidate()
    {
        if (!materialInitFlag)
        {
            materialInitFlag = true;
            material.SetDefaultValues();
        }
        
        meshFilter = GetComponent<MeshFilter>();
        
        if (bvh == null || builtMesh != meshFilter.sharedMesh)
            SetupGeometry();
    }

    public void SetupGeometry()
    {
        var mesh = meshFilter.sharedMesh;
        
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var indices = mesh.triangles;
        
        triangles = new Triangle[indices.Length / 3];
        
        for (int i = 0; i < triangles.Length; i++)
        {
            var a = indices[i * 3];
            var b = indices[i * 3 + 1];
            var c = indices[i * 3 + 2];

            triangles[i].posA = vertices[a];
            triangles[i].normalA = normals[a];

            triangles[i].posB = vertices[b];
            triangles[i].normalB = normals[b];

            triangles[i].posC = vertices[c];
            triangles[i].normalC = normals[c];
        }
        
        triangleCount = triangles.Length;

        boundsMin = vertices[0];
        boundsMax = vertices[0];

        for (int i = 1; i < vertices.Length; i++)
        {
            boundsMin = Vector3.Min(boundsMin, vertices[i]);
            boundsMax = Vector3.Max(boundsMax, vertices[i]);
        }

#if UNITY_EDITOR
        var timer = CollectStats
            ? System.Diagnostics.Stopwatch.StartNew()
            : null;
#endif
        
        bvh = new BVH(triangles);
        builtMesh = mesh;
        
#if UNITY_EDITOR
        if (timer != null)
        {
            timer.Stop();
            Stats = BVHStats.Collect(bvh, timer.Elapsed.TotalMilliseconds);
            return;
        }
        
        Stats = null;
#endif
    }
    
    internal void UseGeometryFrom(RayTracedMesh source)
    {
        Debug.Assert(source.builtMesh == meshFilter.sharedMesh);

        triangles = source.triangles;
        bvh = source.bvh;
        builtMesh = source.builtMesh;

        triangleCount = source.triangleCount;
        boundsMin = source.boundsMin;
        boundsMax = source.boundsMax;

#if UNITY_EDITOR
        Stats = source.Stats;
#endif
    }
}
