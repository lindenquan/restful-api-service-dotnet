using Application.Interfaces.Repositories;
using Application.Patients.Operations;
using Domain;
using Moq;
using Shouldly;

namespace Tests.Application.Patients;

/// <summary>
/// Unit tests for GetAllPatientsHandler.
/// </summary>
public class GetAllPatientsHandlerTests
{
    private static readonly Guid PatientId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PatientId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPatientRepository> _patientRepoMock;
    private readonly GetAllPatientsHandler _handler;

    public GetAllPatientsHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _patientRepoMock = new Mock<IPatientRepository>();

        _unitOfWorkMock.Setup(u => u.Patients).Returns(_patientRepoMock.Object);

        _handler = new GetAllPatientsHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithPatients_ShouldReturnAllPatients()
    {
        // Arrange
        var patients = new List<Patient>
        {
            new Patient
            {
                Id = PatientId1,
                FirstName = "John",
                LastName = "Doe",
                Email = "john@example.com",
                DateOfBirth = new DateTime(1990, 1, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new Patient
            {
                Id = PatientId2,
                FirstName = "Jane",
                LastName = "Smith",
                Email = "jane@example.com",
                DateOfBirth = new DateTime(1985, 5, 20, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        _patientRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(patients);

        var query = new GetAllPatientsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Count().ShouldBe(2);

        var patientList = result.ToList();
        patientList[0].FirstName.ShouldBe("John");
        patientList[0].FullName.ShouldBe("John Doe");
        patientList[1].FirstName.ShouldBe("Jane");
        patientList[1].FullName.ShouldBe("Jane Smith");
    }

    [Fact]
    public async Task Handle_WithNoPatients_ShouldReturnEmptyList()
    {
        // Arrange
        _patientRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Patient>());

        var query = new GetAllPatientsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }
}

