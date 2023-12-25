using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using ChessLogic;
using Microsoft.CodeAnalysis;
using SocketProject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace ChessUI
{
    public partial class MainWindow : Window
    {
        private readonly Image[,] pieceImages = new Image[8, 8];
        private readonly Rectangle[,] highlights = new Rectangle[8, 8];
        private readonly Dictionary<Position, Move> moveCache = new Dictionary<Position, Move>();
        private GameState gameState;
        private Position selectedPos = null;
        private SocketManager socket;
        private LoginMenu loginMenu;
        private Board board = new Board();
        private Stack<Board> stack = new Stack<Board>();
        private Stack<GameState> gameStateStack = new Stack<GameState>();
        private bool isLANRun = false;
        private Player player;
        private bool isAIActive = false;
        //void SetGameState(GameState gs) => gameState = gs;
        //string GetText() => loginMenu.MessageTextBlock.Text ?? "";
        public MainWindow()
        {
            InitializeComponent();
            socket = new SocketManager();
            ShowLoginMenu();
            InitializeBoard();
            gameState = new GameState(Player.White, board.Initial());
            DrawBoard(gameState.Board);
            SetCursor(gameState.CurrentPlayer);
        }
        private void InitializeBoard()
        {
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    Image image = new Image();
                    pieceImages[r, c] = image;
                    PieceGrid.Children.Add(image);
                    Rectangle highlight = new Rectangle();
                    highlights[r, c] = highlight;
                    HighlightGrid.Children.Add(highlight);
                }
            }
        }
        private void DrawBoard(Board board)
        {
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    Piece piece = board[r, c];
                    pieceImages[r, c].Source = Images.GetImage(piece); // call from invalid thread
                }
            }
        }
        private void BoardGrid_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (IsMenuOnScreen())
                return;

            if (isLANRun && player != gameState.CurrentPlayer)
                return;

            Point point = e.GetPosition(BoardGrid);
            Position pos = ToSquarePosition(point);

            if (selectedPos == null)
            {
                OnFromPostionSelected(pos);
            }
            else
            {
                OnToPositionSelected(pos);
            }

            if (isLANRun)
            {
                socket.Send(new SocketData((int)SocketCommand.SEND_GAME_STATE, "", gameState));
                Listen();
                //BoardGrid.IsEnabled = false;
            }
        }

        #region Function to redraw image when receive

        async void RunSetImage(Image i, Piece p)
        {
            _ = Task.Run(() => OnImageFromAnotherThread(i, p));
        }

        /*
        void SetText(string text) => loginMenu.MessageTextBlock.Text = text;
        string GetText() => loginMenu.MessageTextBlock.Text ?? "";
        */
        async void OnImageFromAnotherThread(Image i, Piece p)
        {
            try
            {
                Dispatcher.UIThread.Post(() => SetImage(i, p));
                //var result = await Dispatcher.UIThread.InvokeAsync(GetImage);
            }
            catch (Exception)
            {
                throw;
            }
        }


        void SetImage(Image i, Piece p) => i.Source = Images.GetImage(p);
        //IImage GetImage(Image i) => i.Source ?? null;

        #endregion

        void DrawBoard_OtherPlayer(Board board)
        {
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    Piece piece = board[r, c];
                    //pieceImages[r, c].Source = Images.GetImage(piece); // call from invalid thread
                    RunSetImage(pieceImages[r, c], piece);
                }
            }
        }

        public void BoardGrid_OtherPlayerPointerPressed(GameState otherGameState)
        {
            gameState = otherGameState;
            //gameState.CurrentPlayer.Oppenent();
            DrawBoard_OtherPlayer(gameState.Board);
            //isTurn = true;
        }

        private Position ToSquarePosition(Point point)
        {
            double squareSize = (BoardGrid.Bounds.Width / 8);
            int row = (int)(point.Y / squareSize);
            int col = (int)(point.X / squareSize);
            return new Position(row, col);
        }
        private void OnFromPostionSelected(Position pos)
        {
            IEnumerable<Move> moves = gameState.LegalMovesForPiece(pos);
            if (moves.Any())
            {
                selectedPos = pos;
                CacheMoves(moves);
                ShowHighlights();
            }
        }
        private void OnToPositionSelected(Position pos)
        {
            selectedPos = null;
            HideHighlights();

            if (moveCache.TryGetValue(pos, out Move move))
            {
                if (move.Type == MoveType.PawnPromotion)
                {
                    HandlePromotion(move.FromPos, move.ToPos);
                }
                else
                {
                    HandleMove(move);
                }
            }

        }
        private void HandlePromotion(Position from, Position to)
        {
            pieceImages[to.Row, to.Column].Source = Images.GetImage(gameState.CurrentPlayer, PieceType.Pawn);
            pieceImages[from.Row, to.Column].Source = null;

            PromotionMenu promMenu = new PromotionMenu(gameState.CurrentPlayer);
            MenuContainer.Content = promMenu;

            promMenu.PieceSelected += type =>
            {
                MenuContainer.Content = null;
                Move promMove = new PawnPromotion(from, to, type);
                HandleMove(promMove);
            };
        }
        private void HandleMove(Move move)
        {
            gameState.MakeMove(move);
            DrawBoard(gameState.Board);
            SetCursor(gameState.CurrentPlayer);

            if (gameState.IsGameOver())
            {
                ShowGameOver();
            }

            /*
            if (isAIActive && gameState.CurrentPlayer == Player.Black)
            {
                //MakeRandomMove(gameState.CurrentPlayer);
                Board copyBoard = board.Copy();
                GameState copyGameState = new GameState(gameState.CurrentPlayer, copyBoard);
                Player AIPlayer = copyGameState.CurrentPlayer;
                Move bestMove = MiniMaxRoot(3, copyGameState, AIPlayer);
                gameState.MakeMove(bestMove);
                DrawBoard(gameState.Board);
                SetCursor(gameState.CurrentPlayer);
            }
            */

            if (isAIActive && gameState.CurrentPlayer == Player.Black)
                AIMakeMove(gameState.CurrentPlayer);
        }

        
        private void MakeRandomMove(Player player)
        {
            IEnumerable<Move> movesCollection = gameState.AllLegalMovesFor(player); // get all moves posible
            List<Move> moveList = movesCollection.ToList();
            Random random = new Random();
            if (!gameState.IsGameOver())
            {
                int randomNext = random.Next(0, moveList.Count - 1);
                HandleMove(moveList[randomNext]);
            }
        }
        
        private void AIMakeMove(Player player)
        {
            board.
            GameState copyGameState = new GameState(player, board.Copy());
            Move bestMove = MiniMaxRoot(3, copyGameState, player);
            HandleMove(bestMove);
        }

        #region AI code goes here

        private Move MiniMaxRoot(int depth, GameState gameState, Player player)
        {
            /*
            Board boardCopy = board.Copy();
            GameState gameStateCopy = new GameState(gameState.CurrentPlayer, boardCopy);
            */
            IEnumerable<Move> movesCollection = gameState.AllLegalMovesFor(player); // get all moves posible
            List<Move> moveList = movesCollection.ToList();

            int bestMove = -99999;
            Move bestMoveFound = new Move();

            for (int i = 0; i < moveList.Count; i++)
            {
                gameStateStack.Push(gameState);
                Move newGameMove = moveList[i];
                gameState.MakeMove(newGameMove);
                //DrawBoard(gameState.Board);

                int value = MiniMax(depth - 1, gameState, -10000, 10000, player.Oppenent());
                gameState = gameStateStack.Pop();

                if (value >= bestMove)
                {
                    bestMove = value;
                    bestMoveFound = newGameMove;
                }
            }


            return bestMoveFound;

        }

        private int MiniMax(int depth, GameState gameState, int alpha, int beta, Player player)
        {
            if (depth == 0)
            {
                return -gameState.Board.GetValue();
            }

            if (player == Player.Black)
            {
                IEnumerable<Move> movesCollection = gameState.AllLegalMovesFor(player); // get all moves posible
                List<Move> moveList = movesCollection.ToList();
                int bestMove = -99999;
                for (int i = 0; i < moveList.Count; i++)
                {
                    gameStateStack.Push(gameState);
                    Move newGameMove = moveList[i];
                    gameState.MakeMove(newGameMove);
                    bestMove = Math.Max(bestMove, MiniMax(depth - 1, gameState, alpha, beta, player.Oppenent()));
                    if (gameStateStack.Count > 0)
                    {
                        gameState = gameStateStack.Pop();
                    }

                    alpha = Math.Max(alpha, bestMove);
                    if (beta <= alpha)
                    {
                        return bestMove;
                    }
                }

                return bestMove;
            }

            else
            {
                int bestMove = 99999;
                IEnumerable<Move> movesCollection = gameState.AllLegalMovesFor(player); // get all moves posible
                List<Move> moveList = movesCollection.ToList();
                for (int i = 0; i < moveList.Count; i++)
                {
                    gameStateStack.Push(gameState);
                    Move newGameMove = moveList[i];
                    gameState.MakeMove(newGameMove);
                    bestMove = Math.Min(bestMove, MiniMax(depth - 1, gameState, alpha, beta, player.Oppenent()));
                    if (gameStateStack.Count > 0)
                    {
                        gameState = gameStateStack.Pop();
                    }

                    beta = Math.Min(beta, bestMove);
                    if (beta <= alpha)
                    {
                        return bestMove;
                    }
                }

                return bestMove;
            }
        }


        #endregion

        private void CacheMoves(IEnumerable<Move> moves)
        {
            moveCache.Clear();
            foreach (Move move in moves)
            {
                moveCache[move.ToPos] = move;
            }
        }
        private void ShowHighlights()
        {
            Color color = Color.FromArgb(255, 144, 238, 144);
            foreach (Position to in moveCache.Keys)
            {
                highlights[to.Row, to.Column].Fill = new SolidColorBrush(color);
            }
        }
        private void HideHighlights()
        {
            foreach (Position to in moveCache.Keys)
            {
                highlights[to.Row, to.Column].Fill = Brushes.Transparent;
            }
        }
        private void SetCursor(Player player)
        {
            if (player == Player.White)
            {
                Cursor = ChessCursors.WhiteCursor;
            }
            else
            {
                Cursor = ChessCursors.BlackCursor;
            }
        }
        private bool IsMenuOnScreen()
        {
            return MenuContainer.Content != null;
        }
        private void ShowGameOver()
        {
            GameOverMenu gameOverMenu = new GameOverMenu(gameState);
            MenuContainer.Content = gameOverMenu;
            gameOverMenu.OptionSelected += option =>
            {
                if (option == Option.Restart)
                {
                    MenuContainer.Content = null;
                    RestartGame();
                }
                else
                {
                    Close();
                }
            };
        }
        private void RestartGame()
        {
            selectedPos = null;
            HideHighlights();
            moveCache.Clear();
            gameState = new GameState(Player.White, board.Initial());
            DrawBoard(gameState.Board);
            SetCursor(gameState.CurrentPlayer);
        }
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!IsMenuOnScreen() && e.Key == Key.Escape)
            {
                ShowPauseMenu();
            }
        }
        private void ShowPauseMenu()
        {
            PauseMenu pauseMenu = new PauseMenu();
            MenuContainer.Content = pauseMenu;
            pauseMenu.OptionSelected += option =>
            {
                MenuContainer.Content = null;
                if (option == Option.Restart)
                {
                    RestartGame();
                }
            };
        }

        private void ShowLoginMenu()
        {
            loginMenu = new LoginMenu();
            loginMenu.IPTextBox.Text = socket.GetLocalIPv4(NetworkInterfaceType.Wireless80211);
            if (string.IsNullOrEmpty(loginMenu.IPTextBox.Text))
            {
                loginMenu.IPTextBox.Text = socket.GetLocalIPv4(NetworkInterfaceType.Ethernet);
            }

            MenuContainer.Content = loginMenu;
            loginMenu.OptionSelected += option =>
            {
                MenuContainer.Content = null;
                if (option == Option.Start)
                {
                    //player = null;
                    RestartGame();
                }
                else if (option == Option.LAN)
                {
                    isLANRun = true;
                    LANRun();
                } else if (option == Option.PlayAgainstAI)
                {
                    isAIActive = true;
                }
            };
        }

        #region LAN goes here
        void LANRun()
        {
            socket.IP = loginMenu.IPTextBox.Text;
            if (!socket.ConnectServer()) // is server
            {
                player = Player.White;
                socket.CreateServer();
                
            }
            else // is client
            {
                player = Player.Black;
                Listen();
            }
        }

        void Listen()
        {
            try
            {
                Thread listenThread = new Thread(() =>
                {
                    SocketData data = socket.Receive();
                    //_ = Task.Run(() => OnTextFromAnotherThread(data)); // data can be replaced by whatever u want
                    ProcessData(data);
                });

                listenThread.IsBackground = true;
                listenThread.Start();
            }
            catch
            {

            }

        }


        /*
        async void ShowMessage(string message)
        {
            _ = Task.Run(() => OnTextFromAnotherThread(message));
        }
        
        
        void SetText(string text) => loginMenu.MessageTextBlock.Text = text;
        string GetText() => loginMenu.MessageTextBlock.Text ?? "";
        
        async void OnTextFromAnotherThread(string text)
        {
            try
            {
                Dispatcher.UIThread.Post(() => SetText(text));
                var result = await Dispatcher.UIThread.InvokeAsync(GetText);
            }
            catch (Exception)
            {
                throw;
            }
        }
        */

        void ProcessData(SocketData data)
        {
            switch (data.Command)
            {
                case (int)SocketCommand.NOTIFY:
                    //ShowMessage(data.Message);
                    break;
                case (int)SocketCommand.SEND_GAME_STATE:
                    BoardGrid_OtherPlayerPointerPressed(data.GameState);
                    break;
                case (int)SocketCommand.QUIT:
                    break;
                default:
                    break;
            }

            Listen();
        }
        #endregion

    }

}