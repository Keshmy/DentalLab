namespace DentalLab.ViewModels.Identity
{
    public class RoleVM
    {
        public string Id { get; set; } = string.Empty;
        public required string Name { get; set; }
        public int UsersCount { get; set; }
    }
}
