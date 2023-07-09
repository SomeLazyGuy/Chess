using ChessNamespace;
using System;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Chess = ChessNamespace.Chess;

public class StartMenuManager : MonoBehaviour {
    //Parents
    [SerializeField] private GameObject mainMenuButtonParent;
    [SerializeField] private GameObject gameModeButtonParent;
    [SerializeField] private GameObject timeSelectParent;
    [SerializeField] private GameObject settingsParent;

    //Input elements
    [SerializeField] private Toggle flipBoardToggle;
    [SerializeField] private TMP_InputField minutesInput;
    [SerializeField] private TMP_InputField secondsInput;
    [SerializeField] private TMP_InputField lightColorInput;
    [SerializeField] private TMP_InputField darkColorInput;
    [SerializeField] private GameObject backButton;
    [SerializeField] private Slider eloSlider;

    [SerializeField] private Image lightColorPreview;
    [SerializeField] private Image darkColorPreview;
    [SerializeField] private TextMeshProUGUI eloText;
    [SerializeField] private GameObject eloContainer;

    [SerializeField] private GameObject colorSelectorContainer;
    [SerializeField] private GameObject selector;
    [SerializeField] private GameObject whiteColorSelect;
    [SerializeField] private GameObject blackColorSelect;

    private void Start() {
        //load save data
        Chess.Load();

        //set color to preview image
        lightColorPreview.color = Chess.lightSquareColor;
        darkColorPreview.color = Chess.darkSquareColor;
        eloText.text = Stockfish.elo.ToString();
        eloSlider.value = Stockfish.elo;
        flipBoardToggle.isOn = Chess.flipBoard;

        selector.transform.position = whiteColorSelect.transform.position;
    }

    public void PlayButtonPressed(bool local) {
        //Change menu
        mainMenuButtonParent.SetActive(false);
        gameModeButtonParent.SetActive(true);

        //Activate back button
        backButton.SetActive(true);

        //Flip board toggle or elo slider
        if (local) {
            flipBoardToggle.gameObject.SetActive(true);
            eloContainer.gameObject.SetActive(false);
            colorSelectorContainer.SetActive(false);
        } else {
            flipBoardToggle.gameObject.SetActive(false);
            eloContainer.gameObject.SetActive(true);
            colorSelectorContainer.SetActive(true);
        }

        Chess.local = local;
    }

    public void GameModeButtonPressed(int gameMode) {
        if (gameMode == 0) {
            Chess.gameMode = GameMode.Standard;
            Chess.clockTime = int.MaxValue;
        } else if (gameMode == 1) {
            Chess.gameMode = GameMode.Blitz;
            Chess.clockTime = 3 * 60;
        } else if (gameMode == 2) {
            Chess.gameMode = GameMode.Blitz;
            Chess.clockTime = 5 * 60;
        } else if (gameMode == 3) {
            Chess.gameMode = GameMode.Rapid;
            Chess.clockTime = 10 * 60;
        } else if (gameMode == 4) {
            Chess.gameMode = GameMode.Rapid;
            Chess.clockTime = 15 * 60;
        }

        Chess.flipBoard = flipBoardToggle.isOn;

        Chess.Save();

        SceneManager.LoadScene(1);
    }

    public void ShowTimeSelect() {
        gameModeButtonParent.SetActive(false);
        timeSelectParent.SetActive(true);
    }

    //TODO: find better name
    public void PlayButtonPressed() {
        Chess.gameMode = GameMode.Custom;
        int minutes = 0;
        int seconds = 0;

        if (!int.TryParse(minutesInput.text, out minutes)) Debug.LogError("Error parsing minutes");
        if (!int.TryParse(secondsInput.text, out seconds)) Debug.LogError("Error parsing seconds");

        if (minutes >= 0 && seconds >= 0) {
            Chess.clockTime = minutes * 60 + seconds;
        }

        Chess.flipBoard = flipBoardToggle.isOn;

        Chess.Save();

        SceneManager.LoadScene(1);
    }

    public void ShowSettings() {
        mainMenuButtonParent.SetActive(false);
        settingsParent.SetActive(true);

        backButton.SetActive(true);
    }

    public void OnLightColorInputChange() {
        string hexString = lightColorInput.text;
        int rgb = Convert.ToInt32(hexString, 16);
        byte r = (byte)((rgb >> 16) & 255);
        byte g = (byte)((rgb >> 8) & 255);
        byte b = (byte)((rgb) & 255);

        Chess.lightSquareColor = new Color32(r, g, b, 255);
        lightColorPreview.color = Chess.lightSquareColor;
    }

    public void OnDarkColorInputChange() {
        string hexString = darkColorInput.text;
        int rgb = Convert.ToInt32(hexString, 16);
        byte r = (byte)((rgb >> 16) & 255);
        byte g = (byte)((rgb >> 8) & 255);
        byte b = (byte)((rgb) & 255);

        Chess.darkSquareColor = new Color32(r, g, b, 255);
        darkColorPreview.color = Chess.darkSquareColor;
    }

    public void BackButtonPressed() {
        Chess.Save();

        mainMenuButtonParent.SetActive(true);
        gameModeButtonParent.SetActive(false);
        timeSelectParent.SetActive(false);
        settingsParent.SetActive(false);

        backButton.SetActive(false);
    }

    public void OnEloChange() {
        Stockfish.elo = (int)eloSlider.value;
        eloText.text = ((int)eloSlider.value).ToString();
    }

    public void ResetToDefault() {
        Chess.lightSquareColor = new Color32(239, 216, 183, 255);
        lightColorPreview.color = Chess.lightSquareColor;

        Chess.darkSquareColor = new Color32(180, 135, 102, 255);
        darkColorPreview.color = Chess.darkSquareColor;

        Stockfish.elo = -1;
        eloSlider.value = Stockfish.elo;

        Chess.flipBoard = false;
        flipBoardToggle.isOn = Chess.flipBoard;
    }

    public void ColorSelect(GameObject selectorClicked) {
        selector.transform.position = selectorClicked.transform.position;

        if (selectorClicked.name == "White") {
            Chess.localColor = PieceColor.White;
        } else if (selectorClicked.name == "Black") {
            Chess.localColor = PieceColor.Black;
        }
    }

    public void Quit() {
        Application.Quit();
    }
}