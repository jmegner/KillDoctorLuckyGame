using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Util;

namespace Kdl.Core
{
    public class Mcts<TTurn,TGameState>
        where TTurn : ITurn
        where TGameState : IGameState<TTurn>
    {
        protected Node _root;
        protected Random _random;

        public Mcts(TGameState gameState, Random random = null)
        {
            _random = random ?? new Random();
            _root = new Node(null, gameState);
        }

        public int NumNodes => _root.NumNodes;

        public IEnumerable<Node> GetTopTurns(CancellationToken token)
        {
            _root.BuildTree(token, _random);
            return _root.Children
                .OrderByDescending(child => child.ExploitationValue)
                .OrderByDescending(child => child.NumRuns);
        }

        public bool Reroot(TGameState goalState)
        {
            var stateHist = new List<TGameState>();
            var state = goalState;

            while (state != null)
            {
                stateHist.Add(state);
                state = (TGameState)state.PrevState;
            }

            stateHist.Reverse();

            foreach(var fwdState in stateHist)
            {
                var matchingChild = _root.Children.Where(child => child.GameState.Equals(fwdState)).FirstOrDefault();

                if(matchingChild != default)
                {
                    _root = matchingChild;
                }
            }

            if(_root.GameState.Equals(goalState))
            {
                _root.ForgetParent();
                return true;
            }

            _root = new Node(null, goalState);
            return false;
        }

        public class Node
        {

            public Node Parent { get; set; }
            public IList<Node> Children { get; init; } = new List<Node>();
            public int NumRuns { get; set; }
            public double NumWins { get; set; }
            public TGameState GameState { get; init; }
            public TTurn TurnTaken => GameState.PrevTurn;
            public List<TGameState> UntriedNextStates { get; init; }
            public double HeuristicScoreForPrevPlayer { get; init; }
            public double ExploitationValue => NumWins / NumRuns;
            public double ExplorationValue => Math.Sqrt(2.0 * Math.Log(Parent.NumRuns) / NumRuns);
            public double HeuristicValue => Math.Atan(HeuristicScoreForPrevPlayer) / Math.Sqrt(NumRuns);
            public double Uct => ExploitationValue + ExplorationValue;
            public double SelectionPreferenceValue => ExploitationValue + ExplorationValue + HeuristicValue;
            public int NumNodes => 1 + Children.Sum(child => child.NumNodes);

            public Node(
                Node parent,
                TGameState gameState)
            {
                Parent = parent;
                GameState = gameState;
                HeuristicScoreForPrevPlayer = gameState.HeuristicScore(parent?.GameState.CurrentPlayerId ?? 0);
                UntriedNextStates = gameState.NextStates<TTurn,TGameState>(true);

                var winningNextState = UntriedNextStates.FirstOrDefault(state => state.Winner == GameState.CurrentPlayerId);
                if(winningNextState != null)
                {
                    UntriedNextStates.Clear();
                    UntriedNextStates.Add(winningNextState);
                    UntriedNextStates.TrimExcess();
                }
            }

            public override string ToString()
                => $"{TurnTaken, -10} {NumWins, 5}/{NumRuns, -5}, HS={HeuristicScoreForPrevPlayer,5:F2}"
                + $", {HeuristicValue,5:F2} + {ExploitationValue:F2} + {ExplorationValue:F2} = {SelectionPreferenceValue:F2}";

            public void ForgetParent() => Parent = null;

            public void BuildTree(CancellationToken token, Random random = null)
            {
                random ??= new Random();

                while(!token.IsCancellationRequested)
                {
                    var node = this;

                    // select descendant
                    while(!node.UntriedNextStates.Any() && !node.GameState.HasWinner)
                    {
                        node = node.Children.MaxElementBy(child => child.SelectionPreferenceValue);
                    }

                    // expand
                    if(node.UntriedNextStates.Any())
                    {
                        var lastIdx = node.UntriedNextStates.Count - 1;
                        var stateToTry = node.UntriedNextStates[lastIdx];
                        node.UntriedNextStates.RemoveAt(lastIdx);

                        if(node.UntriedNextStates.Count == 0)
                        {
                            node.UntriedNextStates.TrimExcess();
                        }

                        var child = new Node(node, stateToTry);
                        node.Children.Add(child);
                        node = child;
                    }

                    var terminalState = node.GameState;

                    // simulate
                    while(!terminalState.HasWinner)
                    {
                        terminalState = terminalState.WeightedRandomNextState<TTurn, TGameState>(random);
                    }

                    // back propagate simulation results
                    while(node != null)
                    {
                        node.NumRuns++;

                        if(terminalState.Winner == node.Parent?.GameState.CurrentPlayerId)
                        {
                            node.NumWins++;
                        }

                        node = node.Parent;
                    }
                }
            }

        }


    }
}
