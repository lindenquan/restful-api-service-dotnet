using Application.Interfaces.Repositories;
using Application.Prescriptions.Operations;
using Domain;
using DTOs.Shared;
using Moq;
using Shouldly;

namespace Tests.Application.Prescriptions;

/// <summary>
/// Unit tests for GetPrescriptionsPagedHandler with OData support.
/// </summary>
public class GetPrescriptionsPagedHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPrescriptionRepository> _prescriptionRepoMock;
    private readonly GetPrescriptionsPagedHandler _handler;

    public GetPrescriptionsPagedHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _prescriptionRepoMock = new Mock<IPrescriptionRepository>();

        _unitOfWorkMock.Setup(u => u.Prescriptions).Returns(_prescriptionRepoMock.Object);

        _handler = new GetPrescriptionsPagedHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidODataParameters_ShouldReturnPagedData()
    {
        // Arrange
        const int skip = 0;
        const int top = 20;
        const bool includeCount = true;
        const string orderBy = "prescribedDate";
        const bool descending = true;

        var prescriptions = new List<Prescription>
        {
            new Prescription
            {
                Id = Guid.NewGuid(),
                MedicationName = "Aspirin",
                Dosage = "100mg",
                Frequency = "Daily",
                Quantity = 30,
                RefillsRemaining = 2,
                PrescriberName = "Dr. Smith",
                PrescribedDate = DateTime.UtcNow.AddDays(-10),
                ExpiryDate = DateTime.UtcNow.AddDays(350),
                PatientId = Guid.NewGuid()
            }
        };

        var pagedData = new PagedData<Prescription>(prescriptions, 1);

        _prescriptionRepoMock
            .Setup(r => r.GetPagedWithPatientsAsync(skip, top, includeCount, orderBy, descending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedData);

        var query = new GetPrescriptionsPagedQuery(skip, top, includeCount, orderBy, descending);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(1);
        result.TotalCount.ShouldBe(1);
        result.Items[0].MedicationName.ShouldBe("Aspirin");

        _prescriptionRepoMock.Verify(
            r => r.GetPagedWithPatientsAsync(skip, top, includeCount, orderBy, descending, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyResults_ShouldReturnEmptyPagedData()
    {
        // Arrange
        const int skip = 0;
        const int top = 20;
        var pagedData = new PagedData<Prescription>(new List<Prescription>(), 0);

        _prescriptionRepoMock
            .Setup(r => r.GetPagedWithPatientsAsync(skip, top, false, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedData);

        var query = new GetPrescriptionsPagedQuery(skip, top);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_WithSortingDescending_ShouldPassParametersToRepository()
    {
        // Arrange
        const int skip = 0;
        const int top = 50;
        const bool includeCount = true;
        const string orderBy = "expiryDate";
        const bool descending = true;

        var pagedData = new PagedData<Prescription>(new List<Prescription>(), 0);

        int? capturedSkip = null;
        int? capturedTop = null;
        bool? capturedIncludeCount = null;
        string? capturedOrderBy = null;
        bool? capturedDescending = null;

        _prescriptionRepoMock
            .Setup(r => r.GetPagedWithPatientsAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, int, bool, string?, bool, CancellationToken>((s, t, ic, ob, d, _) =>
            {
                capturedSkip = s;
                capturedTop = t;
                capturedIncludeCount = ic;
                capturedOrderBy = ob;
                capturedDescending = d;
            })
            .ReturnsAsync(pagedData);

        var query = new GetPrescriptionsPagedQuery(skip, top, includeCount, orderBy, descending);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        capturedSkip.ShouldBe(skip);
        capturedTop.ShouldBe(top);
        capturedIncludeCount.ShouldBe(includeCount);
        capturedOrderBy.ShouldBe(orderBy);
        capturedDescending.ShouldBe(descending);
    }
}

