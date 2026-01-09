using Domain;
using DTOs.Shared;
using Infrastructure.Persistence.Models;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Partial class containing paged query methods and ordering logic.
/// </summary>
public sealed partial class MongoPrescriptionOrderRepository
{
    public async Task<IEnumerable<PrescriptionOrder>> GetAllWithDetailsAsync(CancellationToken ct = default)
    {
        var orders = (await GetAllAsync(ct)).ToList();
        await LoadDetailsAsync(orders, ct);
        return orders;
    }

    public async Task<IEnumerable<PrescriptionOrder>> GetByPatientIdWithDetailsAsync(Guid patientId, CancellationToken ct = default)
    {
        var filter = Builders<PrescriptionOrderDataModel>.Filter.And(
            Builders<PrescriptionOrderDataModel>.Filter.Eq(o => o.PatientId, patientId),
            Builders<PrescriptionOrderDataModel>.Filter.Eq(o => o.Metadata.IsDeleted, false));

        var orderModels = Session != null
            ? await _collection.Find(Session, filter).SortByDescending(o => o.OrderDate).ToListAsync(ct)
            : await _collection.Find(filter).SortByDescending(o => o.OrderDate).ToListAsync(ct);

        var orders = orderModels.Select(ToDomain).ToList();
        await LoadDetailsAsync(orders, ct);
        return orders;
    }

    public async Task<IEnumerable<PrescriptionOrder>> GetByStatusWithDetailsAsync(OrderStatus status, CancellationToken ct = default)
    {
        var dataStatus = (OrderStatusData)status;
        var filter = Builders<PrescriptionOrderDataModel>.Filter.And(
            Builders<PrescriptionOrderDataModel>.Filter.Eq(o => o.Status, dataStatus),
            Builders<PrescriptionOrderDataModel>.Filter.Eq(o => o.Metadata.IsDeleted, false));

        var orderModels = Session != null
            ? await _collection.Find(Session, filter).SortByDescending(o => o.OrderDate).ToListAsync(ct)
            : await _collection.Find(filter).SortByDescending(o => o.OrderDate).ToListAsync(ct);

        var orders = orderModels.Select(ToDomain).ToList();
        await LoadDetailsAsync(orders, ct);
        return orders;
    }

    public async Task<PagedData<PrescriptionOrder>> GetPagedWithDetailsAsync(
        int skip,
        int top,
        bool includeCount = false,
        string? orderBy = null,
        bool descending = false,
        CancellationToken ct = default)
    {
        var filter = Builders<PrescriptionOrderDataModel>.Filter.Eq(e => e.Metadata.IsDeleted, false);

        var query = Session != null
            ? _collection.Find(Session, filter)
            : _collection.Find(filter);
        query = ApplyOrderingForOrders(query, orderBy, descending);

        var orderModels = await query
            .Skip(skip)
            .Limit(top)
            .ToListAsync(ct);

        long totalCount = 0;
        if (includeCount)
        {
            totalCount = Session != null
                ? await _collection.CountDocumentsAsync(Session, filter, cancellationToken: ct)
                : await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
        }

        var orders = orderModels.Select(ToDomain).ToList();
        await LoadDetailsAsync(orders, ct);

        return new PagedData<PrescriptionOrder>(orders, totalCount);
    }

    public async Task<PagedData<PrescriptionOrder>> GetPagedByPatientWithDetailsAsync(
        Guid patientId,
        int skip,
        int top,
        bool includeCount = false,
        string? orderBy = null,
        bool descending = false,
        CancellationToken ct = default)
    {
        var filter = Builders<PrescriptionOrderDataModel>.Filter.And(
            Builders<PrescriptionOrderDataModel>.Filter.Eq(o => o.PatientId, patientId),
            Builders<PrescriptionOrderDataModel>.Filter.Eq(o => o.Metadata.IsDeleted, false));

        var query = Session != null
            ? _collection.Find(Session, filter)
            : _collection.Find(filter);
        query = ApplyOrderingForOrders(query, orderBy, descending);

        var orderModels = await query
            .Skip(skip)
            .Limit(top)
            .ToListAsync(ct);

        long totalCount = 0;
        if (includeCount)
        {
            totalCount = Session != null
                ? await _collection.CountDocumentsAsync(Session, filter, cancellationToken: ct)
                : await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
        }

        var orders = orderModels.Select(ToDomain).ToList();
        await LoadDetailsAsync(orders, ct);

        return new PagedData<PrescriptionOrder>(orders, totalCount);
    }

    /// <summary>
    /// Apply ordering specific to PrescriptionOrder fields.
    /// </summary>
    private static IFindFluent<PrescriptionOrderDataModel, PrescriptionOrderDataModel> ApplyOrderingForOrders(
        IFindFluent<PrescriptionOrderDataModel, PrescriptionOrderDataModel> query,
        string? orderBy,
        bool descending)
    {
        if (string.IsNullOrEmpty(orderBy))
        {
            return query.SortByDescending(e => e.OrderDate);
        }

        var normalizedOrderBy = orderBy.ToLowerInvariant();

        return normalizedOrderBy switch
        {
            "orderdate" => descending
                ? query.SortByDescending(e => e.OrderDate)
                : query.SortBy(e => e.OrderDate),
            "status" => descending
                ? query.SortByDescending(e => e.Status)
                : query.SortBy(e => e.Status),
            "fulfilleddate" => descending
                ? query.SortByDescending(e => e.FulfilledDate)
                : query.SortBy(e => e.FulfilledDate),
            "pickupdate" => descending
                ? query.SortByDescending(e => e.PickupDate)
                : query.SortBy(e => e.PickupDate),
            "createdat" => descending
                ? query.SortByDescending(e => e.Metadata.CreatedAt)
                : query.SortBy(e => e.Metadata.CreatedAt),
            _ => query.SortByDescending(e => e.OrderDate)
        };
    }
}

