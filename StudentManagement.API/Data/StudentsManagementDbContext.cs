using Microsoft.EntityFrameworkCore;
using StudentManagement.API.Models;

namespace StudentManagement.API.Data
{
    public class StudentsManagementDbContext: DbContext
    {
        public StudentsManagementDbContext(DbContextOptions dbContextOptions): base(dbContextOptions)
        {
            
        }

        public DbSet<Students> Student { get; set; }
    }
}
