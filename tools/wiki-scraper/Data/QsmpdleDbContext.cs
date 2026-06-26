using Microsoft.EntityFrameworkCore;
using WikiScraper.Models;

namespace WikiScraper.Data;

public sealed class QsmpdleDbContext(DbContextOptions<QsmpdleDbContext> options) : DbContext(options)
{
    public DbSet<Member> Members => Set<Member>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Member>(builder =>
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired();

            builder.HasIndex(x => x.Name)
                .IsUnique();

            builder.Property(x => x.AliasesJson)
                .IsRequired();

            builder.Property(x => x.PronounsJson)
                .IsRequired();

            builder.Property(x => x.AffiliationsJson)
                .IsRequired();

            builder.Property(x => x.SpeciesJson)
                .IsRequired();

            builder.Property(x => x.CharacterIconUrl)
                .IsRequired();

            builder.Property(x => x.MemberPageUrl)
                .IsRequired();
        });
    }
}
