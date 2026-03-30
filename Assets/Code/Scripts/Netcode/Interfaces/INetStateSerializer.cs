public interface INetStateSerializer
{
    byte[] Serialize(NetState state);
    NetState Deserialize(byte[] bytes);
}
