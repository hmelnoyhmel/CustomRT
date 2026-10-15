Shader "Custom/RT Shader"
{   
    SubShader
    {
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        
        #define USE_FULL_PRECISION_BLIT_TEXTURE
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        
        #include "RayTracingTypes.hlsl"
        #include "RayTracingGeometry.hlsl"
        #include "RayTracingSampling.hlsl"
        ENDHLSL

        Tags { "RenderType"="Opaque" }
        LOD 100
        ZWrite Off Cull Off ZTest Always
        
        Pass
        {
            Name "RT Shader"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            
            float4 ViewParams;
            float4x4 CamLocalToWorld;
            
            int AccumulatedFrames;
			uint FrameIndex;
            
            int MaxBounceCount;
            int NumRaysPerPixel;
            
            float3 SunLightDirection;
            float SunIntensity;
            float SunFocus;
            
            float3 SkyColorHorizon;
            float3 SkyColorZenith;
            float3 GroundColor;
            
            float3 GetEnvironmentLight(Ray ray)
			{				
				float skyGradientT = pow(smoothstep(0, 0.4, ray.dir.y), 0.35);
				float3 skyGradient = lerp(SkyColorHorizon, SkyColorZenith, skyGradientT);
				float sun = pow(max(0, dot(ray.dir, -SunLightDirection)), SunFocus) * SunIntensity;
				
				float groundToSkyT = smoothstep(-0.01, 0, ray.dir.y);
				float sunMask = groundToSkyT >= 1;
				return lerp(GroundColor, skyGradient, groundToSkyT) + sun * sunMask;
			}
            
            void ProcessHit(HitInfo hitInfo, inout Ray ray, inout float3 rayColor, inout float3 incomingLight, inout uint rngState)
            {
            	// ProcessHit also "refreshes" ray data, and thus generates a new ray (bounce)
            	
            	RayTracingMaterial material = hitInfo.material;
            	
            	ray.origin = hitInfo.hitPoint + hitInfo.normal * 0.0001;
            	float3 diffuseDirection = normalize(hitInfo.normal + RandomDirection(rngState));
            	float3 specularDirection = reflect(ray.dir, hitInfo.normal);
            	bool isSpecularBounce = material.specularProbability >= RandomValue(rngState);
            	ray.dir = normalize(lerp(diffuseDirection, specularDirection, material.smoothness * isSpecularBounce));
						
            	float3 emittedLight = material.emissionColor * material.emissionStrength;
            	incomingLight += emittedLight * rayColor;
            	rayColor *= lerp(material.color, material.specularColor, isSpecularBounce);
            }
            
            float3 Trace(Ray ray, HitInfo firstHit, inout uint rngState)
			{
				float3 incomingLight = 0;
				float3 rayColor = 1;
            	
            	// first hit is always the same, so we can process it outside of the loop
				ProcessHit(firstHit, ray, rayColor, incomingLight, rngState);
            	
				for (int i = 1; i <= MaxBounceCount; i++)
				{
					HitInfo hitInfo = CalculateRayCollision(ray);
					
					if (hitInfo.didHit)
					{
						ProcessHit(hitInfo, ray, rayColor, incomingLight, rngState);
						continue;
					}
					incomingLight += GetEnvironmentLight(ray) * rayColor;
					break;
				}
				
				return incomingLight;
			}
            
            float4 ApplyAccumulation(float3 pixelColor, float2 uv)
			{
				if (AccumulatedFrames == 0)
					return float4(pixelColor, 1);
            	
				float3 historyColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).rgb;
            	
				float weight = 1.0 / (AccumulatedFrames + 1.0);
				float3 accumulatedColor = lerp(historyColor, pixelColor, weight);
            	
				return float4(accumulatedColor, 1);
			}
            
            float4 Frag (Varyings input) : SV_Target
            {
            	uint2 numPixels = _ScreenParams.xy;
            	uint2 pixelCoord = input.texcoord * numPixels;
            	uint pixelIndex = pixelCoord.y * numPixels.x + pixelCoord.x;
            	uint rngState = pixelIndex + FrameIndex * 747796405u;
            	
                // calculate current pixel on the cam projection plane
                float3 viewPointLocal = float3(input.texcoord - 0.5, 1) * ViewParams;
                float3 viewPoint = mul(CamLocalToWorld, float4(viewPointLocal, 1));
                
                Ray ray;
                ray.origin = _WorldSpaceCameraPos;
                ray.dir = normalize(viewPoint - ray.origin);
            	
            	HitInfo firstHit = CalculateRayCollision(ray);
            	if (!firstHit.didHit) 
            		return ApplyAccumulation(GetEnvironmentLight(ray), input.texcoord);
            	
            	float3 totalIncomingLight = 0;
            	for (int rayIndex = 0; rayIndex < NumRaysPerPixel; rayIndex++)
            	{
            		totalIncomingLight += Trace(ray, firstHit, rngState);
            	}
            	
                float3 pixelColor = totalIncomingLight / NumRaysPerPixel;
                return ApplyAccumulation(pixelColor, input.texcoord);
            }
            
            ENDHLSL
        }
    }
}
