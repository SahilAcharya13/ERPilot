using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Domain.Entities;

namespace ERP.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.UserID);
        builder.Property(u => u.Name).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(u => u.IsActive).HasDefaultValue(true);
        
        builder.HasIndex(u => u.IsDeleted);
        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.CustomerID);
        builder.Property(c => c.CustomerCode).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.CustomerCode).IsUnique();
        builder.Property(c => c.CompanyName).HasMaxLength(150).IsRequired();
        builder.Property(c => c.ContactPerson).HasMaxLength(100);
        builder.Property(c => c.Phone).HasMaxLength(20);
        builder.Property(c => c.Email).HasMaxLength(256);
        
        builder.HasIndex(c => c.CompanyName);
        builder.HasIndex(c => c.Phone);
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.ProductID);
        builder.Property(p => p.ProductName).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Category).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Unit).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Rate).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.GST).HasPrecision(5, 2).HasDefaultValue(0.00m);
        builder.Property(p => p.Status).HasMaxLength(50).HasDefaultValue("Active");
        
        builder.HasIndex(p => p.ProductName);
        builder.HasIndex(p => p.Category);
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.OrderID);
        builder.Property(o => o.OrderNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.Property(o => o.OrderStatus).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(o => o.TotalAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(o => o.PaidAmount).HasPrecision(18, 2).IsRequired();
        
        // Database-agnostic computed column formulation that works for both SqlServer and Sqlite
        builder.Property(o => o.PendingAmount)
            .HasComputedColumnSql("TotalAmount - PaidAmount", stored: true);
            
        builder.Property(o => o.Remarks).HasMaxLength(500);
        
        builder.HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerID)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasIndex(o => o.CustomerID);
        builder.HasIndex(o => o.OrderDate);
        builder.HasIndex(o => o.OrderStatus);
        builder.HasQueryFilter(o => !o.IsDeleted);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(oi => oi.OrderItemID);
        builder.Property(oi => oi.Description).HasMaxLength(250);
        builder.Property(oi => oi.Quantity).HasPrecision(18, 4).IsRequired();
        builder.Property(oi => oi.Unit).HasMaxLength(20).IsRequired();
        builder.Property(oi => oi.Rate).HasPrecision(18, 2).IsRequired();
        
        builder.Property(oi => oi.Amount)
            .HasComputedColumnSql("Quantity * Rate", stored: true);
            
        builder.HasOne(oi => oi.Order)
            .WithMany(o => o.OrderItems)
            .HasForeignKey(oi => oi.OrderID)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne(oi => oi.Product)
            .WithMany()
            .HasForeignKey(oi => oi.ProductID)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasIndex(oi => oi.OrderID);
        builder.HasIndex(oi => oi.ProductID);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(p => p.PaymentID);
        builder.Property(p => p.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.PaymentMode).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(p => p.ReferenceNumber).HasMaxLength(100);
        builder.Property(p => p.Remarks).HasMaxLength(500);
        
        builder.HasOne(p => p.Customer)
            .WithMany()
            .HasForeignKey(p => p.CustomerID)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasOne(p => p.Order)
            .WithMany()
            .HasForeignKey(p => p.OrderID)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
            
        builder.HasIndex(p => p.CustomerID);
        builder.HasIndex(p => p.OrderID);
        builder.HasIndex(p => p.PaymentDate);
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}

public class AiActionLogConfiguration : IEntityTypeConfiguration<AiActionLog>
{
    public void Configure(EntityTypeBuilder<AiActionLog> builder)
    {
        builder.HasKey(l => l.LogID);
        builder.Property(l => l.OriginalPrompt).IsRequired();
        builder.Property(l => l.ExtractedIntent).HasMaxLength(100);
        builder.Property(l => l.ApprovalStatus).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(l => l.ExecutionStatus).HasConversion<string>().HasMaxLength(50).IsRequired();
        
        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserID)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.ApprovedByUser)
            .WithMany()
            .HasForeignKey(l => l.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
            
        builder.HasIndex(l => l.UserID);
        builder.HasIndex(l => l.ApprovedByUserId);
        builder.HasIndex(l => l.Timestamp);
    }
}
