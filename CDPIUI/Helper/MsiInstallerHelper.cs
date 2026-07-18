using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CDPI_UI.Helper
{
    public class MsiInstallerHelper
    {
        public enum MsiState
        {
            GettingReady,
            Installing,
            Complete,
            CompleteRestartRequest,
            ExceptionHappens
        }
        public class MsiCallback
        {
            public string operationId;
            public MsiState State;
        }

        private TaskCompletionSource<string> _tcs;
        public Action<MsiCallback> callbackAction;
        private readonly string _operationId;
        private readonly string _filename;
        private MsiState _state;

        public MsiInstallerHelper(string operationId, string filename) 
        {
            _operationId = operationId;
            _filename = filename;
        }

        public async Task<MsiCallback> Run(CancellationToken ct = default)
        {
            _tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using (cts.Token.Register(() => _tcs.TrySetCanceled()))
            {
                PipeClient.Instance.SendMsiInstallMessage(_operationId, _filename, this);
                await _tcs.Task.ConfigureAwait(false);
                return new MsiCallback()
                {
                    operationId = _operationId,
                    State = _state,
                };
            }
            
        }

        public void OnResponse(MsiState state)
        {
            callbackAction?.Invoke(new()
            {
                operationId = _operationId,
                State = state
            });
            Logger.Instance.CreateDebugLog(nameof(MsiInstallerHelper), $"STATE {state}");
            _state  = state;

            if (state == MsiState.Complete || state == MsiState.CompleteRestartRequest || state == MsiState.ExceptionHappens)
                _tcs.TrySetResult(string.Empty);
        }
    }
}
