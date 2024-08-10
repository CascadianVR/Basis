using Basis.Scripts.Addressable_Driver.Resource;
using Basis.Scripts.BasisSdk.Helpers;
using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Command_Line_Args;
using Basis.Scripts.Device_Management.Devices;
using Basis.Scripts.Device_Management.Devices.Desktop;
using Basis.Scripts.Device_Management.Devices.OpenVR;
using Basis.Scripts.Device_Management.Devices.OpenXR;
using Basis.Scripts.Device_Management.Devices.Simulation;
using Basis.Scripts.Drivers;
using Basis.Scripts.Player;
using Basis.Scripts.TransformBinders;
using Basis.Scripts.TransformBinders.BoneControl;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Basis.Scripts.Device_Management
{
    public partial class BasisDeviceManagement : MonoBehaviour
    {
        public BasisBootedMode CurrentMode = BasisBootedMode.None;
        public BasisBootedMode DefaultMode = BasisBootedMode.Desktop;
        public static BasisDeviceManagement Instance;
        public BasisOpusSettings BasisOpusSettings;
        public event Action<BasisBootedMode> OnBootModeChanged;
        public event Action<BasisBootedMode> OnBootModeStopped;
        [SerializeField]
        public BasisObservableList<BasisInput> AllInputDevices = new BasisObservableList<BasisInput>();
        [SerializeField]
        public BasisXRManagement BasisXRManagement = new BasisXRManagement();
        [SerializeField]
        public BasisOpenVRManagement BasisOpenVRManagement = new BasisOpenVRManagement();
        [SerializeField]
        public BasisOpenXRManagement BasisOpenXRManagement = new BasisOpenXRManagement();
        [SerializeField]
        public BasisDesktopManagement BasisDesktopManagement = new BasisDesktopManagement();
        [SerializeField]
        public BasisSimulateXR BasisSimulateXR = new BasisSimulateXR();
        [SerializeField]
        public List<BasisLockToInput> BasisLockToInputs = new List<BasisLockToInput>();
        public delegate Task InitializationCompletedHandler();
        public event InitializationCompletedHandler OnInitializationCompleted;
        public bool FireOffNetwork = true;
        public bool HasEvents = false;
        [SerializeField]
        public List<StoredPreviousDevice> PreviouslyConnectedDevices = new List<StoredPreviousDevice>();
        public List<BasisDeviceMatchSettings> UseAbleDeviceConfigs = new List<BasisDeviceMatchSettings>();
        [SerializeField]
        public BasisDeviceNameMatcher BasisDeviceNameMatcher;
        public const string InvalidConst = "Invalid";
        void Start()
        {
            if (BasisHelpers.CheckInstance<BasisDeviceManagement>(Instance))
            {
                Instance = this;
            }
            Initialize();
        }
        void OnDestroy()
        {
            ShutDownXR(true);
            BasisDesktopManagement.StopDesktop();
            BasisSimulateXR.StopXR();
            if (HasEvents)
            {
                BasisXRManagement.CheckForPass -= CheckForPass;

                OnInitializationCompleted -= RunAfterInitialized;
            }
        }
        public async void Initialize()
        {
            CommandLineArgs.Initialize();
            await LoadAndOrSaveDefaultDeviceConfigs(Application.persistentDataPath + "/Devices");
            InstantiationParameters parameters = new InstantiationParameters();
            await BasisPlayerFactory.CreateLocalPlayer(parameters);

            SwitchMode(DefaultMode);
            if (HasEvents == false)
            {
                BasisXRManagement.CheckForPass += CheckForPass;

                OnInitializationCompleted += RunAfterInitialized;
                HasEvents = true;
            }
            await OnInitializationCompleted?.Invoke();
        }
        public async Task RunAfterInitialized()
        {
            if (FireOffNetwork)
            {
                await LoadGameobject("NetworkManagement", new InstantiationParameters());
            }
        }
        public void SwitchMode(BasisBootedMode newMode)
        {
            if (CurrentMode != BasisBootedMode.None)
            {
                Debug.Log("killing off " + CurrentMode);
                if (newMode == BasisBootedMode.Desktop)
                {
                    ShutDownXR();
                }
                else
                {
                    BasisDesktopManagement.StopDesktop();
                }
            }

            CurrentMode = newMode;
            OnBootModeChanged?.Invoke(CurrentMode);

            Debug.Log("Loading " + CurrentMode);

            switch (CurrentMode)
            {
                case BasisBootedMode.OpenVRLoader:
                    BasisXRManagement.BeginLoad();
                    break;
                case BasisBootedMode.OpenXRLoader:
                    BasisXRManagement.BeginLoad();
                    break;
                case BasisBootedMode.Desktop:
                    BasisDesktopManagement.BeginDesktop();
                    break;
                case BasisBootedMode.Exiting:
                    break;
                default:
                    Debug.LogError("This should not occur (default)");
                    BasisDesktopManagement.BeginDesktop();
                    break;
            }
        }
        public void SetCameraRenderState(bool state)
        {
            BasisLocalCameraDriver.Instance.CameraData.allowXRRendering = state;
        }
        public void ShutDownXR(bool isExiting = false)
        {
            BasisSimulateXR.StopXR();
            BasisOpenXRManagement.StopXRSDK();
            BasisOpenVRManagement.StopXRSDK();

            BasisXRManagement.StopXR(isExiting);
            AllInputDevices.RemoveAll(item => item == null);

            OnBootModeStopped?.Invoke(CurrentMode);
        }
        public static async Task<BasisPlayer> LoadGameobject(string playerAddressableID, InstantiationParameters instantiationParameters)
        {
            var data = await AddressableResourceProcess.LoadAsGameObjectsAsync(playerAddressableID, instantiationParameters);
            var gameObjects = data.Item1;

            if (gameObjects.Count == 0)
            {
                Debug.LogError("Missing ");
            }

            return null;
        }
        public static void ForceLoadXR()
        {
            SwitchSetMode(BasisBootedMode.OpenVRLoader);
        }
        public static void ForceSetDesktop()
        {
            SwitchSetMode(BasisBootedMode.Desktop);
        }
        public static void SwitchSetMode(BasisBootedMode Mode)
        {
            if (Instance != null && Mode != Instance.CurrentMode)
            {
                Instance.SwitchMode(Mode);
            }
        }
        public static async void ShowTrackers()
        {
            await ShowTrackersAsync();
        }
        public static async Task ShowTrackersAsync()
        {
            var inputDevices = Instance.AllInputDevices;
            var showTrackedVisualTasks = new List<Task>();

            foreach (var input in inputDevices)
            {
                showTrackedVisualTasks.Add(input.ShowTrackedVisual());
            }

            await Task.WhenAll(showTrackedVisualTasks);
        }
        public static void HideTrackers()
        {
            for (int Index = 0; Index < Instance.AllInputDevices.Count; Index++)
            {
                Instance.AllInputDevices[Index].HideTrackedVisual();
            }
        }
        public void RemoveDevicesFrom(string SubSystem, string id)
        {
            for (int Index = 0; Index < AllInputDevices.Count; Index++)
            {
                BasisInput device = AllInputDevices[Index];
                if (device != null)
                {
                    if (device.SubSystemIdentifier == SubSystem && device.UniqueDeviceIdentifier == id)
                    {
                        CacheDevice(device);
                        AllInputDevices[Index] = null;
                        GameObject.Destroy(device.gameObject);
                    }
                }
            }

            AllInputDevices.RemoveAll(item => item == null);
        }
        private void CheckForPass(BasisBootedMode type)
        {
            Debug.Log("Loading " + type);
            BasisSimulateXR.StartSimulation();

            switch (type)
            {
                case BasisBootedMode.OpenVRLoader:
                    BasisOpenVRManagement ??= new BasisOpenVRManagement();
                    BasisOpenVRManagement.StartXRSDK();
                    SetCameraRenderState(true);

                    break;
                case BasisBootedMode.OpenXRLoader:
                    BasisOpenXRManagement ??= new BasisOpenXRManagement();
                    BasisOpenXRManagement.StartXRSDK();
                    SetCameraRenderState(true);
                    break;
                case BasisBootedMode.Desktop:
                    BasisDesktopManagement.BeginDesktop();
                    break;
                case BasisBootedMode.Exiting:
                    break;
                default:
                    Debug.LogError("This should not occur (default)");
                    BasisDesktopManagement.BeginDesktop();
                    break;
            }
        }
        public bool TryAdd(BasisInput basisXRInput)
        {
            if (AllInputDevices.Contains(basisXRInput) == false)
            {
                AllInputDevices.Add(basisXRInput);
                if (RestoreDevice(basisXRInput.SubSystemIdentifier, basisXRInput.UniqueDeviceIdentifier, out StoredPreviousDevice PreviousDevice))
                {
                    if (CheckBeforeOverride(PreviousDevice))
                    {
                        StartCoroutine(RestoreInversetOffsets(basisXRInput, PreviousDevice));
                    }
                    else
                    {
                        Debug.Log("bailing out of restore already has a replacement");
                    }
                }
                return true;
            }
            else
            {
                Debug.LogError("already added a Input Device thats identical!");
            }
            return false;
        }
        IEnumerator RestoreInversetOffsets(BasisInput basisXRInput, StoredPreviousDevice PreviousDevice)
        {
            yield return new WaitForEndOfFrame();
            if (basisXRInput != null && basisXRInput.Control != null)
            {
                if (CheckBeforeOverride(PreviousDevice))
                {
                    Debug.Log("device is restored " + PreviousDevice.trackedRole);
                    basisXRInput.ApplyTrackerCalibration(PreviousDevice.trackedRole);
                    basisXRInput.Control.InverseOffsetFromBone = PreviousDevice.InverseOffsetFromBone;
                }
            }

        }
        public bool CheckBeforeOverride(StoredPreviousDevice Stored)
        {
            foreach (var device in AllInputDevices)
            {
                if (device.TryGetRole(out BasisBoneTrackedRole Role))
                {
                    if (Role == Stored.trackedRole)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        public bool FindDevice(out BasisInput FindDevice, BasisBoneTrackedRole FindRole)
        {
            foreach (var device in AllInputDevices)
            {
                if (device != null && device.Control != null)
                {
                    if (device.Control.HasBone)
                    {
                        if (device.TryGetRole(out BasisBoneTrackedRole Role))
                        {
                            if (Role == FindRole)
                            {
                                FindDevice = device;
                                return true;

                            }
                        }
                    }
                }
            }
            FindDevice = null;
            return false;
        }
        public void CacheDevice(BasisInput DevicesThatsGettingPurged)
        {
            if (DevicesThatsGettingPurged.TryGetRole(out BasisBoneTrackedRole Role) && DevicesThatsGettingPurged.Control != null)
            {
                StoredPreviousDevice StoredPreviousDevice = new StoredPreviousDevice
                { InverseOffsetFromBone = DevicesThatsGettingPurged.Control.InverseOffsetFromBone }; ;

                StoredPreviousDevice.trackedRole = Role;
                StoredPreviousDevice.hasRoleAssigned = DevicesThatsGettingPurged.hasRoleAssigned;
                StoredPreviousDevice.SubSystem = DevicesThatsGettingPurged.SubSystemIdentifier;
                StoredPreviousDevice.UniqueID = DevicesThatsGettingPurged.UniqueDeviceIdentifier;
                PreviouslyConnectedDevices.Add(StoredPreviousDevice);
            }
        }
        public bool RestoreDevice(string SubSystem, string id, out StoredPreviousDevice StoredPreviousDevice)
        {
            foreach (StoredPreviousDevice Device in PreviouslyConnectedDevices)
            {
                if (Device.UniqueID == id && Device.SubSystem == SubSystem)
                {
                    Debug.Log("this device is restoreable restoring..");
                    PreviouslyConnectedDevices.Remove(Device);
                    StoredPreviousDevice = Device;
                    return true;
                }
            }
            StoredPreviousDevice = null;
            return false;
        }
        public async Task LoadAndOrSaveDefaultDeviceConfigs(string directoryPath)
        {
            var builtInDevices = BasisDeviceNameMatcher.BasisDevice;
            //save to disc any that do not exist
            await BasisDeviceLoaderAndSaver.SaveDevices(directoryPath, builtInDevices);
            //now lets load them all and override versions that are outdated.
            var loadedDevices = await BasisDeviceLoaderAndSaver.LoadDeviceAsync(directoryPath);

            // Dictionary to store devices by DeviceID for quick lookup
            var deviceDictionary = builtInDevices.ToDictionary(
                device => string.IsNullOrEmpty(device.DeviceID) ? InvalidConst : device.DeviceID,
                device => device
            );

            foreach (var loadedDevice in loadedDevices)
            {
                var loadedDeviceID = string.IsNullOrEmpty(loadedDevice.DeviceID) ? InvalidConst : loadedDevice.DeviceID;

                if (deviceDictionary.TryGetValue(loadedDeviceID, out var existingDevice))
                {
                    // Replace the built-in device if the loaded one has a higher version number
                    if (loadedDevice.VersionNumber > existingDevice.VersionNumber)
                    {
                        deviceDictionary[loadedDeviceID] = loadedDevice;
                    }
                }
                else
                {
                    // Add the new loaded device
                    deviceDictionary[loadedDeviceID] = loadedDevice;
                }
            }

            UseAbleDeviceConfigs = deviceDictionary.Values.ToList();
        }
    }
}