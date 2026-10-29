using MediatR;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Notifications;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Staff;

internal sealed record DeleteStaffCommand(long Id) : IRequest<Result>;

internal sealed class DeleteStaffHandler(IdentityDbContext db, ICurrentUser user) : IRequestHandler<DeleteStaffCommand, Result>
{
    public async Task<Result> Handle(DeleteStaffCommand cmd, CancellationToken ct)
    {
        var found = await db.FindStaffOfMainAccountAsync(user, cmd.Id, ct);
        if (found.IsFailure) return found.Error;
        // Giữ dòng người liên hệ (hệ thống cũ còn dùng), chỉ gỡ đăng nhập — phiên đang mở hết hạn khi làm mới.
        found.Value.RemoveLogin();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
