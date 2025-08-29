using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Board : MonoBehaviour
{
    // --- Breakables ---
    [Header("Breakables (optional)")]
    public GameObject BreakablePrefab;    // SpriteRenderer + Breakable
    [System.Serializable]
    public class BreakablePlacement { public Vector2Int Cell; public int Layers = 2; }
    public List<BreakablePlacement> Breakables = new List<BreakablePlacement>();

    private Breakable[,] _breakGrid;

    [Header("Grid")]
    public int Width = 8;
    public int Height = 8;
    public float TileSize = 1f;

    [Header("Tiles & Sprites")]
    // TilePrefab: SpriteRenderer + BoxCollider2D + Tile
    public GameObject TilePrefab;
    public Sprite RedSprite, BlueSprite, GreenSprite, YellowSprite, PurpleSprite, OrangeSprite;

    [Header("Gameplay")]
    public bool InputEnabled = false;

    [Header("Hints")]
    public float HintDelay = 5f;   // kaç saniye idle sonra ipucu verilsin

    private Tile[,] _grid;
    private Tile _selected;
    private Dictionary<TileType, Sprite> _spriteMap;

    // skor / durum için olaylar
    public System.Action<int> OnTilesCleared;        // o turda kaç taş temizlendi
    public System.Action OnBoardStable;              // swap denemesinden sonra tahta durduğunda
    public System.Action<int, int> OnChainResolved;  // (chainIndex, clearedThisChain)

    // dahili sayaçlar
    private int _lastClearedCount = 0;
    private float _idle = 0f;
    private Coroutine _hintCo;

    private void Start()
    {
        _spriteMap = new Dictionary<TileType, Sprite>
        {
            { TileType.Red,    RedSprite },
            { TileType.Blue,   BlueSprite },
            { TileType.Green,  GreenSprite },
            { TileType.Yellow, YellowSprite },
            { TileType.Purple, PurpleSprite },
            { TileType.Orange, OrangeSprite }
        };

        GenerateBoard();
        InputEnabled = true;
    }

    private void Update()
    {
        _idle += Time.deltaTime;
        if (_idle > HintDelay)
        {
            _idle = 0f;
            if (_hintCo == null) _hintCo = StartCoroutine(ShowOneHint());
        }
    }

    // ---------- Kurulum ----------
    public void GenerateBoard()
    {
        _grid = new Tile[Width, Height];
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var type = GetRandomTypeAvoidingMatches(x, y);
                SpawnTile(new Vector2Int(x, y), type);
            }
        }
        // --- breakable kurulum ---
        InitBreakables();

        StartCoroutine(ResolveBoard());
    }
    void InitBreakables()
    {
        _breakGrid = new Breakable[Width, Height];
        if (BreakablePrefab == null || Breakables == null) return;

        foreach (var b in Breakables)
        {
            if (b == null) continue;
            var c = b.Cell;
            if (c.x < 0 || c.x >= Width || c.y < 0 || c.y >= Height) continue;

            var go = Instantiate(BreakablePrefab, GridToWorld(c), Quaternion.identity, transform);
            var br = go.GetComponent<Breakable>();
            if (br != null)
            {
                br.SetLayers(Mathf.Max(1, b.Layers));
                _breakGrid[c.x, c.y] = br;
            }
        }
    }
    public void HitBreakableAt(Vector2Int cell)
    {
        if (_breakGrid == null) return;
        var br = _breakGrid[cell.x, cell.y];
        if (br == null) return;

        bool destroyed = br.Hit();
        if (destroyed) _breakGrid[cell.x, cell.y] = null;
    }

    TileType GetRandomType()
    {
        int r = Random.Range(0, 6);
        return (TileType)r;
    }

    TileType GetRandomTypeAvoidingMatches(int x, int y)
    {
        TileType t;
        int guard = 0;
        do
        {
            t = GetRandomType();
            guard++;
        } while (WouldCreateImmediateMatch(x, y, t) && guard < 20);
        return t;
    }

    bool WouldCreateImmediateMatch(int x, int y, TileType t)
    {
        if (x >= 2)
        {
            if (_grid[x - 1, y] && _grid[x - 2, y]
                && _grid[x - 1, y].Type == t && _grid[x - 2, y].Type == t)
                return true;
        }
        if (y >= 2)
        {
            if (_grid[x, y - 1] && _grid[x, y - 2]
                && _grid[x, y - 1].Type == t && _grid[x, y - 2].Type == t)
                return true;
        }
        return false;
    }

    void SpawnTile(Vector2Int pos, TileType type, Special special = Special.None)
    {
        var go = Instantiate(TilePrefab, GridToWorld(pos), Quaternion.identity, transform);
        var tile = go.GetComponent<Tile>();
        tile.Init(this, pos, type, _spriteMap[type], special);
        _grid[pos.x, pos.y] = tile;
    }

    Vector3 GridToWorld(Vector2Int p) => new Vector3(p.x * TileSize, p.y * TileSize, 0);

    // ---------- Input köprüleri ----------
    public Tile GetTile(Vector2Int p) => _grid[p.x, p.y];
    public void TrySwapPublic(Tile a, Tile b) { StartCoroutine(TrySwap(a, b)); }

    public void SelectTile(Tile t)
    {
        _idle = 0f;
        if (_selected == null) _selected = t;
        else
        {
            if (AreAdjacent(_selected, t)) StartCoroutine(TrySwap(_selected, t));
            _selected = null;
        }
    }

    public void HoverTile(Tile t)
    {
        _idle = 0f;
        if (_selected != null && AreAdjacent(_selected, t))
        {
            StartCoroutine(TrySwap(_selected, t));
            _selected = null;
        }
    }

    bool AreAdjacent(Tile a, Tile b)
        => Mathf.Abs(a.GridPos.x - b.GridPos.x) + Mathf.Abs(a.GridPos.y - b.GridPos.y) == 1;

    IEnumerator TrySwap(Tile a, Tile b)
    {
        if (!InputEnabled) yield break;
        _idle = 0f;
        InputEnabled = false;

        SwapInGrid(a, b);
        yield return AnimateSwap(a, b);

        var anyMatch = MarkMatches();
        if (anyMatch)
        {
            yield return ResolveBoard();
        }
        else
        {
            // geri al
            SwapInGrid(a, b);
            yield return AnimateSwap(a, b);
        }

        InputEnabled = true;
        OnBoardStable?.Invoke();
    }

    void SwapInGrid(Tile a, Tile b)
    {
        var posA = a.GridPos;
        var posB = b.GridPos;
        _grid[posA.x, posA.y] = b;
        _grid[posB.x, posB.y] = a;
        a.SetGridPos(posB);
        b.SetGridPos(posA);
    }

    IEnumerator AnimateSwap(Tile a, Tile b)
    {
        var wa = StartCoroutineLerp(a, GridToWorld(a.GridPos), 0.1f);
        var wb = StartCoroutineLerp(b, GridToWorld(b.GridPos), 0.1f);
        yield return wa; yield return wb;
    }

    Coroutine StartCoroutineLerp(Tile t, Vector3 target, float time)
        => StartCoroutine(CoLerp(t ? t.transform : null, target, time));

    IEnumerator CoLerp(Transform tr, Vector3 target, float time)
    {
        if (tr == null) yield break;
        Vector3 start = tr.position; float elapsed = 0f;
        while (elapsed < time)
        {
            if (tr == null) yield break;
            elapsed += Time.deltaTime;
            tr.position = Vector3.Lerp(start, target, elapsed / time);
            yield return null;
        }
        if (tr != null) tr.position = target;
    }

    // ---------- Match + Özel taş üretimi ----------
    // run>=3 işaretler, run==4: çizgili; run>=5: bomba
    bool MarkMatches()
    {
        bool any = false;


        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_grid[x, y]) _grid[x, y].IsMatched = false;


        for (int y = 0; y < Height; y++)
        {
            int run = 1;
            for (int x = 1; x < Width; x++)
            {
                if (_grid[x, y] && _grid[x - 1, y] && _grid[x, y].Type == _grid[x - 1, y].Type) run++;
                else
                {
                    if (run >= 3)
                    {
                        int start = x - run;
                        if (run == 4) MakeSpecial(new Vector2Int(start + 1, y), Special.LineH);
                        else if (run >= 5) MakeSpecial(new Vector2Int(start + run / 2, y), Special.Bomb);

                        for (int k = 0; k < run; k++)
                        {
                            var t = _grid[start + k, y];
                            if (t && t.SpecialType == Special.None) t.IsMatched = true;
                        }
                        any = true;
                    }
                    run = 1;
                }
            }
            if (run >= 3)
            {
                int start = Width - run;
                if (run == 4) MakeSpecial(new Vector2Int(start + 1, y), Special.LineH);
                else if (run >= 5) MakeSpecial(new Vector2Int(start + run / 2, y), Special.Bomb);

                for (int k = 0; k < run; k++)
                {
                    var t = _grid[start + k, y];
                    if (t && t.SpecialType == Special.None) t.IsMatched = true;
                }
                any = true;
            }
        }


        for (int x = 0; x < Width; x++)
        {
            int run = 1;
            for (int y = 1; y < Height; y++)
            {
                if (_grid[x, y] && _grid[x, y - 1] && _grid[x, y].Type == _grid[x, y - 1].Type) run++;
                else
                {
                    if (run >= 3)
                    {
                        int start = y - run;
                        if (run == 4) MakeSpecial(new Vector2Int(x, start + 1), Special.LineV);
                        else if (run >= 5) MakeSpecial(new Vector2Int(x, start + run / 2), Special.Bomb);

                        for (int k = 0; k < run; k++)
                        {
                            var t = _grid[x, start + k];
                            if (t && t.SpecialType == Special.None) t.IsMatched = true;
                        }
                        any = true;
                    }
                    run = 1;
                }
            }
            if (run >= 3)
            {
                int start = Height - run;
                if (run == 4) MakeSpecial(new Vector2Int(x, start + 1), Special.LineV);
                else if (run >= 5) MakeSpecial(new Vector2Int(x, start + run / 2), Special.Bomb);

                for (int k = 0; k < run; k++)
                {
                    var t = _grid[x, start + k];
                    if (t && t.SpecialType == Special.None) t.IsMatched = true;
                }
                any = true;
            }
        }

        return any;
    }

    void MakeSpecial(Vector2Int p, Special spec)
    {
        var t = _grid[p.x, p.y];
        if (t == null) return;
        t.IsMatched = false;
        t.SpecialType = spec;

        t.transform.localScale = (spec == Special.Bomb) ? Vector3.one * 1.15f : new Vector3(1.1f, 1.1f, 1f);
    }

    void MarkRow(int y)
    {
        for (int x = 0; x < Width; x++)
            if (_grid[x, y]) _grid[x, y].IsMatched = true;
    }
    void MarkCol(int x)
    {
        for (int y = 0; y < Height; y++)
            if (_grid[x, y]) _grid[x, y].IsMatched = true;
    }
    void MarkBombArea(Vector2Int center, int radius = 1)
    {
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                int xx = center.x + dx, yy = center.y + dy;
                if (xx >= 0 && xx < Width && yy >= 0 && yy < Height && _grid[xx, yy])
                    _grid[xx, yy].IsMatched = true;
            }
    }

    // ---------- Çözümleme döngüsü ----------
    public IEnumerator ResolveBoard()
    {
        int chain = 0;


        yield return ClearMatches();
        yield return Collapse();
        yield return Refill();
        chain++;
        OnChainResolved?.Invoke(chain, _lastClearedCount);

        while (MarkMatches())
        {
            yield return ClearMatches();
            yield return Collapse();
            yield return Refill();
            chain++;
            OnChainResolved?.Invoke(chain, _lastClearedCount);
        }


        if (!HasAnyValidSwap())
            yield return ShuffleBoard();
    }

    IEnumerator ClearMatches()
    {

        List<Tile> specials = new List<Tile>();
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var t = _grid[x, y];
                if (t && t.IsMatched && t.SpecialType != Special.None)
                    specials.Add(t);
            }
        foreach (var s in specials)
        {
            if (s.SpecialType == Special.LineH) MarkRow(s.GridPos.y);
            else if (s.SpecialType == Special.LineV) MarkCol(s.GridPos.x);
            else if (s.SpecialType == Special.Bomb) MarkBombArea(s.GridPos, 1);
        }

        int cleared = 0;
        var byType = new Dictionary<TileType, int>();

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var t = _grid[x, y];
                if (t && t.IsMatched)
                {
                    cleared++;
                    if (!byType.ContainsKey(t.Type)) byType[t.Type] = 0;
                    byType[t.Type]++;

                    Destroy(t.gameObject);

                    HitBreakableAt(new Vector2Int(x, y));
                    _grid[x, y] = null;
                }
            }
        }

        _lastClearedCount = cleared;
        if (cleared > 0) OnTilesCleared?.Invoke(cleared);


        if (byType.Count > 0 && MatchEvents.Instance != null)
        {
            foreach (var kv in byType)
                MatchEvents.Instance.ReportCleared(kv.Key, kv.Value);
        }

        yield return new WaitForSeconds(0.05f);
    }

    // sütunları aşağı indir
    public IEnumerator Collapse()
    {
        for (int x = 0; x < Width; x++)
        {
            int emptyY = -1;
            for (int y = 0; y < Height; y++)
            {
                if (_grid[x, y] == null)
                {
                    if (emptyY == -1) emptyY = y;
                }
                else if (emptyY != -1)
                {
                    var t = _grid[x, y];
                    _grid[x, emptyY] = t; _grid[x, y] = null;
                    t.SetGridPos(new Vector2Int(x, emptyY));
                    StartCoroutine(CoLerp(t ? t.transform : null, GridToWorld(t.GridPos), 0.08f));
                    emptyY++;
                }
            }
        }
        yield return new WaitForSeconds(0.12f);
    }

    // boşlukları üstten doldur
    public IEnumerator Refill()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (_grid[x, y] == null)
                {
                    var type = GetRandomType();
                    SpawnTile(new Vector2Int(x, y), type);
                    var t = _grid[x, y];
                    var spawnPos = GridToWorld(new Vector2Int(x, y + 3));
                    if (t)
                    {
                        t.transform.position = spawnPos;
                        StartCoroutine(CoLerp(t.transform, GridToWorld(t.GridPos), 0.1f));
                    }
                }
            }
        }
        yield return new WaitForSeconds(0.12f);
    }

    // ---------- No-move kontrolü + Shuffle ----------
    bool SwapCreatesMatch(Vector2Int a, Vector2Int b)
    {
        var A = _grid[a.x, a.y]; var B = _grid[b.x, b.y];
        if (A == null || B == null) return false;

        // geçici swap
        _grid[a.x, a.y] = B; _grid[b.x, b.y] = A;
        bool ok = MarkMatches();

        // geri al ve flag'leri temizle
        _grid[a.x, a.y] = A; _grid[b.x, b.y] = B;
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_grid[x, y]) _grid[x, y].IsMatched = false;

        return ok;
    }

    bool HasAnyValidSwap()
    {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var p = new Vector2Int(x, y);
                if (x + 1 < Width && SwapCreatesMatch(p, new Vector2Int(x + 1, y))) return true;
                if (y + 1 < Height && SwapCreatesMatch(p, new Vector2Int(x, y + 1))) return true;
            }
        return false;
    }

    IEnumerator ShuffleBoard()
    {
        // tipleri karıştır
        List<TileType> bag = new List<TileType>();
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_grid[x, y]) bag.Add(_grid[x, y].Type);

        for (int i = 0; i < bag.Count; i++)
        {
            int j = Random.Range(i, bag.Count);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }

        int idx = 0;
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_grid[x, y])
                {
                    _grid[x, y].Type = bag[idx++];
                    _grid[x, y].IsMatched = false;
                    var sr = _grid[x, y].GetComponent<SpriteRenderer>();
                    if (sr) sr.sprite = _spriteMap[_grid[x, y].Type];
                    _grid[x, y].transform.localScale = Vector3.one; // ipucundan kalan scale'i sıfırla
                }


        while (MarkMatches())
        {
            yield return ClearMatches();
            yield return Collapse();
            yield return Refill();
        }
    }

    // ---------- İpucu (hint) ----------
    IEnumerator ShowOneHint()
    {

        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                var p = new Vector2Int(x, y);
                if (x + 1 < Width && SwapCreatesMatch(p, new Vector2Int(x + 1, y)))
                { yield return Pulse(_grid[x, y], _grid[x + 1, y]); break; }
                if (y + 1 < Height && SwapCreatesMatch(p, new Vector2Int(x, y + 1)))
                { yield return Pulse(_grid[x, y], _grid[x, y + 1]); break; }
            }
        _hintCo = null;
    }

    IEnumerator Pulse(Tile a, Tile b)
    {
        if (a == null || b == null) yield break;
        float t = 0f, dur = 0.6f;
        var ta = a.transform; var tb = b.transform;
        Vector3 sa = ta.localScale, sb = tb.localScale;
        while (t < dur)
        {
            t += Time.deltaTime;
            float s = 1f + 0.1f * Mathf.Sin(t * 12f);
            if (ta) ta.localScale = sa * s;
            if (tb) tb.localScale = sb * s;
            yield return null;
        }
        if (ta) ta.localScale = sa;
        if (tb) tb.localScale = sb;
    }
}
