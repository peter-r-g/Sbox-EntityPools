using Sandbox;
using System;
using System.Threading.Tasks;

namespace EntityPools;

/// <summary>
/// Contains a number of extension methods for the <see cref="IEntity"/> interface.
/// </summary>
public static class IEntityExtensions
{
	/// <summary>
	/// Returns an <see ref="entity"/> to its pool after a number of milliseconds.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity being returned.</typeparam>
	/// <param name="entity">The entity to return to its pool.</param>
	/// <param name="ms">The time in milliseconds to delay before returning the entity.</param>
	/// <param name="pool">The pool to return the entity to. If null, the entity will be returned to <see cref="EntityPool{TEntity}.Shared"/>.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public static async Task ReturnAsync<TEntity>( this TEntity entity, int ms, EntityPool<TEntity>? pool = null )
		where TEntity : IEntity, new()
	{
		pool ??= EntityPool<TEntity>.Shared;

		await GameTask.Delay( ms );
		pool.Return( entity );
	}

	/// <summary>
	/// Returns an <see ref="entity"/> to its pool after a number of seconds.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity being returned.</typeparam>
	/// <param name="entity">The entity to return to its pool.</param>
	/// <param name="seconds">The time in seconds to delay before returning the entity.</param>
	/// <param name="pool">The pool to return the entity to. If null, the entity will be returned to <see cref="EntityPool{TEntity}.Shared"/>.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public static async Task ReturnAsync<TEntity>( this TEntity entity, float seconds, EntityPool<TEntity>? pool = null )
		where TEntity : IEntity, new()
	{
		pool ??= EntityPool<TEntity>.Shared;

		await GameTask.DelaySeconds( seconds );
		pool.Return( entity );
	}
}
