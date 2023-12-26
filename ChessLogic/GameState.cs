﻿using System.ComponentModel.Design;

namespace ChessLogic
{
    [Serializable]
    public class GameState
    {
        public Board Board { get; set; }
        public Player CurrentPlayer { get; private set; }
        public Result Result { get; set; } = null;
        private int noCaptureOrPawnMoves = 0;
        private string stateString;
        private readonly Dictionary<string, int> stateHistory = new Dictionary<string, int>();
        public TimeSpan timerWhite { get; set; } = new TimeSpan(0, 10, 0);
        public TimeSpan timerBlack { get; set; } = new TimeSpan(0, 10, 0);
        public GameState(Player player, Board board) 
        {
            CurrentPlayer = player;
            Board = board;

            stateString = new StateString(CurrentPlayer, Board).ToString();
            stateHistory[stateString] = 1;
        }
        public IEnumerable<Move> LegalMovesForPiece(Position pos)
        {
            if (Board.IsEmpty(pos) || Board[pos].Color!=CurrentPlayer)
            {
                return Enumerable.Empty<Move>();
            }

            Piece piece = Board[pos];
            IEnumerable<Move> moveCandidates = piece.GetMoves(pos, Board);
            return moveCandidates.Where(move => move.IsLegal(Board));
        }
        public void MakeMove(Move move)
        {
            Board.SetPawnSkipPosition(CurrentPlayer, null);
            bool captureOrPawn = move.Execute(Board);
            if (captureOrPawn)
            {
                noCaptureOrPawnMoves = 0;
                stateHistory.Clear();
            }
            else
            {
                noCaptureOrPawnMoves++;
            }
            CurrentPlayer = CurrentPlayer.Oppenent();
            //AIMakeMove();
            UpdateStateString();
            CheckForGameOver();
        }

       

        public IEnumerable<Move> AllLegalMovesFor(Player player)
        {
            IEnumerable<Move> moveCandidates = Board.PiecePositionsFor(player).SelectMany(pos =>
            {
                Piece piece = Board[pos];
                return piece.GetMoves(pos, Board);
            });
            return moveCandidates.Where(move => move.IsLegal(Board));
        }
        private void CheckForGameOver()
        {
            if(!AllLegalMovesFor(CurrentPlayer).Any()) 
            {
                if (Board.IsInCheck(CurrentPlayer))
                {
                    Result = Result.Win(CurrentPlayer.Oppenent(), EndReason.Checkmate);
                }
                else
                {
                    Result = Result.Draw(EndReason.Stalemate);
                }
            }
            else if (Board.InsufficientMaterial())
            {
                Result = Result.Draw(EndReason.InsufficientMaterial);
            }
            else if (FiftyMoveRule())
            {
                Result = Result.Draw(EndReason.FiftyMoveRule);
            }
            else if(ThreefoldRepetition())
            {
                Result = Result.Draw(EndReason.ThreefoldRepetion);
            }
        }
        public bool IsGameOver()
        {
            return Result != null;
        }
        private bool FiftyMoveRule()
        {
            int fullMoves = noCaptureOrPawnMoves / 2;
            return fullMoves == 50;
        }

        private void UpdateStateString()
        {
            stateString = new StateString(CurrentPlayer, Board).ToString();

            if (!stateHistory.ContainsKey(stateString))
            {
                stateHistory[stateString] = 1;
            }
            else
            {
                stateHistory[stateString]++;
            }
        }
        private bool ThreefoldRepetition()
        {
            return stateHistory[stateString] == 3;
        }
    }
}
