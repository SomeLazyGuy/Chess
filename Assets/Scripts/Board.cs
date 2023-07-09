using ChessNamespace;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class Board : MonoBehaviour {
    [SerializeField] private GameManager gameManager;

    public string position = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    //Custom types
    public static Dictionary<char, PieceType> PieceTypes = new Dictionary<char, PieceType>() {
        { 'k', PieceType.King },
        { 'q', PieceType.Queen },
        { 'r', PieceType.Rook },
        { 'b', PieceType.Bishop },
        { 'n', PieceType.Knight },
        { 'p', PieceType.Pawn },
        { 'K', PieceType.King },
        { 'Q', PieceType.Queen },
        { 'R', PieceType.Rook },
        { 'B', PieceType.Bishop },
        { 'N', PieceType.Knight },
        { 'P', PieceType.Pawn }
    };
    public static Dictionary<PieceType, char> PieceTypesReversed = new Dictionary<PieceType, char>() {
        { PieceType.King  , 'k' },
        { PieceType.Queen , 'q' },
        { PieceType.Rook  , 'r' },
        { PieceType.Bishop, 'b' },
        { PieceType.Knight, 'n' },
        { PieceType.Pawn  , 'p' },
    };
    public static string[] Letters = { "A", "B", "C", "D", "E", "F", "G", "H" };

    //Sprites
    [SerializeField] private Sprite squareSprite;
    [SerializeField] private Sprite captureSprite;
    [SerializeField] private Sprite[] whiteSprites;
    [SerializeField] private Sprite[] blackSprites;
    [SerializeField] private Sprite[] letterSprites;
    [SerializeField] private Sprite[] numberSprites;

    //Editor fields
    [SerializeField] private Color darkSqaureColor = Color.black;
    [SerializeField] private Color lightSquareColor = Color.white;
    [SerializeField] private GameObject moveIndicatorPrefab;
    [SerializeField] private GameObject horizontalContainer;
    [SerializeField] private GameObject verticalContainer;

    //Object references
    private GameObject whiteUI;
    private GameObject blackUI;
    private GameObject topReference;
    private GameObject bottomReference;
    private TextMeshProUGUI whiteClock;
    private TextMeshProUGUI blackClock;
    private TextMeshProUGUI currentClock;
    private GameObject boardParent;
    private GameObject squaresParent;
    private GameObject piecesParent;
    private GameObject coordinatesParent;
    private List<GameObject> moveIndicators = new List<GameObject>();
    private List<GameObject> lastMoveMarker = new List<GameObject>();
    private List<GameObject> coordinates = new List<GameObject>();
    private Piece[,] pieces = new Piece[8, 8];
    private Piece emptyPiece;
    private Piece selectedPiece;

    //Game data
    private FEN fen;
    private List<FEN> moveLog = new List<FEN>();
    private Move lastMove;
    private bool enPassantSet = false;
    private bool whiteKingMoved = false;
    private bool blackKingMoved = false;
    private bool whiteQueenRookMoved = false;
    private bool whiteKingRookMoved = false;
    private bool blackQueenRookMoved = false;
    private bool blackKingRookMoved = false;
    private bool currentlyMoving = false;
    private float whiteTime = 0;
    private float blackTime = 0;
    private float currentTime;
    private float squareSize;
    private Vector2 safeSpace;
    private Vector3 startingPosition;
    private bool stockfishInitialized = false;
    private bool gettingMoveFromStockfish = false;
    private bool gameInitialized = false;

    private void Start() {
        lightSquareColor = Chess.lightSquareColor;
        darkSqaureColor = Chess.darkSquareColor;

        SetupGame();
    }

    private void Update() {
        //return if game setup isn't finished
        if (!gameInitialized) return;

        //check if we need stockfish and if it is finished initilizing
        if (!Chess.local && stockfishInitialized) {
            //check if it's stockfishs turn
            if (fen.colorToMove != Chess.localColor) {
                //check if we are already getting a move
                if (!gettingMoveFromStockfish && !currentlyMoving) {
                    gettingMoveFromStockfish = true;

                    //Request stockfish move
                    StartCoroutine(GetStockfishMove());
                }
            }
        }

        //get user mouse input
        if (Input.GetMouseButtonDown(0)) {
            //get mouse position
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mousePos2D = new Vector2(mousePos.x, mousePos.y);

            //return if already moving
            if (currentlyMoving) return;

            //cast ray to get hit object
            RaycastHit2D hit = Physics2D.Raycast(mousePos2D, Vector2Int.zero);
            //check if we hit any object
            if (hit) {
                //check if it was a square
                if (hit.collider.tag == "Square") {
                    //get clicked square
                    Square square = hit.collider.GetComponent<Square>();

                    //check if we already have selected a piece
                    if (selectedPiece) {
                        Piece newPiece = pieces[square.file, square.rank];
                        //check if piece was already selected
                        if (newPiece == selectedPiece) {
                            //deselect piece
                            selectedPiece = null;
                            SetMoveIndicators(null);
                            return;
                        }

                        //select new piece if it has the same color
                        if (newPiece.color == selectedPiece.color) {
                            selectedPiece = newPiece;
                            SetMoveIndicators(selectedPiece);
                            return;
                        }

                        //get move
                        Move move = GetMove(selectedPiece, square);
                        //check if move is valid and move piece
                        if (move.originalFile != -1) MovePiece(move);
                        else {
                            //deselect if not a valid move
                            selectedPiece = null;
                            SetMoveIndicators(null);
                        }
                    } else {
                        //get piece
                        Piece piece = pieces[square.file, square.rank];

                        //check if piece can be selected if not local
                        if (!Chess.local && piece.color != Chess.localColor && piece.color != PieceColor.None) return;
                        
                        //check if piece is not empty and hast correct color
                        if (piece.color == fen.colorToMove) {
                            //select piece
                            selectedPiece = piece;
                            SetMoveIndicators(piece);
                        }
                    }
                } else {
                    //deselect if click was not on the board
                    selectedPiece = null;
                    SetMoveIndicators(null);
                }
            }
        }
    }

    public void SetupGame() {
        if (boardParent) Destroy(boardParent);

        //create parents 
        boardParent = new GameObject("Board");
        squaresParent = new GameObject("Squares");
        squaresParent.transform.parent = boardParent.transform;
        piecesParent = new GameObject("Pieces");
        piecesParent.transform.parent = boardParent.transform;
        coordinatesParent = new GameObject("Coordinates");
        coordinatesParent.transform.parent = boardParent.transform;

        //create empty piece
        emptyPiece = boardParent.AddComponent<Piece>();
        emptyPiece.file = -1;
        emptyPiece.rank = -1;
        emptyPiece.color = PieceColor.None;
        emptyPiece.type = PieceType.None;

        //setup camera rotation
        Camera.main.transform.eulerAngles = new Vector3(0, 0, 0);

        //clear move markers
        for (int i = 0; i < lastMoveMarker.Count; i++) Destroy(lastMoveMarker[i]);
        lastMoveMarker.Clear();

        //setup board according to fen
        SetupBoard();
        LoadPositionFromFEN(position);

        //create stockfish instance
        if (!Chess.local) {
            stockfishInitialized = Stockfish.SetupStockfish();
        }

        //create coordinates
        SetupCoordinates();

        //switch between horizontal and vertical ui
        GameObject currentContainer = SetUIOrientation();

        //get all references
        blackUI = currentContainer.transform.GetChild(0).gameObject;
        whiteUI = currentContainer.transform.GetChild(1).gameObject;
        topReference = currentContainer.transform.GetChild(2).gameObject;
        bottomReference = currentContainer.transform.GetChild(3).gameObject;

        if (Chess.localColor == PieceColor.White) {
            //set ui position based on local color
            blackUI.transform.position = topReference.transform.position;
            whiteUI.transform.position = bottomReference.transform.position;

            //rotate ui based on local color and flip board property
            if (Chess.local && !Chess.flipBoard) {
                blackUI.transform.eulerAngles = new Vector3(0, 0, 180);
                whiteUI.transform.eulerAngles = new Vector3(0, 0, 0);
            }
        } else if(Chess.localColor == PieceColor.Black) {
            //set ui position based on local color
            blackUI.transform.position = bottomReference.transform.position;
            whiteUI.transform.position = topReference.transform.position;

            //rotate ui based on local color and flip board property
            if (Chess.local && !Chess.flipBoard) {
                whiteUI.transform.eulerAngles = new Vector3(0, 0, 180);
                blackUI.transform.eulerAngles = new Vector3(0, 0, 0);
            }

            FlipBoard();
        }

        //set clock reference based on selected rotation
        whiteClock = whiteUI.transform.GetChild(0).GetChild(0).gameObject.GetComponent<TextMeshProUGUI>();
        blackClock = blackUI.transform.GetChild(0).GetChild(0).gameObject.GetComponent<TextMeshProUGUI>();

        //initialize clock if needed
        if (Chess.gameMode != GameMode.Standard) {
            //set clock time
            whiteTime = Chess.clockTime;
            blackTime = Chess.clockTime;

            //set clock time text
            int minutes = (int)(whiteTime / 60);
            int seconds = (int)(whiteTime % 60);
            string time;
            if (Chess.clockTime > 60) time = minutes + ":" + seconds;
            else time = seconds + "";
            whiteClock.text = time;
            blackClock.text = time;

            //set current clock to use
            currentTime = whiteTime;
            currentClock = whiteClock;

            //start clock timer
            StartCoroutine(ClockCo());
        } else {
            whiteClock.transform.parent.gameObject.SetActive(false);
            blackClock.transform.parent.gameObject.SetActive(false);
        }

        gameInitialized = true;
    }

    public void DestroyAll() {
        Destroy(boardParent);
    }

    private void LoadPositionFromFEN(string fenString) {
        moveLog.Clear();
        fen = LoadFEN(fenString);
        moveLog.Add(fen);

        //Setup position
        SetupPieces(fen.position);
    }

    private FEN LoadFEN(string fenSting) {
        FEN _fen = new FEN();

        string[] fields = fenSting.Split(' ');

        _fen.position = fields[0];

        //Load player turn
        if (fields[1] == "w") _fen.colorToMove = PieceColor.White;
        else if (fields[1] == "b") _fen.colorToMove = PieceColor.Black;

        //Load player castle direction
        if (fields[2].Contains('-')) {
            _fen.castleDirectionWhite = CastleDirection.None;
            _fen.castleDirectionBlack = CastleDirection.None;
            whiteKingMoved = true;
            blackKingMoved = true;
        }
        if (fields[2].Contains('Q')) {
            _fen.castleDirectionWhite = CastleDirection.Queen;
            whiteKingRookMoved = true;
        }
        if (fields[2].Contains('K')) {
            if (fields[2].Contains('Q')) {
                _fen.castleDirectionWhite = CastleDirection.KingAndQueen;
                whiteKingRookMoved = false;
                whiteQueenRookMoved = false;
            } else {
                _fen.castleDirectionWhite = CastleDirection.King;
                whiteQueenRookMoved = true;
            }
        }
        if (fields[2].Contains('q')) {
            _fen.castleDirectionBlack = CastleDirection.Queen;
            blackKingRookMoved = true;
        }
        if (fields[2].Contains('k')) {
            if (fields[2].Contains('q')) {
                _fen.castleDirectionBlack = CastleDirection.KingAndQueen;
                blackKingRookMoved = false;
                blackQueenRookMoved = false;
            } else {
                _fen.castleDirectionBlack = CastleDirection.King;
                blackQueenRookMoved = true;
            }
        }

        //Load en passant target
        if (!fields[3].Contains("-")) {
            int file = 0;
            int rank = fields[3][0] - '0';
            for (int i = 0; i < Letters.Length; i++) if (fields[3][0] == Letters[i].ToCharArray()[0]) file = i;
            _fen.enPassantTarget = new Vector2Int(file, rank);
        }

        //Load halfmoves
        int.TryParse(fields[4], out _fen.halfMoves);

        //Load fullmoves
        int.TryParse(fields[5], out _fen.fullMoves);

        return _fen;
    }

    private string CreateFENString(FEN fen) {
        string fenString = "";

        fenString += fen.position;

        fenString += " " + (fen.colorToMove == PieceColor.White ? "w" : "b");

        string castleString = "";

        if (fen.castleDirectionWhite == CastleDirection.KingAndQueen) castleString += "KQ";
        if (fen.castleDirectionWhite == CastleDirection.King) castleString += "K";
        if (fen.castleDirectionWhite == CastleDirection.Queen) castleString += "Q";

        if (fen.castleDirectionBlack == CastleDirection.KingAndQueen) castleString += "kq";
        if (fen.castleDirectionBlack == CastleDirection.King) castleString += "k";
        if (fen.castleDirectionBlack == CastleDirection.Queen) castleString += "q";

        if (castleString == "") castleString = "-";

        fenString += " " + castleString;

        if (fen.enPassantTarget.x > 0) fenString += " " + Letters[fen.enPassantTarget.x] + (fen.colorToMove == PieceColor.White ? fen.enPassantTarget.y - 1 : fen.enPassantTarget.y + 1);
        else fenString += " -";

        fenString += " " + fen.halfMoves;
        fenString += " " + fen.fullMoves;

        return fenString;
    }

    private FEN CreateFEN() {
        FEN _fen = fen;
        string position = "";
        for (int r = 7; r >= 0; r--) {
            int space = 0;
            for (int f = 0; f < 8; f++) {
                Piece piece = pieces[f, r];
                if (piece == emptyPiece) {
                    space++;
                } else {
                    if (space > 0) {
                        position += space;
                        space = 0;
                    }
                    char c = PieceTypesReversed[piece.type];
                    if (piece.color == PieceColor.White) c = char.ToUpper(c);
                    position += c;
                }
            }
            if (space > 0) {
                position += space;
                space = 0;
            }
            position += "/";
        }

        _fen.position = position;

        return _fen;
    }

    private void SetupBoard() {
        int width = Screen.width;
        int height = Screen.height;

        int defaultCameraSize = (int)Camera.main.orthographicSize;
        int pixelsPerUnit = Mathf.RoundToInt(1.0f * height / (defaultCameraSize * 2));

        height = defaultCameraSize * 2;
        width /= pixelsPerUnit;

        //check if higher than wide
        bool alignWithHeight = false;
        if (width / height > 0) alignWithHeight = true;
        Chess.alignWithHeight = alignWithHeight;

        //calculate square size
        if (alignWithHeight) {
            safeSpace.y = height * 0.025f;
            squareSize = (height - safeSpace.y * 2) / 8;
        } else {
            safeSpace.x = -width * 0.0625f;
            squareSize = (width - safeSpace.x * 2) / 8;
            var test = squareSize;
        }

        //calculate starting position for centered board
        startingPosition = new Vector3(-squareSize * 4, -squareSize * 4, 0);

        //create board
        for (int r = 0; r < 8; r++) {
            for (int f = 0; f < 8; f++) {
                GameObject square = new GameObject(Letters[r] + (f + 1).ToString());

                Square _square = square.AddComponent<Square>();
                _square.rank = r;
                _square.file = f;

                Rigidbody2D rigidBody = square.AddComponent<Rigidbody2D>();
                rigidBody.gravityScale = 0;
                square.tag = "Square";

                BoxCollider2D boxCollider = square.AddComponent<BoxCollider2D>();
                boxCollider.size = new Vector2Int(1, 1);
                boxCollider.isTrigger = true;

                SpriteRenderer sprite = square.AddComponent<SpriteRenderer>();
                sprite.sprite = squareSprite;
                sprite.color = (r + f) % 2 == 0 ? darkSqaureColor : lightSquareColor;

                square.transform.parent = squaresParent.transform;
                square.transform.localScale = new Vector3(squareSize, squareSize, 0);
                square.transform.position = CalculateSquarePosition(f, r);
            }
        }
    }

    private void SetupPieces(string position) {
        int currentRank = 7;
        int currentFile = 0;

        for (int i = 0; i < 8; i++) {
            for (int j = 0; j < 8; j++) {
                pieces[i, j] = emptyPiece;
            }
        }

        for (int i = 0; i < position.Length; i++) {
            if (char.IsNumber(position[i])) {
                currentFile += position[i] - '0';
            } else {
                if (position[i] == '/') {
                    currentRank--;
                    currentFile = 0;
                } else {
                    GameObject pieceObject = new GameObject(PieceTypes[position[i]].ToString());

                    Piece piece = pieceObject.AddComponent<Piece>();
                    piece.file = currentFile;
                    piece.rank = currentRank;
                    piece.type = PieceTypes[position[i]];

                    SpriteRenderer spriteRenderer = pieceObject.AddComponent<SpriteRenderer>();
                    if (char.IsLower(position[i])) {
                        piece.color = PieceColor.Black;
                        spriteRenderer.sprite = blackSprites[(int)PieceTypes[position[i]]];
                    } else {
                        piece.color = (int)PieceColor.White;
                        spriteRenderer.sprite = whiteSprites[(int)PieceTypes[position[i]]];
                    }

                    piece.transform.parent = piecesParent.transform;
                    piece.transform.localScale = new Vector3(squareSize, squareSize, 0);
                    piece.transform.position = CalculateSquarePosition(currentFile, currentRank) + new Vector3(0, 0, -1);

                    pieces[currentFile, currentRank] = piece;

                    currentFile++;
                }
            }
        }
    }

    private void SetupCoordinates() {
        //destroy existing coordinates
        for (int i = 0; i < coordinates.Count; i++) {
            Destroy(coordinates[i]);
        }
        coordinates.Clear();

        for (int f = 0; f < 8; f++) {
            //create new gamebject
            GameObject coordinate = new GameObject(letterSprites[f].name);

            //attach sprite renderer and set sprite
            SpriteRenderer sr = coordinate.AddComponent<SpriteRenderer>();
            sr.sprite = letterSprites[f];

            if (f % 2 == 0) sr.color = lightSquareColor;
            else sr.color = darkSqaureColor;

            //attach squre component
            Square square = coordinate.AddComponent<Square>();
            square.file = f;
            square.rank = 0;

            //set position and scale
            coordinate.transform.parent = coordinatesParent.transform;
            coordinate.transform.localScale = new Vector3(squareSize, squareSize, 0);
            coordinate.transform.position = CalculateSquarePosition(f, 0) + new Vector3(0, 0, -0.5f);

            //add to coordinates list
            coordinates.Add(coordinate);
        }

        for (int r = 0; r < 8; r++) {
            //create new gamebject
            GameObject coordinate = new GameObject(numberSprites[r].name);

            //attach sprite renderer and set sprite
            SpriteRenderer sr = coordinate.AddComponent<SpriteRenderer>();
            sr.sprite = numberSprites[r];
            if (r % 2 == 0) sr.color = darkSqaureColor;
            else sr.color = lightSquareColor;

            //attach squre component
            Square square = coordinate.AddComponent<Square>();
            square.file = 7;
            square.rank = r;

            //set position and scale
            coordinate.transform.parent = coordinatesParent.transform;
            coordinate.transform.localScale = new Vector3(squareSize, squareSize, 0);
            coordinate.transform.position = CalculateSquarePosition(7, r) + new Vector3(0, 0, -0.5f);

            //add to coordinates list
            coordinates.Add(coordinate);
        }
    }

    public void ResizeBoard() {
        //Destory old objects
        Destroy(squaresParent);

        //Create new parents
        squaresParent = new GameObject("Squares");
        squaresParent.transform.parent = boardParent.transform;

        //Create new squares
        SetupBoard();

        //Adjust transform
        for (int i = 0; i < piecesParent.transform.childCount; i++) {
            Transform transform = piecesParent.transform.GetChild(i);
            Piece piece = transform.GetComponent<Piece>();
            transform.localScale = new Vector3(squareSize, squareSize, 0);
            transform.position = CalculateSquarePosition(piece.file, piece.rank) + new Vector3(0, 0, -1);
        }

        //adjust move marker size
        for (int i = 0; i < lastMoveMarker.Count; i++) {
            lastMoveMarker[i].transform.localScale = new Vector3(squareSize, squareSize, 0);

            Vector3 squarePosition = new Vector3();
            if (i == 0) squarePosition = CalculateSquarePosition(lastMove.originalFile, lastMove.originalRank);
            else if (i == 1) squarePosition = CalculateSquarePosition(lastMove.targetFile, lastMove.targetRank);
            lastMoveMarker[i].transform.position = squarePosition;
        }

        //Change clock location
        //switch between horizontal and vertical ui
        if (Chess.alignWithHeight) {
            horizontalContainer.SetActive(true);
            verticalContainer.SetActive(false);
        } else {
            horizontalContainer.SetActive(false);
            verticalContainer.SetActive(true);
        }

        //Get correct clock
        List<GameObject> clocks = GameObject.FindGameObjectsWithTag("clock").ToList();

        for (int i = 0; i < clocks.Count; i++) {
            if (clocks[i].name.Contains("White")) {
                if (clocks[i].activeInHierarchy) whiteClock = clocks[i].GetComponent<TextMeshProUGUI>();
            } else if (clocks[i].name.Contains("Black")) {
                if (clocks[i].activeInHierarchy) blackClock = clocks[i].GetComponent<TextMeshProUGUI>();
            }
        }

        if (!whiteClock || !blackClock) Debug.LogError("Clock not found!");

        //set clock to use for current timer
        if (fen.colorToMove == PieceColor.White) currentClock = whiteClock;
        else currentClock = blackClock;
    }

    public void FlipBoard() {
        //Flip Camera
        Transform cameraTransform = Camera.main.transform;
        Vector3 rotation = cameraTransform.eulerAngles;
        if (rotation.z == 180) rotation.z = 0;
        else if (rotation.z == 0) rotation.z = -180;
        cameraTransform.eulerAngles = rotation;

        //Rotate pieces
        for (int f = 0; f < 8; f++) {
            for (int r = 0; r < 8; r++) {
                if (pieces[f, r] == emptyPiece) continue;
                pieces[f, r].transform.eulerAngles = rotation;
            }
        }

        FlipCoordinates();
    }

    private void FlipUI() {
        if (whiteUI.transform.position == bottomReference.transform.position) {
            //flip position
            whiteUI.transform.position = topReference.transform.position;
            blackUI.transform.position = bottomReference.transform.position;
        } else if(whiteUI.transform.position == topReference.transform.position) {
            //flip position
            whiteUI.transform.position = bottomReference.transform.position;
            blackUI.transform.position = topReference.transform.position;
        }
    }

    private void FlipCoordinates() {
        //loop through all coordinates
        for (int i = 0; i < coordinates.Count; i++) {
            //rotate coordinates
            Vector3 newRotation = new Vector3(0, 0, 0);
            if (coordinates[i].transform.eulerAngles.z == 0) newRotation.z = 180;
            else newRotation.z = 0;
            coordinates[i].transform.eulerAngles = newRotation;

            //get square component
            Square square = coordinates[i].GetComponent<Square>();

            //check if it's a letter or number
            if (char.IsLetter(coordinates[i].name[0])) {
                if (square.rank == 0) square.rank = 7;
                else square.rank = 0;
            } else {
                if (square.file == 0) square.file = 7;
                else square.file = 0;
            }

            //set new position
            coordinates[i].transform.position = CalculateSquarePosition(square.file, square.rank) + new Vector3(0, 0, -0.5f);

            //switch color
            SpriteRenderer sr = coordinates[i].GetComponent<SpriteRenderer>();
            if (sr.color == lightSquareColor) sr.color = darkSqaureColor;
            else sr.color = lightSquareColor;
        }
    }

    GameObject SetUIOrientation() {
        GameObject currentContainer;

        if (Chess.alignWithHeight) {
            //select ui to show
            horizontalContainer.SetActive(true);
            verticalContainer.SetActive(false);

            //set correct container reference
            currentContainer = horizontalContainer;
        } else {
            //select ui to show
            horizontalContainer.SetActive(false);
            verticalContainer.SetActive(true);

            //set corret container reference
            currentContainer = verticalContainer;
        }

        return currentContainer;
    }

    private Vector3 CalculateSquarePosition(int file, int rank) {
        return new Vector3(startingPosition.x + file * squareSize + squareSize / 2, startingPosition.y + rank * squareSize + squareSize / 2, 0);
    }

    private List<Move> CalculateLegalMoves(Piece piece) {
        Vector2Int square = new Vector2Int(piece.file, piece.rank);
        List<Move> legalMoves = new List<Move>();

        if (piece.type == PieceType.Pawn) {
            //get pawn move direction
            Vector2Int direction = piece.color == (int)PieceColor.White ? new Vector2Int(0, 1) : new Vector2Int(0, -1);

            //check pawn foreward movement
            if (square.y + direction.y >= 0 && square.y + direction.y <= 7) {
                if (pieces[square.x, (square.y + direction.y)] == emptyPiece) legalMoves.Add(new Move(piece, square + direction)); //foreward
            }

            //check if pawn can capture right
            if (square.x + 1 >= 0 && square.x + 1 <= 7) {
                //check if there is a piece to the right
                if (pieces[square.x + 1, square.y + direction.y] != emptyPiece) {
                    //check if the piece has a different color
                    if (pieces[square.x + 1, square.y + direction.y].color != piece.color) {
                        legalMoves.Add(new Move(piece, square + new Vector2Int(1, direction.y), true)); //right
                    }
                }
            }

            //check if pawn can capture left
            if (square.x - 1 >= 0 && square.x - 1 <= 7) {
                //check if there is a piece to the left
                if (pieces[square.x - 1, square.y + direction.y] != emptyPiece) {
                    //check if the piece has a different color
                    if (pieces[square.x - 1, square.y + direction.y].color != piece.color) {
                        legalMoves.Add(new Move(piece, square + new Vector2Int(-1, direction.y), true)); //left
                    }
                }
            }

            //check if pawn is on starting rank
            if ((piece.color == (int)PieceColor.White && piece.rank == 1) || (piece.color == PieceColor.Black && piece.rank == 6)) {
                //check of both squares are empty
                if (pieces[square.x, square.y + direction.y] == emptyPiece && pieces[square.x, square.y + direction.y * 2] == emptyPiece) {
                    legalMoves.Add(new Move(piece, square + direction * 2));
                }
            }

            //check if pawn can en passant left and right
            if (fen.enPassantTarget == square + new Vector2Int(1, direction.y)) legalMoves.Add(new Move(piece, square + new Vector2Int(1, direction.y), true));
            else if (fen.enPassantTarget == square + new Vector2Int(-1, direction.y)) legalMoves.Add(new Move(piece, square + new Vector2Int(-1, direction.y), true));

        } else if (piece.type == PieceType.Knight) {
            //setup nigth move shape
            Vector2Int verticalShape = new Vector2Int(1, 2);
            Vector2Int horizontalShape = new Vector2Int(2, 1);

            for (int i = 0; i < 2; i++) {
                verticalShape.x *= -1;
                for (int j = 0; j < 2; j++) {
                    verticalShape.y *= -1;

                    //check if inside board
                    if(square.x + verticalShape.x <= 7 && square.x + verticalShape.x >= 0 && square.y + verticalShape.y <= 7 && square.y + verticalShape.y >= 0) {
                        //check if target is not own color
                        if (pieces[square.x + verticalShape.x, square.y + verticalShape.y].color != piece.color) {
                            //check if is capture
                            bool capture = false;
                            if (pieces[square.x + verticalShape.x, square.y + verticalShape.y].color != PieceColor.None) capture = true;
                            legalMoves.Add(new Move(piece, square + verticalShape, capture));
                        }
                    }
                }

                horizontalShape.x *= -1;
                for (int j = 0; j < 2; j++) {
                    horizontalShape.y *= -1;

                    //check if inside board
                    if (square.x + horizontalShape.x <= 7 && square.x + horizontalShape.x >= 0 && square.y + horizontalShape.y <= 7 && square.y + horizontalShape.y >= 0) {
                        //check if target is not own color
                        if (pieces[square.x + horizontalShape.x, square.y + horizontalShape.y].color != piece.color) {
                            //check if is capture
                            bool capture = false;
                            if (pieces[square.x + horizontalShape.x, square.y + horizontalShape.y].color != PieceColor.None) capture = true;
                            legalMoves.Add(new Move(piece, square + horizontalShape, capture));
                        }
                    }
                }
            }
        } else if (piece.type == PieceType.Bishop) {
            //setup direction vectors
            Vector2Int tr, tl, br, bl;
            //initialize direction vector to piece position
            tr = tl = br = bl = new Vector2Int(piece.file, piece.rank);

            //top right
            while (tr.x < 7 && tr.y < 7) {
                tr += new Vector2Int(1, 1);
                //add move if empty square
                if (pieces[tr.x, tr.y] == emptyPiece) legalMoves.Add(new Move(piece, tr));
                else {
                    //get piece
                    Piece _piece = pieces[tr.x, tr.y];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece, tr, true));
                        break;
                    }
                }
            }

            //top left
            while (tl.x > 0 && tl.y < 7) {
                tl += new Vector2Int(-1, 1);
                //add move if empty square
                if (pieces[tl.x, tl.y] == emptyPiece) legalMoves.Add(new Move(piece, tl));
                else {
                    //get piece
                    Piece _piece = pieces[tl.x, tl.y];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece, tl, true));
                        break;
                    }
                }
            }

            //bottom right
            while (br.x < 7 && br.y > 0) {
                br += new Vector2Int(1, -1);
                //add move if empty square
                if (pieces[br.x, br.y] == emptyPiece) legalMoves.Add(new Move(piece, br));
                else {
                    //get piece
                    Piece _piece = pieces[br.x, br.y];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece, br, true));
                        break;
                    }
                }
            }

            //bottom left
            while (bl.x > 0 && bl.y > 0) {
                bl += new Vector2Int(-1, -1);
                //add move if empty square
                if (pieces[bl.x, bl.y] == emptyPiece) legalMoves.Add(new Move(piece, bl));
                else {
                    //get piece
                    Piece _piece = pieces[bl.x, bl.y];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece, bl, true));
                        break;
                    }
                }
            }
        } else if (piece.type == PieceType.Rook) {
            //right
            for (int f = piece.file + 1; f < 8; f++) {
                //add move if empty square
                if (pieces[f, piece.rank] == emptyPiece) legalMoves.Add(new Move(piece, new Vector2Int(f, piece.rank)));
                else {
                    //get piece
                    Piece _piece = pieces[f, piece.rank];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece, new Vector2Int(f, piece.rank), true));
                        break;
                    }
                }
            }

            //left
            for (int f = piece.file - 1; f >= 0; f--) {
                //add move if empty square
                if (pieces[f, piece.rank] == emptyPiece) legalMoves.Add(new Move(piece, new Vector2Int(f, piece.rank)));
                else {
                    //get piece
                    Piece _piece = pieces[f, piece.rank];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece, new Vector2Int(f, piece.rank), true));
                        break;
                    }
                }
            }

            //top
            for (int r = piece.rank + 1; r < 8; r++) {
                //add move if empty square
                if (pieces[piece.file, r] == emptyPiece) legalMoves.Add(new Move(piece, new Vector2Int(piece.file, r)));
                else {
                    //get piece
                    Piece _piece = pieces[piece.file, r];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece, new Vector2Int(piece.file, r), true));
                        break;
                    }
                }
            }

            //bottom
            for (int r = piece.rank - 1; r >= 0; r--) {
                //add move if empty square
                if (pieces[piece.file, r] == emptyPiece) legalMoves.Add(new Move(piece, new Vector2Int(piece.file, r)));
                else {
                    //get piece
                    Piece _piece = pieces[piece.file, r];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece, new Vector2Int(piece.file, r), true));
                        break;
                    }
                }
            }
        } else if (piece.type == PieceType.Queen) {
            //right
            for (int f = piece.file + 1; f < 8; f++) {
                //add move if empty square
                if (pieces[f, piece.rank] == emptyPiece) legalMoves.Add(new Move(piece, new Vector2Int(f, piece.rank)));
                else {
                    //get piece
                    Piece _piece = pieces[f, piece.rank];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece, new Vector2Int(f, piece.rank), true));
                        break;
                    }
                }
            }

            //left
            for (int f = piece.file - 1; f >= 0; f--) {
                //add move if empty square
                if (pieces[f, piece.rank] == emptyPiece) legalMoves.Add(new Move(piece, new Vector2Int(f, piece.rank)));
                else {
                    //get piece
                    Piece _piece = pieces[f, piece.rank];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece, new Vector2Int(f, piece.rank), true));
                        break;
                    }
                }
            }

            //top
            for (int r = piece.rank + 1; r < 8; r++) {
                //add move if empty square
                if (pieces[piece.file, r] == emptyPiece) legalMoves.Add(new Move(piece, new Vector2Int(piece.file, r)));
                else {
                    //get piece
                    Piece _piece = pieces[piece.file, r];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece,new Vector2Int(piece.file, r), true));
                        break;
                    }
                }
            }

            //bottom
            for (int r = piece.rank - 1; r >= 0; r--) {
                //add move if empty square
                if (pieces[piece.file, r] == emptyPiece) legalMoves.Add(new Move(piece, new Vector2Int(piece.file, r)));
                else {
                    //get piece
                    Piece _piece = pieces[piece.file, r];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece, new Vector2Int(piece.file, r), true));
                        break;
                    }
                }
            }

            //setup direction vectors
            Vector2Int tr, tl, br, bl;
            //initialize direction vector to piece position
            tr = tl = br = bl = new Vector2Int(piece.file, piece.rank);

            //top right
            while (tr.x < 7 && tr.y < 7) {
                tr += new Vector2Int(1, 1);
                //add move if empty square
                if (pieces[tr.x, tr.y] == emptyPiece) legalMoves.Add(new Move(piece, tr));
                else {
                    //get piece
                    Piece _piece = pieces[tr.x, tr.y];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece, tr, true));
                        break;
                    }
                }
            }

            //top left
            while (tl.x > 0 && tl.y < 7) {
                tl += new Vector2Int(-1, 1);
                //add move if empty square
                if (pieces[tl.x, tl.y] == emptyPiece) legalMoves.Add(new Move(piece, tl));
                else {
                    //get piece
                    Piece _piece = pieces[tl.x, tl.y];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece, tl, true));
                        break;
                    }
                }
            }

            //bottom right
            while (br.x < 7 && br.y > 0) {
                br += new Vector2Int(1, -1);
                //add move if empty square
                if (pieces[br.x, br.y] == emptyPiece) legalMoves.Add(new Move(piece, br));
                else {
                    //get piece
                    Piece _piece = pieces[br.x, br.y];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece, br, true));
                        break;
                    }
                }
            }

            //bottom left
            while (bl.x > 0 && bl.y > 0) {
                bl += new Vector2Int(-1, -1);
                //add move if empty square
                if (pieces[bl.x, bl.y] == emptyPiece) legalMoves.Add(new Move(piece, bl));
                else {
                    //get piece
                    Piece _piece = pieces[bl.x, bl.y];
                    //check if same color
                    if (_piece.color == piece.color) break;
                    else {
                        legalMoves.Add(new Move(piece, bl, true));
                        break;
                    }
                }
            }
        } else if (piece.type == PieceType.King) {
            //loop 3 x 3 field around king
            for (int f = -1; f <= 1; f++) {
                for (int r = -1; r <= 1; r++) {
                    //calculate new position
                    Vector2Int newPos = square + new Vector2Int(f, r);

                    //check if new square will be near other king
                    bool nearKing = false;
                    for (int f2 = -1; f2 <= 1; f2++) {
                        for (int r2 = -1; r2 <= 1; r2++) {
                            //check if position is inside board
                            if (newPos.x + f2 < 0 || newPos.x + f2 > 7 || newPos.y + r2 < 0 || newPos.y + r2 > 7) continue;

                            //get piece
                            Piece current = pieces[newPos.x + f2, newPos.y + r2];
                            //check if will be near enemy king
                            if (current.type == PieceType.King && current.color != fen.colorToMove) nearKing = true;
                            if (nearKing) break;
                        }
                        if (nearKing) break;
                    }

                    //add move if not near king
                    if (!nearKing) {
                        //check if inside board
                        if (square.x + f >= 0 && square.x + f <= 7 && square.y + r >= 0 && square.y + r <= 7) {
                            //check if occupied by own piece
                            if (pieces[square.x + f, square.y + r].color != piece.color) {
                                //check if is capture
                                bool capture = false;
                                if (pieces[square.x + f, square.y + r].color != piece.color && pieces[square.x + f, square.y + r].color != PieceColor.None) capture = true;

                                //add move
                                legalMoves.Add(new Move(piece, square + new Vector2Int(f, r), capture));
                            }
                        }
                    }
                }
            }

            //check if can castle
            CastleDirection castleDirection = piece.color == (int)PieceColor.White ? fen.castleDirectionWhite : fen.castleDirectionBlack;

            bool canCastleQueen = true;
            bool canCastleKing = true;

            if (castleDirection == CastleDirection.Queen) {
                canCastleKing = false;
                //check if next two squares to the left are empty
                for (int f = piece.file - 1; f > 0; f--) {
                    if (pieces[f, piece.rank] != emptyPiece) {
                        canCastleQueen = false;
                        break;
                    }
                }
            } else if (castleDirection == CastleDirection.King) {
                canCastleQueen = false;
                //check if next two squares to the right are empty
                for (int f = piece.file + 1; f < 6; f++) {
                    if (pieces[f, piece.rank] != emptyPiece) {
                        canCastleKing = false;
                        break;
                    }
                }
            } else if (castleDirection == CastleDirection.KingAndQueen) {
                //check if next two squares to the left are empty
                for (int f = piece.file - 1; f > 0; f--) {
                    if (pieces[f, piece.rank] != emptyPiece) {
                        canCastleQueen = false;
                        break;
                    }
                }

                //check if next two squares to the right are empty
                for (int f = piece.file + 1; f < 6; f++) {
                    if (pieces[f, piece.rank] != emptyPiece) {
                        canCastleKing = false;
                        break;
                    }
                }
            } else if (castleDirection == CastleDirection.None) {
                canCastleKing = false;
                canCastleQueen = false;
            }

            //add castle moves
            if (canCastleKing) legalMoves.Add(new Move(piece, new Vector2Int(piece.file + 2, piece.rank)));
            if (canCastleQueen) legalMoves.Add(new Move(piece, new Vector2Int(piece.file - 2, piece.rank)));
        }

        List<Move> legalMovesNoCheck = new List<Move>();

        //remove move if king will be in check
        Piece king = GetKing(piece.color);
        for (int i = 0; i < legalMoves.Count; i++) {
            int file = piece.file;
            int rank = piece.rank;
            Piece previousPiece = pieces[legalMoves[i].targetFile, legalMoves[i].targetRank];

            pieces[legalMoves[i].targetFile, legalMoves[i].targetRank] = piece;
            pieces[piece.file, piece.rank] = emptyPiece;
            piece.file = legalMoves[i].targetFile;
            piece.rank = legalMoves[i].targetRank;

            if (!CheckIfKingIsInCheck(king)) legalMovesNoCheck.Add(legalMoves[i]);

            pieces[file, rank] = piece;
            pieces[legalMoves[i].targetFile, legalMoves[i].targetRank] = previousPiece;
            piece.file = file;
            piece.rank = rank;
        }

        if (piece.type == PieceType.King) {
            //Remove castle moves if king is in check
            if (CheckIfKingIsInCheck(piece)) {
                int queenCastleIndex = legalMovesNoCheck.FindIndex(m => m.GetTargetPosition() == new Vector2Int(piece.file - 2, piece.rank));
                if (queenCastleIndex >= 0) legalMovesNoCheck.RemoveAt(queenCastleIndex);

                int kingCastleIndex = legalMovesNoCheck.FindIndex(m => m.GetTargetPosition() == new Vector2Int(piece.file + 2, piece.rank));
                if (kingCastleIndex >= 0) legalMovesNoCheck.RemoveAt(kingCastleIndex);
            } else {
                //Remove queen side castle if would move through check
                if (legalMovesNoCheck.FindIndex(m => m.GetTargetPosition() == new Vector2Int(piece.file - 1, piece.rank)) == -1) {
                    int queenCastleIndex = legalMovesNoCheck.FindIndex(m => m.GetTargetPosition() == new Vector2Int(piece.file - 2, piece.rank));
                    if (queenCastleIndex >= 0) legalMovesNoCheck.RemoveAt(queenCastleIndex);
                }

                //Remove king side castle if would move through check
                if (legalMovesNoCheck.FindIndex(m => m.GetTargetPosition() == new Vector2Int(piece.file + 1, piece.rank)) == -1) {
                    int kingCastleIndex = legalMovesNoCheck.FindIndex(m => m.GetTargetPosition() == new Vector2Int(piece.file + 2, piece.rank));
                    if (kingCastleIndex >= 0) legalMovesNoCheck.RemoveAt(kingCastleIndex);
                }
            }
        }

        return legalMovesNoCheck;
    }

    private void SetMoveIndicators(Piece piece) {
        //remove all indicators
        foreach (GameObject indicator in moveIndicators) Destroy(indicator);
        moveIndicators.Clear();

        //stop if piece is null
        if (!piece) return;

        //add new indicators for specified piece
        List<Move> moves = CalculateLegalMoves(piece);
        foreach (Move move in moves) {
            GameObject moveIndicator = Instantiate(moveIndicatorPrefab);
            moveIndicator.transform.position = CalculateSquarePosition(move.targetFile, move.targetRank) + new Vector3(0, 0, -2);
            if (move.capture) {
                moveIndicator.GetComponent<SpriteRenderer>().sprite = captureSprite;
                moveIndicator.transform.localScale = new Vector3(squareSize, squareSize, 0);
            } else {
                moveIndicator.transform.localScale = new Vector3(squareSize * 0.3f, squareSize * 0.3f, 0);
            }
            moveIndicators.Add(moveIndicator);
        }
    }

    private void SetMoveMarkers(Move move) {
        //create move marker for original position
        GameObject originMoveMarker = new GameObject();
        originMoveMarker.name = "Origin";
        originMoveMarker.transform.parent = boardParent.transform;
        originMoveMarker.transform.localScale = new Vector3(squareSize, squareSize, 0);
        originMoveMarker.transform.position = CalculateSquarePosition(move.originalFile, move.originalRank);
        SpriteRenderer sr = originMoveMarker.AddComponent<SpriteRenderer>();
        sr.sprite = squareSprite;
        sr.color = new Color32(255, 238, 0, 90);
        lastMoveMarker.Add(originMoveMarker);

        //create move marker for target position
        GameObject targetMoveMarker = new GameObject();
        targetMoveMarker.name = "Target";
        targetMoveMarker.transform.parent = boardParent.transform;
        targetMoveMarker.transform.localScale = new Vector3(squareSize, squareSize, 0);
        targetMoveMarker.transform.position = CalculateSquarePosition(move.targetFile, move.targetRank);
        sr = targetMoveMarker.AddComponent<SpriteRenderer>();
        sr.sprite = squareSprite;
        sr.color = new Color32(255, 238, 0, 90);
        lastMoveMarker.Add(targetMoveMarker);
    }

    private Move GetMove(Piece pieceToMove, Square square) {
        List<Move> legalMoves = CalculateLegalMoves(pieceToMove);
        if (legalMoves.FindIndex(m => m.GetTargetPosition() == new Vector2Int(square.file, square.rank)) >= 0) {
            return new Move(pieceToMove.file, pieceToMove.rank, square.file, square.rank);
        }

        return new Move(-1, -1, -1, -1);
    }

    private void MovePiece(Move move) {
        currentlyMoving = true;

        Piece pieceToMove = pieces[move.originalFile, move.originalRank];

        //handle special moves
        if (pieceToMove.type == PieceType.Pawn) {
            moveLog.Clear();

            if (move.targetFile == pieceToMove.file && Mathf.Abs(move.targetRank - pieceToMove.rank) == 2) {
                fen.enPassantTarget = new Vector2Int(pieceToMove.file, pieceToMove.rank + 1);
                enPassantSet = true;
            }

            if (move.targetFile == fen.enPassantTarget.x && move.targetRank == fen.enPassantTarget.y) {
                int direction = pieceToMove.color == (int)PieceColor.White ? -1 : 1;
                Destroy(pieces[fen.enPassantTarget.x, fen.enPassantTarget.y + direction]);
                pieces[fen.enPassantTarget.x, fen.enPassantTarget.y + direction] = emptyPiece;
            }

            if (move.targetRank == 7 || move.targetRank == 0) {
                pieceToMove.type = PieceType.Queen;
                if (pieceToMove.color == (int)PieceColor.White) pieceToMove.gameObject.GetComponent<SpriteRenderer>().sprite = whiteSprites[(int)pieceToMove.type];
                else if (pieceToMove.color == PieceColor.Black) pieceToMove.gameObject.GetComponent<SpriteRenderer>().sprite = blackSprites[(int)pieceToMove.type];
            }
        }

        if (pieceToMove.type == PieceType.King) {
            if (pieceToMove.color == (int)PieceColor.White && !whiteKingMoved) {
                whiteKingMoved = true;
                fen.castleDirectionWhite = CastleDirection.None;
            }
            if (pieceToMove.color == PieceColor.Black && !blackKingMoved) {
                blackKingMoved = true;
                fen.castleDirectionBlack = CastleDirection.None;
            }

            if (Mathf.Abs(pieceToMove.file - move.targetFile) == 2) {
                int file = 0;
                int direction = 0;

                if (move.targetFile == 2) {
                    file = 0;
                    direction = -1;
                } else if (move.targetFile == 6) {
                    file = 7;
                    direction = 1;
                }

                Piece rook = pieces[file, pieceToMove.rank];

                rook.file = pieceToMove.file + direction;
                pieces[rook.file, rook.rank] = rook;
                pieces[file, rook.rank] = emptyPiece;

                rook.transform.position = CalculateSquarePosition(rook.file, rook.rank) + new Vector3(0, 0, -1);

                moveLog.Clear();
            }
        }

        if (pieceToMove.type == PieceType.Rook) {
            if (pieceToMove.color == (int)PieceColor.White) {
                if (!whiteKingRookMoved) {
                    if (pieceToMove.file == 7) {
                        whiteKingRookMoved = true;
                        if (fen.castleDirectionWhite == CastleDirection.KingAndQueen) fen.castleDirectionWhite = CastleDirection.Queen;
                        else fen.castleDirectionWhite = CastleDirection.None;
                    }
                }

                if (!whiteQueenRookMoved) {
                    if (pieceToMove.file == 0) {
                        whiteQueenRookMoved = true;
                        if (fen.castleDirectionWhite == CastleDirection.KingAndQueen) fen.castleDirectionWhite = CastleDirection.King;
                        else fen.castleDirectionWhite = CastleDirection.None;
                    }
                }
            }

            if (pieceToMove.color == PieceColor.Black) {
                if (!blackKingRookMoved) {
                    if (pieceToMove.file == 7) {
                        blackKingRookMoved = true;
                        if (fen.castleDirectionBlack == CastleDirection.KingAndQueen) fen.castleDirectionBlack = CastleDirection.Queen;
                        else fen.castleDirectionBlack = CastleDirection.None;
                    }
                }

                if (!blackQueenRookMoved) {
                    if (pieceToMove.file == 0) {
                        blackQueenRookMoved = true;
                        if (fen.castleDirectionBlack == CastleDirection.KingAndQueen) fen.castleDirectionBlack = CastleDirection.King;
                        else fen.castleDirectionBlack = CastleDirection.None;
                    }
                }
            }
        }

        if (!enPassantSet) {
            fen.enPassantTarget = new Vector2Int(-1, -1);
        }
        
        //update fen values
        if (fen.colorToMove == PieceColor.Black) fen.fullMoves++;
        fen.halfMoves++;

        if (pieceToMove.type == PieceType.Pawn) fen.halfMoves = 0;

        //handle capturing
        Piece targetPiece = pieces[move.targetFile, move.targetRank];
        if (targetPiece != emptyPiece) {
            Destroy(targetPiece.gameObject);
            targetPiece = emptyPiece;
            fen.halfMoves = 0;
            moveLog.Clear();
        }

        //actually move the piece
        pieces[pieceToMove.file, pieceToMove.rank] = emptyPiece;

        pieceToMove.rank = move.targetRank;
        pieceToMove.file = move.targetFile;
        pieces[pieceToMove.file, pieceToMove.rank] = pieceToMove;

        pieceToMove.transform.position = CalculateSquarePosition(pieceToMove.file, pieceToMove.rank) + new Vector3(0, 0, -1);

        //Switch color to move
        if (fen.colorToMove == PieceColor.White) {
            fen.colorToMove = PieceColor.Black;
            whiteTime = currentTime;
            currentTime = blackTime;
            whiteClock = currentClock;
            currentClock = blackClock;
        } else if (fen.colorToMove == PieceColor.Black) {
            fen.colorToMove = PieceColor.White;
            blackTime = currentTime;
            currentTime = whiteTime;
            blackClock = currentClock;
            currentClock = whiteClock;
        }

        //Check for game end
        Piece king = GetKing(fen.colorToMove);
        if (CheckForCheckMate(king)) gameManager.EndGame(king.color == PieceColor.White ? false : true, GameOverReason.Checkmate);
        if (CheckForStaleMate(king)) gameManager.EndGame(king.color == PieceColor.White ? false : true, GameOverReason.Stalemate); ;
        if (CheckForInsuficientMaterial()) gameManager.EndGame(king.color == PieceColor.White ? false : true, GameOverReason.InsuficientMaterial); ;
        if (CheckForRepetition()) gameManager.EndGame(king.color == PieceColor.White ? false : true, GameOverReason.Repetition); ;
        if (CheckFor50MoveDraw()) gameManager.EndGame(king.color == PieceColor.White ? false : true, GameOverReason.Draw50MoveRule); ;

        if (Chess.local && Chess.flipBoard) {
            FlipBoard();
            FlipUI();
        }

        //remove move markers and add new ones
        for (int i = 0; i < lastMoveMarker.Count; i++) Destroy(lastMoveMarker[i]);
        lastMoveMarker.Clear();

        //create move marker
        SetMoveMarkers(move);

        //save the last move
        lastMove = move;

        //clear all running values
        enPassantSet = false;
        selectedPiece = null;
        SetMoveIndicators(null);

        currentlyMoving = false;
        gettingMoveFromStockfish = false;
    }

    private Piece GetKing(PieceColor color) {
        Piece king = null;

        for (int f = 0; f < pieces.Length; f++) {
            for (int r = 0; r < pieces.Length; r++) {
                if (f < 0 || f > 7 || r < 0 || r > 7) continue;

                Piece current = pieces[f, r];
                if (current.color == color && current.type == PieceType.King) {
                    king = current;
                    break;
                }
            }
            if (king) break;
        }

        return king;
    }

    private bool CheckIfKingIsInCheck(Piece[,] pieces, Piece king) {
        if (!king) return false;

        bool inCheck = false;

        //rook and queen
        for (int f = king.file + 1; f < 8; f++) {
            Piece piece = pieces[f, king.rank];
            if ((piece.type == PieceType.Queen || piece.type == PieceType.Rook) && piece.color != fen.colorToMove) inCheck = true;
            else if (piece.type != PieceType.None) break;
        }
        for (int f = king.file - 1; f >= 0; f--) {
            Piece piece = pieces[f, king.rank];
            if ((piece.type == PieceType.Queen || piece.type == PieceType.Rook) && piece.color != fen.colorToMove) inCheck = true;
            else if (piece.type != PieceType.None) break;
        }
        for (int r = king.rank + 1; r < 8; r++) {
            Piece piece = pieces[king.file, r];
            if ((piece.type == PieceType.Queen || piece.type == PieceType.Rook) && piece.color != fen.colorToMove) inCheck = true;
            else if (piece.type != PieceType.None) break;
        }
        for (int r = king.rank - 1; r >= 0; r--) {
            Piece piece = pieces[king.file, r];
            if ((piece.type == PieceType.Queen || piece.type == PieceType.Rook) && piece.color != fen.colorToMove) inCheck = true;
            else if (piece.type != PieceType.None) break;
        }

        //bishop and queen
        Vector2Int tr, tl, br, bl;
        tr = tl = br = bl = new Vector2Int(king.file, king.rank);
        while (tr.x < 7 && tr.y < 7) {
            tr += new Vector2Int(1, 1);

            Piece piece = pieces[tr.x, tr.y];

            if (piece != emptyPiece) {
                //check of piece is queen or bishop
                if ((piece.type == PieceType.Queen || piece.type == PieceType.Bishop) && piece.color != fen.colorToMove) inCheck = true;
                else if (piece.type != PieceType.None) break;
            }
        }
        while (tl.x > 0 && tl.y < 7) {
            tl += new Vector2Int(-1, 1);

            Piece piece = pieces[tl.x, tl.y];

            if (piece != emptyPiece) {
                //check of piece is queen or bishop
                if ((piece.type == PieceType.Queen || piece.type == PieceType.Bishop) && piece.color != fen.colorToMove) inCheck = true;
                else if (piece.type != PieceType.None) break;
            }
        }
        while (br.x < 7 && br.y > 0) {
            br += new Vector2Int(1, -1);

            Piece piece = pieces[br.x, br.y];

            if (piece != emptyPiece) {
                //check of piece is queen or bishop
                if ((piece.type == PieceType.Queen || piece.type == PieceType.Bishop) && piece.color != fen.colorToMove) inCheck = true;
                else if (piece.type != PieceType.None) break;
            }
        }
        while (bl.x > 0 && bl.y > 0) {
            bl += new Vector2Int(-1, -1);

            Piece piece = pieces[bl.x, bl.y];

            if (piece != emptyPiece) {
                //check of piece is queen or bishop
                if ((piece.type == PieceType.Queen || piece.type == PieceType.Bishop) && piece.color != fen.colorToMove) inCheck = true;
                else if (piece.type != PieceType.None) break;
            }
        }

        //knight
        Vector2Int verticalShape = new Vector2Int(1, 2);
        Vector2Int horizontalShape = new Vector2Int(2, 1);
        for (int i = 0; i < 2; i++) {
            verticalShape.x *= -1;
            for (int j = 0; j < 2; j++) {
                verticalShape.y *= -1;
                if (king.file + verticalShape.x < 0 || king.file + verticalShape.x > 7 || king.rank + verticalShape.y < 0 || king.rank + verticalShape.y > 7) continue;
                Piece piece = pieces[king.file + verticalShape.x, king.rank + verticalShape.y];
                if (piece.type == PieceType.Knight && piece.color != fen.colorToMove) inCheck = true;
            }

            horizontalShape.x *= -1;
            for (int j = 0; j < 2; j++) {
                horizontalShape.y *= -1;
                if (king.file + horizontalShape.x < 0 || king.file + horizontalShape.x > 7 || king.rank + horizontalShape.y < 0 || king.rank + horizontalShape.y > 7) continue;
                Piece piece = pieces[king.file + horizontalShape.x, king.rank + horizontalShape.y];
                if (piece.type == PieceType.Knight && piece.color != fen.colorToMove) inCheck = true;
            }
        }

        //pawn
        int direction = king.color == (int)PieceColor.White ? 1 : -1;

        if (king.rank + direction >= 0 && king.rank + direction <= 7) {
            if (king.file + 1 >= 0 && king.file + 1 <= 7) {
                Piece _piece = pieces[king.file + 1, king.rank + direction];
                if (_piece.type == PieceType.Pawn && _piece.color != fen.colorToMove) inCheck = true;
            }
            if (king.file - 1 >= 0 && king.file - 1 <= 7) {
                Piece _piece = pieces[king.file - 1, king.rank + direction];
                if (_piece.type == PieceType.Pawn && _piece.color != fen.colorToMove) inCheck = true;
            }
        }

        return inCheck;
    }

    private bool CheckIfKingIsInCheck(Piece king) {
        return CheckIfKingIsInCheck(pieces, king);
    }

    private bool CheckForCheckMate(Piece king) {
        if (CheckIfKingIsInCheck(king)) {
            if (CalculateLegalMoves(king).Count == 0) {
                if (!ColorHasLegalMoves(king.color)) return true;
            }
        }

        return false;
    }

    private bool CheckForStaleMate(Piece king) {
        if (!CheckIfKingIsInCheck(king)) {
            if (CalculateLegalMoves(king).Count == 0) {
                if (!ColorHasLegalMoves(king.color)) return true;
            }
        }

        return false;
    }

    private bool CheckForInsuficientMaterial() {
        int whiteKnights = 0;
        int blackKnights = 0;
        int whiteBishops = 0;
        int blackBishops = 0;

        for (int f = 0; f < 7; f++) {
            for (int r = 0; r < 7; r++) {
                Piece piece = pieces[f, r];
                if (piece.type == PieceType.Pawn) return false;
                if (piece.type == PieceType.Queen) return false;
                if (piece.type == PieceType.Rook) return false;

                if (piece.type == PieceType.Knight) _ = piece.color == PieceColor.White ? whiteKnights++ : blackKnights++;
                if (piece.type == PieceType.Bishop) _ = piece.color == PieceColor.White ? whiteBishops++ : blackBishops++;
            }
        }

        int sumKnights = whiteKnights + blackKnights;
        int sumBishops = whiteBishops + blackBishops;

        if (sumKnights == 0 && sumBishops == 0) return true;
        if (sumKnights == 1 && sumBishops == 0) return true;
        if (sumKnights == 0 && sumBishops == 1) return true;

        return false;
    }

    private bool CheckForRepetition() {
        fen = CreateFEN();
        moveLog.Add(fen);
        List<FEN> found = moveLog.FindAll(f => f.position == fen.position);
        if (found.Count == 3) return true;

        return false;
    }

    private bool CheckFor50MoveDraw() {
        if (fen.halfMoves >= 100) return true;
        return false;
    }

    private bool ColorHasLegalMoves(PieceColor color) {
        List<Piece> _pieces = GetPiecesOfColor(color);

        //Check if any have moves
        List<Move> moves = new List<Move>();
        for (int i = 0; i < _pieces.Count; i++) {
            moves.AddRange(CalculateLegalMoves(_pieces[i]));
            if (moves.Count > 0) return true;
        }

        return false;
    }

    private List<Piece> GetPiecesOfColor(PieceColor color) {
        List<Piece> _pieces = new List<Piece>();
        for (int f = 0; f < 7; f++) {
            for (int r = 0; r < 7; r++) {
                Piece _piece = pieces[r, f];
                if (_piece.color == color) _pieces.Add(_piece);

            }
        }

        return _pieces;
    }

    private IEnumerator ClockCo() {
        while(true) {
            //check for timeout
            if (currentTime <= 0) {
                if (CheckForInsuficientMaterial()) gameManager.EndGame(fen.colorToMove == PieceColor.White ? false : true, GameOverReason.TimeoutVsInsuficientMaterial);
                else gameManager.EndGame(fen.colorToMove == PieceColor.White ? false : true, GameOverReason.Timeout);

                yield break;
            }

            //decrement clock
            yield return new WaitForSeconds(.01f);
            currentTime -= .01f;

            //display time on clock
            if (currentTime > 60) {
                int roundedTime = (int)currentTime;
                int minutes = roundedTime / 60;
                int seconds = roundedTime % 60;

                string s;
                if (seconds == 0) s = "00";
                else if (seconds < 10 && seconds > 0) s = "0" + seconds;
                else s = seconds.ToString();

                currentClock.text = minutes + ":" + s;
            } else {
                string s = string.Format("{0:0.00}", currentTime);
                s = s.Replace(",", ".");
                currentClock.text = s;
            }
        }
    }

    private IEnumerator GetStockfishMove() {
        //get the best move from stockfish
        Move bestMove = Stockfish.GetBestMove(CreateFENString(fen), whiteTime, blackTime);
        
        //wait a random time to make it not too fast
        float timeout = Random.Range(0.2f, 2f);
        yield return new WaitForSeconds(timeout);

        //actually perform the move
        MovePiece(bestMove);
    }
}