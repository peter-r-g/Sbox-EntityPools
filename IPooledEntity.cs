using Sandbox;

namespace EntityPools;

/// <summary>
/// Defines an entity that overrides the default rent and return behavior in an <see cref="EntityPool{TEntity}"/>.
/// </summary>
public interface IPooledEntity : IEntity
{
	/// <summary>
	/// Invoked when the entity is being rented out to a consumer.
	/// </summary>
	void OnRent();
	/// <summary>
	/// Invoked when the entity is being returned to the pool.
	/// </summary>
	void OnReturn();
}
