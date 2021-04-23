using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Util;

namespace Kdl.Core
{
    public class GameState
    {
        public bool IsLogEnabled { get; set; } = true;
        public RuleFlags Rules { get; protected set; }
        public Board Board { get; protected set; }
        public IDeck Deck { get; protected set; }
        public int NumNormalPlayers { get; init; }
        public int DoctorRoomId { get; protected set; }
        public int TurnId { get; protected set; }
        public int CurrentPlayerId { get; protected set; }
        public List<Player> Players { get; protected set; }
        public Player CurrentPlayer => Players[CurrentPlayerId];
        public HashSet<int> PlayersWhoHadTurn { get; protected set; } = new();
        public GameHistory Hist { get; protected set; }
        public int WinnerPlayerId { get; protected set; } = -1;

        public GameState(
            RuleFlags rules,
            Random rng,
            int numNormalPlayers,
            string deckPath,
            string boardPath,
            IEnumerable<string> closedWingNames)
        {
            Rules = rules;
            Deck = RuleHelper.NewDeck(rules, deckPath, rng);
            Board = Board.FromJson(boardPath, "", closedWingNames);

            if(!Board.IsValid(out var mistakes))
            {
                throw new Exception("board is invalid:\n" + string.Join('\n', mistakes));
            }

            NumNormalPlayers = numNormalPlayers;
            DoctorRoomId = Board.DoctorStartRoomId;
            TurnId = 1;
            CurrentPlayerId = 0;
            Players = new();
            Hist = new(numNormalPlayers);

            if(numNormalPlayers == 2)
            {
                for(int playerId = 0; playerId < 2 * numNormalPlayers; playerId++)
                {
                    var playerType = playerId % 2 == 0 ? PlayerType.Normal : PlayerType.Stranger;
                    var cards = playerType == PlayerType.Normal
                        ? Deck.DrawMany(playerId, RuleHelper.NormalPlayerNumStartingCards)
                        : new();
                    Players.Add(new Player(playerId, playerType, Board.PlayerStartRoomId, cards));
                }
            }
            else
            {
                for(int playerId = 0; playerId < numNormalPlayers; playerId++)
                {
                    var cards = Deck.DrawMany(playerId, RuleHelper.NormalPlayerNumStartingCards);
                    Players.Add(new Player(playerId, PlayerType.Normal, Board.PlayerStartRoomId, cards));
                }
            }
        }

        public string Summary(int indentationLevel)
        {
            return Summary(Indentation(indentationLevel));
        }

        public string Summary(string leadingText = "")
        {
            var sb = new StringBuilder();
            sb.Append($"{leadingText}Turn {TurnId}, {CurrentPlayer}");
            sb.Append($"\n{leadingText}  AttackHist={{{string.Join(',', Hist.Attackers.Select(pid => pid + 1))}}}");
            sb.Append($"\n{leadingText}  Doctor@R{DoctorRoomId}");

            var playersWhoCanSeeDoctor = Players
                .Where(p => Board.Sight[p.RoomId, DoctorRoomId])
                .Select(p => p.DisplayNum).ToArray();

            if(playersWhoCanSeeDoctor.Any())
            {
                sb.Append(", seen by players{" + string.Join(',', playersWhoCanSeeDoctor) + "}");
            }
            else
            {
                sb.Append(", unseen by players");
            }

            foreach(var player in Players)
            {
                sb.Append($"\n{leadingText}  {player}");

                if(player.Id == CurrentPlayerId)
                {
                    sb.Append(" current player");
                }
            }

            var text = sb.ToString();
            return text;
        }

        protected string Indentation(int indentationLevel)
            => new(' ', 2 * indentationLevel);

        public bool CheckSimpleTurn(SimpleTurn turn, out string errorMsg)
        {
            var currPlayer = Players[CurrentPlayerId];

            // move phase ======================================================

            var totalDist = turn.Moves.Sum(move => DistanceForMove(move));

            if(currPlayer.SimpleMovePoints + 1 < totalDist)
            {
                errorMsg = $"player {currPlayer} used too many move points ({totalDist})";
                return false;
            }

            foreach(var move in turn.Moves)
            {
                var movingPlayer = Players[move.PlayerId];

                if (movingPlayer.Id != currPlayer.Id && movingPlayer.PlayerType != PlayerType.Stranger)
                {
                    errorMsg = $"player {currPlayer.Id} tried to move non-stranger {movingPlayer.Id}";
                    return false;
                }
            }

            // action phase (attack or loot) ===================================

            errorMsg = null;
            return true;
        }

