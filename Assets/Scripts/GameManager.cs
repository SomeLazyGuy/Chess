using ChessNamespace;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour {
    //Gameover Objects
    [SerializeField] private GameObject gameOverParent;
    [SerializeField] private Image titleImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI reasonText;
    [SerializeField] private Image image1;
    [SerializeField] private Image image2;
    [SerializeField] private Image gameModeImage;

    [SerializeField] private Board board;

    [SerializeField] private Sprite blitzSprite;
    [SerializeField] private Sprite rapidSprite;
    [SerializeField] private Sprite customSprite;

    public void EndGame(bool whiteWon, GameOverReason reason) {
        gameOverParent.SetActive(true);

        if (Chess.local) {
            if (whiteWon) {
                titleImage.color = new Color32(59, 192, 64, 255);
                titleText.text = "White won!";
            } else {
                titleImage.color = new Color32(85, 85, 85, 255);
                titleText.text = "White lost!";
            }
        } else {
            if ((Chess.localColor == PieceColor.White && whiteWon) || (Chess.localColor == PieceColor.Black && !whiteWon)) {
                //display win
                titleImage.color = new Color32(59, 192, 64, 255);
                titleText.text = "You won!";
            } else {
                //display lose
                titleImage.color = new Color32(85, 85, 85, 255);
                titleText.text = "You lost!";
            }
        }

        if (reason != GameOverReason.Checkmate && reason != GameOverReason.Timeout) {
            titleImage.color = new Color32(85, 85, 85, 255);
            titleText.text = "Draw!";
        }
        reasonText.text = "by " + reason.ToString();

        if (Chess.gameMode == GameMode.Blitz) {
            gameModeImage.sprite = blitzSprite;
            gameModeImage.color = new Color32(255, 228, 0, 255);
        } else if (Chess.gameMode == GameMode.Rapid) {
            gameModeImage.sprite = rapidSprite;
            gameModeImage.color = new Color32(13, 168, 10, 255);
        } else if (Chess.gameMode == GameMode.Custom || Chess.gameMode == GameMode.Standard) {
            gameModeImage.sprite = customSprite;
            gameModeImage.color = new Color32(79, 79, 79, 255);
        }

        board.StopAllCoroutines();

        Stockfish.StopStockfish();
    }

    public void Rematch() {
        board.DestroyAll();

        if (Chess.localColor == PieceColor.White) Chess.localColor = PieceColor.Black;
        else Chess.localColor = PieceColor.White;

        board.SetupGame();

        gameOverParent.SetActive(false);
    }

    public void BackToTitleScreen() {
        SceneManager.LoadScene(0);
    }
}
