namespace ChessLogic
{
    [Serializable]
    public class Bishop : Piece
    {
        public override int Weight { get; }
        public override PieceType Type => PieceType.Bishop;
        public override Player Color { get; }
        private static readonly Direction[] dirs = new Direction[]
        {
            Direction.NorthEast,
            Direction.NorthWest,
            Direction.SouthEast,
            Direction.SouthWest
        };
        public Bishop(Player color)
        {
            Color = color;
            if (Color == Player.White)
                Weight = 30;
            else
                Weight = -30;

        }
        public override Piece Copy()
        {
            Bishop copy = new Bishop(Color);
            copy.HasMoved = HasMoved;
            return copy;
        }
        public override IEnumerable<Move> GetMoves(Position from, Board board)
        {
            return MovePositionsInDirs(from, board, dirs).Select(to => new NormalMove(from, to));
        }
    }
}
