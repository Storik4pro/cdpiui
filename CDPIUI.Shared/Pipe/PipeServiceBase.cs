using CDPIUI.Shared.Logger;
using CDPIUI.Shared.Pipe.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CDPIUI.Shared.Pipe
{
    public class PipeServiceBase : IDisposable
    {
        protected PipeStream? PipeStream;
        protected StreamString? StreamString;

        protected CancellationTokenSource? CancellationTokenSource;
        protected CancellationToken? CancellationToken;

        protected ILogger? Logger;

        public event Action? Connected;
        public event Action? Disconnected;

        public bool IsConnected => PipeStream?.IsConnected ?? false;

        protected void CreateCancellationToken()
        {
            CancellationTokenSource = new CancellationTokenSource();
            CancellationToken = CancellationTokenSource.Token;
        }

        protected PipeServiceBase()
        {

        }

        private readonly SemaphoreSlim SendLock = new(1, 1);

        public async Task HandleConnectionAsync()
        {
            if (PipeStream == null) return;

            StreamString = new StreamString(PipeStream);

            _ = SendConnectionPacket();

            while (PipeStream.IsConnected && !(CancellationToken?.IsCancellationRequested ?? true))
            {
                string message;
                try
                {
                    message = await StreamString.ReadStringAsync();

                    if (string.IsNullOrEmpty(message))
                    {
                        continue;
                    }

                    RunStringMessageActions(message);
                }
                catch (EndOfStreamException)
                {
                    continue;
                }

                Logger?.CreateInfoLog(nameof(PipeServiceBase), $"Received message: {message}");
            }
            Logger?.CreateInfoLog(nameof(PipeServiceBase), $"{CancellationToken?.IsCancellationRequested}, {PipeStream.IsConnected.ToString()}");
        }

        public async void SendMessage(string message)
        {
            await SendMessageAsync(message);
        }
        public async Task<bool> SendMessageAsync(string message)
        {
            return await SendStringMessageToPipe(message);
        }

        protected virtual async Task SendConnectionPacket()
        {
            if (PipeStream == null) return;

            await SendMessageAsync(ServiceMessageModel.ConnectionSuccessful().ToString());
        }

        private async void RunStringMessageActions(string message)
        {
            try
            {
                var model = PipeModelConvertor.ConvertBack(message);
                if (model != null) await RunMessageActions(model);
            }
            catch (Exception ex)
            {
                Logger?.CreateWarningLog(nameof(PipeServiceBase), "Exception " + ex.Message);
            }
        }

        protected virtual async Task RunMessageActions(IPipeMessage model)
        {
            if (model is ServiceMessageModel serviceMessageModel && 
                serviceMessageModel.MessageType == ServiceMessageIds.AuthOK)
            {
                Connected?.Invoke();
            }

            await Task.CompletedTask;
        }

        private async Task<bool> SendStringMessageToPipe(string message)
        {
            Logger?.CreateDebugLog(nameof(PipeServiceBase), $"Message \"{message}\" added to send queue");

            if (PipeStream == null || !PipeStream.IsConnected || StreamString == null)
            {
                Logger?.CreateInfoLog(nameof(PipeServiceBase), "Cannot send any message. Connect PIPE first!");
                return false;
            }

            await SendLock.WaitAsync();

            try
            {
                await StreamString.WriteStringAsync(message, default);
                return true;
            }
            catch (Exception ex)
            {
                Logger?.CreateWarningLog(nameof(PipeServiceBase), $"Message \"{message}\" cannot be sended during exception {ex.Message}");
                return false; 
            }
            finally
            {
                SendLock.Release();
            }
        }

        protected void NotifyConnected() => Connected?.Invoke();
        protected void NotifyDisconnected() => Disconnected?.Invoke();

        public virtual void Dispose()
        {
            Disconnected?.Invoke();
            PipeStream?.WaitForPipeDrain();
            PipeStream?.Dispose();
            StreamString = null;

            if (CancellationTokenSource != null)
            {
                CancellationTokenSource.Cancel();
                CancellationTokenSource.Dispose();
                CancellationTokenSource = null;
                CancellationToken = null;
            }
        }
    }
}
