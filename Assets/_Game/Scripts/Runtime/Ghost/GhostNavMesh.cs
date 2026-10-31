using System.Collections.Generic;
using SpiritMovers.Interaction;
using UnityEngine;
using UnityEngine.AI;

namespace SpiritMovers.Ghost
{
    /// <summary>
    /// Host: a walkable map for the ghost, built at run time from the house's fixed colliders (walls, floors, stairs) —
    /// no baked asset and no project-wide agent settings. Items and players are left out: the ghost drifts through them.
    /// </summary>
    internal static class GhostNavMesh
    {
        private static readonly Bounds s_Area = new(new Vector3(0f, 2f, 0f), new Vector3(40f, 10f, 40f));

        private static NavMeshDataInstance s_Instance;
        private static int s_Users;

        public static void Acquire()
        {
            if (s_Users++ > 0)
                return;

            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(s_Area, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(s => s.component is not Collider c || c.isTrigger || c is CharacterController ||
                                   c.GetComponentInParent<Carryable>() != null);

            // A small floating agent: fits the loft and climbs the 0.2 m steps.
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.3f;
            settings.agentHeight = 1.0f;
            settings.agentClimb = 0.3f;
            settings.agentSlope = 45f;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, s_Area, Vector3.zero, Quaternion.identity);
            s_Instance = NavMesh.AddNavMeshData(data);
        }

        public static void Release()
        {
            if (s_Users == 0 || --s_Users > 0)
                return;
            s_Instance.Remove();
        }
    }
}
