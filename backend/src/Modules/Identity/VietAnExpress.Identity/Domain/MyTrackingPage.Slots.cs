namespace VietAnExpress.Identity.Domain;

/// <summary>
/// 15 ô cổng liên lạc (CongTinNhan_01…15) và 15 ô mạng xã hội (CongTruyenThong_01…15) có sẵn của bảng cũ —
/// ứng dụng nào ở ô nào xem <see cref="MyTrackingChannels"/>.
/// </summary>
internal sealed partial class MyTrackingPage
{
    public string? CongTinNhan01 { get; private set; }
    public string? CongTinNhan02 { get; private set; }
    public string? CongTinNhan03 { get; private set; }
    public string? CongTinNhan04 { get; private set; }
    public string? CongTinNhan05 { get; private set; }
    public string? CongTinNhan06 { get; private set; }
    public string? CongTinNhan07 { get; private set; }
    public string? CongTinNhan08 { get; private set; }
    public string? CongTinNhan09 { get; private set; }
    public string? CongTinNhan10 { get; private set; }
    public string? CongTinNhan11 { get; private set; }
    public string? CongTinNhan12 { get; private set; }
    public string? CongTinNhan13 { get; private set; }
    public string? CongTinNhan14 { get; private set; }
    public string? CongTinNhan15 { get; private set; }
    public string? CongTruyenThong01 { get; private set; }
    public string? CongTruyenThong02 { get; private set; }
    public string? CongTruyenThong03 { get; private set; }
    public string? CongTruyenThong04 { get; private set; }
    public string? CongTruyenThong05 { get; private set; }
    public string? CongTruyenThong06 { get; private set; }
    public string? CongTruyenThong07 { get; private set; }
    public string? CongTruyenThong08 { get; private set; }
    public string? CongTruyenThong09 { get; private set; }
    public string? CongTruyenThong10 { get; private set; }
    public string? CongTruyenThong11 { get; private set; }
    public string? CongTruyenThong12 { get; private set; }
    public string? CongTruyenThong13 { get; private set; }
    public string? CongTruyenThong14 { get; private set; }
    public string? CongTruyenThong15 { get; private set; }

    /// <param name="social">true = CongTruyenThong (mạng xã hội), false = CongTinNhan (cổng liên lạc).</param>
    /// <param name="slot">Số ô 1…15.</param>
    public string? Slot(bool social, int slot) => social
        ? slot switch
        {
            1 => CongTruyenThong01,
            2 => CongTruyenThong02,
            3 => CongTruyenThong03,
            4 => CongTruyenThong04,
            5 => CongTruyenThong05,
            6 => CongTruyenThong06,
            7 => CongTruyenThong07,
            8 => CongTruyenThong08,
            9 => CongTruyenThong09,
            10 => CongTruyenThong10,
            11 => CongTruyenThong11,
            12 => CongTruyenThong12,
            13 => CongTruyenThong13,
            14 => CongTruyenThong14,
            15 => CongTruyenThong15,
            _ => null
        }
        : slot switch
        {
            1 => CongTinNhan01,
            2 => CongTinNhan02,
            3 => CongTinNhan03,
            4 => CongTinNhan04,
            5 => CongTinNhan05,
            6 => CongTinNhan06,
            7 => CongTinNhan07,
            8 => CongTinNhan08,
            9 => CongTinNhan09,
            10 => CongTinNhan10,
            11 => CongTinNhan11,
            12 => CongTinNhan12,
            13 => CongTinNhan13,
            14 => CongTinNhan14,
            15 => CongTinNhan15,
            _ => null
        };

    private void SetSlot(bool social, int slot, string? value)
    {
        if (social)
            switch (slot)
            {
            case 1: CongTruyenThong01 = value; break;
            case 2: CongTruyenThong02 = value; break;
            case 3: CongTruyenThong03 = value; break;
            case 4: CongTruyenThong04 = value; break;
            case 5: CongTruyenThong05 = value; break;
            case 6: CongTruyenThong06 = value; break;
            case 7: CongTruyenThong07 = value; break;
            case 8: CongTruyenThong08 = value; break;
            case 9: CongTruyenThong09 = value; break;
            case 10: CongTruyenThong10 = value; break;
            case 11: CongTruyenThong11 = value; break;
            case 12: CongTruyenThong12 = value; break;
            case 13: CongTruyenThong13 = value; break;
            case 14: CongTruyenThong14 = value; break;
            case 15: CongTruyenThong15 = value; break;
            }
        else
            switch (slot)
            {
            case 1: CongTinNhan01 = value; break;
            case 2: CongTinNhan02 = value; break;
            case 3: CongTinNhan03 = value; break;
            case 4: CongTinNhan04 = value; break;
            case 5: CongTinNhan05 = value; break;
            case 6: CongTinNhan06 = value; break;
            case 7: CongTinNhan07 = value; break;
            case 8: CongTinNhan08 = value; break;
            case 9: CongTinNhan09 = value; break;
            case 10: CongTinNhan10 = value; break;
            case 11: CongTinNhan11 = value; break;
            case 12: CongTinNhan12 = value; break;
            case 13: CongTinNhan13 = value; break;
            case 14: CongTinNhan14 = value; break;
            case 15: CongTinNhan15 = value; break;
            }
    }
}