        public void DoSimpleTurn(SimpleTurn turn)
        {
            // no turn validity checking; that is done in other method
            var currPlayer = Players[CurrentPlayerId];

            if(IsLogEnabled)
            {
                Console.WriteLine($"Turn {TurnId}, {CurrentPlayer.BriefText()}");
                Console.WriteLine($"  at start...");
                Console.WriteLine(Summary(Indentation(2)));
            }

            // move phase ======================================================

            var totalDist = turn.Moves.Sum(move => DistanceForPlayer(move.PlayerId, move.DestRoomId));
            currPlayer.SubtractSimpleMovePoints(totalDist - 1); // -1 for one free move point per turn

            if(IsLogEnabled)
            {
                Console.WriteLine($"  moves...");
            }

            foreach(var move in turn.Moves)
            {
                var movingPlayer = Players[move.PlayerId];

                if(IsLogEnabled)
                {
                    Console.WriteLine($"    MOVE {movingPlayer.BriefText()} from R{movingPlayer.RoomId} to R{move.DestRoomId} ({DistanceForMove(move)}mp)");
                }

                movingPlayer.RoomId = move.DestRoomId;
            }

            /*
            if(IsLogEnabled)
            {
                Console.WriteLine($"  after moves...");
                Console.WriteLine(Summary(Indentation(2)));
            }
            */

            // action phase (attack or loot) ===================================

            if(ActionIsAllowed(PlayerAction.Attack))
            {
                currPlayer.Strength++;
                Hist.RememberAttack(currPlayer.Id);

                if(IsLogEnabled)
                {
                    Console.WriteLine($"  ATTACK({currPlayer.BriefText()}): hist={Hist.AttackDisplayNums()}");
                }
            }
            else if(ActionIsAllowed(PlayerAction.Loot))
            {
                currPlayer.AddCard(Deck.Draw(currPlayer.Id));
                Hist.RememberLoot(currPlayer.Id);

                if(IsLogEnabled)
                {
                    Console.WriteLine($"  LOOT: {currPlayer.BriefText()} has {currPlayer.Cards.Count} cards");
                }
            }

            // doctor phase ====================================================
            DoDoctorPhase();
            Hist.Turns.Add(turn);

            // possible stranger turn ==========================================
            if(CurrentPlayer.PlayerType == PlayerType.Stranger)
            {
                DoSimpleStrangerTurn();
            }
            else if(IsLogEnabled)
            {
                Console.WriteLine($"  preview of next turn...");
                Console.WriteLine(Summary(Indentation(2)));
            }
        }

        protected void DoDoctorPhase()
        {
            var priorPlayer = CurrentPlayer;

            TurnId++;
            PlayersWhoHadTurn.Add(CurrentPlayerId);

            var oldDoctorRoomId = DoctorRoomId;
            DoctorRoomId = Board.NextRoomId(DoctorRoomId, 1);

            if(IsLogEnabled)
            {
                Console.WriteLine($"  doctor moves from R{oldDoctorRoomId} to R{DoctorRoomId}");
            }

            var playersInDoctorRoom = Players
                .Where(p => p.RoomId == DoctorRoomId)
                .OrderBy(p => p.NumSlotsAfter(priorPlayer, Players.Count))
                .ToArray();

            if(PlayersWhoHadTurn.Count == Players.Count && playersInDoctorRoom.Any())
            {
                CurrentPlayerId = playersInDoctorRoom.First().Id;

                if(IsLogEnabled)
                {
                    Console.WriteLine(
                        "  doctor activates "
                        + CurrentPlayer.BriefText()
                        + " from players{"
                        + string.Join(',', playersInDoctorRoom.Select(p => p.DisplayNum))
                        + "}");
                }
            }
            else
            {
                CurrentPlayerId = (CurrentPlayerId + 1) % Players.Count;
            }
        }

        public void DoSimpleStrangerTurn()
        {
            if(IsLogEnabled)
            {
                Console.WriteLine($"Turn {TurnId}, {CurrentPlayer.BriefText()}");
                Console.WriteLine($"  at start...");
                Console.WriteLine(Summary(Indentation(2)));
            }

            // stranger moves if and only if it can not attack the doctor
            if(!ActionIsAllowed(PlayerAction.Attack))
            {
                var oldRoomId = CurrentPlayer.RoomId;
                CurrentPlayer.RoomId = Board.NextRoomId(CurrentPlayer.RoomId, -1);

                if(IsLogEnabled)
                {
                    Console.WriteLine($"  MOVE {CurrentPlayer.BriefText()} from R{oldRoomId} to R{CurrentPlayer.RoomId}");
                }
            }

            if(ActionIsAllowed(PlayerAction.Attack))
            {
                CurrentPlayer.Strength++;
                Hist.Attackers.Add(CurrentPlayerId);
                if(IsLogEnabled)
                {
                    Console.WriteLine($"  ATTACK({CurrentPlayer.BriefText()}): hist={Hist.AttackDisplayNums()}");
                }
            }

            DoDoctorPhase();

            if(CurrentPlayer.PlayerType == PlayerType.Stranger)
            {
                DoSimpleStrangerTurn();
            }
            else if(IsLogEnabled)
            {
                Console.WriteLine($"  preview of next turn...");
                Console.WriteLine(Summary(Indentation(2)));
            }
        }

        public bool ActionIsAllowed(PlayerAction action)
        {
            if(action == PlayerAction.Attack && CurrentPlayer.RoomId != DoctorRoomId)
            {
                return false;
            }

            var playerRoomIds = Players.OtherPlayerRoomIds(CurrentPlayer);

            return VisibilityAllowsAction(
                CurrentPlayerId,
                action,
                CurrentPlayer.RoomId,
                DoctorRoomId,
                Players.OtherPlayerRoomIds(CurrentPlayerId));
        }

        public bool VisibilityAllowsAction(
            int playerId,
            PlayerAction action,
            int playerRoomId,
            int doctorRoomId,
            IEnumerable<int> otherPlayerRoomIds)
        {
            if(action == PlayerAction.None)
            {
                return true;
            }

            var relevantRoomIds = otherPlayerRoomIds;

            if(action == PlayerAction.Loot)
            {
                relevantRoomIds = relevantRoomIds.Append(DoctorRoomId);
            }

            return !Board.RoomIsSeenBy(playerRoomId, relevantRoomIds);
        }

        public int DistanceForMove(PlayerMove move)
        {
            return DistanceForPlayer(move.PlayerId, move.DestRoomId);
        }

        public int DistanceForPlayer(int playerId, int destRoomId)
        {
            return Board.Distance[Players[playerId].RoomId, destRoomId];
        }

    }
}
