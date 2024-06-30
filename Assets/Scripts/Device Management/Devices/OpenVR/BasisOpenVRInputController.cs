using UnityEngine;
using Valve.VR;
[DefaultExecutionOrder(15101)]
public class BasisOpenVRInputController : BasisInput
{
    public OpenVRDevice Device;
    public SteamVR_Input_Sources inputSource;
    public SteamVR_Action_Pose poseAction = SteamVR_Input.GetAction<SteamVR_Action_Pose>("Pose");
    public void Initialize(OpenVRDevice device, string UniqueID, string UnUniqueID)
    {
        Device = device;
        TryAssignRole(Device.deviceClass);
        ActivateTracking(UniqueID, UnUniqueID);
        if (poseAction != null)
        {
            poseAction[inputSource].onUpdate += SteamVR_Behaviour_Pose_OnUpdate;
          //  poseAction[inputSource].onDeviceConnectedChanged += OnDeviceConnectedChanged;
        //    poseAction[inputSource].onTrackingChanged += OnTrackingChanged;
         //   poseAction[inputSource].onChange += SteamVR_Behaviour_Pose_OnChange;
        }
    }
    public new void OnDestroy()
    {
        if (poseAction != null)
        {
            poseAction[inputSource].onUpdate -= SteamVR_Behaviour_Pose_OnUpdate;
        //    poseAction[inputSource].onDeviceConnectedChanged -= OnDeviceConnectedChanged;
         //   poseAction[inputSource].onTrackingChanged -= OnTrackingChanged;
         //   poseAction[inputSource].onChange -= SteamVR_Behaviour_Pose_OnChange;
        }
        historyBuffer.Clear();
        base.OnDestroy();
    }

    public void TryAssignRole(ETrackedDeviceClass deviceClass)
    {
        if (deviceClass == ETrackedDeviceClass.Controller)
        {
            bool isLeftHand = SteamVR.instance.hmd.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand) == Device.deviceIndex;
            if (isLeftHand)
            {
                TrackedRole = BasisBoneTrackedRole.LeftHand;
                inputSource = SteamVR_Input_Sources.LeftHand;
            }
            bool isRightHand = SteamVR.instance.hmd.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand) == Device.deviceIndex;
            if (isRightHand)
            {
                TrackedRole = BasisBoneTrackedRole.RightHand;
                inputSource = SteamVR_Input_Sources.RightHand;
            }
        }
        if (deviceClass == ETrackedDeviceClass.HMD)
        {
            TrackedRole = BasisBoneTrackedRole.CenterEye;
        }
    }
    public override void PollData()
    {
        if (SteamVR.active)
        {
            State.primary2DAxis = SteamVR_Actions._default.Joystick.GetAxis(inputSource);
            State.primaryButtonGetState = SteamVR_Actions._default.A_Button.GetState(inputSource);
            State.secondaryButtonGetState = SteamVR_Actions._default.B_Button.GetState(inputSource);
            State.Trigger = SteamVR_Actions._default.Trigger.GetAxis(inputSource);
            UpdatePlayerControl();
        }
    }
    private void SteamVR_Behaviour_Pose_OnUpdate(SteamVR_Action_Pose fromAction, SteamVR_Input_Sources fromSource)
    {
        UpdateHistoryBuffer();
        LocalRawPosition = poseAction[inputSource].localPosition;
        LocalRawRotation = poseAction[inputSource].localRotation;
        transform.SetLocalPositionAndRotation(poseAction[inputSource].localPosition, poseAction[inputSource].localRotation);
        if (hasRoleAssigned)
        {
            if (Control.HasTrackerPositionDriver != BasisHasTracked.HasNoTracker && LocalRawPosition != Vector3.zero)
            {
                Control.TrackerData.position = LocalRawPosition - LocalRawRotation * pivotOffset;
            }
            if (Control.HasTrackerPositionDriver != BasisHasTracked.HasNoTracker && LocalRawRotation != Quaternion.identity)
            {
                Control.TrackerData.rotation = LocalRawRotation * rotationOffset;
            }
        }
    }
    #region Mostly Unused Steam
    protected SteamVR_HistoryBuffer historyBuffer = new SteamVR_HistoryBuffer(30);
    protected int lastFrameUpdated;
    protected void UpdateHistoryBuffer()
    {
        int currentFrame = Time.frameCount;
        if (lastFrameUpdated != currentFrame)
        {
            historyBuffer.Update(poseAction[inputSource].localPosition, poseAction[inputSource].localRotation, poseAction[inputSource].velocity, poseAction[inputSource].angularVelocity);
            lastFrameUpdated = currentFrame;
        }
    }
    public Vector3 GetVelocity()
    {
        return poseAction[inputSource].velocity;
    }
    public Vector3 GetAngularVelocity()
    {
        return poseAction[inputSource].angularVelocity;
    }
    public bool GetVelocitiesAtTimeOffset(float secondsFromNow, out Vector3 velocity, out Vector3 angularVelocity)
    {
        return poseAction[inputSource].GetVelocitiesAtTimeOffset(secondsFromNow, out velocity, out angularVelocity);
    }
    public void GetEstimatedPeakVelocities(out Vector3 velocity, out Vector3 angularVelocity)
    {
        int top = historyBuffer.GetTopVelocity(10, 1);

        historyBuffer.GetAverageVelocities(out velocity, out angularVelocity, 2, top);
    }
    public bool isValid { get { return poseAction[inputSource].poseIsValid; } }
    public bool isActive { get { return poseAction[inputSource].active; } }
    #endregion
}