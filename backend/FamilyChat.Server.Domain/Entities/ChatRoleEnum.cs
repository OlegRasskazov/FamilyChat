namespace FamilyChat.Server.Domain.Entities
{
    [Flags]
    public enum ChatRoleEnum
    {
        None = 0,
        Member = 1,
        Admin = 2,
        Owner = 4,
        Deleted = 8
    }
}
