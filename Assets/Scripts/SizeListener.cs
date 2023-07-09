using UnityEngine;

public class SizeListener : MonoBehaviour {
    [SerializeField] private Board board;

    private float lastScreenWidth = 0f;
    private float lastScreenHeight = 0f;

    void Start() {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    void Update() {
        if (lastScreenWidth != Screen.width ||lastScreenHeight != Screen.height) {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            board.ResizeBoard();
        }

    }
}
