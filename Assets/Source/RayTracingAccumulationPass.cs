using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public sealed class RayTracingAccumulationPass : ScriptableRenderPass, IDisposable
{
    private sealed class History
    {
        public RTHandle previous;
        public RTHandle next;
        public int frameCount;

        public Matrix4x4 cameraMatrix;
        public Matrix4x4 projectionMatrix;
        public Vector4 settings;
        public Material material;
        public int passIndex;
        
        public Color skyColorHorizon;
        public Color skyColorZenith;
        public Color groundColor;

        public readonly MaterialPropertyBlock properties = new();

        public void Release()
        {
            previous?.Release();
            next?.Release();
        }
    }

    private sealed class CopyPassData
    {
        public TextureHandle source;
        public TextureHandle destination;
    }

    private readonly Dictionary<Camera, History> histories = new();

    private Material material;
    private int materialPassIndex;

    public void Setup(Material tracingMaterial, int passIndex)
    {
        material = tracingMaterial;
        materialPassIndex = passIndex;
    }

    public void ResetHistory()
    {
        foreach (var history in histories.Values)
            history.frameCount = 0;
    }
    
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var cameraData = frameData.Get<UniversalCameraData>();
        var resources = frameData.Get<UniversalResourceData>();
        var camera = cameraData.camera;

        if (!histories.TryGetValue(camera, out var history))
        {
            history = new History();
            histories.Add(camera, history);
        }

        var descriptor = cameraData.cameraTargetDescriptor;
        descriptor.graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat;
        descriptor.depthStencilFormat = GraphicsFormat.None;
        descriptor.depthBufferBits = 0;
        descriptor.msaaSamples = 1;
        descriptor.bindMS = false;
        descriptor.sRGB = false;
        descriptor.useMipMap = false;
        descriptor.autoGenerateMips = false;
        descriptor.enableRandomWrite = false;
        descriptor.useDynamicScale = false;
        descriptor.memoryless = RenderTextureMemoryless.None;

        bool changed = RenderingUtils.ReAllocateHandleIfNeeded(
            ref history.previous, descriptor,
            FilterMode.Point, TextureWrapMode.Clamp,
            name: "RT History");

        changed |= RenderingUtils.ReAllocateHandleIfNeeded(
            ref history.next, descriptor,
            FilterMode.Point, TextureWrapMode.Clamp,
            name: "RT History");

        var cameraMatrix = camera.transform.localToWorldMatrix;
        var projectionMatrix = camera.projectionMatrix;

        changed |= !history.cameraMatrix.Equals(cameraMatrix);
        changed |= !history.projectionMatrix.Equals(projectionMatrix);
        changed |= history.material != material;
        changed |= history.passIndex != materialPassIndex;

        history.cameraMatrix = cameraMatrix;
        history.projectionMatrix = projectionMatrix;
        history.material = material;
        history.passIndex = materialPassIndex;

        int raysPerPixel = material.GetInt("NumRaysPerPixel");
        int bounceCount = material.GetInt("MaxBounceCount");
        float sunIntensity = material.GetFloat("SunIntensity");
        float sunFocus = material.GetFloat("SunFocus");

        var settings = new Vector4(raysPerPixel, bounceCount, sunIntensity, sunFocus);

        changed |= !history.settings.Equals(settings);
        history.settings = settings;

        var properties = history.properties;

        properties.SetMatrix("CamLocalToWorld", cameraMatrix);
        properties.SetInt("NumRaysPerPixel", raysPerPixel);
        properties.SetInt("MaxBounceCount", bounceCount);
        properties.SetFloat("SunIntensity", sunIntensity);
        properties.SetFloat("SunFocus", sunFocus);

        changed |= CopyVector(properties, "ViewParams");
        changed |= CopyVector(properties, "SunLightDirection");
        changed |= CopyColor(properties, "SkyColorHorizon", ref history.skyColorHorizon);
        changed |= CopyColor(properties, "SkyColorZenith", ref history.skyColorZenith);
        changed |= CopyColor(properties, "GroundColor", ref history.groundColor);

        if (changed)
            history.frameCount = 0;

        properties.SetInt("AccumulatedFrames", history.frameCount);
        properties.SetInt("FrameIndex", history.frameCount);

        var previous = renderGraph.ImportTexture(history.previous);
        var next = renderGraph.ImportTexture(history.next);

        var parameters = new RenderGraphUtils.BlitMaterialParameters(previous, next, material, materialPassIndex)
        {
            propertyBlock = properties
        };

        renderGraph.AddBlitPass(parameters, passName: "RT Accumulation");

        using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("RT Display", out var passData))
        {
            passData.source = next;
            passData.destination = resources.activeColorTexture;

            builder.UseTexture(passData.source, AccessFlags.Read);
            builder.SetRenderAttachment(passData.destination, 0, AccessFlags.Write);

            builder.SetRenderFunc(
                static (CopyPassData data, RasterGraphContext context) =>
                {
                    bool flipY =
                        context.GetTextureUVOrigin(data.source) !=
                        context.GetTextureUVOrigin(data.destination);

                    var scaleBias = flipY
                        ? new Vector4(1, -1, 0, 1)
                        : new Vector4(1, 1, 0, 0);

                    Blitter.BlitTexture(
                        context.cmd, data.source, scaleBias, 0, false);
                });
        }

        (history.previous, history.next) =
            (history.next, history.previous);

        history.frameCount++;
    }

    private bool CopyVector(MaterialPropertyBlock properties, string name)
    {
        var value = material.GetVector(name);
        bool changed = !properties.GetVector(name).Equals(value);
        properties.SetVector(name, value);
        return changed;
    }

    private bool CopyColor(MaterialPropertyBlock properties, string name, ref Color previousColor)
    {
        var value = material.GetColor(name);
        bool changed = !previousColor.Equals(value);

        previousColor = value;
        properties.SetColor(name, value);

        return changed;
    }
    
    public void Dispose()
    {
        foreach (var history in histories.Values)
            history.Release();

        histories.Clear();
    }
}