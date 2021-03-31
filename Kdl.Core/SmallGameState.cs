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
        SmallGameState PrevState)
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

        public bool IsNormalTurn => Common.GetPlayerType(CurrentPlayerId) == PlayerType.Normal;

        public int Ply()
        {
            int ply = 0;
            var state = this.PrevState;

            while(state != null)
            {
                if(state.IsNormalTurn)
                {
                    ply++;
                }

                state = state.PrevState;
            }

            return ply;
        }

        public PlayerType CurrentPlayerType => Common.GetPlayerType(CurrentPlayerId);

        public string PlayerText() => PlayerText(CurrentPlayerId);
        public string PlayerText(int playerId) => Common.PlayerText(playerId);

        public string PlayerTextLong(int playerId)
            => Common.PlayerText(playerId)
            + "(R" + PlayerRoomIds[playerId].ToString("D2")
            + (Common.GetPlayerType(playerId) == PlayerType.Stranger ? "" : ",M" + PlayerMovePoints[playerId])
            + ")";

        public bool PlayerSeesPlayer(int playerId1, int playerId2)
            => Common.Board.Sight[PlayerRoomIds[playerId1], PlayerRoomIds[playerId2]];

        public string Summary(int indentationLevel)
        {
            return StateSummary(Util.Print.Indentation(indentationLevel));
        }

        public string StateSummary(string leadingText = "")
        {
            var sb = new StringBuilder();
            sb.Append($"{leadingText}Turn {TurnId}, {PlayerText()}, P1Score={AttackScore():N2}");
            sb.Append($"\n{leadingText}  AttackHist={{{string.Join(',', AttackerHist.Select(CommonGameState.ToPlayerDisplayNum))}}}");
            sb.Append($"\n{leadingText}  Dr@R{DoctorRoomId}");

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
                    sb.Append(" *");
                }

                if(PlayerRoomIds[playerId] == DoctorRoomId)
                {
                    sb.Append(" D");
                }
            }

            var text = sb.ToString();
            return text;
        }

        public bool CheckNormalTurn(IEnumerable<PlayerMove> moves, out string errorMsg)
        {
            foreach(var move in moves)
            {
                if(move.PlayerId >= Common.NumAllPlayers)
                {
                    errorMsg = $"invalid playerId {move.PlayerId} (displayed {PlayerText(move.PlayerId)}";
                    return false;
                }
                else if(!Common.Board.RoomIds.Contains(move.DestRoomId))
                {
                    errorMsg = $"invalid roomId {move.DestRoomId}";
                    return false;
                }
            }
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

            // move phase ======================================================

            var totalDist = moves.Sum(move => Common.Board.Distance[PlayerRoomIds[move.PlayerId], move.DestRoomId]);
            var movePointsSpent = Math.Max(0, totalDist - 1);
            var newPlayerMovePoints = PlayerMovePoints.IncrementVal(CurrentPlayerId, -movePointsSpent);

            ImmutableArray<int>.Builder newPlayerRoomIdsBuilder = null;

            bool movedStrangerThatSawCurrentPlayer = false;

            foreach(var move in moves)
            {
                if(newPlayerRoomIdsBuilder == null)
                {
                    newPlayerRoomIdsBuilder = PlayerRoomIds.ToBuilder();
                }

                newPlayerRoomIdsBuilder[move.PlayerId] = move.DestRoomId;

                if(move.PlayerId != CurrentPlayerId && PlayerSeesPlayer(move.PlayerId, CurrentPlayerId))
                {
                    movedStrangerThatSawCurrentPlayer = true;
                }
            }

            // action phase (attack or loot) ===================================

            var newAttackerHist = AttackerHist;

            var action = BestActionAllowed(
                CurrentPlayerId,
                DoctorRoomId,
                newPlayerRoomIdsBuilder,
                movedStrangerThatSawCurrentPlayer);

            if(action == PlayerAction.Attack)
            {
                newAttackerHist = newAttackerHist.Add(CurrentPlayerId);
            }
            else if(action == PlayerAction.Loot)
            {
                newPlayerMovePoints = newPlayerMovePoints.IncrementVal(CurrentPlayerId, RuleHelper.SuperSimple.MovePointsPerLoot);
            }

            // doctor phase ====================================================
            DoDoctorPhase(
                newPlayerRoomIdsBuilder,
                out var newPlayersHadTurn,
                out var newCurrentPlayerId,
                out var newDoctorRoomId);

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

            if(wantLog)
            {
                Console.WriteLine(newState.PrevTurnSummary(true));
            }

            if(Common.GetPlayerType(newCurrentPlayerId) == PlayerType.Stranger)
            {
                // could avoid newState allocation and pass underlying variables directly
                return newState.AfterStrangerTurn(wantLog);
            }

            return newState;
        }

        public SmallGameState AfterStrangerTurn(bool wantLog)
        {
            var bestAction = BestActionAllowed(CurrentPlayerId, DoctorRoomId, PlayerRoomIds, false);

            // move phase ======================================================

            // stranger moves if and only if it can not attack the doctor
            var newCurrentPlayerRoomId = bestAction == PlayerAction.Attack
                ? PlayerRoomIds[CurrentPlayerId]
                : Common.Board.NextRoomId(PlayerRoomIds[CurrentPlayerId], -1);
            var newPlayerRoomIds = PlayerRoomIds.WithVal(CurrentPlayerId, newCurrentPlayerRoomId);

            // check for attack again after move
            if(bestAction != PlayerAction.Attack)
            {
                bestAction = BestActionAllowed(CurrentPlayerId, DoctorRoomId, newPlayerRoomIds, false);
            }

            // action phase ====================================================

            var newAttackerHist = AttackerHist;

            if(bestAction == PlayerAction.Attack)
            {
                newAttackerHist = newAttackerHist.Add(CurrentPlayerId);
            }

            // doctor phase ====================================================
            DoDoctorPhase(
                newPlayerRoomIds,
                out var newPlayersHadTurn,
                out var newCurrentPlayerId,
                out var newDoctorRoomId);

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
                Console.WriteLine(newState.PrevTurnSummary(true));
            }

            return newState;
        }

        public void DoDoctorPhase(
            IList<int> playerRoomIds,
            out ImmutableArray<bool> newPlayersHadTurn,
            out int newCurrentPlayerId,
            out int newDoctorRoomId)
        {
            newPlayersHadTurn = PlayersHadTurn.WithVal(CurrentPlayerId, true);
            newDoctorRoomId = Common.Board.NextRoomId(DoctorRoomId, 1);

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
                        break;
                    }
                }
            }
        }

        public PlayerAction BestActionAllowed(
            int currentPlayerId,
            int doctorRoomId,
            IList<int> playerRoomIds,
            bool movedStrangerThatSawCurrentPlayer)
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

            if(currentPlayerRoomId == DoctorRoomId && !movedStrangerThatSawCurrentPlayer)
            {
                return PlayerAction.Attack;
            }

            return Common.Board.Sight[currentPlayerRoomId, doctorRoomId]
                ? PlayerAction.None
                : PlayerAction.Loot;
        }

        public string NormalTurnHist()
        {
            var sb = new StringBuilder("r; ");
            var states = new List<SmallGameState>();
            var stateForTraversal = this;

            while(stateForTraversal != null)
            {
                states.Add(stateForTraversal);
                stateForTraversal = stateForTraversal.PrevState;
            }

            states.Reverse();

            foreach(var state in states.Skip(1))
            {
                var prevState = state.PrevState;

                if(prevState.CurrentPlayerType == PlayerType.Stranger)
                {
                    continue;
                }

                sb.Append(state.PrevTurnSummary() + ' ');
            }

            var text = sb.ToString();
            return text;
        }

        protected string PrevTurnSummary(bool verbose = false)
        {
            // non-verbose summary: (P1MA)1@24(21)

            var prevPlayer = PrevState.CurrentPlayerId;
            var verboseMoveTexts = new List<string>();
            var shortMoveTexts = new List<string>();
            var totalDist = 0;

            foreach(var playerId in Common.PlayerIds)
            {
                var prevRoomId = PrevState.PlayerRoomIds[playerId];
                var roomId = PlayerRoomIds[playerId];

                if(prevRoomId != roomId)
                {
                    var dist = Common.Board.Distance[prevRoomId, roomId];
                    var distText = dist == 0 ? "" : $" ({dist}mp)";

                    totalDist += dist;
                    shortMoveTexts.Add($"{CommonGameState.ToPlayerDisplayNum(playerId)}@{roomId}({prevRoomId})");
                    verboseMoveTexts.Add($"    MOVE {PlayerText(playerId)}: R{prevRoomId} to R{roomId}{distText}");
                }
            }

            if(!shortMoveTexts.Any())
            {
                var roomId = PlayerRoomIds[prevPlayer];
                shortMoveTexts.Add($"{CommonGameState.ToPlayerDisplayNum(prevPlayer)}@{roomId}({roomId})");
                verboseMoveTexts.Add($"    MOVE {PlayerText(prevPlayer)}: stayed at R{roomId}");
            }

            var action = PlayerAction.None;

            if(PrevState.AttackerHist.Length != AttackerHist.Length)
            {
                action = PlayerAction.Attack;
            }
            else if(PrevState.PlayerMovePoints[prevPlayer] % 1 != PlayerMovePoints[prevPlayer] % 1)
            {
                action = PlayerAction.Loot;
            }

            var moveSignifier = new string('M', Math.Max(0, totalDist - 1));
            var actionSignifier = action == PlayerAction.None ? "" : action.ToString()[0].ToString();

            var shortSummary
                = "(" + PlayerText(prevPlayer) + moveSignifier + actionSignifier
                + ")" + string.Join(' ', shortMoveTexts)
                + ";";

            if(!verbose)
            {
                return shortSummary;
            }

            var sb = new StringBuilder();
            sb.Append($"  Turn{PrevState.TurnId}, Ply{PrevState.Ply()}, {shortSummary}");
            verboseMoveTexts.ForEach(x => sb.Append('\n' + x));

            if(action == PlayerAction.Loot)
            {
                sb.Append($"\n    LOOT {PlayerText(prevPlayer)}: now " + PlayerMovePoints[prevPlayer] + "mp");
            }
            else if(action == PlayerAction.Attack)
            {
                sb.Append($"\n    ATTACK: hist=" + string.Join(',', AttackerHist.Select(CommonGameState.ToPlayerDisplayNum)));
            }

            sb.Append($"\n    DR MOVE: R{PrevState.DoctorRoomId} to R{DoctorRoomId}");

            if(DoctorRoomId == PlayerRoomIds[CurrentPlayerId])
            {
                var otherPlayersInRoom = Common.PlayerIds
                    .Where(pid => pid != CurrentPlayerId && PlayerRoomIds[pid] == DoctorRoomId)
                    .Select(CommonGameState.ToPlayerDisplayNum);
                var unactivatedPlayersText = otherPlayersInRoom.Any()
                    ? ", unactivated players{" + string.Join(',', otherPlayersInRoom) + "}"
                    : "";
                sb.Append($"\n    DR ACTIVATE: {PlayerText()}{unactivatedPlayersText}");
            }

            //if(Common.GetPlayerType(CurrentPlayerId) == PlayerType.Normal)
            {
                sb.Append("\n    start of next turn...\n");
                sb.Append(StateSummary(Util.Print.Indentation(3)));
            }

            return sb.ToString();
        }

        public static long AppraiseExecCount = 0;

        public AppraisedPlayerMove Appraise(int analysisPlayerId, int analysisLevel)
        {
            AppraiseExecCount++;

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
                var opponentBasedFuzzyMultiplier = Common.NumNormalPlayers == 2 ? 0.5 : 1;

                var appraisal = attackScore + opponentBasedFuzzyMultiplier * gainFromAttackingNext * fuzzyGoodnessRatio;
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

//#error TODO: have current player maximize their own appraisal, but re-appraise for analysisPlayer?
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
