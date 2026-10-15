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
    
    public BVH(RayTracedMesh.Triangle[] triangles, int maxDepth = 12)
    {
        if (triangles == null || triangles.Length == 0) return;
        
        tree.Add(CreateLeaf(triangles, 0, triangles.Length));
        SplitNode(triangles, 0, 0, maxDepth);
    }

    private static Node CreateLeaf(RayTracedMesh.Triangle[] triangles, int startIndex, int triangleCount)
    {
        var leaf = new Node()
        {
            startIndex = startIndex,
            triangleCount = triangleCount,
            boundsMin = triangles[startIndex].posA,
            boundsMax = triangles[startIndex].posA
        };
        
        var endIndex = startIndex + triangleCount;

        for (int i = startIndex; i < endIndex; i++)
        {
            leaf.boundsMin = Vector3.Min(leaf.boundsMin, GetTriangleMin(triangles[i]));
            leaf.boundsMax = Vector3.Max(leaf.boundsMax, GetTriangleMax(triangles[i]));
        }

        return leaf;
    }
    
    // request description from codex
    private void SplitNode(RayTracedMesh.Triangle[] triangles, int nodeIndex, int depth, int maxDepth)
    {
        var node = tree[nodeIndex];
        if (node.triangleCount <= 1) return;
        if (depth >= maxDepth) return;

        if (!TryFindBestSplit(triangles, node, out var axis, out var splitPosition)) return;

        int leftCount = 0;
        int endIndex = node.startIndex + node.triangleCount;

        for (int i = node.startIndex; i < endIndex; i++)
        {
            var center = GetTriangleCenter(triangles[i]);

            if (center[axis] >= splitPosition) continue;

            int swapIndex = node.startIndex + leftCount;
            (triangles[swapIndex], triangles[i]) = (triangles[i], triangles[swapIndex]);
            leftCount++;
        }

        int rightCount = node.triangleCount - leftCount;

        if (leftCount == 0 || rightCount == 0) return;

        int firstChildIndex = tree.Count;

        tree.Add(CreateLeaf(triangles, node.startIndex, leftCount));
        tree.Add(CreateLeaf(triangles, node.startIndex + leftCount, rightCount));

        node.startIndex = firstChildIndex;
        node.triangleCount = 0;

        tree[nodeIndex] = node;
        SplitNode(triangles, firstChildIndex, depth + 1, maxDepth);
        SplitNode(triangles, firstChildIndex + 1, depth + 1, maxDepth);
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
        RayTracedMesh.Triangle[] triangles,
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
            var triangle = triangles[i];

            var center = GetTriangleCenter(triangle);
            var triangleMin = GetTriangleMin(triangle);
            var triangleMax = GetTriangleMax(triangle);

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
        RayTracedMesh.Triangle[] triangles,
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
                    triangles,
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
