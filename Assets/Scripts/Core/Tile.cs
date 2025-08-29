using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class Tile : MonoBehaviour
{
    public Vector2Int GridPos { get; private set; }
    public TileType Type;
    public Special SpecialType = Special.None;
    public bool IsMatched;

    [HideInInspector] public Board Board;
    private SpriteRenderer _sr;

    // drag için
    private Vector3 _pressWorld;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    public void Init(Board board, Vector2Int gridPos, TileType type, Sprite sprite, Special special = Special.None)
    {
        Board = board;
        GridPos = gridPos;
        Type = type;
        SpecialType = special;
        _sr.sprite = sprite;
        IsMatched = false;
        name = $"Tile_{type}_{gridPos.x}_{gridPos.y}";
    }

    public void SetGridPos(Vector2Int p) => GridPos = p;

    private void OnMouseDown()
    {
        if (!Board || !Board.InputEnabled) return;
        _pressWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Board.SelectTile(this);
    }

    private void OnMouseEnter()
    {
        if (!Board || !Board.InputEnabled) return;
        if (Input.GetMouseButton(0))
            Board.HoverTile(this);
    }

    private void OnMouseUp()
    {
        if (!Board || !Board.InputEnabled) return;

        var release = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 d = release - _pressWorld;
        if (d.magnitude < 0.15f) return; // küçük hareketi yok say

        Vector2Int dir = Mathf.Abs(d.x) > Mathf.Abs(d.y)
            ? new Vector2Int(d.x > 0 ? 1 : -1, 0)
            : new Vector2Int(0, d.y > 0 ? 1 : -1);

        var p = GridPos + dir;
        if (p.x < 0 || p.x >= Board.Width || p.y < 0 || p.y >= Board.Height) return;

        var neighbor = Board.GetTile(p);
        if (neighbor) Board.TrySwapPublic(this, neighbor);
    }
}
