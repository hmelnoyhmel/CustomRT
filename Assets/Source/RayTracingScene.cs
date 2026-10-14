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
            public int firstNodeIndex;
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
        private BVH.Node[] bvhNodes;
        
        private GraphicsBuffer trianglesBuffer;
        private GraphicsBuffer allMeshInfoBuffer;
        private GraphicsBuffer bvhNodesBuffer;
        
        private bool sceneDirty = true;
        
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
            material.SetBuffer("BVHNodes", bvhNodesBuffer);
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
            
            bvhNodesBuffer?.Dispose();
            bvhNodesBuffer = null;
        }
        
        private bool TryCreateMeshes()
        {
            RayTracedMesh[] meshObjects = Object.FindObjectsByType<RayTracedMesh>();
            if (!MeshesChanged(meshObjects)) return false;
        
            sceneDirty = true;
            var tris =  new List<RayTracedMesh.Triangle>();
            var nodes = new List<BVH.Node>();
            meshes = new MeshInfo[meshObjects.Length];
            previousMeshStates = new Dictionary<RayTracedMesh, MeshState>();
        
            for (int i = 0; i < meshObjects.Length; i++)
            {
                var mesh = meshObjects[i];
                mesh.SetupGeometry();
                var count = tris.Count;
                meshes[i] = new MeshInfo()
                {
                    firstTriangleIndex = count,
                    numTriangles = mesh.triangleCount,
                    firstNodeIndex = nodes.Count,
                    boundsMin = mesh.boundsMin,
                    boundsMax = mesh.boundsMax,
                    material = mesh.material
                };
                tris.AddRange(mesh.Triangles);
                nodes.AddRange(mesh.Bvh.Tree);
            
                var meshState = CreateMeshState(mesh);
                previousMeshStates.TryAdd(mesh, meshState);
            }

            triangles = tris.ToArray();
            bvhNodes = nodes.ToArray();
            return true;
        }
        
        private bool MeshesChanged(RayTracedMesh[] meshObjects)
        {
            // check for initial scene build
            if (sceneDirty) return true;
            if (triangles == null) return true;
            if (meshes == null) return true;
            if (bvhNodes == null) return true;
        
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
            ResizeBuffer(ref allMeshInfoBuffer, meshes.Length, 96);
            ResizeBuffer(ref bvhNodesBuffer, bvhNodes.Length, 32);
            
            trianglesBuffer.SetData(triangles);
            allMeshInfoBuffer.SetData(meshes);
            bvhNodesBuffer.SetData(bvhNodes);
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