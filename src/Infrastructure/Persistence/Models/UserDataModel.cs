namespace Infrastructure.Persistence.Models;

/// <summary>
/// User type enum - duplicated here to avoid domain dependency.
/// </summary>
public enum UserTypeData
{
    Regular = 0,
    Admin = 1
}

/// <summary>
/// MongoDB data model for User.
/// Contains domain data plus infrastructure metadata.
/// </summary>
public class UserDataModel : BaseDataModel
{
    public string ApiKeyHash { get; set; } = string.Empty;
    public string ApiKeyPrefix { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserTypeData UserType { get; set; } = UserTypeData.Regular;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

