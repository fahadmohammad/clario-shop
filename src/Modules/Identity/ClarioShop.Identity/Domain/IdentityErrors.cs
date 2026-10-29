namespace ClarioShop.Identity.Domain;

internal static class IdentityErrors
{
    public static readonly Error UserNotFound = Error.NotFound("identity.user_not_found", "No staff account has this id.");

    public static readonly Error LastOwner = Error.Conflict(
        "identity.last_owner",
        "This is the shop's only active Owner; make someone else an Owner before removing the role.");

    public static Error UnknownRoles(string field) => Error.Validation(field, "One or more roles do not exist.");
}
