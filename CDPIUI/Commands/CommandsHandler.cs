using CDPIUI.Shared.Pipe.Models;
using CDPIUI.AddOns.GoodCheck;
using CDPIUI.AddOns.Troubleshooting;
using CDPIUI.Core.Features;
using System.Diagnostics;
using CDPIUI.Helper.WindowHelper;
using CDPIUI.Helper.Items;
using System.Threading.Tasks;


namespace CDPIUI.Commands
{
    internal class CommandsHandler
    {
        public static bool HandleCommand(string commandUri)
        {
            var message = CommandUriMapper.ConvertBack(commandUri);
            return message != null && HandleCommand(message);
        }

        public static bool HandleCommand(IPipeMessage message)
        {
            if (!CanHandle(message))
                return false;

            _ = HandleCommandAsync(message);
            return true;
        }

        public static async Task<bool> HandleCommandAsync(string commandUri)
        {
            var message = CommandUriMapper.ConvertBack(commandUri);
            return message != null && await HandleCommandAsync(message);
        }

        public static async Task<bool> HandleCommandAsync(IPipeMessage message)
        {
            switch (message)
            {
                case PresentationMessageModel model:
                    await HandlePresentationMessage(model);
                    break;

                case GoodCheckMessageModel model:
                    HandleGoodCheckMessage(model);
                    break;

                case UtilsMessageModel model:
                    HandleUtilsMessage(model);
                    break;

                case UpdateMessageModel model:
                    await HandleUpdateMessage(model);
                    break;

                case CompatibilityCheckMessageModel model:
                    await HandleCompatibilityCheckMessage(model);
                    break;
                default:
                    return false;
            }
            return true;
        }

        private static bool CanHandle(IPipeMessage message) =>
            message is PresentationMessageModel or
                GoodCheckMessageModel or
                UtilsMessageModel or
                UpdateMessageModel or
                CompatibilityCheckMessageModel;

        private static async Task HandlePresentationMessage(PresentationMessageModel model)
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

        private static async Task HandleUpdateMessage(UpdateMessageModel model)
        {
            switch (model.MessageType)
            {
                case UpdateMessageIds.CheckForUpdates:
                    await ApplicationUpdate.Instance.CheckForUpdates(notify: true);
                    return;
            }
        }

        private static async Task HandleCompatibilityCheckMessage(CompatibilityCheckMessageModel model)
        {
            switch (model.MessageType)
            {
                case CompatibilityCheckMessageIds.Begin:
                    await CompatibilityCheckHelper.Instance.BeginCheck();
                    return;
            }
        }
    }
}
