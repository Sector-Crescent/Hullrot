using System.Runtime.CompilerServices;

namespace Content.Shared._Scp.Helpers;

/// <summary>
/// Provides a static object pool for collections to minimize garbage collection allocations.
/// </summary>
/// <typeparam name="TCollection">The type of the collection being pooled. Must implement <see cref="ICollection{T}"/>.</typeparam>
/// <typeparam name="T">The type of the elements contained in the collection.</typeparam>
public static class CollectionPool<TCollection, T>
    where TCollection : class, ICollection<T>
{
    private static readonly Stack<TCollection> Pool = new();
    private static Func<TCollection>? _factory;

    /// <summary>
    /// Configures the factory function used to instantiate new collections when the pool is empty.
    /// </summary>
    /// <param name="factory">The delegate used to create new instances of <typeparamref name="TCollection"/>.</param>
    /// <exception cref="InvalidOperationException">Thrown when the provided <paramref name="factory"/> is null.</exception>
    public static void Configure(Func<TCollection> factory)
    {
        _factory = factory ?? throw new InvalidOperationException("Factory cannot be null");
    }

    /// <summary>
    /// Creates a new collection using the configured factory.
    /// </summary>
    /// <returns>A new instance of <typeparamref name="TCollection"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the pool has not been configured via <see cref="Configure"/>.</exception>
    private static TCollection Create()
    {
        if (_factory is null)
        {
            throw new InvalidOperationException(
                $"CollectionPool<{typeof(TCollection).Name}, {typeof(T).Name}> " +
                $"is not configured. Call Configure(factory) before use.");
        }

        return _factory();
    }

    /// <summary>
    /// Rents a collection from the pool. If the pool is empty, a new collection is created.
    /// </summary>
    /// <returns>A disposable <see cref="PooledCollection"/> wrapper. Use within a <see langword="using"/> statement to automatically return the collection to the pool.</returns>
    public static PooledCollection Rent()
    {
        return Pool.TryPop(out var collection)
            ? new PooledCollection(collection)
            : new PooledCollection(Create());
    }

    /// <summary>
    /// Returns a collection to the pool. Clears the collection before storing it.
    /// Collections will be dropped and garbage collected if the pool is full (>= 512 items) or if the collection capacity exceeds 2048.
    /// </summary>
    /// <param name="collection">The collection to return to the pool.</param>
    internal static void Return(TCollection collection)
    {
        if (Pool.Count >= 512)
            return;

        if (collection is List<T> list && list.Capacity > 2048)
            return;

        if (collection is HashSet<T> hashSet && hashSet.Capacity > 2048)
            return;

        collection.Clear();
        Pool.Push(collection);
    }

    /// <summary>
    /// An allocation-free disposable wrapper around a rented collection.
    /// </summary>
    public struct PooledCollection : IDisposable
    {
        private TCollection? _value;
        private bool _disposed;

        /// <summary>
        /// Gets the underlying rented collection.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if accessed after the collection has been returned to the pool.</exception>
        public TCollection Value
        {
            get
            {
                if (_disposed)
                    throw new InvalidOperationException("The collection has already been returned to the pool.");

                return _value!;
            }
        }

        internal PooledCollection(TCollection value)
        {
            _value   = value;
            _disposed = false;
        }

        /// <summary>
        /// Returns the underlying collection to the pool and marks this wrapper as disposed.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_value is not null)
            {
                Return(_value);
                _value = default;
            }
        }
    }
}

/// <summary>
/// A pre-configured pool specifically for <see cref="List{T}"/> instances.
/// </summary>
/// <typeparam name="T">The type of elements in the list.</typeparam>
public static class ListPool<T>
{
    static ListPool()
    {
        CollectionPool<List<T>, T>.Configure(() => new List<T>());
    }

    /// <summary>
    /// Rents a list from the pool.
    /// </summary>
    /// <returns>A disposable wrapper containing the rented list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollectionPool<List<T>, T>.PooledCollection Rent()
        => CollectionPool<List<T>, T>.Rent();
}

/// <summary>
/// A helper pool for renting lists of entities with specific components.
/// </summary>
/// <typeparam name="T">The component type associated with the entity.</typeparam>
public static class ListPoolEntity<T> where T : IComponent
{
    /// <summary>
    /// Rents a list of <see cref="Entity{T}"/> from the pool.
    /// </summary>
    /// <returns>A disposable wrapper containing the rented entity list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollectionPool<List<Entity<T>>, Entity<T>>.PooledCollection Rent()
        => ListPool<Entity<T>>.Rent();
}

/// <summary>
/// A pre-configured pool specifically for <see cref="HashSet{T}"/> instances.
/// </summary>
/// <typeparam name="T">The type of elements in the hash set.</typeparam>
public static class HashSetPool<T>
{
    static HashSetPool()
    {
        CollectionPool<HashSet<T>, T>.Configure(() => new HashSet<T>());
    }

    /// <summary>
    /// Rents a hash set from the pool.
    /// </summary>
    /// <returns>A disposable wrapper containing the rented hash set.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollectionPool<HashSet<T>, T>.PooledCollection Rent()
        => CollectionPool<HashSet<T>, T>.Rent();
}

/// <summary>
/// A helper pool for renting hash sets of entities with specific components.
/// </summary>
/// <typeparam name="T">The component type associated with the entity.</typeparam>
public static class HashSetPoolEntity<T> where T : IComponent
{
    /// <summary>
    /// Rents a hash set of <see cref="Entity{T}"/> from the pool.
    /// </summary>
    /// <returns>A disposable wrapper containing the rented entity hash set.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CollectionPool<HashSet<Entity<T>>, Entity<T>>.PooledCollection Rent()
        => HashSetPool<Entity<T>>.Rent();
}
