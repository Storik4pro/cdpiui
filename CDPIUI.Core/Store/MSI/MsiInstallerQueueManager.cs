using CDPIUI.Core.Communication;
using CDPIUI.Shared.Pipe.Models;
using static CDPIUI.Core.Store.MSI.MsiInstallerService;

namespace CDPIUI.Core.Store.MSI
{
    public class MsiInstallerQueueManager
    {
        private static MsiInstallerQueueManager? _instance;
        private static readonly object _lock = new object();

        public static MsiInstallerQueueManager Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new MsiInstallerQueueManager();
                    return _instance;
                }
            }
        }

        private class MsiInstallerModel
        {
            public string? OperationId { get; set; }
            public MsiInstallerService? MsiInstallerHelper { get; set; }
        }

        private List<MsiInstallerModel> installerModels = [];

        public void SendMsiInstallMessage(string operationId, string filename, MsiInstallerService installerHelper)
        {
            installerModels.Add(new MsiInstallerModel
            {
                OperationId = operationId,
                MsiInstallerHelper = installerHelper
            });
            _ = PipeHelper.SendMSIPacket(MSIInstallationMessageIds.Begin, operationId, filename);
        }

        public void RemoveMsiInstallerModel(string operationId, bool notify = true)
        {
            MsiInstallerModel msiInstallerModel = installerModels.FirstOrDefault(i => i.OperationId == operationId);
            if (msiInstallerModel != null)
            {
                installerModels.Remove(msiInstallerModel);
                if (notify) _ = PipeHelper.SendMSIPacket(MSIInstallationMessageIds.Kill, operationId);
            }
        }

        public void GetMsiInstallerMessage(string operationId, MsiState message)
        {
            MsiInstallerModel msiInstallerModel = installerModels.FirstOrDefault(i => i.OperationId == operationId);
            if (msiInstallerModel != null)
            {
                msiInstallerModel.MsiInstallerHelper.OnResponse(message);
            }
        }
    }
}
