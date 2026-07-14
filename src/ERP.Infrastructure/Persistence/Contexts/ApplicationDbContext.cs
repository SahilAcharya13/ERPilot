using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ERP.Domain.Common;
using ERP.Domain.Entities;
using ERP.Domain.Enums;

namespace ERP.Infrastructure.Persistence.Contexts;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AiActionLog> AiActionLogs => Set<AiActionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply configurations
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Seed Data
        SeedData(modelBuilder);
    }

    public override int SaveChanges()
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditAndSoftDelete()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is BaseEntity baseEntity)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        baseEntity.CreatedDate = now;
                        baseEntity.ModifiedDate = now;
                        break;

                    case EntityState.Modified:
                        baseEntity.ModifiedDate = now;
                        break;

                    case EntityState.Deleted:
                        if (entry.Entity is SoftDeleteEntity softDeleteEntity)
                        {
                            // Intercept physical delete and transform to update
                            entry.State = EntityState.Modified;
                            softDeleteEntity.IsDeleted = true;
                            softDeleteEntity.DeletedAt = now;
                            baseEntity.ModifiedDate = now;
                        }
                        break;
                }
            }
        }
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // 1. Seed Users (Roles: Admin, Sales, Accounts, Manager)
        modelBuilder.Entity<User>().HasData(
            new User
            {
                UserID = 1,
                Name = "System Admin",
                Email = "admin@erp.com",
                PasswordHash = "$2b$12$Hs4kRUkTrScs6j1Jfe3d.OP56.pLuzi1y43urhLqi74DVy40otKQ2",
                Role = UserRole.Admin,
                IsActive = true,
                CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            },
            new User
            {
                UserID = 2,
                Name = "Sales Manager",
                Email = "sales@erp.com",
                PasswordHash = "$2b$12$98PCQ1/QtR4RtogsOXooqO4q96jcauGMIda7K8R5fZ/Oh7FgZSQRm",
                Role = UserRole.Sales,
                IsActive = true,
                CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            },
            new User
            {
                UserID = 3,
                Name = "Finance Officer",
                Email = "accounts@erp.com",
                PasswordHash = "$2b$12$xF03tsGBpgBEXVd9SKFigeOfHBcoSuxJHINWZ2Opj8IJvql4SXSrG",
                Role = UserRole.Accounts,
                IsActive = true,
                CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            },
            new User
            {
                UserID = 4,
                Name = "General Manager",
                Email = "manager@erp.com",
                PasswordHash = "$2b$12$Wq6necim2kgRnkB1pPj5L.IJv86YxOOjIWU3Rgi1t8W7xaGLlTiNu",
                Role = UserRole.Manager,
                IsActive = true,
                CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            }
        );

        // 2. Seed Customers
        modelBuilder.Entity<Customer>().HasData(
            new Customer
            {
                CustomerID = 12,
                CustomerCode = "XYZ001",
                CompanyName = "XYZ Traders",
                ContactPerson = "Rajesh Kumar",
                Phone = "+919876543210",
                Email = "rajesh@xyztraders.com",
                CreatedDate = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            },
            new Customer
            {
                CustomerID = 13,
                CustomerCode = "ABC001",
                CompanyName = "ABC Ltd",
                ContactPerson = "John Doe",
                Phone = "+919876543211",
                Email = "john@abcltd.com",
                CreatedDate = new DateTime(2026, 1, 11, 10, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 1, 11, 10, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            }
        );

        // 3. Seed Products
        modelBuilder.Entity<Product>().HasData(
            new Product
            {
                ProductID = 1,
                ProductName = "Steel Bar",
                Category = "Metal",
                Unit = "pcs",
                Rate = 1200.00m,
                GST = 18.00m,
                Status = "Active",
                CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            },
            new Product
            {
                ProductID = 2,
                ProductName = "Cement",
                Category = "Building Materials",
                Unit = "bag",
                Rate = 450.00m,
                GST = 28.00m,
                Status = "Active",
                CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            },
            new Product
            {
                ProductID = 3,
                ProductName = "Brick",
                Category = "Building Materials",
                Unit = "pcs",
                Rate = 10.00m,
                GST = 5.00m,
                Status = "Active",
                CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            },
            new Product
            {
                ProductID = 4,
                ProductName = "Gravel",
                Category = "Building Materials",
                Unit = "ton",
                Rate = 800.00m,
                GST = 12.00m,
                Status = "Active",
                CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            },
            new Product
            {
                ProductID = 5,
                ProductName = "Sand",
                Category = "Building Materials",
                Unit = "ton",
                Rate = 600.00m,
                GST = 12.00m,
                Status = "Active",
                CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            }
        );
    }
}
