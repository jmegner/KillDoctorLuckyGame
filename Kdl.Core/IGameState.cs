using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Kdl.Core
{
    public interface IGameState<TTurn>
        where TTurn : ITurn
    {
        int CurrentPlayerId { get; }
        bool HasWinner { get; }
        int Winner { get; }
        TTurn PrevTurn { get; }
        IGameState<TTurn> PrevState { get; }

        IGameState<TTurn> AfterTurn(TTurn turn);
        List<TTurn> PossibleTurns();
        double HeuristicScore(int analysisPlayerId);

    }

    public static class GameStateExtensions
    {
        public static List<TGameState> NextStates<TTurn,TGameState>(this TGameState gameState, bool sortAscending = false)
            where TTurn : ITurn
            where TGameState : IGameState<TTurn>
        {
            var turns = gameState.PossibleTurns();
            var nextStates = turns.Select(turn => (TGameState)gameState.AfterTurn(turn));

            double stateToScore(TGameState state) => state.HeuristicScore(gameState.CurrentPlayerId);

            nextStates
                = (sortAscending
                ? nextStates.OrderBy(stateToScore)
                : nextStates.OrderByDescending(stateToScore)
                );
            var scores = nextStates.Select(state => stateToScore(state)).ToList();
            return nextStates.ToList();
        }

        public static TGameState WeightedRandomNextState<TTurn,TGameState>(
            this TGameState gameState,
            Random random)
            where TTurn : ITurn
            where TGameState : IGameState<TTurn>
        {
            var newStates = gameState.NextStates<TTurn,TGameState>(false);

            var winningNewState = newStates.FirstOrDefault(state => state.Winner == gameState.CurrentPlayerId);
            if(winningNewState != null)
            {
                return winningNewState;
            }

            var numStates = newStates.Count();
            /*
            var desiredLinearWeightSum = random.Next(numStates * (numStates + 1) / 2);
            var stateIdx = (int)(numStates + 0.5 - 0.5 * Math.Sqrt(
                4 * numStates * numStates
                + 4 * numStates
                - 8 * desiredLinearWeightSum
                + 1));
            */
            var desiredExponentialWeightSum = random.NextDouble();
            const double decayFactor = 0.9;
            var stateIdx = (int)(
                Math.Log(1 + desiredExponentialWeightSum * (Math.Pow(decayFactor, numStates) - 1))
                / Math.Log(decayFactor)
                );
            return newStates.Skip(stateIdx).First();
        }

    }
}
