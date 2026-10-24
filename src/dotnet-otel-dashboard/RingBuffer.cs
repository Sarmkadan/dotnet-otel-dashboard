namespace DotnetOtelDashboard;

/// <summary>
/// Fixed-capacity, thread-safe FIFO ring buffer. When full, <see cref="Add"/> overwrites the oldest item.
/// Backed by a single array with a head index and a count; no reallocation after construction.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
internal sealed class RingBuffer<T>
{
    private readonly T[] _buffer;
    private readonly object _sync = new();
    private int _head;
    private int _count;

    /// <summary>
    /// Creates an empty buffer that holds at most <paramref name="capacity"/> items.
    /// </summary>
    /// <param name="capacity">Maximum number of items; must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than or equal to zero.</exception>
    public RingBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _buffer = new T[capacity];
    }

    /// <summary>
    /// Number of items currently held.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _count;
            }
        }
    }

    /// <summary>
    /// Maximum number of items the buffer can hold.
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Appends <paramref name="item"/>. If the buffer is full, the oldest item is overwritten.
    /// </summary>
    /// <param name="item">Item to append.</param>
    public void Add(T item)
    {
        lock (_sync)
        {
            AddCore(item);
        }
    }

    /// <summary>
    /// Appends each item in <paramref name="items"/> in order, under a single lock acquisition.
    /// Items that do not fit overwrite the oldest entries.
    /// </summary>
    /// <param name="items">Items to append.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        lock (_sync)
        {
            foreach (var item in items)
            {
                AddCore(item);
            }
        }
    }

    /// <summary>
    /// Returns a new array containing the current items, oldest first. The returned array is not affected by later mutations.
    /// </summary>
    /// <returns>A copy of the items in insertion order.</returns>
    public T[] Snapshot()
    {
        lock (_sync)
        {
            var result = new T[_count];
            for (var i = 0; i < _count; i++)
            {
                result[i] = _buffer[IndexOf(i)];
            }

            return result;
        }
    }

    /// <summary>
    /// Removes items from the oldest end while <paramref name="predicate"/> returns <see langword="true"/>.
    /// Stops at the first item for which the predicate returns <see langword="false"/>.
    /// </summary>
    /// <param name="predicate">Eviction test, called with each item from oldest to newest. It runs while the buffer lock is held
    /// and must not call back into this buffer.</param>
    /// <returns>The number of items removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    public int RemoveWhile(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        lock (_sync)
        {
            var removed = 0;
            while (_count > 0 && predicate(_buffer[_head]!))
            {
                _buffer[_head] = default!;
                _head = Advance(_head);
                _count--;
                removed++;
            }

            return removed;
        }
    }

    /// <summary>
    /// Removes all items. <see cref="Capacity"/> is unchanged.
    /// </summary>
    public void Clear()
    {
        lock (_sync)
        {
            Array.Clear(_buffer);
            _head = 0;
            _count = 0;
        }
    }

    // Caller must hold _sync.
    private void AddCore(T item)
    {
        if (_count < _buffer.Length)
        {
            _buffer[IndexOf(_count)] = item;
            _count++;
        }
        else
        {
            // Full: the slot at _head holds the oldest item. Overwrite it and make the new item the newest.
            _buffer[_head] = item;
            _head = Advance(_head);
        }
    }

    // Maps a logical offset from the oldest item to a physical array index. Caller must hold _sync.
    private int IndexOf(int offset)
    {
        var index = _head + offset;
        return index >= _buffer.Length ? index - _buffer.Length : index;
    }

    private int Advance(int index)
    {
        var next = index + 1;
        return next == _buffer.Length ? 0 : next;
    }
}
