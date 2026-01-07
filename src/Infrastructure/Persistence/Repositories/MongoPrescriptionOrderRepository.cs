using Application.Interfaces.Repositories;
using Domain;
using DTOs.Shared;
using Infrastructure.Persistence.Mappers;
using Infrastructure.Persistence.Models;
using Infrastructure.Resilience;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of IPrescriptionOrderRepository.
/// Sealed for performance optimization and design intent.
/// </summary>
public sealed class MongoPrescriptionOrderRepository : MongoRepository<PrescriptionOrder, PrescriptionOrderDataModel>, IPrescriptionOrderRepository
{
    private readonly IMongoCollection<PatientDataModel> _patientsCollection;
    private readonly IMongoCollection<PrescriptionDataModel> _prescriptionsCollection;

    public MongoPrescriptionOrderRepository(
        IMongoCollection<PrescriptionOrderDataModel> collection,
        IMongoCollection<PatientDataModel> patientsCollection,
        IMongoCollection<PrescriptionDataModel> prescriptionsCollection,
        IResilientExecutor resilientExecutor)
        : base(collection, resilientExecutor)
    {
        _patientsCollection = patientsCollection;
        _prescriptionsCollection = prescriptionsCollection;
    }

    protected override PrescriptionOrder ToDomain(PrescriptionOrderDataModel model) =>
        PrescriptionOrderPersistenceMapper.ToDomain(model);

    protected override PrescriptionOrderDataModel ToDataModel(PrescriptionOrder entity) =>
        PrescriptionOrderPersistenceMapper.ToDataModel(entity);

    public async Task<IEnumerable<PrescriptionOrder>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
    {
        var orderModels = await _collection
            .Find(o => o.PatientId == patientId && !o.Metadata.IsDeleted)
            .SortByDescending(o => o.OrderDate)
            .ToListAsync(ct);

        var orders = orderModels.Select(ToDomain).ToList();
        await LoadPrescriptionsAsync(orders, ct);
        return orders;
    }

    public async Task<IEnumerable<PrescriptionOrder>> GetByPrescriptionIdAsync(Guid prescriptionId, CancellationToken ct = default)
    {
        var orderModels = await _collection
            .Find(o => o.PrescriptionId == prescriptionId && !o.Metadata.IsDeleted)
            .SortByDescending(o => o.OrderDate)
            .ToListAsync(ct);

        var orders = orderModels.Select(ToDomain).ToList();
        await LoadPatientsAsync(orders, ct);
        return orders;
    }

    public async Task<IEnumerable<PrescriptionOrder>> GetByStatusAsync(OrderStatus status, CancellationToken ct = default)
    {
        var dataStatus = (OrderStatusData)status;
        var orderModels = await _collection
            .Find(o => o.Status == dataStatus && !o.Metadata.IsDeleted)
            .SortByDescending(o => o.OrderDate)
            .ToListAsync(ct);

        var orders = orderModels.Select(ToDomain).ToList();
        await LoadDetailsAsync(orders, ct);
        return orders;
    }

    public async Task<IEnumerable<PrescriptionOrder>> GetPendingOrdersAsync(CancellationToken ct = default)
    {
        return await GetByStatusAsync(OrderStatus.Pending, ct);
    }

    public async Task<PrescriptionOrder?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        var order = await GetByIdAsync(id, ct);
        if (order == null)
            return null;

        // Load patient
        var patientModel = await _patientsCollection
            .Find(p => p.Id == order.PatientId && !p.Metadata.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (patientModel != null)
        {
            order.Patient = PatientPersistenceMapper.ToDomain(patientModel);
        }

        // Load prescription
        var prescriptionModel = await _prescriptionsCollection
            .Find(p => p.Id == order.PrescriptionId && !p.Metadata.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (prescriptionModel != null)
        {
            order.Prescription = PrescriptionPersistenceMapper.ToDomain(prescriptionModel);
        }

        return order;
    }

    /// <summary>
    /// Load prescriptions for a list of orders.
    /// </summary>
    private async Task LoadPrescriptionsAsync(List<PrescriptionOrder> orders, CancellationToken ct)
    {
        var prescriptionIds = orders.Select(o => o.PrescriptionId).Distinct().ToList();
        var prescriptionModels = await _prescriptionsCollection
            .Find(p => prescriptionIds.Contains(p.Id) && !p.Metadata.IsDeleted)
            .ToListAsync(ct);

        var prescriptionDict = prescriptionModels.ToDictionary(
            p => p.Id,
            p => PrescriptionPersistenceMapper.ToDomain(p));

        foreach (var order in orders)
        {
            if (prescriptionDict.TryGetValue(order.PrescriptionId, out var prescription))
            {
                order.Prescription = prescription;
            }
        }
    }

    /// <summary>
    /// Load patients for a list of orders.
    /// </summary>
    private async Task LoadPatientsAsync(List<PrescriptionOrder> orders, CancellationToken ct)
    {
        var patientIds = orders.Select(o => o.PatientId).Distinct().ToList();
        var patientModels = await _patientsCollection
            .Find(p => patientIds.Contains(p.Id) && !p.Metadata.IsDeleted)
            .ToListAsync(ct);

        var patientDict = patientModels.ToDictionary(
            p => p.Id,
            p => PatientPersistenceMapper.ToDomain(p));

        foreach (var order in orders)
        {
            if (patientDict.TryGetValue(order.PatientId, out var patient))
            {
                order.Patient = patient;
            }
        }
    }

    /// <summary>
    /// Load both patients and prescriptions for a list of orders.
    /// </summary>
    private async Task LoadDetailsAsync(List<PrescriptionOrder> orders, CancellationToken ct)
    {
        await LoadPatientsAsync(orders, ct);
        await LoadPrescriptionsAsync(orders, ct);
    }

    public async Task<IEnumerable<PrescriptionOrder>> GetAllWithDetailsAsync(CancellationToken ct = default)
    {
        var orders = (await GetAllAsync(ct)).ToList();
        await LoadDetailsAsync(orders, ct);
        return orders;
    }

    public async Task<IEnumerable<PrescriptionOrder>> GetByPatientIdWithDetailsAsync(Guid patientId, CancellationToken ct = default)
    {
        var orderModels = await _collection
            .Find(o => o.PatientId == patientId && !o.Metadata.IsDeleted)
            .SortByDescending(o => o.OrderDate)
            .ToListAsync(ct);

        var orders = orderModels.Select(ToDomain).ToList();
        await LoadDetailsAsync(orders, ct);
        return orders;
    }

    public async Task<IEnumerable<PrescriptionOrder>> GetByStatusWithDetailsAsync(OrderStatus status, CancellationToken ct = default)
    {
        var dataStatus = (OrderStatusData)status;
        var orderModels = await _collection
            .Find(o => o.Status == dataStatus && !o.Metadata.IsDeleted)
            .SortByDescending(o => o.OrderDate)
            .ToListAsync(ct);

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

        var query = _collection.Find(filter);
        query = ApplyOrderingForOrders(query, orderBy, descending);

        var orderModels = await query
            .Skip(skip)
            .Limit(top)
            .ToListAsync(ct);

        long totalCount = 0;
        if (includeCount)
        {
            totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
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

        var query = _collection.Find(filter);
        query = ApplyOrderingForOrders(query, orderBy, descending);

        var orderModels = await query
            .Skip(skip)
            .Limit(top)
            .ToListAsync(ct);

        long totalCount = 0;
        if (includeCount)
        {
            totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
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

