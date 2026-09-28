using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using ECommerce.Domain.Entities;
using ECommerce.Application.Abstractions.Observability;

namespace ECommerce.Persistence.Contexts
{
    public sealed class AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICorrelationContext? correlationContext = null) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            base.OnModelCreating(modelBuilder);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            AssignCorrelationIds();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            AssignCorrelationIds();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void AssignCorrelationIds()
        {
            if (string.IsNullOrWhiteSpace(correlationContext?.CorrelationId)) return;

            foreach (var entry in ChangeTracker.Entries<OutboxMessage>()
                         .Where(entry => entry.State == EntityState.Added))
                entry.Entity.AssignCorrelationId(correlationContext.CorrelationId);
        }


        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<UserActionToken> UserActionTokens => Set<UserActionToken>();
        public DbSet<UserAuditEntry> UserAuditEntries => Set<UserAuditEntry>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductImage> ProductImages => Set<ProductImage>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Inventory> Inventories => Set<Inventory>();
        public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public DbSet<Payment> Payments => Set<Payment>();
    }
}
