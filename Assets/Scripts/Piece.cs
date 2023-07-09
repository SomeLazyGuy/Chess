using UnityEngine;
using ChessNamespace;
using System;

public class Piece : MonoBehaviour {
    public int file;
    public int rank;
    public PieceColor color;
    public PieceType type;

    public Piece() { }

    public Piece(int file, int rank, PieceColor color, PieceType type) {
        this.file = file;
        this.rank = rank;
        this.color = color;
        this.type = type;
    }
}
