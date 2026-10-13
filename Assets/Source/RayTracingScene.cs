using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Source
{
    public class RayTracingScene : IDisposable
    {
        private struct MeshInfo
        {
            public int firstTriangleIndex;
            public int numTriangles;
            public Vector3 boundsMin;
            public Vector3 boundsMax;
            public RayTracingMaterial material;
        }
        
        private struct MeshState
        {
            public Mesh mesh;
            public Matrix4x4 localToWorld;
            public RayTracingMaterial material;
        }
        
        private Dictionary<RayTracedMesh, MeshState> previousMeshStates = new Dictionary<RayTracedMesh, MeshState>();

        private RayTracedMesh.Triangle[] triangles;
        private MeshInfo[] meshes;
        
        private bool sceneDirty = true;
        
        private GraphicsBuffer trianglesBuffer;
        private GraphicsBuffer allMeshInfoBuffer;
        
        public void BuildSceneData()
        {
            var rebuiltMeshes = TryCreateMeshes();
            UploadSceneData(rebuiltMeshes);
            sceneDirty = false;
        }
        
        public void BindSceneData(Material material)
        {
            material.SetInt("NumMeshes", meshes.Length);
            material.SetBuffer("Triangles", trianglesBuffer);
            material.SetBuffer("AllMeshInfo", allMeshInfoBuffer);
        }
        
        public void MarkAsDirty()
        {
            sceneDirty = true;
        }

        public void Dispose()
        {
            trianglesBuffer?.Dispose();
            trianglesBuffer = null;
            
            allMeshInfoBuffer?.Dispose();
            allMeshInfoBuffer = null;
        }
        
        private bool TryCreateMeshes()
        {
            RayTracedMesh[] meshObjects = Object.FindObjectsByType<RayTracedMesh>();
            if (!MeshesChanged(meshObjects)) return false;
        
            sceneDirty = true;
            var tris =  new List<RayTracedMesh.Triangle>();
            meshes = new MeshInfo[meshObjects.Length];
            previousMeshStates = new Dictionary<RayTracedMesh, MeshState>();
        
            for (int i = 0; i < meshObjects.Length; i++)
            {
                meshObjects[i].SetupGeometry();
                var count = tris.Count;
                meshes[i] = new MeshInfo()
                {
                    firstTriangleIndex = count,
                    numTriangles = meshObjects[i].triangleCount,
                    boundsMin = meshObjects[i].boundsMin,
                    boundsMax = meshObjects[i].boundsMax,
                    material = meshObjects[i].material
                };
                tris.AddRange(meshObjects[i].Triangles);
            
                var meshState = CreateMeshState(meshObjects[i]);
                previousMeshStates.TryAdd(meshObjects[i], meshState);
            }

            triangles = tris.ToArray();
            return true;
        }
        
        private bool MeshesChanged(RayTracedMesh[] meshObjects)
        {
            // check for initial scene build
            if (sceneDirty) return true;
            if (triangles == null) return true;
            if (meshes == null) return true;
        
            if (meshObjects.Length != previousMeshStates.Count) return true;
        
            for (int i = 0; i < meshObjects.Length; i++)
            {
                if (!previousMeshStates.TryGetValue(meshObjects[i], out var previousState))
                    return true;

                var currentState = CreateMeshState(meshObjects[i]);

                if (currentState.mesh != previousState.mesh) return true;
                if (!currentState.localToWorld.Equals(previousState.localToWorld)) return true;
                if (!currentState.material.Equals(previousState.material)) return true;
            }

            return false;
        }
        
        private void UploadSceneData(bool needsUpload)
        {
            if (!needsUpload) return;
            ResizeBuffer(ref trianglesBuffer, triangles.Length, 72);
            ResizeBuffer(ref allMeshInfoBuffer, meshes.Length, 92);
            trianglesBuffer.SetData(triangles);
            allMeshInfoBuffer.SetData(meshes);
        }
        
        private void ResizeBuffer(ref GraphicsBuffer buffer, int elementCount, int stride)
        {
            var size = Mathf.Max(1, elementCount);

            if (buffer == null)
            {
                buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, size, stride);
                return;
            }
        
            if (buffer.count == size && buffer.stride == stride) return;
        
            buffer.Dispose();
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, size, stride);
        }
        
        private MeshState CreateMeshState(RayTracedMesh mesh)
        {
            return new MeshState
            {
                mesh = mesh.meshFilter.sharedMesh,
                localToWorld = mesh.meshFilter.transform.localToWorldMatrix,
                material = mesh.material
            };
        }
    }
}