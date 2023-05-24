using Sandbox;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace EntityPools;

/// <summary>
/// A pool of entities that can be rented and returned. Useful for types of entities that have limited lifecycles.
/// </summary>
/// <typeparam name="TEntity">The type of entity for the pool to contain.</typeparam>
public sealed partial class EntityPool<TEntity> : BaseNetworkable, INetworkSerializer
	where TEntity : IEntity, new()
{
	/// <summary>
	/// A shared instance of <see cref="EntityPool{TEntity}"/>.
	/// </summary>
	/// <remarks>NOTE: Hotloading could destroy this instance.</remarks>
	public static EntityPool<TEntity> Shared
	{
		get
		{
			sharedInstance ??= new();
			return sharedInstance;
		}
	}
	/// <summary>
	/// Backing field to <see cref="Shared"/>.
	/// </summary>
	private static EntityPool<TEntity>? sharedInstance;

	/// <summary>
	/// Gets or sets the maximum capacity of this pool.
	/// </summary>
	public int MaxCapacity
	{
		get => maxCapacity;
		set
		{
			if ( value < Entities.Count && !AttemptDownsize( value ) )
				throw new ArgumentOutOfRangeException( $"EntityPool<{typeof( TEntity ).Name}> cannot be downsized to {value}" );

			maxCapacity = value;
			Entities.EnsureCapacity( maxCapacity );
		}
	}
	/// <summary>
	/// Backing field to <see cref="MaxCapacity"/>.
	/// </summary>
	[Net] private int maxCapacity { get; set; }

	/// <summary>
	/// The starting capacity of all pools.
	/// </summary>
	private const int InitialCapacity = 50;

	/// <summary>
	/// The pool of entities contained. A boolean for whether or not it is currently rented out.
	/// </summary>
	private Dictionary<TEntity, bool> Entities { get; set; }

	/// <summary>
	/// Initializes a new instance of <see cref="EntityPool{TEntity}"/>.
	/// </summary>
	/// <param name="initialCapacity">The starting capacity of the pool.</param>
	/// <param name="fill">Whether or not to fill the pool on construction.</param>
	/// <param name="cb">The callback to provide to the <see cref="Fill(Action{TEntity}?)"/> method.</param>
	private EntityPool( int initialCapacity = InitialCapacity, bool fill = false, Action<TEntity>? cb = null )
	{
		Entities = new( initialCapacity );
		MaxCapacity = initialCapacity;

		if ( fill )
			Fill( cb );
	}

	/// <summary>
	/// Clears any invalid entities from the pool.
	/// </summary>
	public void ClearInvalidEntities()
	{
		var invalidEntities = new Stack<TEntity>();

		foreach ( var (entity, _) in Entities )
		{
			if ( !entity.IsValid )
				invalidEntities.Push( entity );
		}

		while ( invalidEntities.TryPop( out var entity ) )
			Entities.Remove( entity );

		WriteNetworkData();
	}

	/// <summary>
	/// Fills the pool to capacity.
	/// </summary>
	/// <param name="cb">An optional callback to initialize the entity.</param>
	public void Fill( Action<TEntity>? cb = null )
	{
		for ( var i = Entities.Count; i < MaxCapacity; i++ )
		{
			var entity = new TEntity();
			Return( entity );
			if ( cb is not null )
				cb( entity );
		}

		WriteNetworkData();
	}

	/// <summary>
	/// Fills the pool with an amount of entities.
	/// </summary>
	/// <param name="count">The number of entities to add to the pool.</param>
	/// <param name="cb">An optional callback to initialize the entity.</param>
	/// <exception cref="InvalidOperationException">Thrown when the entity pool is full.</exception>
	public void Fill( int count, Action<TEntity>? cb = null )
	{
		for ( var i = 0; i < count; i++ )
		{
			var entity = new TEntity();
			Return( entity );
			if ( cb is not null )
				cb( entity );
		}

		WriteNetworkData();
	}

	/// <summary>
	/// Returns an entity from the pool. If no entities in the pool are available then a new one is created and returned.
	/// </summary>
	/// <returns>An entity from the pool.</returns>
	/// <exception cref="InvalidOperationException">Thrown when a new entity has to be made but the pool is full.</exception>
	public TEntity Rent()
	{
		// Check if we have an entity to spare.
		foreach ( var (entity, isRented) in Entities )
		{
			// Cleanup any invalid entities before we rent one out.
			if ( !entity.IsValid )
			{
				ClearInvalidEntities();
				return Rent();
			}

			if ( isRented )
				continue;

			Entities[entity] = true;
			PrepareForRent( entity );
			return entity;
		}

		// Make a new entity and add it to the pool.
		AssertCapacity();

		var newEntity = new TEntity();
		PrepareForReturn( newEntity );
		Entities.Add( newEntity, true );

		WriteNetworkData();
		return newEntity;
	}

	/// <summary>
	/// Returns an entity to the pool. This can also be used to add a new entity to the pool.
	/// </summary>
	/// <param name="entity">The entity to return.</param>
	/// <exception cref="ArgumentException">Thrown when an invalid entity is being returned.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the entity is new and there is no space in the pool.</exception>
	public void Return( TEntity entity )
	{
		// Check if the entity being returned is unusable.
		if ( !entity.IsValid )
			throw new ArgumentException( $"EntityPool<{typeof( TEntity ).Name}> Do not return an invalid entity", nameof( entity ) );

		// Check if this entity is already pooled.
		if ( Entities.ContainsKey( entity ) )
		{
			PrepareForReturn( entity );
			Entities[entity] = false;
			return;
		}

		// Add this entity to the pool.
		AssertCapacity();

		PrepareForReturn( entity );
		Entities.Add( entity, false );

		WriteNetworkData();
	}

	/// <summary>
	/// Prepares an entity to be rented out to a consumer.
	/// </summary>
	/// <param name="entity">The entity to prepare.</param>
	private static void PrepareForRent( TEntity entity )
	{
		if ( entity is IPooledEntity pooledEntity )
		{
			pooledEntity.OnRent();
			return;
		}

		if ( entity is Entity basicEntity )
			basicEntity.Transmit = TransmitType.Default;
	}

	/// <summary>
	/// Prepares an entity to be returned to the entity pool.
	/// </summary>
	/// <param name="entity">The entity to prepare.</param>
	private static void PrepareForReturn( TEntity entity )
	{
		if ( entity is IPooledEntity pooledEntity )
		{
			pooledEntity.OnReturn();
			return;
		}

		entity.Position = Vector3.Zero;
		entity.Rotation = Rotation.Identity;
		entity.Velocity = Vector3.Zero;
		if ( entity is Entity basicEntity )
			basicEntity.Transmit = TransmitType.Never;
	}

	/// <summary>
	/// Asserts that the entity pool is not full.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when the entity pool is full.</exception>
	private void AssertCapacity()
	{
		if ( Entities.Count >= MaxCapacity )
			throw new InvalidOperationException( $"{this} ran out of capacity. Consider raising the {nameof( MaxCapacity )}" );
	}

	/// <summary>
	/// Attempts to downsize the pool to the target amount.
	/// </summary>
	/// <param name="targetSize">The target size to downsize to.</param>
	/// <returns>Whether or not the downsize happened.</returns>
	private bool AttemptDownsize( int targetSize )
	{
		// Get all entities that aren't currently being rented out to consumers.
		var unrentedEntities = new Stack<TEntity>();

		foreach( var (entity, isRented) in Entities )
		{
			if ( isRented )
				continue;

			unrentedEntities.Push( entity );
		}

		// Check if the downsize is possible.
		if ( Entities.Count - unrentedEntities.Count > targetSize )
			return false;

		// Remove entities till we're at the target size.
		while ( Entities.Count > targetSize )
		{
			var entity = unrentedEntities.Pop();
			entity.Delete();
			Entities.Remove( entity );
		}

		Entities.TrimExcess();
		WriteNetworkData();

		return true;
	}

	/// <inheritdoc/>
	public void Read( ref NetRead read )
	{
		Entities.Clear();
		MaxCapacity = read.Read<int>();
		var pairCount = read.Read<int>();

		for ( var i = 0; i < pairCount; i++ )
			Entities.Add( (TEntity)(IEntity)Entity.FindByIndex( read.Read<int>() ), read.Read<bool>() );
	}

	/// <inheritdoc/>
	public void Write( NetWrite write )
	{
		write.Write( MaxCapacity );
		write.Write( Entities.Count );

		foreach ( var (entity, isRented) in Entities )
		{
			write.Write( entity.NetworkIdent );
			write.Write( isRented );
		}
	}
	
	/// <summary>
	/// Creates a new <see cref="EntityPool{TEntity}"/> with default options.
	/// </summary>
	/// <returns>A new instance of <see cref="EntityPool{TEntity}"/>.</returns>
	public static EntityPool<TEntity> Create()
	{
		return new EntityPool<TEntity>();
	}

	/// <summary>
	/// Creates a new <see cref="EntityPool{TEntity}"/> with different options.
	/// </summary>
	/// <param name="initialCapacity">The starting capacity of the pool.</param>
	/// <param name="fill">Whether or not to fill the pool on construction.</param>
	/// <param name="cb">The callback to provide to the <see cref="Fill(Action{TEntity}?)"/> method.</param>
	/// <returns>A new instance of <see cref="EntityPool{TEntity}"/>.</returns>
	public static EntityPool<TEntity> Create( int initialCapacity, bool fill = false, Action<TEntity>? cb = null )
	{
		return new EntityPool<TEntity>( initialCapacity, fill, cb );
	}
}
