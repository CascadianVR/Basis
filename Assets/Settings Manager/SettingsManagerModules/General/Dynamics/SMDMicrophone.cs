using BattlePhaze.SettingsManager;
using UnityEngine;

public class SMDMicrophone : SettingsManagerOption
{
    public SettingsManager Manager;
    public string[] MicrophoneDevice;
    // Define a delegate for the callback
    public delegate void MicrophoneChangedHandler(string newMicrophone);

    // Create an event of the delegate type
    public static event MicrophoneChangedHandler OnMicrophoneChanged;

    // Backing field for the SelectedMicrophone property
    private static string selectedMicrophone;

    // Property with a callback in the set accessor
    public static string SelectedMicrophone
    {
        get => selectedMicrophone;
        private set
        {
            selectedMicrophone = value;
            // Invoke the callback event
            OnMicrophoneChanged?.Invoke(selectedMicrophone);
        }
    }

    public override void ReceiveOption(SettingsMenuInput Option, SettingsManager Manager = null)
    {
        if (Manager == null)
        {
            Manager = SettingsManager.Instance;
        }
        if (NameReturn(0, Option))
        {

            MicrophoneDevice = Microphone.devices;

            SettingsManagerDropDown.Clear(Manager, Option.OptionIndex);
            Option.SelectableValueList.Clear();
            foreach (string device in MicrophoneDevice)
            {
                SettingsManagerDropDown.AddDropDownOption(Manager, Option.OptionIndex, device);
                SMSelectableValues.AddSelection(ref Option.SelectableValueList, device, device);
            }

            if (string.IsNullOrEmpty(Option.SelectedValue))
            {
                SettingsManagerDropDown.SetOptionsValue(Manager, 0, 0, true);
                Option.SelectedValue = Option.SelectableValueList[0].RealValue;
                SelectedMicrophone = Option.SelectableValueList[0].UserValue;
            }
            else
            {
                for (int RealValuesIndex = 0; RealValuesIndex < Option.SelectableValueList.Count; RealValuesIndex++)
                {
                    if (Option.SelectableValueList[RealValuesIndex].RealValue == Option.SelectedValue)
                    {
                        SettingsManagerDropDown.SetOptionsValue(Manager, Option.OptionIndex, RealValuesIndex, true);
                        SelectedMicrophone = Option.SelectableValueList[RealValuesIndex].UserValue;
                        return;
                    }
                }
            }
        }
    }
}