using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Kdl.Core
{
    /*
    public record PlayerMove(int PlayerId, int DestRoomId)
    {
        public override string ToString()
            => (PlayerId + 1) + "@" + DestRoomId;
    }
    */

    public struct PlayerMove
    {
        public int PlayerId { get; init; }
        public int DestRoomId { get; init; }

        public PlayerMove(int playerId, int destRoomId)
        {
            PlayerId = playerId;
            DestRoomId = destRoomId;
        }

        public override string ToString()
            => (PlayerId + 1) + "@" + DestRoomId;
    }

    public struct AppraisedPlayerTurn<TTurn,TGameState>
        where TTurn : ITurn
        where TGameState : IGameState<TTurn,TGameState>
    {
        public double Appraisal { get; set; }
        public TTurn Turn;
        public TGameState EndingState { get; set; }

        public AppraisedPlayerTurn(
            double appraisal,
            TTurn turn,
            TGameState state)
        {
            Appraisal = appraisal;
            Turn = turn;
            EndingState = state;
        }

        public override string ToString() => Turn + "^" + Appraisal;
    }

    public static class PlayerMoveExtensions
    {
        public static string ToNiceString(this IEnumerable<PlayerMove> moves)
            => string.Join<PlayerMove>(" ", moves) + ';';
    }
}
