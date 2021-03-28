namespace Kdl.Core
{
    public record PlayerMove(int PlayerId, int DestRoomId)
    {
        public override string ToString()
            => (PlayerId + 1) + "@" + DestRoomId;
    }
}
