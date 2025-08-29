using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Breakable : MonoBehaviour
{
    [Tooltip("Kaç vuruşta tamamen kırılır")]
    public int layers = 2;

    [Tooltip("layers'a göre göstereceğin sprite'lar (index 0 = en kalın)")]
    public Sprite[] visuals;

    private SpriteRenderer _sr;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _sr.sortingOrder = -1;
        Refresh();
    }

    public void SetLayers(int count)
    {
        layers = Mathf.Max(1, count);
        Refresh();
    }

    public bool Hit()
    {
        layers--;
        Refresh();
        if (layers <= 0)
        {
            Destroy(gameObject);
            return true;
        }
        return false;
    }

    private void Refresh()
    {
        if (_sr == null) return;
        if (visuals != null && visuals.Length > 0)
        {
            int idx = Mathf.Clamp(layers - 1, 0, visuals.Length - 1);
            _sr.sprite = visuals[idx];
        }
    }
}
