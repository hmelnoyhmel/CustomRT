using System;
using System.Collections.Generic;
using UnityEngine;

public class RayTracedMesh : MonoBehaviour
{
    public RayTracingMaterial material;
    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;
    public int triangleCount;

    [SerializeField, HideInInspector] bool materialInitFlag;

    public Vector3[] worldPositions;
    public Vector3[] worldNormals;

    public struct Triangle
    {
        public Vector3 posA, posB, posC;
        public Vector3 normalA, normalB, normalC;
    }
    
    public Triangle[] triangles;

    public Vector3 boundsMin;
    public Vector3 boundsMax;
    
    private void OnValidate()
    {
        if (!materialInitFlag)
        {
            materialInitFlag = true;
            material.SetDefaultValues();
        }

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sharedMaterial.color = material.color;
        }
        
        meshFilter = GetComponent<MeshFilter>();
        SetupGeometry();
    }

    public void SetupGeometry()
    {
        var mesh = meshFilter.sharedMesh;
        
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var indices = mesh.triangles;
        
        var localToWorld = meshFilter.transform.localToWorldMatrix;
        
        worldPositions = new Vector3[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            worldPositions[i] = localToWorld.MultiplyPoint3x4(vertices[i]);
        }
        
        var normalMatrix = localToWorld.inverse.transpose;
        worldNormals = new Vector3[normals.Length];
        
        for (int i = 0; i < normals.Length; i++)
        {
            worldNormals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
        }

        triangles = new Triangle[indices.Length / 3];

        for (int i = 0; i < triangles.Length; i++)
        {
            var a = indices[i * 3];
            var b = indices[i * 3 + 1];
            var c = indices[i * 3 + 2];

            triangles[i].posA = worldPositions[a];
            triangles[i].normalA = worldNormals[a];
            
            triangles[i].posB = worldPositions[b];
            triangles[i].normalB = worldNormals[b];
            
            triangles[i].posC = worldPositions[c];
            triangles[i].normalC = worldNormals[c];
        }
        
        triangleCount = triangles.Length;

        boundsMin = worldPositions[0];
        boundsMax = worldPositions[0];

        for (int i = 0; i < worldPositions.Length; i++)
        {
            boundsMin = Vector3.Min(boundsMin, worldPositions[i]);
            boundsMax = Vector3.Max(boundsMax, worldPositions[i]);
        }

    }
}
