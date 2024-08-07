using UnityEngine;
public class BasisAvatarEyeInput : BasisInput
{
    public Camera Camera;
    public BasisLocalAvatarDriver AvatarDriver;
    public BasisLocalInputActions characterInputActions;
    public static BasisAvatarEyeInput Instance;
    public float RangeOfMotionBeforeTurn = 13;
    public float headDownwardForce = 0.002f;
    public float headUpwardForce = 0.001f;
    public float adjustment;
    public float crouchPercentage = 0.5f;
    public float rotationSpeed = 0.1f;
    private float rotationY;
    public float rotationX;
    public float minimumY = -80f;
    public float maximumY = 80f;
    public float DelayedResponseForRotation = 0.6f;
    public float FallBackHeight = 1.73f;
    public bool BlockCrouching;
    public void Initalize(string ID = "Desktop Eye", string subSystems = "BasisDesktopManagement")
    {
        Debug.Log("Initalizing Avatar Eye");
        if (BasisLocalPlayer.Instance.AvatarDriver != null)
        {
            LocalRawPosition = new Vector3(0, BasisLocalPlayer.Instance.PlayerEyeHeight, 0);
            LocalRawRotation = Quaternion.identity;
        }
        else
        {
            LocalRawPosition = new Vector3(0, FallBackHeight, 0);
            LocalRawRotation = Quaternion.identity;
        }
        FinalPosition = LocalRawPosition;
        FinalRotation = LocalRawRotation;
        TrackedRole = BasisBoneTrackedRole.CenterEye;
        ActivateTracking(ID, ID, subSystems);
        if (BasisHelpers.CheckInstance(Instance))
        {
            Instance = this;
        }
        PlayerInitialized();
        BasisLocalPlayer.OnLocalAvatarChanged += PlayerInitialized;
        LockCursor();
        BasisLocalPlayer.OnPlayersHeightChanged += BasisLocalPlayer_OnPlayersHeightChanged;
    }
    public new void OnDestroy()
    {
        BasisLocalPlayer.OnLocalAvatarChanged -= PlayerInitialized;
        BasisLocalPlayer.OnPlayersHeightChanged -= BasisLocalPlayer_OnPlayersHeightChanged;
        base.OnDestroy();
    }

    private void BasisLocalPlayer_OnPlayersHeightChanged()
    {
        BasisLocalPlayer.Instance.PlayerEyeHeight = BasisLocalPlayer.Instance.AvatarDriver.ActiveEyeHeight;
    }

    public void PlayerInitialized()
    {
        Driver = BasisLocalPlayer.Instance.LocalBoneDriver;
        characterInputActions = BasisLocalInputActions.Instance;
        if (characterInputActions != null)
        {
            characterInputActions.CharacterEyeInput = this;
        }
        AvatarDriver = BasisLocalPlayer.Instance.AvatarDriver;
        Camera = BasisLocalCameraDriver.Instance.Camera;
        foreach (BasisLockToInput Input in BasisDeviceManagement.Instance.BasisLockToInputs)
        {
            Input.FindRole();
        }
    }
    public new void OnDisable()
    {
        BasisLocalPlayer.OnLocalAvatarChanged -= PlayerInitialized;
        base.OnDisable();
        UnlockCursor();
    }
    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked; // Lock the cursor to the center of the screen
        Cursor.visible = false; // Hide the cursor
    }
    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None; // Unlock the cursor
        Cursor.visible = true; // Show the cursor
    }
    public void HandleMouseRotation(Vector2 lookVector)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }
        rotationX += lookVector.x * rotationSpeed;
        rotationY -= lookVector.y * rotationSpeed;

    }
    public override void PollData()
    {
        if (hasRoleAssigned)
        {
            Control.TrackerData.rotation = LocalRawRotation;
            // Apply modulo operation to keep rotation within 0 to 360 range
            rotationX %= 360f;
            rotationY %= 360f;
            // Clamp rotationY to stay within the specified range
            rotationY = Mathf.Clamp(rotationY, minimumY, maximumY);
            LocalRawRotation = Quaternion.Euler(rotationY, rotationX, 0);
        }
        if (hasRoleAssigned)
        {
            Vector3 adjustedHeadPosition = new Vector3(0, BasisLocalPlayer.Instance.PlayerEyeHeight, 0);
            if (characterInputActions.Crouching && BlockCrouching == false)
            {
                adjustedHeadPosition.y -= Control.RestingLocalSpace.position.y * crouchPercentage;
            }

            CalculateAdjustment();
            adjustedHeadPosition.y -= adjustment;
            LocalRawPosition = adjustedHeadPosition; // / BasisLocalPlayer.Instance.ScaledUpPlayerPositions;
            Control.TrackerData.position = LocalRawPosition;
        }
        UpdatePlayerControl();
        FinalPosition = LocalRawPosition;
        FinalRotation = LocalRawRotation;
        transform.SetLocalPositionAndRotation(LocalRawPosition, LocalRawRotation);
    }
    public void CalculateAdjustment()
    {
        if (rotationY > 0)
        {
            // Positive rotation
            adjustment = Mathf.Abs(rotationY) * ((headDownwardForce * BasisLocalPlayer.Instance.AvatarDriver.ActiveEyeHeight) / Control.RestingLocalSpace.position.y);
        }
        else
        {
            // Negative rotation
            adjustment = Mathf.Abs(rotationY) * ((headUpwardForce * BasisLocalPlayer.Instance.AvatarDriver.ActiveEyeHeight) / Control.RestingLocalSpace.position.y);
        }
    }
}