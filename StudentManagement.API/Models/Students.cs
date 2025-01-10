using System.Text.Json.Serialization;

namespace StudentManagement.API.Models
{
    public class Students
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }

        public string PasswordSalt { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ResetTokenExpiry {  get; set; }
        public string? ResetToken {  get; set; }
    }
}
