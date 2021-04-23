using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Util;

namespace Kdl.Core
{
    public record CommonGameState(
        bool IsLogEnabled,
        Board Board,
        int NumNormalPlayers,
        int NumAllPlayers)
        : IEquatable<CommonGameState>
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

        public virtual bool Equals(CommonGameState other)
            => other != null
            && Board.Name == other.Board.Name
            && NumNormalPlayers == other.NumNormalPlayers
            && NumAllPlayers == other.NumAllPlayers;

        public override int GetHashCode() => Board.GetHashCode() ^ NumNormalPlayers.GetHashCode();

        public bool HasStrangers => NumNormalPlayers == RuleHelper.NumNormalPlayersWhenHaveStrangers;

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

        public int ToNormalPlayerId(int playerId) => RuleHelper.ToNormalPlayerId(playerId, NumNormalPlayers);
    }

    public record ImmutableGameState(
        CommonGameState Common,
        int TurnId,
        int CurrentPlayerId,
        int DoctorRoomId,
        ImmutableArray<int> PlayerRoomIds,
        ImmutableArray<bool> PlayersHadTurn,
        ImmutableArray<double> PlayerMoveCards,
        ImmutableArray<double> PlayerWeapons,
        ImmutableArray<double> PlayerFailures,
        ImmutableArray<int> PlayerStrengths,
        ImmutableArray<int> AttackerHist,
        int Winner,
        SimpleTurn PrevTurn,
        ImmutableGameState PrevState)
        : IGameState<SimpleTurn,ImmutableGameState>
    {
        public static ImmutableGameState AtStart(CommonGameState common)
        {
            ImmutableArray<T> playerVals<T>(T val) => common.NumAllPlayers.Times(val).ToImmutableArray();
            var game = new ImmutableGameState(
                Common:           common,
                TurnId:           1,
                CurrentPlayerId:  0,
                DoctorRoomId:     common.Board.DoctorStartRoomId,
                PlayerRoomIds:    playerVals(common.Board.PlayerStartRoomId),
                PlayersHadTurn:   playerVals(false),
                PlayerMoveCards:  playerVals(RuleHelper.Simple.PlayerStartingMoveCards),
                PlayerWeapons:    playerVals(RuleHelper.Simple.PlayerStartingWeapons),
                PlayerFailures:   playerVals(RuleHelper.Simple.PlayerStartingFailures),
                PlayerStrengths:  playerVals(RuleHelper.PlayerStartingStrength),
                AttackerHist:     ImmutableArray<int>.Empty,
                Winner:           RuleHelper.InvalidPlayerId,
                PrevTurn:         null,
                PrevState:        null);
            return game;
        }

        public virtual bool Equals(ImmutableGameState other)
        {
            return other != null
                && Common.Equals(other.Common)
                && CurrentPlayerId == other.CurrentPlayerId
                && DoctorRoomId == other.DoctorRoomId
                && PlayerRoomIds.SequenceEqual(other.PlayerRoomIds)
                && PlayersHadTurn.SequenceEqual(other.PlayersHadTurn)
                && PlayerMoveCards.SequenceEqual(other.PlayerMoveCards)
                && PlayerWeapons.SequenceEqual(other.PlayerWeapons)
                && PlayerFailures.SequenceEqual(other.PlayerFailures)
                && PlayerStrengths.SequenceEqual(other.PlayerStrengths)
                && Winner == other.Winner;
        }

        public override int GetHashCode()
            => Common.GetHashCode()
            ^ CurrentPlayerId
            ^ (DoctorRoomId << 3)
            ^ (Winner << 8)
            ^ PlayerRoomIds.GetHashCode()
            ;

        public override string ToString()
            => "T" + TurnId
            + "," + PlayerText(CurrentPlayerId)
            + ",[" + DoctorRoomId
            + "," + string.Join(',', PlayerRoomIds)
            + "]" + (PrevTurn == null ? "" : "," + PrevTurn)
            + "DS=" + DoctorScore().ToString("F3")
            ;

        public bool HasWinner => Winner != RuleHelper.InvalidPlayerId;
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
            + ",S" + PlayerStrengths[playerId]
            + (Common.GetPlayerType(playerId) == PlayerType.Stranger
                ? ""
                : ",M" + PlayerMoveCards[playerId].ToString("N1")
                    + ",W" + PlayerWeapons[playerId].ToString("N1")
                    + ",F" + PlayerFailures[playerId].ToString("N1")
                )
            + ")";

        public bool PlayerSeesPlayer(int playerId1, int playerId2)
            => Common.Board.Sight[PlayerRoomIds[playerId1], PlayerRoomIds[playerId2]];

        public double NumDefensiveClovers()
        {
            var clovers = 0.0;
            var attackingSide = RuleHelper.ToNormalPlayerId(CurrentPlayerId, Common.NumNormalPlayers);

            for(int pid = 0; pid < Common.NumNormalPlayers; pid++)
            {
                if(pid != CurrentPlayerId)
                {
                    if(Common.GetPlayerType(pid) == PlayerType.Normal)
                    {
                        if(pid != attackingSide)
                        {
                            clovers
                                += PlayerFailures[pid] * RuleHelper.Simple.CloversPerWeapon
                                + PlayerWeapons[pid] * RuleHelper.Simple.CloversPerWeapon
                                + PlayerMoveCards[pid] * RuleHelper.Simple.CloversPerMoveCard;
                        }
                    }
                    else // else stranger
                    {
                        clovers++;
                    }
                }
            }

            return clovers;
        }

        public string Summary(int indentationLevel)
        {
            return StateSummary(Util.Print.Indentation(indentationLevel));
        }

        public string StateSummary(string leadingText = "")
        {
            var sb = new StringBuilder();
            sb.Append($"{leadingText}Turn {TurnId}, {PlayerText()}, HeuScore={HeuristicScore(CurrentPlayerId):F2}");
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

        public bool CheckNormalTurn(SimpleTurn turn, out string errorMsg)
        {
            foreach(var move in turn.Moves)
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
            var totalDist = turn.Moves.Sum(move => Common.Board.Distance[PlayerRoomIds[move.PlayerId], move.DestRoomId]);

            if (PlayerMoveCards[CurrentPlayerId] < totalDist - 1)
            {
                errorMsg = $"player {PlayerText()} used too many move points ({totalDist})";
                return false;
            }

            var movingPlayerIds = new List<int>();

            foreach (var move in turn.Moves)
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

        public ImmutableGameState AfterTurn(SimpleTurn turn) => AfterNormalTurn(turn);

        public ImmutableGameState AfterNormalTurn(SimpleTurn turn, bool wantLog = false)
        {
            // no turn validity checking; that is done in other method

            // move phase ======================================================

            var totalDist = turn.Moves.Sum(move => Common.Board.Distance[PlayerRoomIds[move.PlayerId], move.DestRoomId]);
            var moveCardsSpent = Math.Max(0, totalDist - 1);
            var newPlayerMoveCards = PlayerMoveCards.IncrementVal(CurrentPlayerId, -moveCardsSpent);

            ImmutableArray<int>.Builder newPlayerRoomIdsBuilder = null;

            bool movedStrangerThatSawCurrentPlayer = false;

            foreach(var move in turn.Moves)
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

            var newPlayerWeapons = PlayerWeapons;
            var newPlayerFailures = PlayerFailures;
            var newPlayerStrengths = PlayerStrengths;
            var newAttackerHist = AttackerHist;
            var newWinner = RuleHelper.InvalidPlayerId;

            var action = BestActionAllowed(
                CurrentPlayerId,
                DoctorRoomId,
                newPlayerRoomIdsBuilder,
                movedStrangerThatSawCurrentPlayer);

            if(action == PlayerAction.Attack)
            {
                if(ProcessAttack(
                    ref newPlayerMoveCards,
                    ref newPlayerWeapons,
                    ref newPlayerFailures,
                    ref newPlayerStrengths,
                    ref newAttackerHist))
                {
                    newWinner = CurrentPlayerId;
                }

            }
            else if(action == PlayerAction.Loot)
            {
                newPlayerMoveCards = newPlayerMoveCards.IncrementVal(CurrentPlayerId, RuleHelper.Simple.MoveCardsPerLoot);
                newPlayerWeapons = newPlayerWeapons.IncrementVal(CurrentPlayerId, RuleHelper.Simple.WeaponsPerLoot);
                newPlayerFailures = newPlayerFailures.IncrementVal(CurrentPlayerId, RuleHelper.Simple.FailuresPerLoot);
            }

            // doctor phase ====================================================
            ImmutableArray<bool> newPlayersHadTurn;
            int newCurrentPlayerId;
            int newDoctorRoomId;

            if(newWinner == RuleHelper.InvalidPlayerId)
            {
                DoDoctorPhase(
                    newPlayerRoomIdsBuilder,
                    out newPlayersHadTurn,
                    out newCurrentPlayerId,
                    out newDoctorRoomId);
            }
            else
            {
                newPlayersHadTurn = PlayersHadTurn;
                newCurrentPlayerId = CurrentPlayerId;
                newDoctorRoomId = DoctorRoomId;
            }


            // wrap-up phase ===================================================
            var newState = new ImmutableGameState(
                    Common:           Common,
                    TurnId:           TurnId + 1,
                    CurrentPlayerId:  newCurrentPlayerId,
                    DoctorRoomId:     newDoctorRoomId,
                    PlayerRoomIds:    newPlayerRoomIdsBuilder.MoveToImmutable(),
                    PlayersHadTurn:   newPlayersHadTurn,
                    PlayerMoveCards:  newPlayerMoveCards,
                    PlayerWeapons:    newPlayerWeapons,
                    PlayerFailures:   newPlayerFailures,
                    PlayerStrengths:  newPlayerStrengths,
                    AttackerHist:     newAttackerHist,
                    Winner:           newWinner,
                    PrevTurn:         turn,
                    PrevState:        this);

            if(wantLog)
            {
                Console.WriteLine(newState.PrevTurnSummary(true));
            }

            if(!newState.HasWinner && !newState.IsNormalTurn)
            {
                // could avoid newState allocation and pass underlying variables directly
                return newState.AfterStrangerTurn(turn, wantLog);
            }

            return newState;
        }

        public ImmutableGameState AfterStrangerTurn(SimpleTurn normalTurn, bool wantLog)
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

            var newPlayerMoveCards = PlayerMoveCards;
            var newPlayerWeapons = PlayerWeapons;
            var newPlayerFailures = PlayerFailures;
            var newPlayerStrengths = PlayerStrengths;
            var newAttackerHist = AttackerHist;
            var newWinner = RuleHelper.InvalidPlayerId;

            if(bestAction == PlayerAction.Attack)
            {
                if(ProcessAttack(
                    ref newPlayerMoveCards,
                    ref newPlayerWeapons,
                    ref newPlayerFailures,
                    ref newPlayerStrengths,
                    ref newAttackerHist))
                {
                    newWinner = RuleHelper.ToNormalPlayerId(CurrentPlayerId);
                }

            }

            // doctor phase ====================================================
            DoDoctorPhase(
                newPlayerRoomIds,
                out var newPlayersHadTurn,
                out var newCurrentPlayerId,
                out var newDoctorRoomId);

            // wrap-up phase ===================================================
            var newState = new ImmutableGameState(
                    Common:           Common,
                    TurnId:           TurnId + 1,
                    CurrentPlayerId:  newCurrentPlayerId,
                    DoctorRoomId:     newDoctorRoomId,
                    PlayerRoomIds:    newPlayerRoomIds,
                    PlayersHadTurn:   newPlayersHadTurn,
                    PlayerMoveCards:  newPlayerMoveCards,
                    PlayerWeapons:    newPlayerWeapons,
                    PlayerFailures:   newPlayerFailures,
                    PlayerStrengths:  newPlayerStrengths,
                    AttackerHist:     newAttackerHist,
                    Winner:           newWinner,
                    PrevTurn:         normalTurn,
                    PrevState:        this);

            if(wantLog)
            {
                Console.WriteLine(newState.PrevTurnSummary(true));
            }

            if(!newState.HasWinner && !newState.IsNormalTurn)
            {
                // could avoid newState allocation and pass underlying variables directly
                return newState.AfterStrangerTurn(normalTurn, wantLog);
            }

            return newState;
        }

        // returns whether attack was successful and thus attacker is winner
        protected bool ProcessAttack(
            ref ImmutableArray<double> playerMoveCards,
            ref ImmutableArray<double> playerWeapons,
            ref ImmutableArray<double> playerFailures,
            ref ImmutableArray<int> playerStrengths,
            ref ImmutableArray<int> attackerHist)
        {
            var attackStrength = (double)playerStrengths[CurrentPlayerId];

            playerStrengths = playerStrengths.IncrementVal(CurrentPlayerId, 1);
            attackerHist = attackerHist.Add(CurrentPlayerId);

            if(Common.HasStrangers)
            {
                var strangerClovers = 1; // (IsNormalTurn ? 2 : 1) * RuleHelper.Simple.CloversContributedPerStranger;
                attackStrength -= strangerClovers;

                if(attackStrength < 0)
                {
                    return false;
                }

                // if normal player is attacking and attack actually requires normal players to defend
                if(IsNormalTurn)
                {
                    useWeapon(ref attackStrength, ref playerWeapons);
                }

                // player id of normal defender
                var defender = RuleHelper.OpposingNormalPlayer(CurrentPlayerId);

                defendWithCardType(defender, ref attackStrength, ref playerFailures, RuleHelper.Simple.CloversPerFailure);
                defendWithCardType(defender, ref attackStrength, ref playerWeapons, RuleHelper.Simple.CloversPerWeapon);
                defendWithCardType(defender, ref attackStrength, ref playerMoveCards, RuleHelper.Simple.CloversPerMoveCard);

                return attackStrength > 0;
            }
            else
            {
                var numDefensiveClovers = NumDefensiveClovers();

                if(numDefensiveClovers <= 2 * attackStrength)
                {
                    useWeapon(ref attackStrength, ref playerWeapons);
                }

                if(numDefensiveClovers < attackStrength)
                {
                    return true;
                }

                var defender = CurrentPlayerId;

                while(attackStrength > 0)
                {
                    defender = (defender - 1).PositiveRemainder(Common.NumAllPlayers);

                    if(defender == CurrentPlayerId)
                    {
                        return true;
                    }

                    defendWithCardType(defender, ref attackStrength, ref playerFailures, RuleHelper.Simple.CloversPerFailure);
                    defendWithCardType(defender, ref attackStrength, ref playerWeapons, RuleHelper.Simple.CloversPerWeapon);
                    defendWithCardType(defender, ref attackStrength, ref playerMoveCards, RuleHelper.Simple.CloversPerMoveCard);
                }

                return false;
            }

            void useWeapon(ref double attackStrength, ref ImmutableArray<double> playerWeapons)
            {
                if(playerWeapons[CurrentPlayerId] >= 1)
                {
                    attackStrength += RuleHelper.Simple.StrengthPerWeapon;
                    playerWeapons = playerWeapons.IncrementVal(CurrentPlayerId, -1);
                }
            }

            void defendWithCardType(
                int defender,
                ref double attackStrength,
                ref ImmutableArray<double> playerCards,
                double cloversPerCard)
            {
                if(attackStrength > 0 && playerCards[defender] > 0)
                {
                    var numUsedCards = Math.Min(playerCards[defender], attackStrength / cloversPerCard);
                    playerCards = playerCards.IncrementVal(defender, -numUsedCards);
                    attackStrength -= numUsedCards * cloversPerCard;
                }
            }
        }

        protected void DoDoctorPhase(
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
            var sb = new StringBuilder();
            var states = new List<ImmutableGameState>();
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

                if(!prevState.IsNormalTurn)
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
            else if(PrevState.PlayerMoveCards[prevPlayer] % 1 != PlayerMoveCards[prevPlayer] % 1)
            {
                action = PlayerAction.Loot;
            }

            var moveSignifier = PrevState.IsNormalTurn ? new string('M', Math.Max(0, totalDist - 1)) : "";
            var actionSignifier = action == PlayerAction.None ? "" : action.ToString()[0].ToString();
            var winText = HasWinner ? "(" + PlayerText(Winner) + " won)" : "";

            var shortSummary
                = "(" + PlayerText(prevPlayer) + moveSignifier + actionSignifier
                + ")" + string.Join(' ', shortMoveTexts)
                + winText
                + ";";

            if(!verbose)
            {
                return shortSummary;
            }

            var sb = new StringBuilder();
            var plyText = PrevState.IsNormalTurn ? $"/{PrevState.Ply()}" : "";
            sb.Append($"  Turn{PrevState.TurnId}{plyText}, {shortSummary}");

            verboseMoveTexts.ForEach(x => sb.Append('\n' + x));

            if(action == PlayerAction.Loot)
            {
                sb.Append($"\n    LOOT {PlayerText(prevPlayer)}: now " + PlayerTextLong(prevPlayer));
            }
            else if(action == PlayerAction.Attack)
            {
                sb.Append($"\n    ATTACK: hist=" + string.Join(',', AttackerHist.Select(CommonGameState.ToPlayerDisplayNum)));
            }

            if(HasWinner)
            {
                sb.Append("\n    WINNER: " + PlayerText(Winner));
            }
            else
            {
                sb.Append($"\n    DR MOVE: R{PrevState.DoctorRoomId} to R{DoctorRoomId}");

                if (DoctorRoomId == PlayerRoomIds[CurrentPlayerId])
                {
                    var otherPlayersInRoom = Common.PlayerIds
                        .Where(pid => pid != CurrentPlayerId && PlayerRoomIds[pid] == DoctorRoomId)
                        .Select(CommonGameState.ToPlayerDisplayNum);
                    var unactivatedPlayersText = otherPlayersInRoom.Any()
                        ? ", unactivated players{" + string.Join(',', otherPlayersInRoom) + "}"
                        : "";
                    sb.Append($"\n    DR ACTIVATE: {PlayerText()}{unactivatedPlayersText}");
                }

                sb.Append("\n    start of next turn...\n");
                sb.Append(StateSummary(Util.Print.Indentation(3)));
            }

            return sb.ToString();
        }

        public double HeuristicScore(int analysisPlayerId)
        {
            if(Winner != RuleHelper.InvalidPlayerId)
            {
                return analysisPlayerId == Common.ToNormalPlayerId(Winner) ? 1e9 : -1e9;
            }

            double miscScore(int playerId, int alliedStrength, bool isAlliedTurn, double alliedDoctorAdvantage)
                => alliedStrength
                + 0.5 * alliedStrength * 
                    ( PlayerMoveCards[playerId]
                    + (isAlliedTurn ? 0.95 : 0.0)
                    + alliedDoctorAdvantage * 0.9
                    )
                + 0.5 * PlayerWeapons[playerId]
                + 0.125 * PlayerFailures[playerId];

            // allied attack strength minus opposed attack strength
            var overallScore = 0.0;

            if(Common.HasStrangers)
            {
                var strangerAlly = RuleHelper.AlliedStranger(analysisPlayerId);
                var normalOpponent = RuleHelper.OpposingNormalPlayer(analysisPlayerId);
                var strangerOpponent = RuleHelper.AlliedStranger(normalOpponent);
                var alliedStrength = PlayerStrengths[analysisPlayerId] + PlayerStrengths[strangerAlly];
                var opponentStrength = PlayerStrengths[normalOpponent] + PlayerStrengths[strangerOpponent];
                var isMyTurn = analysisPlayerId == CurrentPlayerId;
                var numPlayersNotHadTurn = PlayersHadTurn.Count(x => !x);
                var doctorDeltaForActivation = Math.Max(1, numPlayersNotHadTurn);
                var nextDoctorRoomId = Common.Board.NextRoomId(DoctorRoomId, doctorDeltaForActivation);
                var alliedDoctorAdvantage = DoctorScore(
                    PlayerRoomIds[isMyTurn ? analysisPlayerId : normalOpponent],
                    PlayerRoomIds[isMyTurn ? strangerAlly : strangerOpponent],
                    PlayerRoomIds[isMyTurn ? normalOpponent : analysisPlayerId],
                    PlayerRoomIds[isMyTurn ? strangerOpponent : strangerAlly])
                    * (isMyTurn ? 1 : -1);

                overallScore
                    = miscScore(analysisPlayerId, alliedStrength, isMyTurn, alliedDoctorAdvantage)
                    - miscScore(normalOpponent, opponentStrength, !isMyTurn, -alliedDoctorAdvantage);
            }
            else
            {
                for (int pid = 0; pid < Common.NumAllPlayers; pid++)
                {
                    var weight = RuleHelper.ToNormalPlayerId(pid) == analysisPlayerId ? 1.0 : -1.0 / (Common.NumNormalPlayers - 1);
                    var playerMiscScore =  miscScore(pid, PlayerStrengths[pid], pid == CurrentPlayerId, 0);
                    overallScore += weight * playerMiscScore;
                }
            }

            return overallScore;
        }

        public double DoctorScore() => DoctorScore(
            PlayerRoomIds[CurrentPlayerId],
            PlayerRoomIds[RuleHelper.AlliedStranger(CurrentPlayerId)],
            PlayerRoomIds[RuleHelper.OpposingNormalPlayer(CurrentPlayerId)],
            PlayerRoomIds[RuleHelper.OpposingStranger(CurrentPlayerId)]);

        public double DoctorScore(
            int myRoom,
            int strangerAllyRoom,
            int normalEnemyRoom,
            int strangerEnemyRoom)
        {
            const double decayFactorNormal = 0.9;
            const double decayFactorStranger = 0.5;
            var numPlayersNotHadTurn = PlayersHadTurn.Count(x => !x);
            var doctorDeltaForActivation = Math.Max(1, numPlayersNotHadTurn);
            var nextDoctorRoomId = Common.Board.NextRoomId(DoctorRoomId, doctorDeltaForActivation);

            var doctorRooms = Common.Board.RoomIdsInDoctorVisitOrder(nextDoctorRoomId);
            doctorRooms.Insert(0, DoctorRoomId);

            var myStartingSearchIdx = numPlayersNotHadTurn > 0 ? 1 : 0;
            var myDoctorDist = 999;
            for(int i = myStartingSearchIdx; i < doctorRooms.Count; i++)
            {
                if(doctorRooms[i] == myRoom)
                {
                    myDoctorDist = i;
                    break;
                }
                else if(i > 0 && Common.Board.Distance[myRoom, doctorRooms[i]] <= 1)
                {
                    myDoctorDist = i;
                    break;
                }

            }

            var strangerAllyDoctorDist = doctorRooms.IndexOf(strangerAllyRoom, 1);
            var normalEnemyDoctorDist = doctorRooms.IndexOf(normalEnemyRoom, 1);
            var strangerEnemyDoctorDist = doctorRooms.IndexOf(strangerEnemyRoom, 1);
            var score
                = Math.Pow(decayFactorNormal, myDoctorDist)
                + Math.Pow(decayFactorStranger, strangerAllyDoctorDist)
                - Math.Pow(decayFactorNormal, normalEnemyDoctorDist)
                - Math.Pow(decayFactorStranger, strangerEnemyDoctorDist);
            return score;
        }

        public AppraisedPlayerMove Appraise(int analysisLevel, CancellationToken cancellationToken, out int numStatesVisited)
        {
            numStatesVisited = 0;
            return Appraise(CurrentPlayerId, analysisLevel, cancellationToken, ref numStatesVisited);
        }

        // reminder: analysisPlayerId will never be a stranger
        public AppraisedPlayerMove Appraise(
            int analysisPlayerId,
            int analysisLevel,
            CancellationToken cancellationToken,
            ref int numStates)
        {
            numStates++;

            if(HasWinner || analysisLevel == 0)
            {
                return new AppraisedPlayerMove(HeuristicScore(analysisPlayerId), default, this);
            }

            var appraisalIsForCurrentPlayer = analysisPlayerId == CurrentPlayerId;
            var currentPlayerMoveAbility = PlayerMoveCards[CurrentPlayerId] + 1;

            var bestMove = new AppraisedPlayerMove(double.MinValue, default, default);

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
                        var hypoState = AfterNormalTurn(new SimpleTurn(move));
                        var hypoAppraisedMove = hypoState.Appraise(
                            CurrentPlayerId,
                            analysisLevel - 1,
                            cancellationToken,
                            ref numStates);

                        if(CurrentPlayerId != hypoState.CurrentPlayerId)
                        {
                            hypoAppraisedMove.Appraisal = hypoAppraisedMove.EndingState.HeuristicScore(CurrentPlayerId);
                        }

                        if (bestMove.Appraisal < hypoAppraisedMove.Appraisal)
                        {
                            bestMove = hypoAppraisedMove;
                            bestMove.Move = move;
                        }

                        if(cancellationToken.IsCancellationRequested)
                        {
                            return bestMove;
                        }
                    }
                }
            }

            return bestMove;
        }

        public List<SimpleTurn> PossibleTurns()
        {
            if(HasWinner)
            {
                return new();
            }

            var movablePlayerIds = Common.NumNormalPlayers == RuleHelper.NumNormalPlayersWhenHaveStrangers
                ? new[] { CurrentPlayerId, RuleHelper.StrangerPlayerIdFirst, RuleHelper.StrangerPlayerIdSecond, }
                : new[] { CurrentPlayerId, };

            var movablePlayerSubsets = new List<List<int>>() { new(){ CurrentPlayerId, }, };

            if(Common.HasStrangers)
            {
                var alliedStranger = RuleHelper.AlliedStranger(CurrentPlayerId);
                var opposingStranger = RuleHelper.OpposingStranger(CurrentPlayerId);

                movablePlayerSubsets.Add(new() { alliedStranger, });
                movablePlayerSubsets.Add(new() { opposingStranger, });

                /*
                if(PlayerMoveCards[CurrentPlayerId] > 0)
                {
                    movablePlayerSubsets.Add(new() { CurrentPlayerId, alliedStranger, });
                    movablePlayerSubsets.Add(new() { CurrentPlayerId, opposingStranger, });
                    movablePlayerSubsets.Add(new() { alliedStranger, opposingStranger, });
                }
                */
            }

            var turns = new List<SimpleTurn>();
            var distAllowed = (int)PlayerMoveCards[CurrentPlayerId] + 1;

            foreach(var movablePlayerSubset in movablePlayerSubsets)
            {
                turns.AddRange(PossibleTurns(distAllowed, movablePlayerSubset));
            }

            return turns;
        }

        protected List<SimpleTurn> PossibleTurns(int distAllowed, IList<int> movablePlayers)
            => movablePlayers.Count == 1
            ? PossibleTurns(distAllowed, movablePlayers[0])
            : PossibleTurns(distAllowed, movablePlayers[0], movablePlayers[1]);

        protected List<SimpleTurn> PossibleTurns(int distAllowed, int movablePlayer)
        {
            var movablePlayerRoom = PlayerRoomIds[movablePlayer];
            var turns = new List<SimpleTurn>();

            foreach (var destRoom in Common.Board.RoomIds)
            {
                if (Common.Board.Distance[movablePlayerRoom, destRoom] <= distAllowed)
                {
                    turns.Add(new SimpleTurn(movablePlayer, destRoom));
                }
            }

            return turns;
        }

        protected List<SimpleTurn> PossibleTurns(int distAllowed, int movablePlayerA, int movablePlayerB)
        {
            var srcRoomA = PlayerRoomIds[movablePlayerA];
            var srcRoomB = PlayerRoomIds[movablePlayerB];
            var moves = new List<SimpleTurn>();

            foreach (var dstRoomA in Common.Board.RoomIds)
            {
                var distRemaining = distAllowed - Common.Board.Distance[srcRoomA, dstRoomA];

                if (distRemaining <= 0 || srcRoomA == dstRoomA)
                {
                    continue;
                }

                var moveA = new PlayerMove(movablePlayerA, dstRoomA);

                foreach (var dstRoomB in Common.Board.RoomIds)
                {
                    if (Common.Board.Distance[srcRoomB, dstRoomB] > distRemaining
                        || srcRoomB == dstRoomB)
                    {
                        continue;
                    }

                    var moveB = new PlayerMove(movablePlayerB, dstRoomB);
                    moves.Add(new SimpleTurn(new[] { moveA, moveB }));
                }

            }

            return moves;
        }

    }
}
