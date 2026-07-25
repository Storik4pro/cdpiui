using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Pipe.Models
{
    public enum PipeMessageTargetIds
    {
        Service,
        Presentation,
        CONPTY,
        GoodCheck,
        Utils,
        Settings,
        Update,
        MSIInstallation,
        Proxy,
        CompatibilityCheck,
        Notifications,
        Application
    }

    public enum ServiceMessageIds
    {
        AuthOK,
        ConnectOK,
        AuthFAIL,
        RequestAuth
    }

    public enum PresentationMessageIds
    {
        ShowWindow
    }

    public enum CONPTYMessageIds
    {
        // ToServer
        StartProcessId,
        RestartProcessId,
        StopProcessId,
        GetProcessIdFullOutput,
        GetProcessIdState,
        GetAllProcessStates,
        StopService,
        ProcessIdStartupArgsChanged,
        // ToClient
        GetStartupString,
        GetAllStartupStrings,
        CleanOutputForId,
        MarkProcessIdAsStarted,
        MarkProcessIdAsStopped,
        ChangeProcessIdExecutable,
        ProcessIdNewOutput,
        ProcessIdFullOutput,
    }

    public enum GoodCheckMessageIds
    {
        // ToClient
        Runned,
        Died,
        DiedViaError,
        // ToServer
        Start,
        Stop,
    }

    public enum UtilsMessageIds
    {
        GrantAccessRequest,
        GrantAccessResponse,
    }

    public enum SettingsMessageIds
    {
        // ToClient
        AutorunFalse,
        // ToServer
        AddToAutorun,
        RemoveFromAutorun,
        ReloadSettings,
        ComponentSetupFinished,
        ComponentSetupNotFinished,
        ComponentNotInstalled,
    }

    public enum UpdateMessageIds
    {
        // ToClient
        CheckForUpdates,
        // ToServer
        BeginApplicationUpdate,
        UpdatesAreAvailable
    }

    public enum MSIInstallationMessageIds
    {
        // ToClient
        SetOperationStatus,
        RemoveOperationId,
        // ToServer
        Begin,
        Kill
    }

    public enum CompatibilityCheckMessageIds
    {
        Begin
    }

    public enum ProxyMessageIds
    {
        Init,
        Setup,
        Clean
    }

    public enum NotificationsMessageIds
    {
        ProxySetupRequired,
        CompatibilityCheckAssistant
    }

    public enum ApplicationMessageIds
    {
        HardRestart,
        CloseApplicationUI
    }
}
