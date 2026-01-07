using Domain;
using Infrastructure.Persistence.Models;
using Riok.Mapperly.Abstractions;

namespace Infrastructure.Persistence.Mappers;

/// <summary>
/// Maps between User domain entity and UserDataModel.
/// Uses Mapperly source generator for compile-time mapping.
/// </summary>
[Mapper]
public static partial class UserPersistenceMapper
{
    [MapProperty(nameof(UserDataModel.Metadata) + "." + nameof(DataModelMetadata.CreatedAt), nameof(User.CreatedAt))]
    [MapProperty(nameof(UserDataModel.Metadata) + "." + nameof(DataModelMetadata.CreatedBy), nameof(User.CreatedBy))]
    [MapProperty(nameof(UserDataModel.Metadata) + "." + nameof(DataModelMetadata.UpdatedAt), nameof(User.UpdatedAt))]
    [MapProperty(nameof(UserDataModel.Metadata) + "." + nameof(DataModelMetadata.UpdatedBy), nameof(User.UpdatedBy))]
    public static partial User ToDomain(UserDataModel model);

    // Note: Metadata is managed by repository, not copied from entity
    [MapperIgnoreSource(nameof(User.CreatedAt))]
    [MapperIgnoreSource(nameof(User.CreatedBy))]
    [MapperIgnoreSource(nameof(User.UpdatedAt))]
    [MapperIgnoreSource(nameof(User.UpdatedBy))]
    [MapperIgnoreTarget(nameof(UserDataModel.Metadata))]
    public static partial UserDataModel ToDataModel(User entity);

    // Custom mapping for enum conversion
    private static UserType MapUserType(UserTypeData userType) => (UserType)userType;
    private static UserTypeData MapUserType(UserType userType) => (UserTypeData)userType;

    public static IEnumerable<User> ToDomain(IEnumerable<UserDataModel> models) =>
        models.Select(ToDomain);
}

