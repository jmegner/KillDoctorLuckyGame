using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using Kdl.Core;

namespace Kdl.Cli
{
    public class SessionOptions
    {

    }

    public class Session
    {
        string[] CliArgs { get; set; }
        Random Rng { get; set; } = new Random(1);
        int NumNormalPlayers { get; set; } = 2;
        string DeckName { get; set; } = "DeckStandard";
        string BoardName { get; set; } = "BoardMain";
        List<string> ClosedWingNames { get; set; } = new() { "west" };
        RuleFlags Rules { get; set; } = RuleFlags.SuperSimple;
        CommonGameState GameCommon { get; set; }
        SmallGameState Game { get; set; }
        bool ShouldQuit { get; set; }
        int AnalysisLevel { get; set; }

        string BoardPath => JsonFilePath(BoardName);
        string DeckPath => JsonFilePath(DeckName);
        string JsonFilePath(string baseName) => Program.DataDir + "/" + baseName + ".json";


        public Session(string[] cliArgs = null)
        {
            CliArgs = cliArgs ?? new string[0];
        }

        public void Start()
        {
            ResetGame();
            InterpretationLoop();
        }

        public void InterpretationLoop()
        {
            while (true)
            {
                Console.Write(UserPromptText());
                var line = Console.ReadLine();
                var sublines = line.Split(';');

                foreach (var subline in sublines)
                {
                    InterpretDirective(subline);
                    if(ShouldQuit)
                    {
                        return;
                    }
                }
            }

        }

        protected string WithoutComments(string directive)
        {
            const char commentStartChar = '(';
            const char commentEndChar = ')';

            while(directive.IndexOf(commentStartChar) > -1)
            {
                var commentStartIdx = directive.IndexOf(commentStartChar);
                var commentEndIdx = directive.IndexOf(commentEndChar);

                if(commentEndIdx == -1)
                {
                    directive = directive.Substring(0, commentStartIdx);
                }
                else
                {
                    directive = directive.Substring(0, commentStartIdx) + directive.Substring(commentEndIdx + 1);
                }
            }

            return directive;
        }

        protected void InterpretDirective(string directive)
        {
            directive = WithoutComments(directive);
            var tokens = directive.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var directiveTag = tokens.Length == 0 ? "" : tokens[0].ToLowerInvariant().Trim();

            if (string.IsNullOrWhiteSpace(directive))
            {
                // deliberately nothing
            }
            else if (directiveTag == "q")
            {
                ShouldQuit = true;
            }
            else if (directiveTag == "d")
            {
                Console.WriteLine(Game.Summary(1));
            }
            else if (directiveTag == "r")
            {
                Console.WriteLine("RESET");
                ResetGame();
            }
            else if (directiveTag == "h")
            {
                if (tokens.Length >= 2 && bool.TryParse(tokens[1], out var verbose))
                {
                    // verbose option not implemented yet
                    Console.WriteLine(Game.NormalTurnHist());
                }
                else
                {
                    Console.WriteLine(Game.NormalTurnHist());
                }
            }
            else if (directiveTag == "a"
             || directiveTag == "x")
            {
                if (tokens.Length >= 2 && int.TryParse(tokens[1], out var analysisLevel))
                {
                    AnalysisLevel = analysisLevel;
                }

                var playerId = Game.CurrentPlayerId;

                if (tokens.Length >= 3 && int.TryParse(tokens[2], out var playerDisplayNum))
                {
                    playerId = CommonGameState.ToPlayerId(playerDisplayNum);
                }

                bool doSuggestedMove = directiveTag == "x";

                Analyze(doSuggestedMove, AnalysisLevel, playerId);
            }
            else if (directiveTag == "b" || directiveTag == "board")
            {
                if(tokens.Length != 2)
                {
                    Console.WriteLine("  board directive needs two tokens");
                }
                else
                {
                    BoardName = tokens[1];
                }
            }
            else if (directiveTag == "w" || directiveTag == "wingsclosed")
            {
                ClosedWingNames = tokens.Skip(1).ToList();
            }
            else if (directiveTag == "p" || directiveTag == "numplayers")
            {
                var newVal = NumNormalPlayers;
                if(tokens.Length != 2 || !int.TryParse(tokens[1], out newVal))
                {
                    Console.WriteLine("  numplayers directive needs one integer token");
                }
                else
                {
                    NumNormalPlayers = newVal;
                }
            }
            else if(char.IsDigit(directiveTag.FirstOrDefault()))
            {
                DoMoves(tokens);
            }
            else
            {
                var explanations = new List<string>()
                {
                    "q       | quit",
                    "d       | display game state",
                    "r       | reset game",
                    "h       | display user-turn history",
                    "a [int] | analyze next move [int] deep",
                    "board [boardName]",
                    "closedwings [wing1] [wing2] [...]",
                    "numplayers [int]",
                    "[playerNum@destRoomId] [destRoomIdForCurrentPlayer] submit turn of those moves"
                };

                Console.WriteLine($"  unrecognized directive '{directive}'");
                explanations.Sort();
                explanations.ForEach(x => Console.WriteLine("  " + x));
            }
        }

