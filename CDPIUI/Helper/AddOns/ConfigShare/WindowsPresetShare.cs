using CDPIUI.AddOns.ConfigShare;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using WinRT;
using WinRT.Interop;

namespace CDPIUI.Helper.AddOns.ConfigShare;

internal static class WindowsPresetShare
{
    // Desktop sharing requires the owner HWND, including in an unpackaged WinUI app.
    // https://learn.microsoft.com/windows/apps/develop/ui/display-ui-objects
    [ComImport, Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataTransferManagerInterop
    {
        IntPtr GetForWindow(IntPtr window, in Guid iid);
        void ShowShareUIForWindow(IntPtr window);
    }

    internal static async Task ShareAsync(Window owner, ConfigSharePackage package, CancellationToken cancellationToken)
    {
        StorageFile file = await StorageFile.GetFileFromPathAsync(package.ArchivePath);
        var interop = DataTransferManager.As<IDataTransferManagerInterop>();
        IntPtr window = WindowNative.GetWindowHandle(owner);
        Guid iid = new("A5CAEE9B-8708-49D1-8D36-67D25A8DA00C");
        IntPtr pointer = interop.GetForWindow(window, iid);
        DataTransferManager manager;
        try { manager = MarshalInterface<DataTransferManager>.FromAbi(pointer); }
        finally { Marshal.Release(pointer); }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DataPackage data = null;
        // The archive remains available until the next export dialog, including after cancellation.
        using var cancellation = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        TypedEventHandler<DataPackage, ShareCompletedEventArgs> completed = (_, _) => completion.TrySetResult();
        TypedEventHandler<DataPackage, object> canceled = (_, _) => completion.TrySetResult();
        TypedEventHandler<DataPackage, OperationCompletedEventArgs> operationCompleted = (_, _) => completion.TrySetResult();
        void Requested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            try
            {
                data = args.Request.Data;
                data.Properties.Title = package.Manifest.Name;
                data.Properties.Description = package.Manifest.Developer;
                data.RequestedOperation = DataPackageOperation.Copy;
                data.SetStorageItems(new[] { file });
                data.ShareCompleted += completed;
                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)) data.ShareCanceled += canceled;
                data.OperationCompleted += operationCompleted;
            }
            catch (Exception exception)
            {
                args.Request.FailWithDisplayText(ConfigShareUI.ErrorText(exception));
                completion.TrySetException(exception);
            }
        }
        void Closed(object sender, WindowEventArgs args) => completion.TrySetResult();
        manager.DataRequested += Requested;
        owner.Closed += Closed;
        try
        {
            interop.ShowShareUIForWindow(window);
            await completion.Task;
        }
        finally
        {
            manager.DataRequested -= Requested;
            owner.Closed -= Closed;
            if (data != null)
            {
                data.ShareCompleted -= completed;
                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)) data.ShareCanceled -= canceled;
                data.OperationCompleted -= operationCompleted;
            }
        }
    }
}
