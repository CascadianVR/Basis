using JigglePhysics;
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using static JiggleRigConstruction;
public class JiggleRigBase
{
    public InitalizationData PreInitalData = new InitalizationData();
    public RuntimeData Runtimedata = new RuntimeData();
    public JiggleBone[] JiggleBones;
    public Transform[] ComputedTransforms;
    public int simulatedPointsCount;
    public void MatchAnimationInstantly(int JiggleBoneIndex, double time)
    {
        Vector3 position = GetTransformPosition(JiggleBoneIndex);
        var outputA = Runtimedata.targetAnimatedBoneSignal[JiggleBoneIndex];
        var outputB = Runtimedata.particleSignal[JiggleBoneIndex];
        PositionSignalHelper.FlattenSignal(ref outputA, time, position);
        PositionSignalHelper.FlattenSignal(ref outputB, time, position);

        Runtimedata.targetAnimatedBoneSignal[JiggleBoneIndex] = outputA;
        Runtimedata.particleSignal[JiggleBoneIndex] = outputB;
    }
    /// <summary>
    /// Computes the projected position of a JiggleBone based on its parent JiggleBone.
    /// </summary>
    /// <param name="JiggleBone">Index of the JiggleBone.</param>
    /// <param name="JiggleParent">Index of the JiggleParent.</param>
    /// <returns>The projected position as a Vector3.</returns>
    public Vector3 GetProjectedPosition(int JiggleBone, int JiggleParent)
    {
        Transform parentTransform;

        // Get the parent transform
        if (JiggleBones[JiggleBone].JiggleParentIndex != -1)
        {
            int ParentIndex = JiggleBones[JiggleBone].JiggleParentIndex;
            parentTransform = ComputedTransforms[ParentIndex].transform;
        }
        else
        {
            parentTransform = ComputedTransforms[JiggleBone].parent;
        }

        // Compute and return the projected position
        return ComputedTransforms[JiggleParent].TransformPoint(parentTransform.InverseTransformPoint(ComputedTransforms[JiggleParent].position));
    }
    public Vector3 GetTransformPosition(int BoneIndex)
    {
        if (!Runtimedata.hasTransform[BoneIndex])
        {
            return GetProjectedPosition(BoneIndex, JiggleBones[BoneIndex].JiggleParentIndex);
        }
        else
        {
            return ComputedTransforms[BoneIndex].position;
        }
    }
    public float GetLengthToParent(int BoneIndex)
    {
        int ParentIndex = JiggleBones[BoneIndex].JiggleParentIndex;
        return Vector3.Distance(Runtimedata.currentFixedAnimatedBonePosition[BoneIndex], Runtimedata.currentFixedAnimatedBonePosition[ParentIndex]);
    }
    /// <summary>
    /// Physically accurate teleportation, maintains the existing signals of motion and keeps their trajectories through a teleport. First call PrepareTeleport(), then move the character, then call FinishTeleport().
    /// Use MatchAnimationInstantly() instead if you don't want jiggles to be maintained through a teleport.
    /// </summary>
    public void PrepareTeleport(int JiggleBone)
    {
        Runtimedata.preTeleportPosition[JiggleBone] = GetTransformPosition(JiggleBone);
    }
    public void PrepareTeleport()
    {
        for (int PointsIndex = 0; PointsIndex < simulatedPointsCount; PointsIndex++)
        {
            PrepareTeleport(PointsIndex);
        }
    }
    /// <summary>
    /// The companion function to PrepareTeleport, it discards all the movement that has happened since the call to PrepareTeleport, assuming that they've both been called on the same frame.
    /// </summary>
    public void FinishTeleport(double timeAsDouble)
    {
        for (int PointsIndex = 0; PointsIndex < simulatedPointsCount; PointsIndex++)
        {
            Vector3 position = GetTransformPosition(PointsIndex);
            Vector3 diff = position - Runtimedata.preTeleportPosition[PointsIndex];
            var outputA = Runtimedata.targetAnimatedBoneSignal[PointsIndex];
            var outputB = Runtimedata.particleSignal[PointsIndex];
            PositionSignalHelper.FlattenSignal(ref outputA, timeAsDouble, position);
            PositionSignalHelper.OffsetSignal(ref outputB, diff);
            Runtimedata.targetAnimatedBoneSignal[PointsIndex] = outputA;
            Runtimedata.particleSignal[PointsIndex] = outputB;
            Runtimedata.workingPosition[PointsIndex] += diff;
        }
    }
}