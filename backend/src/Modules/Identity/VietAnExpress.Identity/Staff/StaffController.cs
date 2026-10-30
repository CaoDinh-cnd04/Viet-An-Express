using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Support;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Staff;

/// <summary>Tài khoản con của nhân viên — chỉ tài khoản chính (admin) của khách tạo, sửa, khóa, đặt lại mật khẩu, xóa.</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/account/staff")]
[Tags("Tài khoản")]
[HasPermission(IdentityPermissions.ManageStaff)]
internal sealed class StaffController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<StaffAccountDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => FromResult(await Sender.Send(new ListStaffQuery(), ct));

    [HttpPost]
    [ProducesResponseType<ApiResponse<StaffAccountDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(CreateStaffRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(new CreateStaffCommand(
            body.UserName, body.Password, body.FullName, body.Email, body.Phone, body.Phone2, body.Email2), ct), "Đã tạo tài khoản nhân viên");

    [HttpPut("{id:long}")]
    [ProducesResponseType<ApiResponse<StaffAccountDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, UpdateStaffRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(new UpdateStaffCommand(
            id, body.FullName, body.Email, body.Phone, body.Active, body.Phone2, body.Email2), ct), "Đã cập nhật tài khoản nhân viên");

    [HttpPost("{id:long}/reset-password")]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword(long id, ResetStaffPasswordRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(new ResetStaffPasswordCommand(id, body.NewPassword), ct),
            "Đã đặt lại mật khẩu. Nhân viên sẽ phải đăng nhập lại");

    [HttpDelete("{id:long}")]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct) =>
        FromResult(await Sender.Send(new DeleteStaffCommand(id), ct), "Đã xóa tài khoản nhân viên");
}

internal sealed record CreateStaffRequest(
    string UserName, string Password, string FullName, string? Email, string? Phone,
    string? Phone2 = null, string? Email2 = null);

internal sealed record UpdateStaffRequest(
    string FullName, string? Email, string? Phone, bool Active = true,
    string? Phone2 = null, string? Email2 = null);

internal sealed record ResetStaffPasswordRequest(string NewPassword);
