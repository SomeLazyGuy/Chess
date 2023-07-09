using UnityEngine;

namespace ChessNamespace {
    public enum PieceColor {
        None = -1,
        White,
        Black
    }
    public enum CastleDirection {
        KingAndQueen,
        King,
        Queen,
        None
    }
    public enum PieceType {
        None = -1,
        King,
        Queen,
        Rook,
        Bishop,
        Knight,
        Pawn
    }
    public enum GameMode {
        Standard,
        Custom,
        Blitz,
        Rapid
    }

    public enum GameOverReason {
        Checkmate,
        Timeout,
        Resignation,
        InsuficientMaterial,
        TimeoutVsInsuficientMaterial,
        Stalemate,
        Draw50MoveRule,
        Repetition,
        Agreement
    }

    public struct FEN {
        public string position;
        public PieceColor colorToMove;
        public CastleDirection castleDirectionWhite;
        public CastleDirection castleDirectionBlack;
        public Vector2Int enPassantTarget;
        public int halfMoves;
        public int fullMoves;
    }

    public struct Move {
        public int originalFile;
        public int originalRank;

        public int targetFile;
        public int targetRank;

        public bool capture;

        public Vector2Int GetOriginalPosition() {
            return new Vector2Int(originalFile, originalRank);
        }

        public Vector2Int GetTargetPosition() {
            return new Vector2Int(targetFile, targetRank);
        }

        public Move(Move move, bool capture) {
            this = move;
            this.capture = capture;
        }

        public Move(int originalFile, int originalRank, int targetFile, int targetRank, bool capture = false) {
            this.originalFile = originalFile;
            this.originalRank = originalRank;
            this.targetFile = targetFile;
            this.targetRank = targetRank;
            this.capture = capture;
        }

        public Move(Vector2Int originalPosition, Vector2Int targetPosition, bool capture = false) {
            originalFile = originalPosition.x;
            originalRank = originalPosition.y;
            targetFile = targetPosition.x;
            targetRank = targetPosition.y;
            this.capture = capture;
        }

        public Move(Piece piece, Vector2Int targetPosition, bool capture = false) {
            originalFile = piece.file;
            originalRank = piece.rank;
            targetFile = targetPosition.x;
            targetRank = targetPosition.y;
            this.capture = capture;
        }

        public Move(Square square, Vector2Int targetPosition, bool capture = false) {
            originalFile = square.file;
            originalRank = square.rank;
            targetFile = targetPosition.x;
            targetRank = targetPosition.y;
            this.capture = capture;
        }

        public Move(string moveString, bool capture = false) {
            originalFile = (moveString[0] % 32) - 1;
            originalRank = int.Parse(moveString[1].ToString()) - 1;
            targetFile = (moveString[2] % 32) - 1;
            targetRank = int.Parse(moveString[3].ToString()) - 1;
            this.capture = capture;
        }
    }

    public static class Chess {
        public static bool alignWithHeight;
        public static bool local = true;
        public static bool flipBoard;
        public static int clockTime;
        public static PieceColor localColor = PieceColor.White;
        public static GameMode gameMode = GameMode.Standard;
        public static Color32 lightSquareColor = new Color32(239, 216, 183, 255);
        public static Color32 darkSquareColor = new Color32(180, 135, 102, 255);

        public static void Save() {
            SaveData saveData = new SaveData();
            saveData.lightSquareColor = lightSquareColor;
            saveData.darkSquareColor = darkSquareColor;
            saveData.stockfishElo = Stockfish.elo;
            saveData.flipBoard = flipBoard;

            string json = JsonUtility.ToJson(saveData);
            System.IO.File.WriteAllText(Application.dataPath + "/save.json", json);

            Debug.Log("Data saved");
        }

        public static void Load() {
            string filePath = Application.dataPath + "/save.json";
            if (System.IO.File.Exists(filePath)) {
                string json = System.IO.File.ReadAllText(filePath);
                SaveData saveData = JsonUtility.FromJson<SaveData>(json);

                lightSquareColor = saveData.lightSquareColor;
                darkSquareColor = saveData.darkSquareColor;
                Stockfish.elo = saveData.stockfishElo;
                flipBoard = saveData.flipBoard;
            } else {
                Debug.LogError("No save data found");
                Debug.Log("Default data loaded");
                lightSquareColor = new Color32(239, 216, 183, 255);
                darkSquareColor = new Color32(180, 135, 102, 255);
                Stockfish.elo = -1;
                flipBoard = false;
            }
        }
    }

    public static class Stockfish {
        public static int elo = -1;
        public static int movetimeMS = 5000;

        private static System.Diagnostics.Process stockfishProcess;

        public static bool SetupStockfish() {
            stockfishProcess = new System.Diagnostics.Process();
            stockfishProcess.StartInfo.FileName = Application.dataPath + "/Stockfish/stockfish-windows-x86-64-avx2.exe";
            stockfishProcess.StartInfo.UseShellExecute = false;
            stockfishProcess.StartInfo.RedirectStandardInput = true;
            stockfishProcess.StartInfo.RedirectStandardOutput = true;
            stockfishProcess.StartInfo.CreateNoWindow = true;
            stockfishProcess.Start();

            while (true) {
                string response = stockfishProcess.StandardOutput.ReadLine();
                if (response == "Stockfish 16 by the Stockfish developers (see AUTHORS file)") break;
            }

            stockfishProcess.StandardInput.WriteLine("isready");
            while (true) {
                string response = stockfishProcess.StandardOutput.ReadLine();
                if (response == "readyok") break;
                else if (response != "") {
                    Debug.LogError("Error setting up stockfish");
                    Debug.LogError(response);
                    return false;
                }
            }

            stockfishProcess.StandardInput.WriteLine("uci");
            if (elo > 0) {
                stockfishProcess.StandardInput.WriteLine("setoption name UCI_LimitStrength value true");
                stockfishProcess.StandardInput.WriteLine("setoption name UCI_Elo value " + elo);
            }
            stockfishProcess.StandardInput.WriteLine("ucinewgame");

            return true;
        }

        public static void StopStockfish() {
            if (stockfishProcess == null) return;

            stockfishProcess.StandardInput.WriteLine("stop");
            stockfishProcess.StandardInput.WriteLine("quit");

            if (stockfishProcess == null) return;

            stockfishProcess.Close();
        }

        public static Move GetBestMove(string fen, float wTime, float bTime) {
            stockfishProcess.StandardInput.WriteLine("position fen " + fen);

            if (Chess.gameMode != GameMode.Standard) {
                stockfishProcess.StandardInput.WriteLine("go wtime " + wTime + " btime " + bTime + " movetime " + movetimeMS);
            } else {
                stockfishProcess.StandardInput.WriteLine("go movetime " + movetimeMS);
            }

            while (true) {
                string response = stockfishProcess.StandardOutput.ReadLine();

                if (response.Contains("bestmove")) {
                    string moveString = response.Substring(9, 4);
                    return new Move(moveString);
                }
            }
        }
    }

    class SaveData {
        public Color32 lightSquareColor;
        public Color32 darkSquareColor;
        public int stockfishElo;
        public bool flipBoard;
    }
}