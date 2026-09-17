using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Models
{
    public interface IOperationResultModel<ResultClass, ErrorClass> where ErrorClass : class where ResultClass : class
    {
        bool Success { get; init; }

        ResultClass? Result { get; init; }

        bool ErrorHappens { get; init; }
        ErrorClass? Error { get; init; }
    }

    public interface INamedModel
    {
        /// <summary>
        /// Name of model
        /// </summary>
        string? name { get; set; }
    }
}
