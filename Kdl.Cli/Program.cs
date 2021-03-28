using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using Kdl.Core;

namespace Kdl.Cli
{
    public class Program
    {
        public const string DataDir = "../../../../Kdl.Core/Data";

        static int Main(string[] args)
        {
            Console.WriteLine("program begin");
            //DeserializationFiddle();
            //InteractiveSimpleTwoPlayerGame();
            var session = new Session(args);
            session.Start();
            Console.WriteLine("program end");
            return 0;
        }

        static void DeserializationFiddle()
        {
            var board = Board.FromJson(
                DataDir + "/BoardMain.json",
                "+e",
                new string[] { "west" });

            if(!board.IsValid(out var mistakes))
            {
                Console.WriteLine("board invalid: " + string.Join(',', mistakes));
            }

            var game = new GameState(
                RuleFlags.SuperSimple,
                new Random(1),
                4,
                DataDir + "/DeckStandard.json",
                DataDir + "/BoardMain.json",
                new[] { "west" });

            game.IsLogEnabled = true;

            SimpleTurn makeTurn(int destRoomId) => new SimpleTurn(new PlayerMove(game.CurrentPlayerId, destRoomId));

            game.DoSimpleTurn(makeTurn(2)); // P0, L15
            game.DoSimpleTurn(makeTurn(16)); // P1, L16, attack
            game.DoSimpleTurn(makeTurn(19)); // P2, L17
            game.DoSimpleTurn(makeTurn(18)); // P3, L18, attack
            game.DoSimpleTurn(makeTurn(20)); // P2, L19
            game.DoSimpleTurn(makeTurn(21)); // P2, L20
            game.DoSimpleTurn(makeTurn(21)); // P2, L21, attack
        }

        static void InteractiveSimpleTwoPlayerGame()
        {
            var board = Board.FromJson(
                DataDir + "/BoardMain.json",
                "+e",
                new string[] { "west" });

            if(!board.IsValid(out var mistakes))
            {
                Console.WriteLine("board invalid: " + string.Join(',', mistakes));
            }

            GameState game = null;

            void reset()
            {
                game = new GameState(
                    RuleFlags.SuperSimple,
                    new Random(1),
                    2,
                    DataDir + "/DeckStandard.json",
                    DataDir + "/BoardMain.json",
                    new[] { "west" });

                game.IsLogEnabled = true;
                Console.WriteLine(game.Summary(1));
            }

            reset();

            while (true)
            {
                Console.Write($"{game.CurrentPlayer.BriefText()}> ");
                var line = Console.ReadLine();
                var sublines = line.Split(';');

                foreach (var subline in sublines)
                {
                    var tokens = subline.Split(' ');

                    if (string.IsNullOrWhiteSpace(subline))
                    {
                        // deliberately nothing
                    }
                    else if (tokens[0] == "q")
                    {
                        return;
                    }
                    else if (tokens[0] == "d")
                    {
                        Console.WriteLine(game.Summary(1));
                    }
                    else if (tokens[0] == "r")
                    {
                        Console.WriteLine("RESET");
                        reset();
                    }
                    else if (tokens[0] == "h")
                    {
                        Console.WriteLine(game.Hist.BriefTurnHist());
                    }
                    else
                    {
                        var moves = new List<PlayerMove>();
                        bool hasParseErrors = false;

                        foreach (var token in tokens)
                        {
                            var subtokens = token.Split(',', '@');
                            var idxForDestRoomId = subtokens.Length == 1 ? 0 : 1;
                            var destRoomIdSubtoken = subtokens.Length == 1 ? subtokens[0] : subtokens[1];
                            int playerDisplayNum = game.CurrentPlayerId + 1;

                            if (int.TryParse(destRoomIdSubtoken, out var destRoomId))
                            {
                                if (subtokens.Length >= 2 && !int.TryParse(subtokens[0], out playerDisplayNum))
                                {
                                    Console.WriteLine($"  failed parse for room id from '{subtokens[0]}' subtoken of '{token}'");
                                    hasParseErrors = true;
                                }
                                else
                                {
                                    var playerId = playerDisplayNum - 1;
                                    moves.Add(new PlayerMove(playerId, destRoomId));
                                }
                            }
                            else
                            {
                                Console.WriteLine($"  failed parse for room id from '{token}'");
                                hasParseErrors = true;
                            }
                        }

                        if (!hasParseErrors)
                        {
                            var turn = new SimpleTurn(moves.ToArray());

                            if (turn == null)
                            {
                                Console.WriteLine("  moved too far or moved another normal player");
                            }
                            else
                            {
                                if (game.CheckSimpleTurn(turn, out var errorMsg))
                                {
                                    game.DoSimpleTurn(turn);
                                }
                                else
                                {
                                    Console.WriteLine($"  invalid turn: {errorMsg}");
                                }
                            }
                        }
                    }
                }
            }

        }
    }
}
