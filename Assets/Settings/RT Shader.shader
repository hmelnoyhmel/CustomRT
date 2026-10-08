Shader "Custom/RT Shader"
{   
    SubShader
    {
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        ENDHLSL

        Tags { "RenderType"="Opaque" }
        LOD 100
        ZWrite Off Cull Off
        

        
        Pass
        {
            Name "RT Shader"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Frag
            
            struct Ray
            {
                float3 origin;
                float3 dir;
            };
            
            struct RayTracingMaterial
            {
                float4 color;
            	float4 emissionColor;
            	float emissionStrength;
            	float smoothness;
            	float specularProbability;
            	float4 specularColor;
            };
            
            struct HitInfo
            {
                bool didHit;
                float distance;
                float3 hitPoint;
                float3 normal;
                RayTracingMaterial material;
            };
            
            struct Sphere
            {
                float3 position;
                float radius;
                RayTracingMaterial material;
            };
            
            struct Triangle
			{
				float3 posA, posB, posC;
				float3 normalA, normalB, normalC;
			};
            
            struct MeshInfo
            {
	            uint firstTriangleIndex;
            	uint numTriangles;
            	float3 boundsMin;
            	float3 boundsMax;
            	RayTracingMaterial material;
            };
            
            float4 ViewParams;
            float4x4 CamLocalToWorld;
            int RayTracingEnabled;
            
            StructuredBuffer<Sphere> Spheres;
            int NumSpheres;
            
            StructuredBuffer<Triangle> Triangles;
            StructuredBuffer<MeshInfo> AllMeshInfo;
            int NumMeshes;
            
            int MaxBounceCount;
            int NumRaysPerPixel;
            
            float3 SunLightDirection;
            float SunIntensity;
            float SunFocus;
            
            float3 SkyColorHorizon;
            float3 SkyColorZenith;
            float3 GroundColor;
            
            // --- Random Generation ---
            // PCG (permuted congruential generator)
			uint NextRandom(inout uint state)
			{
				state = state * 747796405 + 2891336453;
				uint result = ((state >> ((state >> 28) + 4)) ^ state) * 277803737;
				result = (result >> 22) ^ result;
				return result;
			}

			float RandomValue(inout uint state)
			{
				return NextRandom(state) / 4294967295.0; // 2^32 - 1
			}

			// Random value in normal distribution (with mean=0 and sd=1)
			float RandomValueNormalDistribution(inout uint state)
			{
				float theta = 2 * 3.1415926 * RandomValue(state);
				float rho = sqrt(-2 * log(RandomValue(state)));
				return rho * cos(theta);
			}

			// Calculate a random direction
			float3 RandomDirection(inout uint state)
			{
				float x = RandomValueNormalDistribution(state);
				float y = RandomValueNormalDistribution(state);
				float z = RandomValueNormalDistribution(state);
				return normalize(float3(x, y, z));
			}
            
            float3 RandomHemisphereDirection(float3 normal, inout uint rngState)
			{
			    float3 dir = RandomDirection(rngState);
			    return dir * sign(dot(normal,dir));
			}
            
            HitInfo RaySphere(Ray ray, float3 sphereCentre, float sphereRadius)
            {
                HitInfo hitInfo = (HitInfo)0;
                float3 offsetRayOrigin = ray.origin - sphereCentre;
                // sqrLength(rayOrigin + rayDir * dist) = radius^2
                float a = dot(ray.dir, ray.dir); 
                float b = 2 * dot(offsetRayOrigin, ray.dir);
                float c = dot(offsetRayOrigin, offsetRayOrigin) - sphereRadius * sphereRadius;
                
                float discriminant = b * b - 4 * a * c;
                
                if (discriminant >= 0)
                {
                    float distance = (-b - sqrt(discriminant)) / (2 * a);
                    
                    if (distance >= 0)
                    {
                        hitInfo.didHit = true;
                        hitInfo.distance = distance;
                        hitInfo.hitPoint = ray.origin + ray.dir * distance;
                        hitInfo.normal = normalize(hitInfo.hitPoint - sphereCentre);
                    }
                }
                return hitInfo;
            }
            
            // Moller-Trumbore algorithm
            HitInfo RayTriangle(Ray ray, Triangle tri)
			{
				float3 edgeAB = tri.posB - tri.posA;
				float3 edgeAC = tri.posC - tri.posA;
				float3 normalVector = cross(edgeAB, edgeAC);
				float3 ao = ray.origin - tri.posA;
				float3 dao = cross(ao, ray.dir);
				
				float determinant = -dot(ray.dir, normalVector);
				float invDeterminant = 1 / determinant;
				
				float dst = dot(ao, normalVector) * invDeterminant;
				float u = dot(edgeAC, dao) * invDeterminant;
				float v = -dot(edgeAB, dao) * invDeterminant;
				float w = 1 - u - v;
				
				HitInfo hitInfo;
				hitInfo.didHit = determinant >= 1E-6 && dst >= 0 && u >= 0 && v >= 0 && w >= 0;
				hitInfo.hitPoint = ray.origin + ray.dir * dst;
				hitInfo.normal = normalize(tri.normalA * w + tri.normalB * u + tri.normalC * v);
				hitInfo.distance = dst;
				return hitInfo;
			}
            
            bool RayBoundingBox(Ray ray, float3 boxMin, float3 boxMax)
			{
				float3 invDir = 1 / ray.dir;
				float3 tMin = (boxMin - ray.origin) * invDir;
				float3 tMax = (boxMax - ray.origin) * invDir;
				float3 t1 = min(tMin, tMax);
				float3 t2 = max(tMin, tMax);
				float tNear = max(max(t1.x, t1.y), t1.z);
				float tFar = min(min(t2.x, t2.y), t2.z);
				return tNear <= tFar;
			};
            
            HitInfo CalculateRayCollision(Ray ray)
            {
                HitInfo closestHit = (HitInfo)0;
                closestHit.distance = 1.#INF; // if haven't hit, the distance is inf
                
            	// for spheres
                for (int i = 0; i < NumSpheres; i++)
                {
                    Sphere sphere = Spheres[i];
                    HitInfo hitInfo = RaySphere(ray, sphere.position, sphere.radius); // collision test
                    
                    // update if this is closest hit
                    if (hitInfo.didHit && hitInfo.distance < closestHit.distance)
                    {
                        closestHit = hitInfo;
                        closestHit.material = sphere.material;
                    }
                }
				
            	// for meshes
				for (int meshIndex = 0; meshIndex < NumMeshes; meshIndex++)
				{
					MeshInfo meshInfo = AllMeshInfo[meshIndex];
					if (!RayBoundingBox(ray, meshInfo.boundsMin, meshInfo.boundsMax))
						continue;
					
					for (int i = 0; i < meshInfo.numTriangles; i++)
					{
						int triIndex = meshInfo.firstTriangleIndex + i;
						Triangle tri = Triangles[triIndex];
						HitInfo hitInfo = RayTriangle(ray, tri);
						
						if (hitInfo.didHit && hitInfo.distance < closestHit.distance)
						{
							closestHit = hitInfo;
							closestHit.material = meshInfo.material;
						}
					}
				}
				
                return closestHit;
            }
            
            float3 GetEnvironmentLight(Ray ray)
			{				
				float skyGradientT = pow(smoothstep(0, 0.4, ray.dir.y), 0.35);
				float3 skyGradient = lerp(SkyColorHorizon, SkyColorZenith, skyGradientT);
				float sun = pow(max(0, dot(ray.dir, -SunLightDirection)), SunFocus) * SunIntensity;
				
				float groundToSkyT = smoothstep(-0.01, 0, ray.dir.y);
				float sunMask = groundToSkyT >= 1;
				return lerp(GroundColor, skyGradient, groundToSkyT) + sun * sunMask;
			}
            
            float3 Trace(Ray ray, inout uint rngState)
			{
				float3 incomingLight = 0;
				float3 rayColor = 1;
				
				for (int i = 0; i <= MaxBounceCount; i++)
				{
					HitInfo hitInfo = CalculateRayCollision(ray);
					RayTracingMaterial material = hitInfo.material;
					
					if (hitInfo.didHit)
					{
						ray.origin = hitInfo.hitPoint + hitInfo.normal * 0.0001;
						float3 diffuseDirection = normalize(hitInfo.normal + RandomDirection(rngState));
						float3 specularDirection = reflect(ray.dir, hitInfo.normal);
						bool isSpecularBounce = material.specularProbability >= RandomValue(rngState);
						ray.dir = normalize(lerp(diffuseDirection, specularDirection, material.smoothness * isSpecularBounce));
						
						float3 emittedLight = material.emissionColor * material.emissionStrength;
						incomingLight += emittedLight * rayColor;
						rayColor *= lerp(material.color, material.specularColor, isSpecularBounce);
					}
					else
					{
						incomingLight += GetEnvironmentLight(ray) * rayColor;
						break;
					}
				}
				
				return incomingLight;
			}
            
            float4 Frag (Varyings input) : SV_Target
            {                
                if (RayTracingEnabled == 0)
                    return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord).rgba;
                
            	uint2 numPixels = _ScreenParams.xy;
            	uint2 pixelCoord = input.texcoord * numPixels;
            	uint pixelIndex = pixelCoord.y * numPixels.x + pixelCoord.x;
            	uint rngState = pixelIndex;
            	
                // calculate current pixel on the cam projection plane
                float3 viewPointLocal = float3(input.texcoord - 0.5, 1) * ViewParams;
                float3 viewPoint = mul(CamLocalToWorld, float4(viewPointLocal, 1));
                
                // cast a ray
                Ray ray;
                ray.origin = _WorldSpaceCameraPos;
                ray.dir = normalize(viewPoint - ray.origin);
            	
            	float3 totalIncomingLight = 0;
            	
            	for (int rayIndex = 0; rayIndex < NumRaysPerPixel; rayIndex++)
            	{
            		totalIncomingLight += Trace(ray, rngState);
            	}
            	
                float3 pixelColor = totalIncomingLight / NumRaysPerPixel;
                return float4(pixelColor, 1);
            }
            
            ENDHLSL
        }
    }
}
