using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BVHDebug
{
    private const string MenuPath = "Tools/Ray Tracing/Toggle BVH Debug View";
    private const string StatsMenuPath = "Tools/Ray Tracing/Toggle BVH Stats";
    private static bool drawAll = true;
    
    private static GUIStyle statsStyle;
    
    static BVHDebug()
    {
        SceneView.duringSceneGui += DrawStats;
    }
    
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawBVH(RayTracedMesh targetMesh, GizmoType gizmoType)
    {
        if (!drawAll && (gizmoType & GizmoType.Selected) == 0) return;
        if (targetMesh == null || targetMesh.meshFilter == null) return;
        
        var bvh = targetMesh.Bvh;
        if (bvh == null || bvh.Tree.Count == 0) return;

        Gizmos.matrix = targetMesh.meshFilter.transform.localToWorldMatrix;
        for (int i = 0; i < bvh.Tree.Count; i++)
        {
            var node = bvh.Tree[i];
            if (node.triangleCount <= 0) continue;

            var center = (node.boundsMin + node.boundsMax) * 0.5f;
            var size = node.boundsMax - node.boundsMin;

            Gizmos.color = Color.greenYellow;
            Gizmos.DrawWireCube(center, size);
        }
    }
    
    [MenuItem(MenuPath)]
    private static void ToggleDrawAll()
    {
        drawAll = !drawAll;
        SceneView.RepaintAll();
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateDrawAll()
    {
        Menu.SetChecked(MenuPath, drawAll);
        return true;
    }
    
    private static void DrawStats(SceneView sceneView)
    {
        if (!RayTracedMesh.CollectStats) return;
        if (Event.current.type != EventType.Repaint) return;
        
        var selectedObject = Selection.activeGameObject;
        if (selectedObject == null) return;

        if (!selectedObject.TryGetComponent<RayTracedMesh>(out var targetMesh))
            return;

        var text = $"BVH: {targetMesh.name}\n";

        if (targetMesh.Stats.HasValue)
        {
            var stats = targetMesh.Stats.Value;

            text += $"Build time: {stats.timeMs:F3} ms\n";
            text += $"Triangles: {stats.triangleCount}\n";
            text += $"Nodes: {stats.nodeCount}\n";
            text += $"Leaves: {stats.leafCount}";
        }
        else
        {
            text += "Statistics not collected";
        }

        Handles.BeginGUI();

        if (statsStyle == null)
        {
            statsStyle = new GUIStyle(EditorStyles.helpBox);
            statsStyle.normal.background = null;
            statsStyle.normal.textColor = Color.green;
        }

        var panelRect = new Rect(12, 12, 120, 100);

        EditorGUI.DrawRect(panelRect, new Color(0.15f, 0.15f, 0.15f, 1f));
        GUI.Label(panelRect, text, statsStyle);

        Handles.EndGUI();
    }
    
    [MenuItem(StatsMenuPath)]
    private static void ToggleCollectStats()
    {
        RayTracedMesh.CollectStats = !RayTracedMesh.CollectStats;

        if (RayTracedMesh.CollectStats)
        {
            foreach (var mesh in Object.FindObjectsByType<RayTracedMesh>())
            {
                if (mesh.Stats.HasValue) continue;

                mesh.SetupGeometry();
            }
        }

        SceneView.RepaintAll();
    }

    [MenuItem(StatsMenuPath, true)]
    private static bool ValidateCollectStats()
    {
        Menu.SetChecked(StatsMenuPath, RayTracedMesh.CollectStats);
        return true;
    }
}
