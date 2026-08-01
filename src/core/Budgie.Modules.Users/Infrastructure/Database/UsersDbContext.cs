using Budgie.Modules.Users.Domain;
using Budgie.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Users.Infrastructure.Database;

public class UsersDbContext(DbContextOptions<UsersDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Email).HasConversion(ValueConverters.EmailAddressConverter).IsRequired().HasMaxLength(255);
            builder.HasIndex(u => u.Email).IsUnique();
            builder.Property(u => u.PasswordHash).IsRequired();
            builder.Property(u => u.Name).HasConversion(ValueConverters.NameConverter).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<UserSettings>(builder =>
        {
            builder.HasKey(s => s.UserId);
            builder.Property(s => s.MainCurrency).HasConversion(ValueConverters.CurrencyCodeConverter).IsRequired().HasMaxLength(3);
            builder.Property(s => s.DefaultCreditConsumptionMode).IsRequired();
            builder.Property(s => s.Language).IsRequired().HasMaxLength(10);
            builder.Property(s => s.IsConfigured).IsRequired();
            builder.Property(s => s.SupportedCurrencies)
                .HasColumnType("jsonb")
                .IsRequired();
        });

        modelBuilder.Entity<RefreshToken>(builder =>
        {
            builder.HasKey(r => r.Id);
            builder.Property(r => r.TokenHash).IsRequired().HasMaxLength(88);
            builder.Property(r => r.ExpiresAt).HasColumnType("timestamp without time zone").IsRequired();
            builder.Property(r => r.CreatedAt).HasColumnType("timestamp without time zone").IsRequired();
            builder.HasIndex(r => r.TokenHash);
        });
    }
}
