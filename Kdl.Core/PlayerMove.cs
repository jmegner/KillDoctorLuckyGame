﻿using System.Collections;
using System.Collections.Generic;

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

    public struct AppraisedPlayerMove
    {
        public PlayerMove Move { get; set; }
        public double Appraisal { get; set; }

        public AppraisedPlayerMove(
            double appraisal,
            PlayerMove move)
        {
            Move = move;
            Appraisal = appraisal;
        }

        public AppraisedPlayerMove(double appraisal)
            : this(appraisal, new PlayerMove(-1, -1))
        {
        }

        public override string ToString() => Move + "^" + Appraisal;
    }

    public static class PlayerMoveExtensions
    {
        public static string ToNiceString(this IEnumerable<PlayerMove> moves)
            => string.Join<PlayerMove>(" ", moves) + ';';
    }
}
