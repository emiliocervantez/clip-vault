namespace ClipVault.Core;

/// <summary>
/// The capped, ordered list of clips, newest first.
/// Deduplicates by content hash. Tracks the last chosen clip.
/// </summary>
public sealed class History
{
    private readonly List<Clip> _items;

    public History(int maxItems, IEnumerable<Clip>? items = null, Guid? lastChosenId = null)
    {
        MaxItems = Settings.ClampMaxItems(maxItems);
        _items = items?.ToList() ?? new List<Clip>();
        LastChosenId = lastChosenId;
        EvictOverflow();
    }

    public IReadOnlyList<Clip> Items => _items;
    public int MaxItems { get; private set; }
    public Guid? LastChosenId { get; private set; }

    /// <summary>Raised when the list or the last chosen clip changes.</summary>
    public event Action? Changed;

    /// <summary>Raised for every clip that leaves the history, so its image file can be deleted.</summary>
    public event Action<Clip>? Removed;

    public bool Contains(string hash) => _items.Any(c => c.Hash == hash);

    /// <summary>If a clip with this hash exists, moves it to the top and returns true.</summary>
    public bool Touch(string hash)
    {
        var index = _items.FindIndex(c => c.Hash == hash);
        if (index < 0) return false;
        if (index > 0)
        {
            var clip = _items[index];
            _items.RemoveAt(index);
            _items.Insert(0, clip);
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>Inserts at the top. An existing clip with the same hash is dropped first.</summary>
    public void Add(Clip clip)
    {
        var existing = _items.FindIndex(c => c.Hash == clip.Hash);
        if (existing >= 0)
        {
            var old = _items[existing];
            _items.RemoveAt(existing);
            Removed?.Invoke(old);
        }
        _items.Insert(0, clip);
        EvictOverflow();
        Changed?.Invoke();
    }

    /// <summary>Marks the clip as last chosen; optionally moves it to the top.</summary>
    public void Choose(Guid id, bool moveToTop)
    {
        var index = _items.FindIndex(c => c.Id == id);
        if (index < 0) return;
        LastChosenId = id;
        if (moveToTop && index > 0)
        {
            var clip = _items[index];
            _items.RemoveAt(index);
            _items.Insert(0, clip);
        }
        Changed?.Invoke();
    }

    public bool Remove(Guid id)
    {
        var index = _items.FindIndex(c => c.Id == id);
        if (index < 0) return false;
        var clip = _items[index];
        _items.RemoveAt(index);
        if (LastChosenId == id) LastChosenId = null;
        Removed?.Invoke(clip);
        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        var removed = _items.ToList();
        _items.Clear();
        LastChosenId = null;
        foreach (var clip in removed) Removed?.Invoke(clip);
        Changed?.Invoke();
    }

    public void SetMaxItems(int maxItems)
    {
        MaxItems = Settings.ClampMaxItems(maxItems);
        if (EvictOverflow()) Changed?.Invoke();
    }

    private bool EvictOverflow()
    {
        var evicted = false;
        while (_items.Count > MaxItems)
        {
            var clip = _items[^1];
            _items.RemoveAt(_items.Count - 1);
            if (LastChosenId == clip.Id) LastChosenId = null;
            Removed?.Invoke(clip);
            evicted = true;
        }
        return evicted;
    }
}
