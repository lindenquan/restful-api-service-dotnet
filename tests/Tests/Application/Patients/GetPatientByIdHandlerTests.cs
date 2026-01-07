using Application.Interfaces.Repositories;
using Application.Patients.Operations;
using Domain;
using Moq;
using Shouldly;

namespace Tests.Application.Patients;

/// <summary>
/// Unit tests for GetPatientByIdHandler.
/// </summary>
public class GetPatientByIdHandlerTests
{
    private static readonly Guid TestPatientId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private static readonly Guid NonExistentId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPatientRepository> _patientRepoMock;
    private readonly GetPatientByIdHandler _handler;

    public GetPatientByIdHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _patientRepoMock = new Mock<IPatientRepository>();

        _unitOfWorkMock.Setup(u => u.Patients).Returns(_patientRepoMock.Object);

        _handler = new GetPatientByIdHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingPatient_ShouldReturnPatientDto()
    {
        // Arrange
        var dateOfBirth = new DateTime(1990, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var patient = new Patient
        {
            Id = TestPatientId,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "555-1234",
            DateOfBirth = dateOfBirth
        };

        _patientRepoMock
            .Setup(r => r.GetByIdAsync(TestPatientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var query = new GetPatientByIdQuery(TestPatientId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result!.Id.ShouldBe(TestPatientId);
        result.FirstName.ShouldBe("John");
        result.LastName.ShouldBe("Doe");
        result.Email.ShouldBe("john.doe@example.com");
        result.Phone.ShouldBe("555-1234");
        result.DateOfBirth.ShouldBe(dateOfBirth);
        result.FullName.ShouldBe("John Doe");
    }

    [Fact]
    public async Task Handle_WithNonExistentPatient_ShouldReturnNull()
    {
        // Arrange
        _patientRepoMock
            .Setup(r => r.GetByIdAsync(NonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var query = new GetPatientByIdQuery(NonExistentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }
}

