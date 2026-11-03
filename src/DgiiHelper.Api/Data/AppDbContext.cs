using Microsoft.EntityFrameworkCore;

namespace DgiiHelper.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<NcfRange> NcfRanges => Set<NcfRange>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<NcfRange>(e =>
        {
            e.Property(r => r.Kind).HasConversion<int>();
            e.Property(r => r.TypeCode).HasMaxLength(2).IsFixedLength().IsUnicode(false);
            // Última barrera: ni un bug en el código puede dejar Next fuera de [First, Last + 1].
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_NcfRanges_Bounds", "[First] >= 1 AND [Last] >= [First]");
                t.HasCheckConstraint("CK_NcfRanges_Next", "[Next] >= [First] AND [Next] <= [Last] + 1");
            });
        });
    }
}
