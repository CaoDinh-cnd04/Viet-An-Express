using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Identity.Domain;

namespace VietAnExpress.Identity.Infrastructure.Configurations;

/// <summary>dbo.PhienThuHoi — bảng mới (người dùng đã đồng ý): phiên đăng nhập đã đăng xuất, còn hạn thì token bị từ chối.</summary>
internal sealed class RevokedSessionConfiguration : IEntityTypeConfiguration<RevokedSession>
{
    public void Configure(EntityTypeBuilder<RevokedSession> b)
    {
        b.ToTable("PhienThuHoi", "dbo");
        b.HasKey(x => x.SessionId);
        b.Property(x => x.SessionId).HasColumnName("Ma_Phien").HasMaxLength(RevokedSession.SessionIdMaxLength).IsUnicode(false);
        b.Property(x => x.ExpiresAt).HasColumnName("Het_Han");
        b.Property(x => x.RevokedAt).HasColumnName("Ngay_Thu_Hoi");
        b.HasIndex(x => x.ExpiresAt).HasDatabaseName("IX_PhienThuHoi_Het_Han");
    }
}
