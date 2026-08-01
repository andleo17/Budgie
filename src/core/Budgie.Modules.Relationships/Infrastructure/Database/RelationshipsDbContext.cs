using Budgie.Modules.Relationships.Domain.Persons;
using Budgie.Modules.Relationships.Domain.Tags;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Relationships.Infrastructure.Database;

public class RelationshipsDbContext(
    DbContextOptions<RelationshipsDbContext> options,
    ICurrentUserProvider currentUserProvider) : DbContext(options)
{
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>(builder =>
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.UserId).IsRequired();
            builder.Property(p => p.Name).HasConversion(ValueConverters.NameConverter).IsRequired().HasMaxLength(100);
            builder.Property(p => p.PhotoPath).HasMaxLength(500);
            builder.HasQueryFilter(p => p.UserId == currentUserProvider.UserId);
        });

        modelBuilder.Entity<Tag>(tag =>
        {
            tag.HasKey(t => t.Id);
            tag.Property(t => t.Name).HasConversion(ValueConverters.NameConverter).IsRequired().HasMaxLength(50);
            tag.Property(t => t.Color).HasConversion(ValueConverters.NullableHexColorConverter).HasMaxLength(7);
            tag.HasQueryFilter(t => t.UserId == currentUserProvider.UserId);

            tag.HasMany(t => t.Persons)
                .WithMany(p => p.Tags)
                .UsingEntity("PersonTags",
                    l => l.HasOne(typeof(Person)).WithMany().HasForeignKey("PersonId"),
                    r => r.HasOne(typeof(Tag)).WithMany().HasForeignKey("TagId"));
        });
    }
}
