using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VietAnExpress.Identity.Domain;

// Only normalized routes and pseudonymous identifiers; no query strings or form contents.
internal sealed class UsageEvent
{
    public Guid Id { get; set; }
    public string Visitor { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Path { get; set; } = "";
    public string Kind { get; set; } = "";
    public string? Mode { get; set; }
    public Guid? FlowId { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class UsageEventConfiguration : IEntityTypeConfiguration<UsageEvent>
{
    public void Configure(EntityTypeBuilder<UsageEvent> b)
    {
        b.ToTable("ThongKeSuDung", "dbo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Visitor).HasMaxLength(100);
        b.Property(x => x.Audience).HasMaxLength(12);
        b.Property(x => x.Path).HasMaxLength(80);
        b.Property(x => x.Kind).HasMaxLength(24);
        b.Property(x => x.Mode).HasMaxLength(8);
        b.HasIndex(x => new { x.CreatedAt, x.Audience, x.Kind });
        b.HasIndex(x => new { x.FlowId, x.Mode, x.Kind });
    }
}
