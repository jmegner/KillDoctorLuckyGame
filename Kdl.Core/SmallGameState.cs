using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Util;

namespace Kdl.Core
{
    public record CommonGameState(
        bool IsLogEnabled,
        Board Board,
        int NumNormalPlayers,
        int NumAllPlayers)
    {
        public CommonGameState(
            bool isLogEnabled,
            Board board,
            int numNormalPlayers)
            : this(
                isLogEnabled,
                board,
                numNormalPlayers,
                RuleHelper.NumAllPlayers(numNormalPlayers))
        {
        }

        public PlayerType GetPlayerType(int playerId)
            => NumNormalPlayers == RuleHelper.NumNormalPlayersWhenHaveStrangers && (playerId % 2 == 1)
            ? PlayerType.Stranger
            : PlayerType.Normal;

        public static int ToPlayerId(int playerDisplayNum) => playerDisplayNum - 1;
        public static int ToPlayerDisplayNum(int playerId) => playerId + 1;

        public string PlayerText(int playerId)
            => (GetPlayerType(playerId) == PlayerType.Normal ? "P" : "p")
            + ToPlayerDisplayNum(playerId);

        public IEnumerable<int> PlayerIds => NumAllPlayers.ToRange();
    }

    public record SmallGameState(
        CommonGameState Common,
        int TurnId,
        int CurrentPlayerId,
        int DoctorRoomId,
        ImmutableArray<int> PlayerRoomIds,
        ImmutableArray<double> PlayerMovePoints,
        ImmutableArray<bool> PlayersHadTurn,
        ImmutableArray<int> AttackerHist,
        SmallGameState PreviousGameState)
    {
        public static SmallGameState AtStart(CommonGameState common)
        {
            var game = new SmallGameState(
                common,
                1,
                0,
                common.Board.DoctorStartRoomId,
                common.NumAllPlayers.Times(common.Board.PlayerStartRoomId).ToImmutableArray(),
                common.NumAllPlayers.Times((double)RuleHelper.SuperSimple.PlayerStartingMovePoints).ToImmutableArray(),
                common.NumAllPlayers.Times(false).ToImmutableArray(),
                ImmutableArray<int>.Empty,
                null );
            return game;
        }

        public double AttackScore(int playerId = 0)
            => RuleHelper.SuperSimple.Score(
                playerId,
                Common.NumNormalPlayers,
                AttackerHist);

        public double AttackScore(int playerId, out double gainFromAttackingNext)
            => RuleHelper.SuperSimple.Score(
                playerId,
                Common.NumNormalPlayers,
                AttackerHist,
                out gainFromAttackingNext);

        public PlayerType CurrentPlayerType => Common.GetPlayerType(CurrentPlayerId);

        public string PlayerText() => PlayerText(CurrentPlayerId);
        public string PlayerText(int playerId) => Common.PlayerText(playerId);

        public string PlayerTextLong(int playerId)
            => Common.PlayerText(playerId)
            + "(R" + PlayerRoomIds[playerId].ToString("D2")
            + (Common.GetPlayerType(playerId) == PlayerType.Stranger ? "" : ",M" + PlayerMovePoints[playerId])
            + ")";

        public string Summary(int indentationLevel)
        {
            return Summary(Util.Print.Indentation(indentationLevel));
        }

        public string Summary(string leadingText = "")
        {
            var sb = new StringBuilder();
            sb.Append($"{leadingText}Turn {TurnId}, {PlayerText()}, P1Score={AttackScore():N2}");
            sb.Append($"\n{leadingText}  AttackHist={{{string.Join(',', AttackerHist.Select(CommonGameState.ToPlayerDisplayNum))}}}");
            sb.Append($"\n{leadingText}  Doctor@R{DoctorRoomId}");

            var playersWhoCanSeeDoctor = Common.PlayerIds.Zip(PlayerRoomIds)
                .Where(x => Common.Board.Sight[x.Second, DoctorRoomId])
                .Select(x => CommonGameState.ToPlayerDisplayNum(x.First));

            if (playersWhoCanSeeDoctor.Any())
            {
                sb.Append(", seen by players{" + string.Join(',', playersWhoCanSeeDoctor) + "}");
            }
            else
            {
                sb.Append(", unseen by players");
            }

            for (int playerId = 0; playerId < Common.NumAllPlayers; playerId++)
            {
                sb.Append($"\n{leadingText}  {PlayerTextLong(playerId)}");

                if (playerId == CurrentPlayerId)
                {
                    sb.Append(" current player");
                }
            }

            var text = sb.ToString();
            return text;
        }

        public bool CheckNormalTurn(IEnumerable<PlayerMove> moves, out string errorMsg)
        {
            var totalDist = moves.Sum(move => Common.Board.Distance[PlayerRoomIds[move.PlayerId], move.DestRoomId]);

            if (PlayerMovePoints[CurrentPlayerId] < totalDist - 1)
            {
                errorMsg = $"player {PlayerText()} used too many move points ({totalDist})";
                return false;
            }

            var movingPlayerIds = new List<int>();

            foreach (var move in moves)
            {
                if(move.PlayerId >= PlayerRoomIds.Length)
                {
                    errorMsg = $"invalid player ({PlayerText(move.PlayerId)} in move";
                    return false;
                }

                if (move.PlayerId != CurrentPlayerId && Common.GetPlayerType(move.PlayerId) != PlayerType.Stranger)
                {
                    errorMsg = $"player {PlayerText()} tried to move non-stranger {PlayerText(move.PlayerId)}";
                    return false;
                }
            }

            errorMsg = null;
            return true;
        }

        public SmallGameState AfterNormalTurn(IEnumerable<PlayerMove> moves, bool wantLog = false)
        {
            // no turn validity checking; that is done in other method

            if(wantLog)
            {
                Console.WriteLine($"Turn {TurnId}, {PlayerText()}");
                Console.WriteLine($"  at start...");
                Console.WriteLine(Summary(Util.Print.Indentation(2)));
            }

            // move phase ======================================================

            var totalDist = moves.Sum(move => Common.Board.Distance[PlayerRoomIds[move.PlayerId], move.DestRoomId]);
            var movePointsSpent = Math.Max(0, totalDist - 1);
            var newPlayerMovePoints = PlayerMovePoints.IncrementVal(CurrentPlayerId, -movePointsSpent);

            ImmutableArray<int>.Builder newPlayerRoomIdsBuilder = null;

            if(wantLog)
            {
                Console.WriteLine($"  moves...");
            }

            foreach(var move in moves)
            {
                if(wantLog)
                {
                    var startRoomId = PlayerRoomIds[move.PlayerId];
                    var dist = Common.Board.Distance[startRoomId, move.DestRoomId];
                    Console.WriteLine($"    MOVE: {PlayerText(move.PlayerId)} from R{startRoomId} to R{move.DestRoomId} ({dist}mp)");
                }

                if(newPlayerRoomIdsBuilder == null)
                {
                    newPlayerRoomIdsBuilder = PlayerRoomIds.ToBuilder();
                }

                newPlayerRoomIdsBuilder[move.PlayerId] = move.DestRoomId;
            }

            // action phase (attack or loot) ===================================

            var newAttackerHist = AttackerHist;

            var action = BestActionAllowed(CurrentPlayerId, DoctorRoomId, newPlayerRoomIdsBuilder);

            if(action == PlayerAction.Attack)
            {
                newAttackerHist = newAttackerHist.Add(CurrentPlayerId);

                if(wantLog)
                {
                    var playerNums = newAttackerHist.Select(CommonGameState.ToPlayerDisplayNum);
                    Console.WriteLine($"  ATTACK({PlayerText()}): hist={string.Join(',', playerNums)}");
                }
            }
            else if(action == PlayerAction.Loot)
            {
                newPlayerMovePoints = newPlayerMovePoints.IncrementVal(CurrentPlayerId, RuleHelper.SuperSimple.MovePointsPerLoot);

                if(wantLog)
                {
                    Console.WriteLine($"  LOOT: {PlayerText()} has {newPlayerMovePoints[CurrentPlayerId]}mp");
                }
            }

            // doctor phase ====================================================
            DoDoctorPhase(
                newPlayerRoomIdsBuilder,
                out var newPlayersHadTurn,
                out var newCurrentPlayerId,
                out var newDoctorRoomId,
                wantLog);

            // wrap-up phase ===================================================
            var newState = new SmallGameState(
                    Common,
                    TurnId + 1,
                    newCurrentPlayerId,
                    newDoctorRoomId,
                    newPlayerRoomIdsBuilder.MoveToImmutable(),
                    newPlayerMovePoints,
                    newPlayersHadTurn,
                    newAttackerHist,
                    this);

            if(Common.GetPlayerType(newCurrentPlayerId) == PlayerType.Stranger)
            {
                // could avoid newState allocation and pass underlying variables directly
                return newState.AfterStrangerTurn(wantLog);
            }

            if(wantLog)
            {
                Console.WriteLine($"  preview of next turn...");
                Console.WriteLine(newState.Summary(Util.Print.Indentation(2)));
            }

            return newState;
        }

        public SmallGameState AfterStrangerTurn(bool wantLog)
        {
            if(wantLog)
            {
                Console.WriteLine($"Turn {TurnId}, {PlayerText()}");
                Console.WriteLine($"  at start...");
                Console.WriteLine(Summary(Util.Print.Indentation(2)));
            }

            var bestAction = BestActionAllowed(CurrentPlayerId, DoctorRoomId, PlayerRoomIds);

            // move phase ======================================================

            // stranger moves if and only if it can not attack the doctor
            var newCurrentPlayerRoomId = bestAction == PlayerAction.Attack
                ? PlayerRoomIds[CurrentPlayerId]
                : Common.Board.NextRoomId(PlayerRoomIds[CurrentPlayerId], -1);
            var newPlayerRoomIds = PlayerRoomIds.WithVal(CurrentPlayerId, newCurrentPlayerRoomId);

            // check for attack again after move
            if(bestAction != PlayerAction.Attack)
            {
                if(wantLog)
                {
                    Console.WriteLine($"  MOVE {PlayerText()} from R{PlayerRoomIds[CurrentPlayerId]} to R{newCurrentPlayerRoomId}");
                }

                bestAction = BestActionAllowed(CurrentPlayerId, DoctorRoomId, newPlayerRoomIds);
            }

            // action phase ====================================================

            var newAttackerHist = AttackerHist;

            if(bestAction == PlayerAction.Attack)
            {
                newAttackerHist = newAttackerHist.Add(CurrentPlayerId);

                if(wantLog)
                {
                    var attackerDisplayNumsText = string.Join(',', newAttackerHist.Select(CommonGameState.ToPlayerDisplayNum));
                    Console.WriteLine($"  ATTACK({PlayerText()}): allAttacks={attackerDisplayNumsText}");
                }
            }

            // doctor phase ====================================================
            DoDoctorPhase(
                newPlayerRoomIds,
                out var newPlayersHadTurn,
                out var newCurrentPlayerId,
                out var newDoctorRoomId,
                wantLog);

            // wrap-up phase ===================================================
            var newState = new SmallGameState(
                    Common,
                    TurnId + 1,
                    newCurrentPlayerId,
                    newDoctorRoomId,
                    newPlayerRoomIds,
                    PlayerMovePoints,
                    newPlayersHadTurn,
                    newAttackerHist,
                    this);

            if(Common.GetPlayerType(newCurrentPlayerId) == PlayerType.Stranger)
            {
                // could avoid newState allocation and pass underlying variables directly
                return newState.AfterStrangerTurn(wantLog);
            }

            if(wantLog)
            {
                Console.WriteLine($"  preview of next turn...");
                Console.WriteLine(newState.Summary(Util.Print.Indentation(2)));
            }

            return newState;
        }

        public void DoDoctorPhase(
            IList<int> playerRoomIds,
            out ImmutableArray<bool> newPlayersHadTurn,
            out int newCurrentPlayerId,
            out int newDoctorRoomId,
            bool wantLog)
        {
            newPlayersHadTurn = PlayersHadTurn.WithVal(CurrentPlayerId, true);
            newDoctorRoomId = Common.Board.NextRoomId(DoctorRoomId, 1);

            if(wantLog)
            {
                Console.WriteLine($"  DOCTOR MOVE: from R{DoctorRoomId} to R{newDoctorRoomId}");
            }

            // normal next player progression
            newCurrentPlayerId = (CurrentPlayerId + 1) % Common.NumAllPlayers;

            // doctor activation may override
            if (newPlayersHadTurn.All(hadTurn => hadTurn))
            {
                for (int playerOffset = 1; playerOffset <= Common.NumAllPlayers; playerOffset++)
                {
                    int playerId = (CurrentPlayerId + playerOffset) % Common.NumAllPlayers;
                    if (playerRoomIds[playerId] == newDoctorRoomId)
                    {
                        newCurrentPlayerId = playerId;

                        if (wantLog)
                        {
                            Console.WriteLine("  DOCTOR ACTIVATES: " + PlayerText(newCurrentPlayerId));
                        }

                        break;
                    }
                }
            }
        }

        public PlayerAction BestActionAllowed(
            int currentPlayerId,
            int doctorRoomId,
            IList<int> playerRoomIds)
        {
            var seenByOtherPlayers = false;
            var currentPlayerRoomId = playerRoomIds[currentPlayerId];

            for(int playerId = 0; playerId < playerRoomIds.Count; playerId++)
            {
                if(playerId != currentPlayerId && Common.Board.Sight[currentPlayerRoomId, playerRoomIds[playerId]])
                {
                    seenByOtherPlayers = true;
                    break;
                }
            }

            if(seenByOtherPlayers)
            {
                return PlayerAction.None;
            }

            if(currentPlayerRoomId == DoctorRoomId)
            {
                return PlayerAction.Attack;
            }

            return Common.Board.Sight[currentPlayerRoomId, doctorRoomId]
                ? PlayerAction.None
                : PlayerAction.Loot;
        }

        public string NormalTurnHist(bool verbose = false)
        {
            var sb = new StringBuilder();
            var states = new List<SmallGameState>();
            var stateForTraversal = this;

            string makeMoveText(int activePlayerId, int movingPlayerId, int destRoomId)
            {
                var moveText = destRoomId.ToString();

                if(verbose || activePlayerId != movingPlayerId)
                {
                    moveText = CommonGameState.ToPlayerDisplayNum(movingPlayerId) + "@" + destRoomId;
                }

                return moveText;
            }

            while(stateForTraversal != null)
            {
                states.Add(stateForTraversal);
                stateForTraversal = stateForTraversal.PreviousGameState;
            }

            states.Reverse();

            foreach(var state in states.Skip(1))
            {
                var prevState = state.PreviousGameState;

                if(prevState.CurrentPlayerType == PlayerType.Stranger)
                {
                    continue;
                }

                var moveTexts = new List<string>();

                foreach(var playerId in Common.PlayerIds)
                {
                    var prevRoomId = prevState.PlayerRoomIds[playerId];
                    var roomId = state.PlayerRoomIds[playerId];

                    if(prevRoomId != roomId)
                    {
                        moveTexts.Add(makeMoveText(prevState.CurrentPlayerId, playerId, roomId));
                    }
                }

                if(!moveTexts.Any())
                {
                    moveTexts.Add(makeMoveText(
                        prevState.CurrentPlayerId,
                        prevState.CurrentPlayerId,
                        prevState.PlayerRoomIds[prevState.CurrentPlayerId]));
                }

                if(sb.Length > 0)
                {
                    sb.Append(' ');
                }

                var movePointDiff = state.PlayerMovePoints[prevState.CurrentPlayerId]
                    - prevState.PlayerMovePoints[prevState.CurrentPlayerId];
                var didUseMovePoints = movePointDiff < 0;
                var didLoot = movePointDiff % 1.0 != 0;
                var didAttack = state.AttackerHist.Length - prevState.AttackerHist.Length != 0;

                if(verbose)
                {
                    sb.Append("(" + PlayerText(prevState.CurrentPlayerId));

                    if(didUseMovePoints || didLoot || didAttack)
                    {
                        sb.Append(' ');

                        if (didUseMovePoints) sb.Append('M');
                        if (didLoot) sb.Append('L');
                        if (didAttack) sb.Append('A');
                    }

                    sb.Append(") ");
                }

                sb.Append(string.Join(' ', moveTexts));

                sb.Append(';');
            }

            var text = sb.ToString();
            return text;
        }

        public AppraisedPlayerMove Appraise(int analysisPlayerId, int analysisLevel)
        {
            if(analysisLevel == 0)
            {
                var attackScore = AttackScore(analysisPlayerId, out var gainFromAttackingNext);
                var connectednessRatio
                    = Common.Board.AdjacencyCount[PlayerRoomIds[analysisPlayerId]]
                    / (double)Common.Board.RoomIds.Length;
                var movePointsBonus
                    = PlayerMovePoints[analysisPlayerId]
                    / (double)RuleHelper.SuperSimple.PlayerStartingMovePoints;
                var isCurrentPlayerBonus = analysisPlayerId == CurrentPlayerId ? 1 : 0;
                var doctorBonus = (PlayerRoomIds[analysisPlayerId] == DoctorRoomId
                    || PlayerRoomIds[analysisPlayerId] == Common.Board.NextRoomId(DoctorRoomId, 1))
                    ? 1 : 0;

                var fuzzyGoodnessRatio
                    = 0.70 * movePointsBonus
                    + 0.15 * isCurrentPlayerBonus
                    + 0.10 * doctorBonus
                    + 0.05 * connectednessRatio;

                var appraisal = attackScore + gainFromAttackingNext * fuzzyGoodnessRatio;
                return new AppraisedPlayerMove(appraisal);
            }

            var appraisalIsForCurrentPlayer = analysisPlayerId == CurrentPlayerId;
            var currentPlayerMoveAbility = PlayerMovePoints[CurrentPlayerId] + 1;

            var extremumAppraisal = appraisalIsForCurrentPlayer ? double.MinValue : double.MaxValue;
            var bestMove = new AppraisedPlayerMove(
                appraisalIsForCurrentPlayer ? double.MinValue : double.MaxValue);

            var movablePlayerIds = Common.NumNormalPlayers == RuleHelper.NumNormalPlayersWhenHaveStrangers
                ? new[] { CurrentPlayerId, RuleHelper.StrangerPlayerIdFirst, RuleHelper.StrangerPlayerIdSecond, }
                : new[] { CurrentPlayerId, };

            // player just moves themself OR a stranger
            foreach (var movablePlayer in movablePlayerIds)
            {
                var movablePlayerRoom = PlayerRoomIds[movablePlayer];

                foreach (var destRoom in Common.Board.RoomIds)
                {
                    if (Common.Board.Distance[movablePlayerRoom, destRoom] <= currentPlayerMoveAbility)
                    {
                        var move = new PlayerMove(movablePlayer, destRoom);
                        var hypoState = AfterNormalTurn(new[] { move });
                        var hypoAppraisedMove = hypoState.Appraise(
                            analysisPlayerId,
                            analysisLevel - 1);

                        if (appraisalIsForCurrentPlayer)
                        {
                            if(bestMove.Appraisal < hypoAppraisedMove.Appraisal)
                            {
                                bestMove = new(hypoAppraisedMove.Appraisal, move);
                            }
                        }
                        else
                        {
                            if(bestMove.Appraisal > hypoAppraisedMove.Appraisal)
                            {
                                bestMove = new(hypoAppraisedMove.Appraisal, move);
                            }
                        }
                    }
                }
            }

            return bestMove;
        }

    }
}
