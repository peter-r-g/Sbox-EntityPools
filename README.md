# Entity Pools
A pooling mechanism for entities with an API like C# [ArrayPool\<T\>](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1?view=net-7.0). Useful for situations where you are creating and deleting many entities with a limited lifespan.

## Features
* Generics support. Any type implementing [IEntity](https://asset.party/api/Sandbox.IEntity) (any [Entity](https://asset.party/api/Sandbox.Entity)) and contains a parameterless constructor will suffice.
* Limited capacity pools. This helps you maintain sensible entity counts and prevent excessive creations.
* Overridable entity rent/return logic. Simply add the [IPooledEntity](https://github.com/peter-r-g/Sbox-EntityPools/blob/master/IPooledEntity.cs) interface to your entity.
* Timed asynchronous returning of entities. Useful for fire and forget renting.
* Networkable (All required functionality is implemented but is currently unusable due to S&box issues.)

## Installation
You can either download from this repo or you can reference it with `gooman.entity_pools` using [asset.party](https://asset.party/)

## License
Distributed under the MIT License. See the [license](https://github.com/peter-r-g/Sbox-EntityPools/blob/master/LICENSE.md) for more information.
