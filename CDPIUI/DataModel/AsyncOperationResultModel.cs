using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.DataModel
{
    public class AsyncOperationResultModel
    {
        public bool IsSuccess { get; set; }

        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }

        public object Result { get; set; } 
    }
}
