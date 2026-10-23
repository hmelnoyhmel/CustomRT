using System.Collections.Generic;
using UnityEngine;

public class BVH
{
    public struct Node
    {
        public Vector3 boundsMin, boundsMax;
        public int startIndex;
        public int triangleCount;
    }

    private List<Node> tree = new List<Node>();
    public IReadOnlyList<Node> Tree => tree;
    
    private struct TriangleBuildData
    {
        public Vector3 boundsMin;
        public Vector3 boundsMax;
        public Vector3 center;
    }
    
    public BVH(RayTracedMesh.Triangle[] triangles, int maxDepth = 12)
    {
        if (triangles == null || triangles.Length == 0) return;
        
        var buildData = new TriangleBuildData[triangles.Length];
        
        for (int i = 0; i < triangles.Length; i++)
        {
            buildData[i] = new TriangleBuildData
            {
                boundsMin = GetTriangleMin(triangles[i]),
                boundsMax = GetTriangleMax(triangles[i]),
                center = GetTriangleCenter(triangles[i])
            };
        }
        
        tree.Add(CreateLeaf(buildData, 0, triangles.Length));
        SplitNode(triangles, buildData, 0, 0, maxDepth);
    }

    private static Node CreateLeaf(TriangleBuildData[] buildData, int startIndex, int triangleCount)
    {
        var leaf = new Node()
        {
            startIndex = startIndex,
            triangleCount = triangleCount,
            boundsMin = buildData[startIndex].boundsMin,
            boundsMax = buildData[startIndex].boundsMax
        };
        
        var endIndex = startIndex + triangleCount;

        for (int i = startIndex; i < endIndex; i++)
        {
            leaf.boundsMin = Vector3.Min(leaf.boundsMin, buildData[i].boundsMin);
            leaf.boundsMax = Vector3.Max(leaf.boundsMax, buildData[i].boundsMax);
        }

        return leaf;
    }
    
    // request description from codex
    private void SplitNode(RayTracedMesh.Triangle[] triangles, TriangleBuildData[] buildData, int nodeIndex, int depth, int maxDepth)
    {
        var node = tree[nodeIndex];
        if (node.triangleCount <= 1) return;
        if (depth >= maxDepth) return;

        if (!TryFindBestSplit(buildData, node, out var axis, out var splitPosition)) return;

        int leftCount = 0;
        int endIndex = node.startIndex + node.triangleCount;

        for (int i = node.startIndex; i < endIndex; i++)
        {
            var center = buildData[i].center;

            if (center[axis] >= splitPosition) continue;

            int swapIndex = node.startIndex + leftCount;
            (triangles[swapIndex], triangles[i]) = (triangles[i], triangles[swapIndex]);
            (buildData[swapIndex], buildData[i]) = (buildData[i], buildData[swapIndex]);
            leftCount++;
        }

        int rightCount = node.triangleCount - leftCount;

        if (leftCount == 0 || rightCount == 0) return;

        int firstChildIndex = tree.Count;

        tree.Add(CreateLeaf(buildData, node.startIndex, leftCount));
        tree.Add(CreateLeaf(buildData, node.startIndex + leftCount, rightCount));

        node.startIndex = firstChildIndex;
        node.triangleCount = 0;

        tree[nodeIndex] = node;
        SplitNode(triangles, buildData, firstChildIndex, depth + 1, maxDepth);
        SplitNode(triangles, buildData, firstChildIndex + 1, depth + 1, maxDepth);
    }

    private static Vector3 GetTriangleMin(RayTracedMesh.Triangle triangle)
    {
        var min = Vector3.Min(triangle.posA, triangle.posB);
        min = Vector3.Min(min, triangle.posC);
        return min;
    }
    
    private static Vector3 GetTriangleMax(RayTracedMesh.Triangle triangle)
    {
        var max = Vector3.Max(triangle.posA, triangle.posB);
        max = Vector3.Max(max, triangle.posC);
        return max;
    }
    
    private static Vector3 GetTriangleCenter(RayTracedMesh.Triangle triangle)
    {
        return (triangle.posA + triangle.posB + triangle.posC) / 3f;
    }
    
    private static float GetSurfaceArea(Vector3 boundsMin, Vector3 boundsMax)
    {
        var size = boundsMax - boundsMin;
        var area = 2f * ((size.x * size.y) + (size.y * size.z) + (size.z * size.x));
        return area;
    }
    
    private static float EvaluateSplitCost(
        TriangleBuildData[] buildData,
        Node node,
        int axis,
        float splitPosition)
    {
        float parentArea = GetSurfaceArea(node.boundsMin, node.boundsMax);
        
        if (parentArea <= 0f) return float.PositiveInfinity;

        var leftMin = Vector3.positiveInfinity;
        var leftMax = Vector3.negativeInfinity;
        var rightMin = Vector3.positiveInfinity;
        var rightMax = Vector3.negativeInfinity;

        int leftCount = 0;
        int rightCount = 0;
        int endIndex = node.startIndex + node.triangleCount;

        for (int i = node.startIndex; i < endIndex; i++)
        {
            var triangleData = buildData[i];

            var center = triangleData.center;
            var triangleMin = triangleData.boundsMin;
            var triangleMax = triangleData.boundsMax;

            if (center[axis] < splitPosition)
            {
                leftCount++;
                leftMin = Vector3.Min(leftMin, triangleMin);
                leftMax = Vector3.Max(leftMax, triangleMax);
            }
            else
            {
                rightCount++;
                rightMin = Vector3.Min(rightMin, triangleMin);
                rightMax = Vector3.Max(rightMax, triangleMax);
            }
        }

        if (leftCount == 0 || rightCount == 0) return float.PositiveInfinity;

        float leftArea = GetSurfaceArea(leftMin, leftMax);
        float rightArea = GetSurfaceArea(rightMin, rightMax);

        float weightedArea = (leftArea * leftCount) + (rightArea * rightCount);

        return 1f + weightedArea / parentArea;
    }
    
    private static bool TryFindBestSplit(
        TriangleBuildData[] buildData,
        Node node,
        out int axis,
        out float splitPosition)
    {
        const int candidateCount = 5;

        axis = -1;
        splitPosition = 0f;

        float bestCost = node.triangleCount;
        var size = node.boundsMax - node.boundsMin;

        for (int candidateAxis = 0; candidateAxis < 3; candidateAxis++)
        {
            if (size[candidateAxis] <= 0f) continue;

            for (int i = 1; i <= candidateCount; i++)
            {
                float fraction = (float)i / (candidateCount + 1);

                float position =
                    node.boundsMin[candidateAxis] +
                    size[candidateAxis] * fraction;

                float cost = EvaluateSplitCost(
                    buildData,
                    node,
                    candidateAxis,
                    position);

                if (cost < bestCost)
                {
                    bestCost = cost;
                    axis = candidateAxis;
                    splitPosition = position;
                }
            }
        }

        return axis >= 0;
    }
}
