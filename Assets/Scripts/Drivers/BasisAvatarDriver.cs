using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Animations.Rigging;
using UnityEngine.Events;

public abstract class BasisAvatarDriver : MonoBehaviour
{
    public float ActiveEyeHeight = 1.75f;
    private static string TPose = "Assets/Animator/Animated TPose.controller";
    public static string BoneData = "Assets/ScriptableObjects/BoneData.asset";
    public UnityEvent BeginningCalibration = new UnityEvent();
    public UnityEvent CalibrationComplete = new UnityEvent();
    public BasisTransformMapping References = new BasisTransformMapping();
    public RuntimeAnimatorController runtimeAnimatorController;
    public SkinnedMeshRenderer[] SkinnedMeshRenderer;
    public BasisPlayer Player;
    public bool InTPose = false;
    public void Calibration(BasisAvatar Avatar)
    {
        BeginningCalibration.Invoke();
        FindSkinnedMeshRenders();
        BasisTransformMapping.AutoDetectReferences(Player.Avatar.Animator, Avatar.transform, out References);
        ActiveEyeHeight = Avatar.AvatarEyePosition.x;
        BasisLocalPlayer.Instance.LocalBoneDriver.Calibrate();
        if (BasisFacialBlinkDriver.MeetsRequirements(Avatar))
        {
            BasisFacialBlinkDriver FacialBlinkDriver = BasisHelpers.GetOrAddComponent<BasisFacialBlinkDriver>(Avatar.gameObject);
            FacialBlinkDriver.Initialize(Avatar);
        }
    }
    public void PutAvatarIntoTpose()
    {
        InTPose = true;
        if (runtimeAnimatorController == null)
        {
            runtimeAnimatorController = Player.Avatar.Animator.runtimeAnimatorController;
        }
        UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<RuntimeAnimatorController> op = Addressables.LoadAssetAsync<RuntimeAnimatorController>(TPose);
        RuntimeAnimatorController RAC = op.WaitForCompletion();
        Player.Avatar.Animator.runtimeAnimatorController = RAC;
        ForceUpdateAnimator(Player.Avatar.Animator);
    }
    public void ResetAvatarAnimator()
    {
        Player.Avatar.Animator.runtimeAnimatorController = runtimeAnimatorController;
        runtimeAnimatorController = null;
        InTPose = false;
    }
    public Bounds GetBounds(Transform animatorParent)
    {
        // Get all renderers in the parent GameObject
        Renderer[] renderers = animatorParent.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, new Vector3(0.3f, 1.7f, 0.3f));
        }
        Bounds bounds = renderers[0].bounds;
        for (int Index = 1; Index < renderers.Length; Index++)
        {
            bounds.Encapsulate(renderers[Index].bounds);
        }
        return bounds;
    }
    public static bool TryConvertToBoneTrackingRole(HumanBodyBones body, out BasisBoneTrackedRole result)
    {
        result = BasisBoneTrackedRole.Chest; // Set a default value or handle it based on your requirements

        if (Enum.TryParse(body.ToString(), out BasisBoneTrackedRole parsedRole))
        {
            result = parsedRole;
            return true; // Successfully parsed
        }

        return false; // Failed to parse
    }
    public static bool TryConvertToHumanoidRole(BasisBoneTrackedRole body, out HumanBodyBones result)
    {
        result = HumanBodyBones.Hips; // Set a default value or handle it based on your requirements

        if (Enum.TryParse(body.ToString(), out HumanBodyBones parsedRole))
        {
            result = parsedRole;
            return true; // Successfully parsed
        }

        return false; // Failed to parse
    }
    public static bool IsApartOfSpineVertical(BasisBoneTrackedRole Role)
    {
        if (Role == BasisBoneTrackedRole.Hips ||
            Role == BasisBoneTrackedRole.Chest ||
            Role == BasisBoneTrackedRole.UpperChest ||
            Role == BasisBoneTrackedRole.Hips ||
            Role == BasisBoneTrackedRole.Spine ||
            Role == BasisBoneTrackedRole.CenterEye ||
            Role == BasisBoneTrackedRole.Mouth ||
            Role == BasisBoneTrackedRole.Head)
        {
            return true;
        }
        return false;
    }
    public void CalculateTransformPositions(Animator anim, BaseBoneDriver driver)
    {
        for (int Index = 0; Index < driver.Controls.Length; Index++)
        {
            BasisBoneControl Control = driver.Controls[Index];
            if (driver.trackedRoles[Index] == BasisBoneTrackedRole.CenterEye)
            {
                GetWorldSpaceRotAndPos(() => Player.Avatar.AvatarEyePosition, out Control.RestingWorldSpace.rotation, out Control.RestingWorldSpace.position);
                SetInitalData(anim, Control, driver.trackedRoles[Index]);
            }
            else
            {
                if (driver.trackedRoles[Index] == BasisBoneTrackedRole.Mouth)
                {
                    GetWorldSpaceRotAndPos(() => Player.Avatar.AvatarMouthPosition, out Control.RestingWorldSpace.rotation, out Control.RestingWorldSpace.position);
                    SetInitalData(anim, Control, driver.trackedRoles[Index]);
                }
                else
                {
                    UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<BasisFallBackBoneData> BasisFallBackBoneDataAsync = Addressables.LoadAssetAsync<BasisFallBackBoneData>(BoneData);
                    BasisFallBackBoneData FBBD = BasisFallBackBoneDataAsync.WaitForCompletion();
                    if (FBBD.FindBone(out BasisFallBone FallBackBone, driver.trackedRoles[Index]))
                    {
                        if (TryConvertToHumanoidRole(driver.trackedRoles[Index], out HumanBodyBones HumanBones))
                        {
                            GetBoneRotAndPos(driver, anim, HumanBones, FallBackBone.PositionPercentage, out Control.RestingWorldSpace.rotation, out Control.RestingWorldSpace.position, out bool UsedFallback);
                            SetInitalData(anim, Control, driver.trackedRoles[Index]);
                        }
                    }
                }
            }
        }
    }
    public void GetBoneRotAndPos(BaseBoneDriver driver, Animator anim, HumanBodyBones bone, Vector3 heightPercentage, out Quaternion Rotation, out Vector3 Position, out bool UsedFallback)
    {
        if (anim.avatar != null && anim.avatar.isHuman)
        {
            Transform boneTransform = anim.GetBoneTransform(bone);
            if (boneTransform == null)
            {
                Rotation = driver.transform.rotation;
                if (BasisHelpers.TryGetFloor(anim, out Position))
                {

                }
               // Position = new Vector3(0, Position.y, 0);
                Position += CalculateFallbackOffset(bone, ActiveEyeHeight, heightPercentage);
                //Position = new Vector3(0, Position.y, 0);
                UsedFallback = true;
            }
            else
            {
                UsedFallback = false;
                boneTransform.GetPositionAndRotation(out Position, out Rotation);
            }
        }
        else
        {
            Rotation = driver.transform.rotation;
            if (BasisHelpers.TryGetFloor(anim, out Position))
            {

            }
            Position = new Vector3(0, Position.y, 0);
            Position += CalculateFallbackOffset(bone, ActiveEyeHeight, heightPercentage);
            Position = new Vector3(0, Position.y, 0);
            UsedFallback = true;
        }
    }
    public Vector3 CalculateFallbackOffset(HumanBodyBones bone, float fallbackHeight, Vector3 heightPercentage)
    {
        Vector3 height = fallbackHeight * heightPercentage;
        return bone == HumanBodyBones.Hips ? Multiply(height, -Vector3.up) : Multiply(height, Vector3.up);
    }
    public static Vector3 Multiply(Vector3 value, Vector3 scale)
    {
        return new Vector3(value.x * scale.x, value.y * scale.y, value.z * scale.z);
    }
    public void GetWorldSpaceRotAndPos(Func<Vector2> positionSelector, out Quaternion rotation, out Vector3 position)
    {
        rotation = Quaternion.identity;
        position = Vector3.zero;
        if (BasisHelpers.TryGetFloor(Player.Avatar.Animator, out Vector3 bottom))
        {
            Vector3 convertedToVector3 = BasisHelpers.AvatarPositionConversion(positionSelector());
            position = BasisHelpers.ConvertFromLocalSpace(convertedToVector3, bottom);
        }
        else
        {
            Debug.LogError("Missing bottom");
        }
    }
    private void ForceUpdateAnimator(Animator Anim)
    {
        // Specify the time you want the Animator to update to (in seconds)
        float desiredTime = Time.time;

        // Call the Update method to force the Animator to update to the desired time
        Anim.Update(desiredTime);
    }
    public GameObject CreateAndSetParent(Transform parent, string name)
    {
        // Create a new empty GameObject
        GameObject newObject = new GameObject(name);

        // Set its parent
        newObject.transform.SetParent(parent);
        return newObject;
    }
    public bool IsNull(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            Debug.LogError("Missing Object during calibration");
            return true;
        }
        else
        {
            return false;
        }
    }
    public void SetInitalData(Animator animator, BasisBoneControl bone, BasisBoneTrackedRole Role)
    {
        bone.RawLocalData.position = BasisLocalBoneDriver.ConvertToAvatarSpaceInital(animator, bone.RestingWorldSpace.position, 0.1f * animator.transform.localScale.y);//out Vector3 WorldSpaceFloor
        bone.RestingLocalSpace.position = bone.RawLocalData.position;
        bone.RestingLocalSpace.rotation = bone.RawLocalData.rotation;
        if (IsApartOfSpineVertical(Role))
        {
            bone.RawLocalData.position = new Vector3(0, bone.RawLocalData.position.y, bone.RawLocalData.position.z);
            bone.RestingLocalSpace.position = bone.RawLocalData.position;
        }
    }
    public void SetAndCreateLock(BaseBoneDriver BaseBoneDriver, BasisBoneTrackedRole TargetBone, BasisBoneTrackedRole AssignedTo, BasisTargetController PositionTargetController,float PositionLerpAmount, BasisClampData clampData, int positionalLockValue, int rotationalLockValue, bool UseAngle, float AngleBeforeMove, BasisTargetController targetController = BasisTargetController.Target, BasisClampAxis clampAxis = BasisClampAxis.x, bool CreateRotationalLock = true)
    {

        if (BaseBoneDriver.FindBone(out BasisBoneControl Bone, AssignedTo) == false)
        {
            Debug.LogError("Cant Find Bone " + AssignedTo);
        }
        if (BaseBoneDriver.FindBone(out BasisBoneControl Target, TargetBone) == false)
        {
            Debug.LogError("Cant Find Bone " + TargetBone);
        }
        BaseBoneDriver.CreatePositionalLock(Bone, Target, PositionTargetController, PositionLerpAmount, BasisVectorLerp.Lerp);
        if (CreateRotationalLock)
        {
            BaseBoneDriver.CreateRotationalLock(Bone, Target, clampAxis, clampData, positionalLockValue, BasisAxisLerp.SphericalLerp, rotationalLockValue, Quaternion.identity, targetController, UseAngle, AngleBeforeMove);
        }
    }
    public void FindSkinnedMeshRenders()
    {
        SkinnedMeshRenderer = Player.Avatar.Animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    }
    public void SetMatrixRecalculation(bool State)
    {
        foreach (SkinnedMeshRenderer Render in SkinnedMeshRenderer)
        {
            Render.forceMatrixRecalculationPerRender = State;
        }
    }
    public void updateWhenOffscreen(bool State)
    {
        foreach (SkinnedMeshRenderer Render in SkinnedMeshRenderer)
        {
            Render.updateWhenOffscreen = State;
        }
    }
    public void EnableTwoBoneIk(TwoBoneIKConstraint Constraint, bool maintainTargetPositionOffset = false, bool maintainTargetRotationOffset = false)
    {
        Constraint.data.targetPositionWeight = 1;
        Constraint.data.targetRotationWeight = 1;
        Constraint.data.maintainTargetPositionOffset = maintainTargetPositionOffset;
        Constraint.data.maintainTargetRotationOffset = maintainTargetRotationOffset;
        Constraint.data.hintWeight = 1;
    }
    public void Damp(BaseBoneDriver driver, GameObject Parent, Transform Source, BasisBoneTrackedRole Role, float rotationWeight = 1, float positionWeight = 1)
    {
        driver.FindBone(out BasisBoneControl Target, Role);
        GameObject DTData = CreateAndSetParent(Parent.transform, "Bone Role " + Role.ToString());
        DampedTransform DT = BasisHelpers.GetOrAddComponent<DampedTransform>(DTData);

        GameObject Ref = CreateAndSetParent(Target.BoneModelTransform, "Offset");

        DT.data.constrainedObject = Source;
        DT.data.sourceObject = Ref.transform;
        DT.data.dampRotation = rotationWeight;
        DT.data.dampPosition = positionWeight;
        GeneratedRequiredTransforms(Source, References.Hips);
    }
    public void MultiRotation(GameObject Parent, Transform Source, Transform Target, float rotationWeight = 1)
    {
        GameObject DTData = CreateAndSetParent(Parent.transform, "Eye Target");
        MultiAimConstraint DT = BasisHelpers.GetOrAddComponent<MultiAimConstraint>(DTData);
        DT.data.constrainedObject = Source;
        WeightedTransformArray Array = new WeightedTransformArray(0);
        WeightedTransform Weighted = new WeightedTransform(Target, rotationWeight);
        Array.Add(Weighted);
        DT.data.sourceObjects = Array;
        DT.data.maintainOffset = false;
        DT.data.aimAxis = MultiAimConstraintData.Axis.Z;
        DT.data.upAxis = MultiAimConstraintData.Axis.Y;
        DT.data.limits = new Vector2(-180, 180);
        DT.data.constrainedXAxis = true;
        DT.data.constrainedYAxis = true;
        DT.data.constrainedZAxis = true;

        GeneratedRequiredTransforms(Source, References.Hips);
    }
    public void MultiRotation(BaseBoneDriver driver, GameObject Parent, Transform Source, BasisBoneTrackedRole Role, float rotationWeight = 1)
    {
        driver.FindBone(out BasisBoneControl Target, Role);
        GameObject DTData = CreateAndSetParent(Parent.transform, "Bone Role " + Role.ToString());
        MultiAimConstraint DT = BasisHelpers.GetOrAddComponent<MultiAimConstraint>(DTData);
        DT.data.constrainedObject = Source;
        WeightedTransformArray Array = new WeightedTransformArray(0);
        WeightedTransform Weighted = new WeightedTransform(Target.BoneModelTransform, rotationWeight);
        Array.Add(Weighted);
        DT.data.sourceObjects = Array;
        DT.data.maintainOffset = false;
        DT.data.aimAxis = MultiAimConstraintData.Axis.Z;
        DT.data.upAxis = MultiAimConstraintData.Axis.Y;
        DT.data.limits = new Vector2(-180, 180);
        DT.data.constrainedXAxis = true;
        DT.data.constrainedYAxis = true;
        DT.data.constrainedZAxis = true;

        GeneratedRequiredTransforms(Source, References.Hips);
    }
    public void OverrideTransform(BaseBoneDriver driver, GameObject Parent, Transform Source, BasisBoneTrackedRole Role, float rotationWeight = 1, float positionWeight = 1, OverrideTransformData.Space Space = OverrideTransformData.Space.World)
    {
        driver.FindBone(out BasisBoneControl Target, Role);
        GameObject DTData = CreateAndSetParent(Parent.transform, "Bone Role " + Role.ToString());
        OverrideTransform DT = BasisHelpers.GetOrAddComponent<OverrideTransform>(DTData);
        DT.data.constrainedObject = Source;
        DT.data.sourceObject = Target.BoneModelTransform;
        DT.data.rotationWeight = rotationWeight;
        DT.data.positionWeight = positionWeight;
        DT.data.space = Space;
        GeneratedRequiredTransforms(Source, References.Hips);
    }
    public void CreateTwoBone(BaseBoneDriver driver, GameObject Parent, Transform root, Transform mid, Transform tip, BasisBoneTrackedRole TargetRole, BasisBoneTrackedRole BendRole,bool UseBoneRole, out TwoBoneIKConstraint TwoBoneIKConstraint, bool maintainTargetPositionOffset, bool maintainTargetRotationOffset)
    {
        driver.FindBone(out BasisBoneControl BoneControl, TargetRole);
        GameObject BoneRole = CreateAndSetParent(Parent.transform, "Bone Role " + TargetRole.ToString());
        TwoBoneIKConstraint = BasisHelpers.GetOrAddComponent<TwoBoneIKConstraint>(BoneRole);
        EnableTwoBoneIk(TwoBoneIKConstraint, maintainTargetPositionOffset, maintainTargetRotationOffset);
        TwoBoneIKConstraint.data.target = BoneControl.BoneModelTransform;
        if (UseBoneRole)
        {
            driver.FindBone(out BasisBoneControl BendBoneControl, BendRole);
            TwoBoneIKConstraint.data.hint = BendBoneControl.BoneModelTransform;
            TwoBoneIKConstraint.data.hintWeight = 1;
        }
        TwoBoneIKConstraint.data.root = root;
        TwoBoneIKConstraint.data.mid = mid;
        TwoBoneIKConstraint.data.tip = tip;
        GeneratedRequiredTransforms(tip, References.Hips);
    }
    public void GeneratedRequiredTransforms(Transform BaseLevel, Transform TopLevelParent)
    {
        BasisLocalAvatarDriver Driver = (BasisLocalAvatarDriver)this;
        // Go up the hierarchy until you hit the TopLevelParent
        if (BaseLevel != null)
        {
            Transform currentTransform = BaseLevel.parent;
            while (currentTransform != null && currentTransform != TopLevelParent)
            {
                // Add component if the current transform doesn't have it
                if (currentTransform.TryGetComponent<RigTransform>(out RigTransform RigTransform))
                {
                    if (Driver.AdditionalTransforms.Contains(RigTransform) == false)
                    {
                        Driver.AdditionalTransforms.Add(RigTransform);
                    }
                }
                else
                {
                    RigTransform = currentTransform.gameObject.AddComponent<RigTransform>();
                    Driver.AdditionalTransforms.Add(RigTransform);
                }
                // Move to the parent for the next iteration
                currentTransform = currentTransform.parent;
            }
        }
    }
}