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
            
            public Matrix4x4 localToWorld;
            public Matrix4x4 worldToLocal;
        }
        
        private struct MeshState
        {
            public Mesh mesh;
            public Matrix4x4 localToWorld;
            public RayTracingMaterial material;
            public BVH bvh;
        }
        
        private Dictionary<RayTracedMesh, MeshState> previousMeshStates = new Dictionary<RayTracedMesh, MeshState>();

        private RayTracedMesh.Triangle[] triangles;
        private MeshInfo[] meshes;
        private BVH.Node[] bvhNodes;
        private RayTracedMesh[] sceneMeshes;
        
        private GraphicsBuffer trianglesBuffer;
        private GraphicsBuffer allMeshInfoBuffer;
        private GraphicsBuffer bvhNodesBuffer;
        
        private bool sceneDirty = true;
        
        public bool TryBuildSceneData()
        {
            var geometryChanged = TryCreateMeshes();
            var meshInfoChanged = geometryChanged || TryUpdateMeshInfo();

            if (meshInfoChanged) sceneDirty = true;
            UploadSceneData(geometryChanged, meshInfoChanged);
            sceneDirty = false;
            
            return meshInfoChanged;
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
            if (!GeometryChanged(meshObjects)) return false;
            
            sceneDirty = true;
            sceneMeshes = meshObjects;
            
            var firstInstanceByMesh = new Dictionary<Mesh, int>();
            var tris =  new List<RayTracedMesh.Triangle>();
            var nodes = new List<BVH.Node>();
            
            meshes = new MeshInfo[meshObjects.Length];
            previousMeshStates = new Dictionary<RayTracedMesh, MeshState>();
        
            for (int i = 0; i < meshObjects.Length; i++)
            {
                var mesh = meshObjects[i];
                var unityMesh = mesh.meshFilter.sharedMesh;
                
                MeshInfo meshInfo;
                
                if (firstInstanceByMesh.TryGetValue(unityMesh, out var firstInstanceIndex))
                {
                    mesh.UseGeometryFrom(meshObjects[firstInstanceIndex]);
                    meshInfo = meshes[firstInstanceIndex];
                }
                else
                {
                    mesh.SetupGeometry();

                    meshInfo = new MeshInfo
                    {
                        firstTriangleIndex = tris.Count,
                        numTriangles = mesh.triangleCount,
                        firstNodeIndex = nodes.Count,
                        boundsMin = mesh.boundsMin,
                        boundsMax = mesh.boundsMax
                    };

                    tris.AddRange(mesh.Triangles);
                    nodes.AddRange(mesh.Bvh.Tree);

                    firstInstanceByMesh.Add(unityMesh, i);
                }
                
                var meshTransform = mesh.meshFilter.transform;

                meshInfo.material = mesh.material;
                meshInfo.localToWorld = meshTransform.localToWorldMatrix;
                meshInfo.worldToLocal = meshTransform.worldToLocalMatrix;

                meshes[i] = meshInfo;
                previousMeshStates.TryAdd(mesh, CreateMeshState(mesh));
            }

            triangles = tris.ToArray();
            bvhNodes = nodes.ToArray();
            return true;
        }
        
        private bool GeometryChanged(RayTracedMesh[] meshObjects)
        {
            // check for initial scene build
            if (sceneDirty) return true;
            if (triangles == null) return true;
            if (meshes == null) return true;
            if (bvhNodes == null) return true;
            if (sceneMeshes == null) return true;
        
            if (meshObjects.Length != previousMeshStates.Count) return true;
        
            for (int i = 0; i < meshObjects.Length; i++)
            {
                if (!previousMeshStates.TryGetValue(meshObjects[i], out var previousState))
                    return true;

                var currentState = CreateMeshState(meshObjects[i]);

                if (currentState.mesh != previousState.mesh) return true;
                if (currentState.bvh != previousState.bvh) return true;
            }

            return false;
        }
        
        private void UploadSceneData(bool geometryChanged, bool meshInfoChanged)
        {
            if (geometryChanged)
            {
                ResizeBuffer(ref trianglesBuffer, triangles.Length, 72);
                ResizeBuffer(ref bvhNodesBuffer, bvhNodes.Length, 32);
                
                trianglesBuffer.SetData(triangles);
                bvhNodesBuffer.SetData(bvhNodes);
            }

            if (meshInfoChanged)
            {
                ResizeBuffer(ref allMeshInfoBuffer, meshes.Length, 224);
                allMeshInfoBuffer.SetData(meshes);
            }
        }
        
        private bool TryUpdateMeshInfo()
        {
            bool changed = false;
            for (int i = 0; i < sceneMeshes.Length; i++)
            {
                var mesh = sceneMeshes[i];
                var previousState = previousMeshStates[mesh];
                var currentState = CreateMeshState(mesh);

                if (currentState.localToWorld.Equals(previousState.localToWorld) &&
                    currentState.material.Equals(previousState.material))
                    continue;

                var meshInfo = meshes[i];

                meshInfo.localToWorld = currentState.localToWorld;
                meshInfo.worldToLocal = mesh.meshFilter.transform.worldToLocalMatrix;
                meshInfo.material = currentState.material;

                meshes[i] = meshInfo;
                previousMeshStates[mesh] = currentState;

                changed = true;
            }
            return changed;
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
                bvh = mesh.Bvh,
                localToWorld = mesh.meshFilter.transform.localToWorldMatrix,
                material = mesh.material
            };
        }
    }
}