using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Persistence.Models;
using Infrastructure.Resilience;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic MongoDB repository implementation with resilience (retry + circuit breaker) and transaction support.
/// Works with data models internally, maps to/from domain entities at boundaries.
/// Uses UUID v7 for entity identifiers.
/// <para>
/// <strong>Transaction Support:</strong>
/// When a transaction is active (via IMongoSessionProvider), all operations automatically
/// participate in the transaction. No code changes needed in calling code.
/// </para>
/// <para>
/// This class is split into partial classes for maintainability:
/// <list type="bullet">
///   <item><description>MongoRepository.cs - Core CRUD operations</description></item>
///   <item><description>MongoRepository.Queries.cs - Query and find operations</description></item>
///   <item><description>MongoRepository.Paged.cs - Pagination and ordering</description></item>
/// </list>
/// </para>
/// </summary>
/// <typeparam name="TEntity">Domain entity type</typeparam>
/// <typeparam name="TDataModel">Data model type for persistence</typeparam>
public abstract partial class MongoRepository<TEntity, TDataModel> : IRepository<TEntity>
    where TEntity : BaseEntity
    where TDataModel : BaseDataModel
{
    protected readonly IMongoCollection<TDataModel> _collection;
    protected readonly IResilientExecutor _resilientExecutor;
    protected readonly IMongoSessionProvider? _sessionProvider;

    protected MongoRepository(
        IMongoCollection<TDataModel> collection,
        IResilientExecutor resilientExecutor,
        IMongoSessionProvider? sessionProvider = null)
    {
        _collection = collection;
        _resilientExecutor = resilientExecutor;
        _sessionProvider = sessionProvider;
    }

    /// <summary>
    /// Gets the current session if a transaction is active.
    /// </summary>
    protected IClientSessionHandle? Session => _sessionProvider?.CurrentSession;

    /// <summary>
    /// Map a data model to a domain entity.
    /// </summary>
    protected abstract TEntity ToDomain(TDataModel model);

    /// <summary>
    /// Map a domain entity to a data model.
    /// </summary>
    protected abstract TDataModel ToDataModel(TEntity entity);

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<TDataModel>.Filter.And(
                Builders<TDataModel>.Filter.Eq(e => e.Id, id),
                Builders<TDataModel>.Filter.Eq(e => e.Metadata.IsDeleted, false));

            var model = Session != null
                ? await _collection.Find(Session, filter).FirstOrDefaultAsync(token)
                : await _collection.Find(filter).FirstOrDefaultAsync(token);

            return model == null ? null : ToDomain(model);
        }, ct);
    }

    public async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<TDataModel>.Filter.Eq(e => e.Metadata.IsDeleted, false);
            var models = Session != null
                ? await _collection.Find(Session, filter).ToListAsync(token)
                : await _collection.Find(filter).ToListAsync(token);

            return models.Select(ToDomain);
        }, ct);
    }

    public async Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            // Generate UUID v7 if not already set
            if (entity.Id == Guid.Empty)
            {
                entity.Id = Guid.CreateVersion7();
            }
            var dataModel = ToDataModel(entity);
            dataModel.Id = entity.Id;
            dataModel.Metadata.CreatedAt = DateTime.UtcNow;
            dataModel.Metadata.CreatedBy = entity.CreatedBy;

            if (Session != null)
                await _collection.InsertOneAsync(Session, dataModel, cancellationToken: token);
            else
                await _collection.InsertOneAsync(dataModel, cancellationToken: token);
        }, ct);
    }

    public async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var dataModels = new List<TDataModel>();

            foreach (var entity in entities)
            {
                // Generate UUID v7 if not already set
                if (entity.Id == Guid.Empty)
                {
                    entity.Id = Guid.CreateVersion7();
                }
                var dataModel = ToDataModel(entity);
                dataModel.Id = entity.Id;
                dataModel.Metadata.CreatedAt = DateTime.UtcNow;
                dataModel.Metadata.CreatedBy = entity.CreatedBy;
                dataModels.Add(dataModel);
            }

            if (Session != null)
                await _collection.InsertManyAsync(Session, dataModels, cancellationToken: token);
            else
                await _collection.InsertManyAsync(dataModels, cancellationToken: token);
        }, ct);
    }

    public async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var dataModel = ToDataModel(entity);
            dataModel.Metadata.UpdatedAt = DateTime.UtcNow;
            dataModel.Metadata.UpdatedBy = entity.UpdatedBy;
            var filter = Builders<TDataModel>.Filter.Eq(e => e.Id, entity.Id);

            if (Session != null)
                await _collection.ReplaceOneAsync(Session, filter, dataModel, cancellationToken: token);
            else
                await _collection.ReplaceOneAsync(filter, dataModel, cancellationToken: token);
        }, ct);
    }

    public async Task SoftDeleteAsync(TEntity entity, Guid? deletedBy = null, CancellationToken ct = default)
    {
        await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<TDataModel>.Filter.Eq(e => e.Id, entity.Id);
            var update = Builders<TDataModel>.Update
                .Set(e => e.Metadata.IsDeleted, true)
                .Set(e => e.Metadata.DeletedAt, DateTime.UtcNow)
                .Set(e => e.Metadata.DeletedBy, deletedBy);

            if (Session != null)
                await _collection.UpdateOneAsync(Session, filter, update, cancellationToken: token);
            else
                await _collection.UpdateOneAsync(filter, update, cancellationToken: token);
        }, ct);
    }

    public async Task HardDeleteAsync(TEntity entity, CancellationToken ct = default)
    {
        await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var filter = Builders<TDataModel>.Filter.Eq(e => e.Id, entity.Id);

            if (Session != null)
                await _collection.DeleteOneAsync(Session, filter, cancellationToken: token);
            else
                await _collection.DeleteOneAsync(filter, cancellationToken: token);
        }, ct);
    }

    public async Task HardDeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        await _resilientExecutor.ExecuteMongoDbAsync(async token =>
        {
            var ids = entities.Select(e => e.Id).ToList();
            var filter = Builders<TDataModel>.Filter.In(e => e.Id, ids);

            if (Session != null)
                await _collection.DeleteManyAsync(Session, filter, cancellationToken: token);
            else
                await _collection.DeleteManyAsync(filter, cancellationToken: token);
        }, ct);
    }
}

