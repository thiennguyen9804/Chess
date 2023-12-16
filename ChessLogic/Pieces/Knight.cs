﻿namespace ChessLogic
{
    [Serializable]
    public class Knight : Piece
    {
        public override int Weight { get; }
        public override PieceType Type => PieceType.Knight;
        public override Player Color { get; }
        public Knight(Player color)
        {
            Color = color;
            if (Color == Player.White)
                Weight = 30;
            else
                Weight = -30;
        }
        public override Piece Copy()
        {
            Knight copy = new Knight(Color);
            copy.HasMoved = HasMoved;
            return copy;
        }
        private static IEnumerable<Position> PotentialToPositions(Position from)
        {
            foreach (Direction vDir in new Direction[] {Direction.North, Direction.South})
            {
                foreach (Direction hDir in new Direction[] {Direction.West, Direction.East})
                {
                    yield return from + (2 * vDir) + hDir;
                    yield return from + (2 * hDir) + vDir;
                }
            }
        }
        private IEnumerable<Position> MovesPositions(Position from, Board board)
        {
            return PotentialToPositions(from).Where(pos => Board.IsInside(pos)
                && (board.IsEmpty(pos) || board[pos].Color != Color));
        }
        public override IEnumerable<Move> GetMoves(Position from, Board board)
        {
            return MovesPositions(from, board).Select(to => new NormalMove(from, to));
        }
    }
}
