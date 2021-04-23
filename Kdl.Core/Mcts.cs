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
        where TGameState : IGameState<TTurn,TGameState>
    {
        protected Node _root;
        protected Random _random;

        public Mcts(TGameState gameState, Random random = null)
        {
            _random = random ?? new Random();
            _root = new Node(null, gameState);
        }

        public int NumRuns => _root.Children.Sum(child => child.NumRuns);
        public double NumWins => _root.Children.Sum(child => child.NumWins);

        public IEnumerable<Node> GetTopTurns(CancellationToken token)
        {
            _root.BuildTree(token, true, _random);
            //_root.BuildTreeParallel(token);
            IsTreeValid();
            return _root.Children
                .OrderByDescending(child => child.ExploitationValue)
                .OrderByDescending(child => child.NumRuns);
        }

        protected bool IsTreeValid(Node node = null, List<int> childrenIdxs = null)
        {
            node ??= _root;
            childrenIdxs ??= new();
            var isValid = true;

            var childrenRunSum = node.Children.Sum(child => child.NumRuns);
            if(!node.GameState.HasWinner
                && childrenRunSum != node.NumRuns
                && childrenRunSum + 1 != node.NumRuns)
            {
                Console.WriteLine($"node({string.Join(',', childrenIdxs)}) {node} has NumRuns mismatch");
                isValid = false;
            }

            /*
            if(node.Parent != null)
            {
                var choosingPlayer = node.Parent.GameState.CurrentPlayerId;
                var winSum = 0;

                foreach(var child in node.Children)
                {
                    if(child.GameState.CurrentPlayerId)
                }
            }
            */

            for(int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                childrenIdxs.Add(i);

                if(!IsTreeValid(child, childrenIdxs))
                {
                    isValid = false;
                }

                childrenIdxs.RemoveAt(childrenIdxs.Count - 1);
            }

            return isValid;
        }

        public bool Reroot(TGameState goalState)
        {
            var stateHist = new List<TGameState>();
            var state = goalState;

            while (state != null)
            {
                stateHist.Add(state);
                state = state.PrevState;
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
            public const double ExplorationCoefficient = 1.4142135623730950488; // sqrt(2)
            public double HeuristicScoreForPrevPlayer { get; init; }
            public double ExploitationValue => NumWins / NumRuns;
            public double ExplorationValue => ExplorationCoefficient * Math.Sqrt(Math.Log(Parent.NumRuns) / NumRuns);
            public double HeuristicValue => Math.Atan(HeuristicScoreForPrevPlayer) / Math.Sqrt(NumRuns); // Math.Pow(NumRuns, 1.0 / 3.0);
            public double Uct => ExploitationValue + ExplorationValue;
            public double SelectionPreferenceValue => ExploitationValue + ExplorationValue + HeuristicValue;

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
                => $"{TurnTaken, -10} {NumWins, 6}/{NumRuns, -6}, HS={HeuristicScoreForPrevPlayer,6:F3}"
                + $", {HeuristicValue,6:F4} + {ExploitationValue:F4} + {ExplorationValue:F4} = {SelectionPreferenceValue:F4}";

            public void ForgetParent() => Parent = null;

            public double HypotheticalSelectionPreferenceValue(TGameState gameState, int decidingPlayerId, int parentNumRuns)
                => Math.Atan(gameState.HeuristicScore(decidingPlayerId))
                + 0.5 // pretend half-win
                + ExplorationCoefficient * Math.Sqrt(Math.Log(parentNumRuns));

            public void BuildTree(CancellationToken token, bool expandAsSoonAsPossible, Random random = null)
            {
                random ??= new Random();

                while(!token.IsCancellationRequested)
                {
                    var node = this;

                    if(expandAsSoonAsPossible)
                    {
                        // phase: select
                        while(!node.UntriedNextStates.Any() && !node.GameState.HasWinner)
                        {
                            node = node.Children.MaxElementBy(child => child.SelectionPreferenceValue);
                        }

                        // phase: expand
                        node = node.Expand();
                    }
                    else // phase: hybrid select+expand
                    {
                        while(!node.GameState.HasWinner)
                        {
                            Node bestChild = default;
                            var childPrefValue = double.MinValue;
                            TGameState bestUntriedState = default;
                            var untriedStatePrefValue = double.MinValue;

                            if(node.Children.Any())
                            {
                                (bestChild, childPrefValue) = node.Children.MaxElementAndCriteria(child
                                    => child.SelectionPreferenceValue);
                            }

                            if(node.UntriedNextStates.Any())
                            {
                                bestUntriedState = node.UntriedNextStates[node.UntriedNextStates.Count - 1];
                                untriedStatePrefValue = HypotheticalSelectionPreferenceValue(
                                    bestUntriedState,
                                    node.GameState.CurrentPlayerId,
                                    node.NumRuns);
                            }

                            if(childPrefValue > untriedStatePrefValue)
                            {
                                node = bestChild;
                            }
                            else
                            {
                                node = node.Expand();
                                break;
                            }
                        }
                    }

                    // phase: simulate
                    var terminalState = SimulateToEnd(node.GameState, random);

                    // phase: back propagate simulation results
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

            public void BuildTreeParallel(CancellationToken token)
            {
                var tasks = new Task[Environment.ProcessorCount];
                foreach(var i in Environment.ProcessorCount.ToRange())
                {
                    tasks[i] = Task.Run(() => BuildTreeParallelPiece(token));
                }

                Task.WaitAll(tasks);
            }

            public void BuildTreeParallelPiece(CancellationToken token)
            {
                var random = new Random();

                while(!token.IsCancellationRequested)
                {
                    var node = this;
                    Monitor.Enter(node);

                    // select descendant
                    while(!node.UntriedNextStates.Any() && !node.GameState.HasWinner)
                    {
                        // deliberately not locking child nodes;
                        // my hope is that it's okay for SelectionPreferenceValue to be slightly wrong
                        var selectedChild = node.Children.MaxElementBy(child => child.SelectionPreferenceValue);
                        Monitor.Exit(node);
                        node = selectedChild;
                        Monitor.Enter(node);
                    }

                    var parentIsLocked = false;

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
                        parentIsLocked = true;
                        Monitor.Enter(node);
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
                        if(!parentIsLocked && node.Parent != null)
                        {
                            Monitor.Enter(node.Parent);
                            parentIsLocked = true;
                        }

                        node.NumRuns++;

                        if(terminalState.Winner == node.Parent?.GameState.CurrentPlayerId)
                        {
                            node.NumWins++;
                        }

                        Monitor.Exit(node);
                        node = node.Parent; // already done Monitor.Enter on node.Parent
                        parentIsLocked = false;
                    }
                }
            }

            protected Node Expand()
            {
                if(!UntriedNextStates.Any())
                {
                    return this;
                }

                var lastIdx = UntriedNextStates.Count - 1;
                var stateToTry = UntriedNextStates[lastIdx];
                UntriedNextStates.RemoveAt(lastIdx);

                if(UntriedNextStates.Count == 0)
                {
                    UntriedNextStates.TrimExcess();
                }

                var child = new Node(this, stateToTry);
                Children.Add(child);
                return child;
            }

            protected static TGameState SimulateToEnd(TGameState gameState, Random random)
            {
                while (!gameState.HasWinner)
                {
                    gameState = gameState.WeightedRandomNextState<TTurn, TGameState>(random);
                    //var nextStates = gameState.NextStates<TTurn, TGameState>();
                    //gameState = nextStates[random.Next(nextStates.Count)];
                }

                return gameState;
            }


        }
    }
}
