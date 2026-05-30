using Microsoft.EntityFrameworkCore;
using __APP_NAME__.Business.Entities;

namespace __APP_NAME__.Infrastructure
{
    // E2E fixture seed overlay. Replaces the generated demo SeedData (see
    // Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs -> GetInfrastructureApplicationDbContextSeedDataData)
    // with the permission/role/category set the e2e specs depend on. This is a partial-class file: it owns ONLY
    // the seed data, never the DbContext constructor or OnModelCreating, so framework plumbing changes can't break it.
    public partial class __APP_NAME__ApplicationDbContext
    {
        private static void SeedData(ModelBuilder modelBuilder)
        {
            Permission[] permissions =
            [
                new Permission { Id = 1, Name = "View users", Code = "ReadUser" },
                new Permission { Id = 2, Name = "Edit existing users", Code = "UpdateUser" },
                new Permission { Id = 3, Name = "Add new users", Code = "InsertUser" },
                new Permission { Id = 4, Name = "Delete users", Code = "DeleteUser" },
                new Permission { Id = 5, Name = "View notifications", Code = "ReadNotification" },
                new Permission { Id = 6, Name = "Edit existing notifications", Code = "UpdateNotification" },
                new Permission { Id = 7, Name = "Add new notifications", Code = "InsertNotification" },
                new Permission { Id = 8, Name = "Delete notifications", Code = "DeleteNotification" },
                new Permission { Id = 9, Name = "View roles", Code = "ReadRole" },
                new Permission { Id = 10, Name = "Edit existing roles", Code = "UpdateRole" },
                new Permission { Id = 11, Name = "Add new roles", Code = "InsertRole" },
                new Permission { Id = 12, Name = "Delete roles", Code = "DeleteRole" },
                new Permission { Id = 13, Name = "View projects", Code = "ReadProject" },
                new Permission { Id = 14, Name = "Edit existing projects", Code = "UpdateProject" },
                new Permission { Id = 15, Name = "Add new projects", Code = "InsertProject" },
                new Permission { Id = 16, Name = "Delete projects", Code = "DeleteProject" },
                new Permission { Id = 17, Name = "View project tasks", Code = "ReadProjectTask" },
                new Permission { Id = 18, Name = "Edit existing project tasks", Code = "UpdateProjectTask" },
                new Permission { Id = 19, Name = "Add new project tasks", Code = "InsertProjectTask" },
                new Permission { Id = 20, Name = "Delete project tasks", Code = "DeleteProjectTask" },
                new Permission { Id = 21, Name = "View task comments", Code = "ReadTaskComment" },
                new Permission { Id = 22, Name = "Edit existing task comments", Code = "UpdateTaskComment" },
                new Permission { Id = 23, Name = "Add new task comments", Code = "InsertTaskComment" },
                new Permission { Id = 24, Name = "Delete task comments", Code = "DeleteTaskComment" },
                new Permission { Id = 25, Name = "View project charters", Code = "ReadProjectCharter" },
                new Permission { Id = 26, Name = "Edit existing project charters", Code = "UpdateProjectCharter" },
                new Permission { Id = 27, Name = "Add new project charters", Code = "InsertProjectCharter" },
                new Permission { Id = 28, Name = "Delete project charters", Code = "DeleteProjectCharter" }
            ];

            modelBuilder.Entity<Permission>().HasData(permissions);

            modelBuilder.Entity<TaskCategory>().HasData(
                new TaskCategory { Id = 1, Name = "Bug", Color = "#e74c3c" },
                new TaskCategory { Id = 2, Name = "Feature", Color = "#2ecc71" },
                new TaskCategory { Id = 3, Name = "Improvement", Color = "#3498db" },
                new TaskCategory { Id = 4, Name = "Research", Color = "#9b59b6" },
                new TaskCategory { Id = 5, Name = "Documentation", Color = "#f39c12" }
            );

            DateTime seedDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<Role>().HasData(new Role
            {
                Id = 1,
                Name = "Admin",
                CreatedAt = seedDate,
                ModifiedAt = seedDate,
            });

            modelBuilder.Entity<Role>()
                .HasMany(r => r.Permissions)
                .WithMany(p => p.Roles)
                .UsingEntity(j => j.HasData(
                    new { RoleId = 1, PermissionId = 1 },
                    new { RoleId = 1, PermissionId = 2 },
                    new { RoleId = 1, PermissionId = 3 },
                    new { RoleId = 1, PermissionId = 4 },
                    new { RoleId = 1, PermissionId = 5 },
                    new { RoleId = 1, PermissionId = 6 },
                    new { RoleId = 1, PermissionId = 7 },
                    new { RoleId = 1, PermissionId = 8 },
                    new { RoleId = 1, PermissionId = 9 },
                    new { RoleId = 1, PermissionId = 10 },
                    new { RoleId = 1, PermissionId = 11 },
                    new { RoleId = 1, PermissionId = 12 },
                    new { RoleId = 1, PermissionId = 13 },
                    new { RoleId = 1, PermissionId = 14 },
                    new { RoleId = 1, PermissionId = 15 },
                    new { RoleId = 1, PermissionId = 16 },
                    new { RoleId = 1, PermissionId = 17 },
                    new { RoleId = 1, PermissionId = 18 },
                    new { RoleId = 1, PermissionId = 19 },
                    new { RoleId = 1, PermissionId = 20 },
                    new { RoleId = 1, PermissionId = 21 },
                    new { RoleId = 1, PermissionId = 22 },
                    new { RoleId = 1, PermissionId = 23 },
                    new { RoleId = 1, PermissionId = 24 },
                    new { RoleId = 1, PermissionId = 25 },
                    new { RoleId = 1, PermissionId = 26 },
                    new { RoleId = 1, PermissionId = 27 },
                    new { RoleId = 1, PermissionId = 28 }
                ));
        }
    }
}
