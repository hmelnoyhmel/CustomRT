using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Mathf;

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class RayTracingManager : MonoBehaviour
{
    [SerializeField] private bool useShaderInSceneView;
    [SerializeField] private Material rayTracingMaterial;
    [SerializeField] private int MaxBounceCount = 4;
    [SerializeField] private int NumRaysPerPixel = 1;
    
    [SerializeField] private Light directionalLight;
    [SerializeField][Min(1)] private float sunFocus;
    
    [SerializeField] private Color SkyColorHorizon = Color.blue;
    [SerializeField] private Color SkyColorZenith = Color.blue;
    [SerializeField] private Color GroundColor = Color.gray;
    
    private static readonly int RayTracingEnabledId = Shader.PropertyToID("RayTracingEnabled");
    
    // we have to pass spheres to the shader
    public struct Sphere
    {
        public Vector3 position;
        public float radius;
        public RayTracingMaterial material;
    }
    private GraphicsBuffer sphereBuffer;

    public struct MeshInfo
    {
        public int firstTriangleIndex;
        public int numTriangles;
        public Vector3 boundsMin;
        public Vector3 boundsMax;
        public RayTracingMaterial material;
    }

    private GraphicsBuffer triangles;
    private GraphicsBuffer allMeshInfo;
    private int numMeshes;
    
    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += UpdateCameraParams;
    }

    // pass near plane params to a shader
    private void UpdateCameraParams(ScriptableRenderContext context, Camera cam)
    {
        if (!useShaderInSceneView)
        {
            rayTracingMaterial.SetInt(RayTracingEnabledId, 0);
            return;
        }
        
        rayTracingMaterial.SetInt(RayTracingEnabledId, 1);
        
        float planeHeight = cam.nearClipPlane * Tan(cam.fieldOfView * 0.5f * Deg2Rad) * 2;
        float planeWidth = planeHeight * cam.aspect;

        rayTracingMaterial.SetVector("ViewParams", new Vector4(planeWidth, planeHeight, cam.nearClipPlane));
        rayTracingMaterial.SetMatrix("CamLocalToWorld", cam.transform.localToWorldMatrix);

        CreateSpheres();
        CreateMeshes();
        SetupSunAndSky();
    }

    void SetupSunAndSky()
    {
        rayTracingMaterial.SetVector("SunLightDirection",  directionalLight.transform.forward);
        rayTracingMaterial.SetFloat("SunIntensity", directionalLight.intensity);
        rayTracingMaterial.SetFloat("SunFocus", sunFocus);
        
        rayTracingMaterial.SetColor("SkyColorHorizon", SkyColorHorizon);
        rayTracingMaterial.SetColor("SkyColorZenith", SkyColorZenith);
        rayTracingMaterial.SetColor("GroundColor", GroundColor);
    }
    
    void CreateSpheres()
    {
        // it's a bad way to find something tbh
        RayTracedSphere[] sphereObjects = FindObjectsByType<RayTracedSphere>();
        Sphere[] spheres = new Sphere[sphereObjects.Length];
        
        for (int i = 0; i < sphereObjects.Length; i++)
        {
            spheres[i] = new Sphere()
            {
                position = sphereObjects[i].transform.position,
                radius = sphereObjects[i].transform.localScale.x * 0.5f,
                material = sphereObjects[i].material
            };
        }
        
        if (sphereBuffer == null)
            sphereBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, spheres.Length, 76);
        
        sphereBuffer.SetData(spheres);
        
        rayTracingMaterial.SetBuffer("Spheres", sphereBuffer);
        rayTracingMaterial.SetInt("NumSpheres", sphereObjects.Length);
        rayTracingMaterial.SetInt("MaxBounceCount", MaxBounceCount);
        rayTracingMaterial.SetInt("NumRaysPerPixel", NumRaysPerPixel);
    }

    
    
    void CreateMeshes()
    {
        RayTracedMesh[] meshObjects = FindObjectsByType<RayTracedMesh>();
        List<RayTracedMesh.Triangle> tris =  new List<RayTracedMesh.Triangle>();
        MeshInfo[] meshes = new MeshInfo[meshObjects.Length];

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
            tris.AddRange(meshObjects[i].triangles);
        }

        var tr = tris.ToArray();
        
        if (triangles == null)
            triangles = new GraphicsBuffer(GraphicsBuffer.Target.Structured, tr.Length, 72);
        
        if (allMeshInfo == null)
            allMeshInfo =  new GraphicsBuffer(GraphicsBuffer.Target.Structured, meshes.Length, 92);
        
        triangles.SetData(tr);
        allMeshInfo.SetData(meshes);
        
        rayTracingMaterial.SetInt("NumMeshes", meshes.Length);
        rayTracingMaterial.SetBuffer("Triangles", triangles);
        rayTracingMaterial.SetBuffer("AllMeshInfo", allMeshInfo);
    }
    
    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        return;
    }
    
    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= UpdateCameraParams;
        
        sphereBuffer?.Dispose();
        sphereBuffer = null;
        
        triangles?.Dispose();
        triangles = null;
        
        allMeshInfo?.Dispose();
        allMeshInfo = null;
    }
}
