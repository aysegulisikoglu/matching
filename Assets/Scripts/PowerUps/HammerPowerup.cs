using UnityEngine;
using UnityEngine.EventSystems;

public class HammerPowerup : MonoBehaviour
{
    [Header("Refs")]
    public Board Board;

    [Header("State")]
    public bool Armed = false;

    private Camera _cam;

    private void Awake()
    {
        _cam = Camera.main;
    }

    private void Update()
    {
        if (!Armed) return;
        if (Input.GetMouseButtonDown(0))
        {
            // UI üzerindeki tıklamayı yok say
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (_cam == null) _cam = Camera.main;
            if (_cam == null || Board == null) return;

            Vector3 wp = _cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 p = new Vector2(wp.x, wp.y);

            var hit = Physics2D.OverlapPoint(p);
            if (hit != null)
            {
                var tile = hit.GetComponent<Tile>();
                if (tile != null)
                {
                    // Taşı yok et ve board’u tamamen çöz
                    Destroy(tile.gameObject);
                    // kırdığı hücrenin breakable katmanını da vur
                    Board.HitBreakableAt(tile.GridPos);
                    StartCoroutine(Board.ResolveBoard());
                    Armed = false;
                }
            }
        }
    }


    public void Arm()
    {
        Armed = true;
    }
}
