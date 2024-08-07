﻿using System.Collections.Generic;
using System;
using UnityEngine;
using UnityOpus;
using DarkRift;
using static SerializableDarkRift;
using DarkRift.Server.Plugins.Commands;
[System.Serializable]
public class BasisAudioTransmission
{
    public event Action OnEncoded;
    public Encoder encoder;
    public BasisNetworkedPlayer NetworkedPlayer;
    public BasisNetworkSendBase Base;
    public BasisOpusSettings settings;
    public byte[] outputBuffer;
    public byte[] encodedData;
    public int encodedLength;
    public BasisLocalPlayer Local;
    public MicrophoneRecorder Recorder;
    public bool IsInitalized = false;
    public AudioSegmentDataMessage AudioSegmentData = new AudioSegmentDataMessage();
    public AudioSilentSegmentDataMessage audioSilentSegmentData = new AudioSilentSegmentDataMessage();
    public void OnEnable(BasisNetworkedPlayer networkedPlayer)
    {
        if (IsInitalized == false)
        {
            NetworkedPlayer = networkedPlayer;
            Base = networkedPlayer.NetworkSend;
            settings = BasisDeviceManagement.Instance.BasisOpusSettings;
            encoder = new Encoder(settings.SamplingFrequency, settings.NumChannels, settings.OpusApplication)
            {
                Bitrate = settings.BitrateKPS,
                Complexity = settings.Complexity,
                Signal = settings.OpusSignal
            };
            OnEncoded += SendVoiceOverNetwork;
            Local = (BasisLocalPlayer)networkedPlayer.Player;
            Recorder = Local.AvatarDriver.MicrophoneRecorder;
            if (Local.AvatarDriver != null && Local.AvatarDriver.MicrophoneRecorder != null)
            {
                Local.AvatarDriver.MicrophoneRecorder.OnHasAudio += OnAudioReady;
                Local.AvatarDriver.MicrophoneRecorder.OnHasSilence += OnAudioSilence;

            }
            IsInitalized = true;
        }
    }
    public void OnDisable()
    {
        if (Recorder != null)
        {
            GameObject.Destroy(Recorder.gameObject);
        }
        Recorder.OnHasAudio -= OnAudioReady;
        Recorder.OnHasSilence -= OnAudioSilence;
        encoder.Dispose();
        encoder = null;
        OnEncoded -= SendVoiceOverNetwork;
    }
    public void OnAudioSilence()
    {
        SendSilenceOverNetwork();
    }
    public void OnAudioReady()
    {
        int PacketSize = Recorder.processBuffer.Count * 4;
        if (outputBuffer == null || PacketSize != outputBuffer.Length)
        {
            outputBuffer = new byte[PacketSize];
        }
        encodedLength = encoder.Encode(Recorder.processBuffer.Array, outputBuffer);

        encodedData = new byte[encodedLength];
        Array.Copy(outputBuffer, 0, encodedData, 0, encodedLength);

        OnEncoded?.Invoke();
    }
    private void SendVoiceOverNetwork()
    {
        using (DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            AudioSegmentData.buffer = encodedData;
            writer.Write(AudioSegmentData);
            BasisNetworkProfiler.AudioUpdatePacket.Sample(writer.Length);
            using (Message msg = Message.Create(BasisTags.AudioSegmentTag, writer))
            {
                BasisNetworkConnector.Instance.Client.SendMessage(msg, DeliveryMethod.Sequenced);
            }
        }
    }
    private void SendSilenceOverNetwork()
    {
        using (DarkRiftWriter writer = DarkRiftWriter.Create())
        {
            writer.Write(audioSilentSegmentData);
            BasisNetworkProfiler.AudioUpdatePacket.Sample(writer.Length);
            using (Message msg = Message.Create(BasisTags.AudioSegmentTag, writer))
            {
                BasisNetworkConnector.Instance.Client.SendMessage(msg, DeliveryMethod.Sequenced);
            }
        }
    }
}