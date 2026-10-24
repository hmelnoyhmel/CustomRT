#ifndef RAY_TRACING_GEOMETRY_INCLUDED
#define RAY_TRACING_GEOMETRY_INCLUDED

#include "RayTracingTypes.hlsl"

StructuredBuffer<Triangle> Triangles;
StructuredBuffer<MeshInfo> AllMeshInfo;
StructuredBuffer<BVHNode> BVHNodes;
int NumMeshes;

// Möller–Trumbore algorithm
HitInfo RayTriangle(Ray ray, Triangle tri, float maxDistance)
{
    HitInfo hitInfo = (HitInfo)0;
    
    float3 edgeAB = tri.posB - tri.posA;
    float3 edgeAC = tri.posC - tri.posA;
    float3 edgeCross = cross(edgeAB, edgeAC);
    float determinant = -dot(ray.dir, edgeCross);
    if (determinant <= 0) return hitInfo;
    
    /*float determinantScaleSquared = dot(ray.dir, ray.dir) * dot(edgeCross, edgeCross);

    if (determinant * determinant <= 1E-12 * determinantScaleSquared)
        return hitInfo;*/
    
    float3 originOffset = ray.origin - tri.posA;
    float invDeterminant = 1 / determinant;
    float distance = dot(originOffset, edgeCross) * invDeterminant;
    if (distance < 0) return hitInfo;
    if (distance >= maxDistance) return hitInfo;
    
    float3 originOffsetCrossDirection = cross(originOffset, ray.dir);
    float u = dot(edgeAC, originOffsetCrossDirection) * invDeterminant;
    if (u < 0) return hitInfo;
    
    float v = -dot(edgeAB, originOffsetCrossDirection) * invDeterminant;
    if (v < 0) return hitInfo;
    
    float w = 1 - u - v;
    if (w < 0) return hitInfo;
    
    hitInfo.didHit = true;
    hitInfo.hitPoint = ray.origin + ray.dir * distance;
    hitInfo.normal = normalize(tri.normalA * w + tri.normalB * u + tri.normalC * v);
    hitInfo.distance = distance;
    return hitInfo;
}

bool RayBoundingBox(Ray ray, float3 boxMin, float3 boxMax, float3 inverseRayDirection, float maxDistance, out float entryDistance)
{
    float3 tMin = (boxMin - ray.origin) * inverseRayDirection;
    float3 tMax = (boxMax - ray.origin) * inverseRayDirection;
    float3 t1 = min(tMin, tMax);
    float3 t2 = max(tMin, tMax);
    float tNear = max(max(t1.x, t1.y), t1.z);
    float tFar = min(min(t2.x, t2.y), t2.z);
    
    entryDistance = max(tNear, 0.0);
    
    // check for tFar >= 0 so ray doesn't consider objects behind camera
    return tNear < maxDistance && tNear <= tFar && tFar >= 0; 
}

Ray TransformRayToLocal(Ray ray, MeshInfo meshInfo)
{
    // mirrored transforms need a separate back-face culling check.
    Ray localRay;
    localRay.origin = mul(
        meshInfo.worldToLocal, float4(ray.origin, 1.0)).xyz;

    localRay.dir = mul(
        meshInfo.worldToLocal, float4(ray.dir, 0.0)).xyz;

    return localRay;
}

HitInfo TransformHitToWorld(HitInfo hitInfo, MeshInfo meshInfo)
{
    hitInfo.hitPoint = mul(
        meshInfo.localToWorld, float4(hitInfo.hitPoint, 1.0)).xyz;

    hitInfo.normal = normalize(mul(
        transpose((float3x3)meshInfo.worldToLocal),
        hitInfo.normal));

    return hitInfo;
}

HitInfo CalculateRayCollisionFullTraversal(Ray ray)
{
    HitInfo closestHit = (HitInfo)0;
    closestHit.distance = 1.#INF; // if ray didn't hit, the distance is inf

    
    float meshEntryDistance;
    
    for (int meshIndex = 0; meshIndex < NumMeshes; meshIndex++)
    {
        MeshInfo meshInfo = AllMeshInfo[meshIndex];
        
        Ray localRay = TransformRayToLocal(ray, meshInfo);
        float3 inverseRayDirection = 1.0 / localRay.dir;
        
        if (!RayBoundingBox(localRay, meshInfo.boundsMin, meshInfo.boundsMax, 
            inverseRayDirection, closestHit.distance, meshEntryDistance))
            continue;

        for (int i = 0; i < meshInfo.numTriangles; i++)
        {
            int triIndex = meshInfo.firstTriangleIndex + i;
            Triangle tri = Triangles[triIndex];
            HitInfo hitInfo = RayTriangle(localRay, tri, closestHit.distance);

            if (hitInfo.didHit && hitInfo.distance < closestHit.distance)
            {
                closestHit = TransformHitToWorld(hitInfo, meshInfo);
                closestHit.material = meshInfo.material;
            }
        }
    }
    return closestHit;
}

