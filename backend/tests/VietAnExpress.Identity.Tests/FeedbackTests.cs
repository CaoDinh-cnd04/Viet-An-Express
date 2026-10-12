using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Infrastructure;
using Xunit;

namespace VietAnExpress.Identity.Tests;

public class FeedbackTests
{
    [Fact]
    public void Migration_an_gop_y_khop_voi_model_SQL_Server()
    {
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=localhost;Database=feedback_model_check;Integrated Security=True;TrustServerCertificate=True").Options);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Khach_xoa_chi_an_khoi_danh_sach_admin_van_xem_noi_dung_va_anh()
    {
        await using var f = new IdentityFixture();
        var ct = TestContext.Current.CancellationToken;
        f.CurrentUser.Setup(u => u.HasPermission(IdentityPermissions.AdminFeedback)).Returns(false);
        var customer = new CustomerFeedbackHandlers(f.Db, f.CurrentUser.Object, f.Customers.Object, f.Clock, f.Jobs, f.Company);
        var admin = new AdminFeedbackHandlers(f.Db, f.Tokens, f.Customers.Object, f.Clock);
        var sent = await customer.Handle(new SendFeedbackCommand("Góp ý cần giữ", "0909", 4, [Image(Png)]), ct);
        var id = long.Parse(sent.Value.Id);
        var imageId = long.Parse(Assert.Single(sent.Value.Images).Id);

        Assert.True((await customer.Handle(new DeleteFeedbackCommand(id), ct)).IsSuccess);
        f.Db.ChangeTracker.Clear();
        Assert.Empty((await customer.Handle(new GetMyFeedbackQuery(), ct)).Value);
        Assert.Equal("Góp ý cần giữ", Assert.Single(await admin.Handle(new GetAdminFeedbackQuery(), ct)).Message);
        var detail = (await admin.Handle(new GetAdminFeedbackDetailQuery(id), ct)).Value;
        Assert.Single(detail.Feedback.Images);
        Assert.True((await f.Db.Feedbacks.SingleAsync(ct)).IsHiddenByCustomer);
        Assert.Equal(Png, (await f.Db.FeedbackImages.SingleAsync(ct)).Data);
        Assert.True((await customer.Handle(new GetFeedbackImageQuery(imageId), ct)).IsFailure);
        f.CurrentUser.Setup(u => u.HasPermission(IdentityPermissions.AdminFeedback)).Returns(true);
        Assert.Equal(Png, (await customer.Handle(new GetFeedbackImageQuery(imageId), ct)).Value.Data);
    }

    [Fact]
    public async Task Admin_xoa_han_gop_y_va_anh_dinh_kem()
    {
        await using var f = new IdentityFixture();
        var ct = TestContext.Current.CancellationToken;
        var customer = new CustomerFeedbackHandlers(f.Db, f.CurrentUser.Object, f.Customers.Object, f.Clock, f.Jobs, f.Company);
        var admin = new AdminFeedbackHandlers(f.Db, f.Tokens, f.Customers.Object, f.Clock);
        var keep = long.Parse((await customer.Handle(new SendFeedbackCommand("Giữ lại", null, 5, []), ct)).Value.Id);
        var id = long.Parse((await customer.Handle(new SendFeedbackCommand("Xóa đi", null, 1, [Image(Png), Image(Png)]), ct)).Value.Id);
        f.Db.ChangeTracker.Clear();

        Assert.True((await admin.Handle(new AdminDeleteFeedbackCommand(id), ct)).IsSuccess);

        f.Db.ChangeTracker.Clear();
        Assert.Equal(keep, (await f.Db.Feedbacks.SingleAsync(ct)).Id);
        Assert.Empty(await f.Db.FeedbackImages.ToListAsync(ct));
        Assert.Equal(FeedbackErrors.NotFound, (await admin.Handle(new AdminDeleteFeedbackCommand(id), ct)).Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Khong_duoc_an_gop_y_cua_khach_khac_hoac_nhan_vien_khac(bool otherCustomer)
    {
        await using var f = new IdentityFixture();
        var ct = TestContext.Current.CancellationToken;
        f.SignInAs(10);
        var feedback = Feedback.Create(otherCustomer ? 99 : IdentityFixture.CustomerId,
            "KH", "ABC", otherCustomer ? 10 : 11, "sender", "Giữ lại", null, null, [], new DateTime(2026, 10, 9));
        f.Db.Feedbacks.Add(feedback);
        await f.Db.SaveChangesAsync(ct);
        var handler = new CustomerFeedbackHandlers(f.Db, f.CurrentUser.Object, f.Customers.Object, f.Clock, f.Jobs, f.Company);
        var result = await handler.Handle(new DeleteFeedbackCommand(feedback.Id), ct);
        Assert.Equal(FeedbackErrors.NotFound, result.Error);
        Assert.False(feedback.IsHiddenByCustomer);
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 1];
    private static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0];

    private static FeedbackUpload Image(byte[] data, string type = "image/png") => new("a.png", type, data);

    [Fact]
    public void Gop_y_hop_le()
    {
        Assert.Null(FeedbackRules.Validate("Giao hàng chậm", null, null, []));
        Assert.Null(FeedbackRules.Validate("Ảnh lỗi", "0909", 4, [Image(Png), Image(Jpg, "image/jpeg")]));
    }

    [Fact]
    public void Tu_choi_noi_dung_trong_qua_dai_va_qua_nhieu_anh()
    {
        Assert.Equal(FeedbackErrors.Empty, FeedbackRules.Validate("   ", null, null, []));
        Assert.Equal(FeedbackErrors.TooLong, FeedbackRules.Validate(new string('a', Feedback.MessageMaxLength + 1), null, null, []));
        Assert.Equal(FeedbackErrors.TooManyImages, FeedbackRules.Validate("x", null, null, Enumerable.Repeat(Image(Png), 6).ToList()));
    }

    [Fact]
    public void Khao_sat_cat_khoang_trang_bo_cau_trong_va_phuong_an_trung()
    {
        var survey = FeedbackRules.ParseSurvey("""
            [
              { "id": "feature_most", "question": " Tính năng dùng nhiều nhất? ", "answers": [" Tạo đơn ", "Tạo đơn", ""] },
              { "id": "mobile", "question": "Dùng trên điện thoại?", "answers": [], "note": "  " },
              { "id": "create_flow", "question": "Tạo đơn có mượt không?", "answers": [], "note": " Thiếu nút lưu nháp " }
            ]
            """);

        Assert.True(survey.IsSuccess);
        Assert.Equal(2, survey.Value.Count);
        Assert.Equal(("feature_most", "Tính năng dùng nhiều nhất?", (string?)null), (survey.Value[0].Id, survey.Value[0].Question, survey.Value[0].Note));
        Assert.Equal(["Tạo đơn"], survey.Value[0].Answers);
        Assert.Equal("create_flow", survey.Value[1].Id);
        Assert.Equal("Thiếu nút lưu nháp", survey.Value[1].Note);
        Assert.Empty(FeedbackRules.ParseSurvey(null).Value);
        Assert.Empty(FeedbackRules.ParseSurvey("[]").Value);
    }

    [Theory]
    [InlineData("khong phai json")]
    [InlineData("{}")]
    [InlineData("""[{ "id": "", "question": "Q", "answers": ["A"] }]""")]
    [InlineData("""[{ "id": "a", "question": "", "answers": ["A"] }]""")]
    [InlineData("""[{ "id": "a", "question": "Q", "answers": ["A"] }, { "id": "a", "question": "Q", "answers": ["B"] }]""")]
    public void Tu_choi_khao_sat_sai_cau_truc_hoac_trung_ma_cau_hoi(string json) =>
        Assert.Equal(FeedbackErrors.Survey, FeedbackRules.ParseSurvey(json).Error);

    [Fact]
    public void Tu_choi_khao_sat_qua_gioi_han()
    {
        var tooManyAnswers = $$"""[{ "id": "a", "question": "Q", "answers": [{{string.Join(",", Enumerable.Range(0, Feedback.MaxSurveyAnswers + 1).Select(i => $"\"{i}\""))}}] }]""";
        var longNote = $$"""[{ "id": "a", "question": "Q", "answers": [], "note": "{{new string('x', Feedback.SurveyNoteMaxLength + 1)}}" }]""";
        Assert.Equal(FeedbackErrors.Survey, FeedbackRules.ParseSurvey(tooManyAnswers).Error);
        Assert.Equal(FeedbackErrors.Survey, FeedbackRules.ParseSurvey(longNote).Error);
    }

    [Fact]
    public void Tra_loi_khao_sat_thi_khong_bat_buoc_noi_dung()
    {
        Assert.Null(FeedbackRules.Validate("", null, null, [], hasSurvey: true));
        Assert.Equal(FeedbackErrors.Empty, FeedbackRules.Validate("", null, null, [], hasSurvey: false));
    }

    [Fact]
    public async Task Gui_khao_sat_luu_va_admin_doc_lai_duoc()
    {
        await using var f = new IdentityFixture();
        var ct = TestContext.Current.CancellationToken;
        var customer = new CustomerFeedbackHandlers(f.Db, f.CurrentUser.Object, f.Customers.Object, f.Clock, f.Jobs, f.Company);
        var admin = new AdminFeedbackHandlers(f.Db, f.Tokens, f.Customers.Object, f.Clock);
        const string survey = """[{ "id": "first_use", "question": "Người mới có hiểu ngay không?", "answers": ["Cần hướng dẫn một chút"], "note": null }]""";

        var sent = await customer.Handle(new SendFeedbackCommand(null, null, 4, [], survey), ct);

        Assert.True(sent.IsSuccess, sent.Error.Message);
        Assert.Equal("", sent.Value.Message);
        await f.Jobs.RunAllAsync(new ServiceCollection().AddSingleton<IEmailSender>(f.Email).BuildServiceProvider());
        var mail = Assert.Single(f.Email.Sent);
        Assert.Equal("[Góp ý portal] SGB EXPRESS HN (SaigonbayHN) — 4★", mail.Subject);
        Assert.Contains("Người mới có hiểu ngay không?: Cần hướng dẫn một chút", mail.Body);
        Assert.Contains("https://portal.test/admin", mail.HtmlBody);
        f.Db.ChangeTracker.Clear();
        var item = Assert.Single(Assert.Single(await admin.Handle(new GetAdminFeedbackQuery(), ct)).Survey);
        Assert.Equal(("first_use", "Người mới có hiểu ngay không?"), (item.Id, item.Question));
        Assert.Equal(["Cần hướng dẫn một chút"], item.Answers);
        Assert.Equal(FeedbackErrors.Empty, (await customer.Handle(new SendFeedbackCommand(" ", null, null, [], "[]"), ct)).Error);
    }

    [Fact]
    public void Chi_nhan_anh_that_khong_tin_loai_file_khai()
    {
        Assert.Equal(FeedbackErrors.ImageType, FeedbackRules.Validate("x", null, null, [Image("<script>alert(1)</script>"u8.ToArray())]));
        Assert.Equal(FeedbackErrors.ImageType, FeedbackRules.Validate("x", null, null, [Image(Png, "application/pdf")]));
        Assert.Equal(FeedbackErrors.ImageTooLarge,
            FeedbackRules.Validate("x", null, null, [Image([.. Png, .. new byte[Feedback.ImageMaxBytes]])]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void So_sao_ngoai_1_den_5_bi_tu_choi(int rating) => Assert.Equal(FeedbackErrors.Rating, FeedbackRules.Validate("x", null, rating, []));

    [Fact]
    public void So_sao_khong_bat_buoc() => Assert.Null(FeedbackRules.Validate("x", null, null, []));

    [Theory]
    [InlineData(@"C:\Users\x\..\anh.png", "anh.png")]
    [InlineData("/home/user/../anh.png", "anh.png")]
    [InlineData(@"C:\fakepath/anh.png", "anh.png")]
    [InlineData(@"\\server\share\anh.png", "anh.png")]
    [InlineData(" anh.png ", "anh.png")]
    [InlineData(null, "anh")]
    [InlineData("", "anh")]
    [InlineData("   ", "anh")]
    [InlineData(@"C:\fakepath\", "anh")]
    [InlineData("/home/user/", "anh")]
    public void Ten_file_bo_duong_dan(string? name, string expected) =>
        Assert.Equal(expected, FeedbackRules.SafeFileName(name));

    [Fact]
    public void Admin_sai_ten_hoac_mat_khau_bi_tu_choi()
    {
        Assert.False(VietAnAdminAccount.Verify(VietAnAdminAccount.UserName, "sai-mat-khau"));
        Assert.False(VietAnAdminAccount.Verify("khach", "bat-ky"));
        Assert.False(VietAnAdminAccount.Verify(VietAnAdminAccount.UserName, ""));
    }

    [Fact]
    public void Danh_dau_da_xem_mot_lan()
    {
        var f = Feedback.Create(1, "KH1", "ABC", null, "abc", "x", null, 5, [], new DateTime(2026, 10, 9));
        Assert.True(f.MarkSeen(new DateTime(2026, 10, 10)));
        Assert.False(f.MarkSeen(new DateTime(2026, 10, 11)));
        Assert.Equal(new DateTime(2026, 10, 10), f.SeenAt);
    }
}