        protected void Analyze(bool doSuggestedMove, int analysisLevel, int playerId)
        {
            SmallGameState.AppraiseExecCount = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var appraisedMove = Game.Appraise(playerId, analysisLevel);
            watch.Stop();
            Console.WriteLine(
                "bestMove=" + appraisedMove.Move
                + ", level=" + analysisLevel
                + ", appraisal=" + appraisedMove.Appraisal.ToString("N4")
                + ", timeMs=" + watch.ElapsedMilliseconds
                + ", states=" + SmallGameState.AppraiseExecCount.ToString("N0")
                );

            if(doSuggestedMove)
            {
                DoMoves(new[] { appraisedMove.Move });
            }
        }

        protected void DoMoves(IList<string> tokens)
        {
            var moves = new List<PlayerMove>();
            bool hasParseErrors = false;

            foreach (var token in tokens)
            {
                var subtokens = token.Split(',', '@');
                var idxForDestRoomId = subtokens.Length == 1 ? 0 : 1;
                var destRoomIdSubtoken = subtokens.Length == 1 ? subtokens[0] : subtokens[1];
                int playerDisplayNum = Game.CurrentPlayerId + 1;

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
                DoMoves(moves);
            }
        }

        protected void DoMoves(IEnumerable<PlayerMove> moves)
        {
            if (Game.CheckNormalTurn(moves, out var errorMsg))
            {
                Game = Game.AfterNormalTurn(moves, true);
            }
            else
            {
                Console.WriteLine($"  invalid turn: {errorMsg}");
            }
        }

        protected bool ResetGame(out List<string> problems)
        {
            try
            {
                var board = Board.FromJson(BoardPath, "", ClosedWingNames);

                if(!board.IsValid(out var mistakes))
                {
                    throw new Exception("board is invalid:\n" + string.Join('\n', mistakes));
                }

                GameCommon = new(
                    true,
                    board,
                    NumNormalPlayers);

                Game = SmallGameState.AtStart(GameCommon);
            }
            catch(Exception e)
            {
                Console.WriteLine("exception while constructing GameState: " + e.Message);
                Console.WriteLine(e.StackTrace);
            }

            return GameCommon.Board.IsValid(out problems);
        }

        protected bool ResetGame()
        {
            var result = ResetGame(out var problems);

            if (result)
            {
                Console.WriteLine(Game.Summary(1));
            }
            else
            {
                Console.WriteLine("problems resetting game");
                foreach(var problem in problems)
                {
                    Console.WriteLine("  " + problem);
                }
            }

            return result;
        }

        protected string UserPromptText() => Game.PlayerText() + "> ";
    }
}