void IntersectLeafTriangles(Ray ray, MeshInfo meshInfo, BVHNode node, inout HitInfo closestHit)
{
    uint firstTriangleIndex = meshInfo.firstTriangleIndex + node.startIndex;

    [loop]
    for (uint i = 0; i < node.triangleCount; i++)
    {
        Triangle tri = Triangles[firstTriangleIndex + i];
        HitInfo hitInfo = RayTriangle(ray, tri, closestHit.distance);

        if (hitInfo.didHit && hitInfo.distance < closestHit.distance)
        {
            closestHit = hitInfo;
            closestHit.material = meshInfo.material;
        }
    }
}

void TraverseMeshBVH(
    Ray ray,
    MeshInfo meshInfo,
    float3 inverseRayDirection,
    inout HitInfo closestHit)
{
    if (meshInfo.numTriangles == 0) return;
        
    uint rootIndex = meshInfo.firstNodeIndex;
    BVHNode root = BVHNodes[rootIndex];

    float rootEntryDistance;

    if (!RayBoundingBox(ray, root.boundsMin,root.boundsMax, inverseRayDirection,closestHit.distance, rootEntryDistance))
        return;
    
    // supports maxDepth <= 31;
    // increase stack capacity if the depth limit grows.
    uint stack[32];
    float stackEntryDistances[32];
    uint stackCount = 1;
    stack[0] = rootIndex;
    stackEntryDistances[0] = rootEntryDistance;
    
    [loop]
    while (stackCount > 0)
    {
        stackCount--;
        
        uint nodeIndex = stack[stackCount];
        float entryDistance = stackEntryDistances[stackCount];
        if (entryDistance >= closestHit.distance) continue;
        
        BVHNode node = BVHNodes[nodeIndex];

        if (node.triangleCount == 0)
        {
            uint leftIndex = meshInfo.firstNodeIndex + node.startIndex;
            uint rightIndex = leftIndex + 1;

            BVHNode leftNode = BVHNodes[leftIndex];
            BVHNode rightNode = BVHNodes[rightIndex];

            float leftEntryDistance;
            float rightEntryDistance;

            bool hitLeft = RayBoundingBox(
                ray,
                leftNode.boundsMin,
                leftNode.boundsMax,
                inverseRayDirection,
                closestHit.distance,
                leftEntryDistance);

            bool hitRight = RayBoundingBox(
                ray,
                rightNode.boundsMin,
                rightNode.boundsMax,
                inverseRayDirection,
                closestHit.distance,
                rightEntryDistance);

            if (hitLeft && hitRight)
            {
                bool leftIsNear = leftEntryDistance <= rightEntryDistance;

                uint nearIndex = leftIsNear ? leftIndex : rightIndex;
                uint farIndex = leftIsNear ? rightIndex : leftIndex;

                stack[stackCount] = farIndex;
                stackEntryDistances[stackCount] = leftIsNear ? rightEntryDistance : leftEntryDistance;
                stackCount++;

                stack[stackCount] = nearIndex;
                stackEntryDistances[stackCount] = leftIsNear ? leftEntryDistance : rightEntryDistance;
                stackCount++;
            }
            else
            {
                if (hitLeft)
                {
                    stack[stackCount] = leftIndex;
                    stackEntryDistances[stackCount] = leftEntryDistance;
                    stackCount++;
                }

                if (hitRight)
                {
                    stack[stackCount] = rightIndex;
                    stackEntryDistances[stackCount] = rightEntryDistance;
                    stackCount++;
                }
            }

            continue;
        }

        IntersectLeafTriangles(ray, meshInfo, node, closestHit);
    }
}

HitInfo CalculateRayCollision(Ray ray)
{
    HitInfo closestHit = (HitInfo)0;
    closestHit.distance = 1.#INF;

    for (int meshIndex = 0; meshIndex < NumMeshes; meshIndex++)
    {
        MeshInfo meshInfo = AllMeshInfo[meshIndex];
        if (meshInfo.numTriangles == 0) continue;
        
        Ray localRay = TransformRayToLocal(ray, meshInfo);
        float3 inverseRayDirection = 1.0 / localRay.dir;
        
        HitInfo meshHit = (HitInfo)0;
        meshHit.distance = closestHit.distance;
        
        TraverseMeshBVH(localRay, meshInfo, inverseRayDirection, meshHit);
        
        if (!meshHit.didHit) continue;
        
        closestHit = TransformHitToWorld(meshHit, meshInfo);
    }

    return closestHit;
}

#endif
