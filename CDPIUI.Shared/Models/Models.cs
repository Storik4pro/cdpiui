using CDPIUI.Shared.PrettyErrorConvertionService;
using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Models
{
    public class EmptyResult { }

    public class OperationResultModelBase<T, Q, TSelf> : IOperationResultModel<T, Q> where T : class where Q : class where TSelf : OperationResultModelBase<T, Q, TSelf>, new()
    {
        public bool Success { get; init; }

        public T? Result { get; init; }

        public bool ErrorHappens { get; init; }
        public Q? Error { get; init; }

        public static TSelf SuccessResult() => new() { Success = true, ErrorHappens = false };
        public static TSelf SuccessResult(T result) => new() { Success = true, ErrorHappens = false, Result = result };
        public static TSelf UnSuccessResult() =>
            new() { Success = false, ErrorHappens = false };
        public static TSelf FailureResult(Q? error) =>
            new() { Success = false, ErrorHappens = true, Error = error };
    }

    public class UnprocessedOperationResultModel<T> : OperationResultModelBase<T, Exception, UnprocessedOperationResultModel<T>> where T : class
    {
        public UnprocessedOperationResultModel<EmptyResult> ToEmptyResult() => new() { Success = Success, ErrorHappens = ErrorHappens, Error = Error };
    }

    public class OperationResultModel<T> : OperationResultModelBase<T, ErrorModel, OperationResultModel<T>> where T : class
    {
        public OperationResultModel<EmptyResult> ToEmptyResult() => new() { Success = Success, ErrorHappens = ErrorHappens, Error = Error };
    }
}
