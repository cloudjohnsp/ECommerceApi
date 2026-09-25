namespace ECommerce.Domain.Enums;

public enum UserAuditAction
{
    Created = 1,
    ProfileUpdated = 2,
    PasswordChanged = 3,
    RoleChanged = 4,
    EmailConfirmed = 5,
    Deactivated = 6
}
