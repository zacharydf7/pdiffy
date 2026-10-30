using System.Collections;

namespace PDiffy;

/// <summary>
/// A read-only list that equals another with the same items in the same order, so a record that holds lists compares
/// by what they hold: two loads of the same document are equal.
/// </summary>
internal sealed class ValueList<T> : IReadOnlyList<T>, IEquatable<ValueList<T>>
{
    private readonly T[] _items;

    private ValueList(T[] items) => _items = items;

    public static ValueList<T> Empty { get; } = new([]);

    // A list that's already one is kept as it is, so assigning one record's list to another doesn't copy it.
    public static ValueList<T> Of(IEnumerable<T> items) => items as ValueList<T> ?? new([.. items]);

    public int Count => _items.Length;

    public T this[int index] => _items[index];

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(ValueList<T>? other) => other is not null && _items.SequenceEqual(other._items);

    public override bool Equals(object? obj) => obj is ValueList<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _items) hash.Add(item);
        return hash.ToHashCode();
    }

    // Records print their properties, so a failing Assert.Equal shows the items rather than a type name.
    public override string ToString() => $"[{string.Join(", ", _items)}]";
}
