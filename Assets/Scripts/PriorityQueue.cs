using System;
using System.Collections.Generic;

/// <summary>
/// A lightweight, generic min-heap–based Priority Queue implementation used internally for game logic,
/// especially pathfinding and AI decision-making. 
/// 
/// This class provides a simple and efficient way to retrieve the next element with the lowest priority value.
/// It avoids external dependencies for performance and maintainability within Unity builds.
/// 
/// Design notes specific to RogueShooter:
/// - Used by the pathfinding and tactical AI systems to determine optimal movement and action order.
/// - Provides deterministic and garbage-free priority management during runtime (no LINQ or heap allocations).
/// - Does not support key priority updates (“decrease-key”) — instead, updated items are re-enqueued,
///   and outdated entries are safely ignored by the higher-level game logic.
/// 
/// In short, this queue enables efficient and predictable priority handling for all turn-based tactical calculations,
/// without relying on .NET’s built-in PriorityQueue (which is unavailable in some Unity versions).
/// </summary>
public sealed class PriorityQueue<T>
{
    private (T item, int priority)[] _heap;
    private int _count;

    public int Count => _count;

    public PriorityQueue(int initialCapacity = 64)
    {
        if (initialCapacity < 1) initialCapacity = 1;
        _heap = new (T, int)[initialCapacity];
        _count = 0;
    }

    public void Clear()
    {
        Array.Clear(_heap, 0, _count);
        _count = 0;
    }

    public void Enqueue(T item, int priority)
    {
        if (_count == _heap.Length) Array.Resize(ref _heap, _heap.Length * 2);
        _heap[_count] = (item, priority);
        SiftUp(_count++);
    }

    public T Dequeue()
    {
        if (_count == 0) throw new InvalidOperationException("PriorityQueue is empty");
        T result = _heap[0].item;
        _heap[0] = _heap[--_count];
        _heap[_count] = default;
        if (_count > 0) SiftDown(0);
        return result;
    }

    public bool TryDequeue(out T item)
    {
        if (_count == 0)
        {
            item = default;
            return false;
        }
        item = Dequeue();
        return true;
    }

    public T Peek()
    {
        if (_count == 0) throw new InvalidOperationException("PriorityQueue is empty");
        return _heap[0].item;
    }

    public int PeekPriority()
    {
        if (_count == 0) throw new InvalidOperationException("PriorityQueue is empty");
        return _heap[0].priority;
    }

    private void SiftUp(int idx)
    {
        while (idx > 0)
        {
            int parent = (idx - 1) >> 1;
            if (_heap[parent].priority <= _heap[idx].priority) break;
            (_heap[parent], _heap[idx]) = (_heap[idx], _heap[parent]);
            idx = parent;
        }
    }

    private void SiftDown(int idx)
    {
        while (true)
        {
            int left = (idx << 1) + 1;
            if (left >= _count) break;
            int right = left + 1;
            int smallest = (right < _count && _heap[right].priority < _heap[left].priority) ? right : left;
            if (_heap[idx].priority <= _heap[smallest].priority) break;
            (_heap[idx], _heap[smallest]) = (_heap[smallest], _heap[idx]);
            idx = smallest;
        }
    }
}
