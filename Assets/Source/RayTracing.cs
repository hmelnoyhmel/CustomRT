using Source;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class RayTracingRendererFeature : FullScreenPassRendererFeature
{
    [SerializeField] private bool useShaderInSceneView;
    [SerializeField] private int MaxBounceCount = 4;
    [SerializeField] private int NumRaysPerPixel = 1;
        
    [SerializeField][Min(1)] private float sunFocus;
    
    [SerializeField] private Color SkyColorHorizon = Color.blue;
    [SerializeField] private Color SkyColorZenith = Color.blue;
    [SerializeField] private Color GroundColor = Color.gray;
    
    private RayTracingScene scene;
    
    
    void UpdateSettings(Camera cam)
    {
        float planeHeight = cam.nearClipPlane * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2;
        float planeWidth = planeHeight * cam.aspect;

        passMaterial.SetVector("ViewParams", new Vector4(planeWidth, planeHeight, cam.nearClipPlane));
        passMaterial.SetMatrix("CamLocalToWorld", cam.transform.localToWorldMatrix);
        
        passMaterial.SetInt("MaxBounceCount", MaxBounceCount);
        passMaterial.SetInt("NumRaysPerPixel", NumRaysPerPixel);
    }

    public override void Create()
    {
        scene?.Dispose();
        scene = new RayTracingScene();
        base.Create();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (passMaterial == null) return;
        
        var cam = renderingData.cameraData.camera;
        if (cam.cameraType != CameraType.Game)
        {
            if (cam.cameraType != CameraType.SceneView) return;
            if (!useShaderInSceneView) return;
        }
        
        
        UpdateSettings(cam);
        scene.BuildSceneData();
        scene.BindSceneData(passMaterial);
        SetupSunAndSky();
        
        base.AddRenderPasses(renderer, ref renderingData);
    }
    
    void SetupSunAndSky()
    {
        passMaterial.SetColor("SkyColorHorizon", SkyColorHorizon);
        passMaterial.SetColor("SkyColorZenith", SkyColorZenith);
        passMaterial.SetColor("GroundColor", GroundColor);
        passMaterial.SetFloat("SunFocus", sunFocus);
        
        var directionalLight = RenderSettings.sun;
        if (directionalLight == null)
        {
            passMaterial.SetFloat("SunIntensity", 0f);
            return;
        }
        
        passMaterial.SetVector("SunLightDirection",  directionalLight.transform.forward);
        passMaterial.SetFloat("SunIntensity", directionalLight.intensity);
    }

    [ContextMenu("Rebuild Scene")]
    public void MarkSceneAsDirty()
    {
        scene?.MarkAsDirty();
    }

    protected override void Dispose(bool disposing)
    {
        scene?.Dispose();
        scene = null;
        base.Dispose(disposing);
    }
}
