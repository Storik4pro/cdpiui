using System;

namespace CDPIUI.Shared.Pipe.Models
{
    public interface IPipeMessage { }

    public interface IPipeMessageBase<T, Object> : IPipeMessage where T : Enum where Object : class
    {
        /// <summary>
        /// Target of message
        /// </summary>
        PipeMessageTargetIds Target { get; }

        /// <summary>
        /// Type of message
        /// </summary>
        T MessageType { get; }

        /// <summary>
        /// Data of message
        /// </summary>
        Object? MessageData { get; }
    }
}
