namespace DotnetOtelDashboard.Tests;

public class RingBufferTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_NonPositiveCapacity_Throws(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(capacity));
    }

    [Fact]
    public void Constructor_SetsCapacityAndStartsEmpty()
    {
        var buffer = new RingBuffer<int>(3);

        Assert.Equal(3, buffer.Capacity);
        Assert.Equal(0, buffer.Count);
        Assert.Empty(buffer.Snapshot());
    }

    [Fact]
    public void Add_BelowCapacity_KeepsInsertionOrder()
    {
        var buffer = new RingBuffer<int>(5);

        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        Assert.Equal(3, buffer.Count);
        Assert.Equal([1, 2, 3], buffer.Snapshot());
    }

    [Fact]
    public void Add_OverCapacity_EvictsOldestAndKeepsTailOldestFirst()
    {
        const int capacity = 4;
        var buffer = new RingBuffer<int>(capacity);

        for (var i = 1; i <= capacity + 2; i++)
        {
            buffer.Add(i);
        }

        Assert.Equal(capacity, buffer.Count);
        Assert.Equal([3, 4, 5, 6], buffer.Snapshot());
    }

    [Fact]
    public void AddRange_MoreItemsThanCapacity_KeepsOnlyTail()
    {
        var buffer = new RingBuffer<int>(3);

        buffer.AddRange([1, 2, 3, 4, 5, 6, 7]);

        Assert.Equal(3, buffer.Count);
        Assert.Equal([5, 6, 7], buffer.Snapshot());
    }

    [Fact]
    public void AddRange_Null_Throws()
    {
        var buffer = new RingBuffer<int>(3);

        Assert.Throws<ArgumentNullException>(() => buffer.AddRange(null!));
    }

    [Fact]
    public void RemoveWhile_RemovesFromOldestEndUntilPredicateFails()
    {
        var buffer = new RingBuffer<int>(4);
        buffer.AddRange([1, 2, 3, 4]);

        var removed = buffer.RemoveWhile(x => x < 3);

        Assert.Equal(2, removed);
        Assert.Equal(2, buffer.Count);
        Assert.Equal([3, 4], buffer.Snapshot());
    }

    [Fact]
    public void RemoveWhile_RemovesEverything_LeavesEmptyAndAddStillWorks()
    {
        var buffer = new RingBuffer<int>(3);
        buffer.AddRange([1, 2, 3]);

        var removed = buffer.RemoveWhile(_ => true);

        Assert.Equal(3, removed);
        Assert.Equal(0, buffer.Count);
        Assert.Empty(buffer.Snapshot());

        buffer.Add(10);
        buffer.Add(20);

        Assert.Equal(2, buffer.Count);
        Assert.Equal([10, 20], buffer.Snapshot());
    }

    [Fact]
    public void RemoveWhile_NullPredicate_Throws()
    {
        var buffer = new RingBuffer<int>(3);

        Assert.Throws<ArgumentNullException>(() => buffer.RemoveWhile(null!));
    }

    [Fact]
    public void Clear_ResetsCountAndKeepsCapacity()
    {
        var buffer = new RingBuffer<int>(3);
        buffer.AddRange([1, 2, 3, 4]);

        buffer.Clear();

        Assert.Equal(0, buffer.Count);
        Assert.Equal(3, buffer.Capacity);
        Assert.Empty(buffer.Snapshot());
    }

    [Fact]
    public void WrapAround_AfterRemovingAndAdding_SnapshotsInInsertionOrder()
    {
        var buffer = new RingBuffer<int>(4);
        buffer.AddRange([1, 2, 3, 4]);

        buffer.RemoveWhile(x => x <= 2);
        buffer.Add(5);
        buffer.Add(6);

        Assert.Equal(4, buffer.Count);
        Assert.Equal([3, 4, 5, 6], buffer.Snapshot());
    }

    [Fact]
    public void Snapshot_IsNotAffectedByLaterMutations()
    {
        var buffer = new RingBuffer<int>(2);
        buffer.AddRange([1, 2]);

        var snapshot = buffer.Snapshot();
        buffer.Add(3);

        Assert.Equal([1, 2], snapshot);
    }

    [Fact]
    public async Task ConcurrentAdds_DoNotThrowAndRespectCapacity()
    {
        const int capacity = 1_000;
        const int tasks = 8;
        const int itemsPerTask = 10_000;
        var buffer = new RingBuffer<int>(capacity);

        var workers = Enumerable.Range(0, tasks).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < itemsPerTask; i++)
            {
                buffer.Add(t * itemsPerTask + i);
            }
        }));

        await Task.WhenAll(workers);

        Assert.Equal(capacity, buffer.Count);
        Assert.Equal(capacity, buffer.Snapshot().Length);
    }
}
