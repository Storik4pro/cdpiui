using CDPIUI.Core.Items;
using CDPIUI.Shared.Pipe.Models;
using CDPIUI.Helper.Basic;
using CDPIUI.AddOns.GoodCheck;
using CDPIUI.AddOns.Troubleshooting;
using CDPIUI.Core.Features;
using System.Diagnostics;


namespace CDPIUI.Commands
{
    internal class CommandsHandler
    {
        public static bool HandleCommand(IPipeMessage message)
        {
            switch (message)
            {
                case PresentationMessageModel model:
                    HandlePresentationMessage(model);
                    break;

                case GoodCheckMessageModel model:
                    HandleGoodCheckMessage(model);
                    break;

                case UtilsMessageModel model:
                    HandleUtilsMessage(model);
                    break;

                case UpdateMessageModel model:
                    HandleUpdateMessage(model);
                    break;

                case CompatibilityCheckMessageModel model:
                    HandleCompatibilityCheckMessage(model);
                    break;
                default:
                    return false;
            }
            return true;
        }

        private static async void HandlePresentationMessage(PresentationMessageModel model)
        {
            if (model.MessageType != PresentationMessageIds.ShowWindow)
                return;

            await WindowOpenHelper.OpenAsync(model.MessageData);
        }
        private static void HandleGoodCheckMessage(GoodCheckMessageModel model)
        {
            switch (model.MessageType)
            {
                case GoodCheckMessageIds.Runned:
                    return;

                case GoodCheckMessageIds.Died:
                    {
                        if (!int.TryParse(model.MessageData?["operationId"], out var id))
                            return;

                        GoodCheckProcessService.Instance.OperationWithIdDied(id);
                        return;
                    }

                case GoodCheckMessageIds.DiedViaError:
                    {
                        var error = model.MessageData?["error"];

                        if (!int.TryParse(model.MessageData?["operationId"], out var id))
                            return;

                        GoodCheckProcessService.Instance.HandleProcessException(error!, id);
                        return;
                    }
            }
        }

        private static void HandleUtilsMessage(UtilsMessageModel model)
        {
            switch (model.MessageType)
            {
                case UtilsMessageIds.GrantAccessResponse:
                    {
                        if (!bool.TryParse(model.MessageData?["result"], out var granted))
                            return;

                        TroubleshootingService.Instance.OnGrantAccessCompleted(granted);
                        return;
                    }
            }
        }

        private static void HandleUpdateMessage(UpdateMessageModel model)
        {
            switch (model.MessageType)
            {
                case UpdateMessageIds.CheckForUpdates:
                    _ = ApplicationUpdate.Instance.CheckForUpdates(notify: true);
                    return;
            }
        }

        private static void HandleCompatibilityCheckMessage(CompatibilityCheckMessageModel model)
        {
            switch (model.MessageType)
            {
                case CompatibilityCheckMessageIds.Begin:
                    _ = CompatibilityCheckHelper.Instance.BeginCheck();
                    return;
            }
        }
    }
}
