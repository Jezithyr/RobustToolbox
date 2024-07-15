using System;
using System.Runtime.CompilerServices;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Robust.Shared.NamedEvents;

public sealed partial class NamedEventManager
{
    private void RegisterNetworkEvent<T>(NetMessageAccept accept = NetMessageAccept.Both) where T : notnull
    {
        _netManager.RegisterNetMessage<NamedEventNetMessage<T>>(HandleNetworkMessage, accept);
        _sawmill.Info($"Registered NetMessage listener for {typeof(T)} " +
                      $"WrapperType:{typeof(NamedEventNetMessage<T>)} Acceptance:{accept}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleNetworkMessage<T>(NamedEventNetMessage<T> message) where T : notnull
    {
        ReceiveNetEvent(message.EventId, ref message.Data, message.OneShot);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RaiseNetworkEventBase<T>(NamedEventId id, ref T data, bool oneShot) where T : notnull
    {
        NamedEventNetMessage<T> netMessage = new()
        {
            EventId = id,
            Data = data,
            OneShot =  oneShot
        };
        if (_netManager.IsClient)
            _netManager.ClientSendMessage(netMessage);
        else
        {
            _netManager.ServerSendToAll(netMessage);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RaiseNetworkEventBase<T>(NamedEventId id, ref T data, bool oneShot, INetChannel channel) where T : notnull
    {

        NamedEventNetMessage<T> netMessage = new()
        {
            EventId = id,
            Data = data,
            OneShot =  oneShot
        };
        _netManager.ServerSendMessage(netMessage, channel);
    }
}

[Flags]
public enum Locality : byte
{
    None = 0,
    Local = 1 << 0,
    Networked = 1 << 2,
    Both = Local | Networked,
}

public sealed class NamedEventNetMessage<T> : WrappedNetMessage<T> where T : notnull
{
    public NamedEventId EventId;
    public bool OneShot;


    public override MsgGroups MsgGroup => MsgGroups.NamedEvent;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        base.ReadFromBuffer(buffer, serializer);
        EventId = ReadTypeFromBuffer<NamedEventId>(buffer, serializer);
        OneShot = buffer.ReadBoolean();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        base.WriteToBuffer(buffer, serializer);
        WriteTypeToBuffer(EventId, buffer, serializer);
        buffer.Write(OneShot);
    }
}
