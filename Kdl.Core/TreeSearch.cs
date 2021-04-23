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
    public class TreeSearch<TTurn,TGameState>
        where TTurn : ITurn
        where TGameState : IGameState<TTurn,TGameState>
    {
        public static AppraisedPlayerTurn<TTurn,TGameState> FindBestTurn(
            TGameState state,
            int analysisLevel,
            CancellationToken cancellationToken,
            out int numStatesVisited,
            double descendProportion = 1.0)
        {
            numStatesVisited = 0;
            return FindBestTurn(
                state,
                state.CurrentPlayerId,
                analysisLevel,
                cancellationToken,
                ref numStatesVisited,
                descendProportion);
        }

        // reminder: analysisPlayerId will never be a stranger
        protected static AppraisedPlayerTurn<TTurn,TGameState> FindBestTurn(
            TGameState currState,
            int analysisPlayerId,
            int analysisLevel,
            CancellationToken cancellationToken,
            ref int numStates,
            double descendProportion = 1.0)
        {
            numStates++;

            if(currState.HasWinner || analysisLevel == 0)
            {
                return new AppraisedPlayerTurn<TTurn,TGameState>(currState.HeuristicScore(analysisPlayerId), currState.PrevTurn, currState);
            }

            var appraisalIsForCurrentPlayer = analysisPlayerId == currState.CurrentPlayerId;
            var bestTurn = new AppraisedPlayerTurn<TTurn,TGameState>(double.MinValue, default, default);
            var turns = currState.PossibleTurns();
            var childStates = turns.Select(turn => currState.AfterTurn(turn));

            if(descendProportion < 1.0 && analysisLevel > 1)
            {
                childStates = childStates
                    .OrderByDescending(childState => childState.HeuristicScore(currState.CurrentPlayerId))
                    .Take(1 + (int)(descendProportion * turns.Count));
            }

            foreach(var childState in childStates)
            {
                var hypoAppraisedTurn = FindBestTurn(
                    childState,
                    currState.CurrentPlayerId,
                    analysisLevel - 1,
                    cancellationToken,
                    ref numStates,
                    descendProportion);

                if(currState.CurrentPlayerId != childState.CurrentPlayerId)
                {
                    hypoAppraisedTurn.Appraisal = hypoAppraisedTurn.EndingState.HeuristicScore(currState.CurrentPlayerId);
                }

                if (bestTurn.Appraisal < hypoAppraisedTurn.Appraisal)
                {
                    bestTurn = hypoAppraisedTurn;
                    bestTurn.Turn = childState.PrevTurn;
                }

                if(cancellationToken.IsCancellationRequested)
                {
                    return bestTurn;
                }
            }

            return bestTurn;
        }

    }

}
