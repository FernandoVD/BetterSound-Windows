using System;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace BetterSound.Audio;

/// <summary>
/// !! VERIFY ON A REAL WINDOWS MACHINE BEFORE RELYING ON THIS !!
///
/// Windows' public WASAPI surface (what NAudio wraps everywhere else in this
/// project) has no documented "set this as the system default output/input
/// device" call — Microsoft never shipped one. Every app that offers this
/// (SoundSwitch, EarTrumpet, NirSoft's SoundVolumeView, the old "Audio
/// Switcher" tray tools) goes through the same reverse-engineered internal
/// interface, IPolicyConfig, that the Windows Settings app itself uses under
/// the hood. It's undocumented, unversioned, and has drifted slightly across
/// OS releases (the CLSID/IID below are the commonly-cited Windows 7+
/// values used by most open-source implementations of this trick).
///
/// This file was written without access to a Windows machine to compile and
/// test against, so — unlike the rest of this codebase, which only uses
/// documented public Core Audio APIs via NAudio — treat the GUIDs below as
/// "needs confirmation." If SetDefaultEndpoint throws a COMException on
/// first use (class/interface not registered), cross-check the values
/// against a known-good reference implementation, e.g. EarTrumpet's
/// (MIT-licensed, https://github.com/File-New-Project/EarTrumpet) or
/// NirSoft's SoundVolumeView source notes, and update just this file.
/// </summary>
internal static class DefaultDeviceInterop
{
    private const string ClsidPolicyConfigClient = "870af99c-171d-4f9e-af0e-6a9904a58ea1";
    private const string IidIPolicyConfig = "f8679f50-850a-41cf-9c72-430f290290c8";

    [ComImport]
    [Guid(IidIPolicyConfig)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(string deviceId, out IntPtr format);
        [PreserveSig] int GetDeviceFormat(string deviceId, bool isDefault, out IntPtr format);
        [PreserveSig] int ResetDeviceFormat(string deviceId);
        [PreserveSig] int SetDeviceFormat(string deviceId, IntPtr endpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod(string deviceId, bool isDefault, out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int SetProcessingPeriod(string deviceId, long period);
        [PreserveSig] int GetShareMode(string deviceId, IntPtr mode);
        [PreserveSig] int SetShareMode(string deviceId, IntPtr mode);
        [PreserveSig] int GetPropertyValue(string deviceId, IntPtr key, out IntPtr value);
        [PreserveSig] int SetPropertyValue(string deviceId, IntPtr key, IntPtr value);
        [PreserveSig] int SetDefaultEndpoint(string deviceId, Role role);
        [PreserveSig] int SetEndpointVisibility(string deviceId, bool visible);
    }

    [ComImport]
    [Guid(ClsidPolicyConfigClient)]
    private class PolicyConfigClient
    {
    }

    /// <summary>
    /// Sets both the "console" and "multimedia" roles to this device — the
    /// combination that makes it show as the selected device everywhere in
    /// the shell (matches what right-clicking the volume icon → "Set as
    /// default device" does). Communications role is left alone, same as
    /// the macOS app which doesn't touch VoIP-specific routing either.
    /// </summary>
    public static void SetAsDefault(MMDevice device)
    {
        var policyConfig = (IPolicyConfig)new PolicyConfigClient();
        try
        {
            Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(device.ID, Role.Console));
            Marshal.ThrowExceptionForHR(policyConfig.SetDefaultEndpoint(device.ID, Role.Multimedia));
        }
        finally
        {
            Marshal.ReleaseComObject(policyConfig);
        }
    }
}
