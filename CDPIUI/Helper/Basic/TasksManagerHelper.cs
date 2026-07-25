using CDPIUI.Commands;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Shared.Pipe.Models;

namespace CDPIUI.Helper.Basic
{
    public class TasksManagerHelper // TODO: init
    {
        private static TasksManagerHelper? _instance;
        private static readonly object _lock = new();
        public static TasksManagerHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new TasksManagerHelper();
                    return _instance;
                }
            }
        }

        private TasksManagerHelper() 
        {
            ComponentTasksManager.Instance.ShowErrorMessageForTaskId += HandleErrorMessage;
        }

        private void HandleErrorMessage(string id)
        {
            CommandsHandler.HandleCommand(
                new PresentationMessageModel()
                {
                    MessageType = PresentationMessageIds.ShowWindow,
                    MessageData = new()
                    {
                        { "windowName", "ViewWindow" }
                    }
                });
        } 
    }
}
