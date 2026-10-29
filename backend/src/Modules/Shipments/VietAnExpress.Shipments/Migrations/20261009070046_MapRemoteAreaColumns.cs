using Microsoft.EntityFrameworkCore.Migrations;
using VietAnExpress.Shipments.Shared;

#nullable disable

namespace VietAnExpress.Shipments.Migrations
{
    /// <inheritdoc />
    internal partial class MapRemoteAreaColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chỉ cập nhật snapshot: Remote_Area, Remote_Area_FedEx, Remote_Area_UPS đã có sẵn trong dbo.MaVanDon (bảng hệ thống cũ).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
