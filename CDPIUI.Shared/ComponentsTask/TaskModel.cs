using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.ComponentsTask
{
    public class TaskModel<T> where T:IProcessService
    {
        public required string Id { get; set; }
        public required T ProcessManager { get; set; }
        public bool? IsSetupComplete { get; set; }
    }
}
