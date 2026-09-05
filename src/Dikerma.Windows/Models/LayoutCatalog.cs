namespace Dikerma.Windows.Models;

public static class LayoutCatalog
{
    public const double CardWidthMm = 85.0;
    public const double CardHeightMm = 115.0;
    public const double SafeMarginMm = 3.0;

    private const string Green = "#00522D";
    private const string Black = "#000000";
    private const string White = "#FFFFFF";

    public static readonly IReadOnlyList<LayoutElementDefinition> Elements = new List<LayoutElementDefinition>
    {
        new("front_logo_1", IdLayoutSide.Front, "Logo 1", IdLayoutKind.Image, 5, 4, 14, 14),
        new("front_logo_2", IdLayoutSide.Front, "Logo 2", IdLayoutKind.Image, 67, 4, 12, 12),
        new("front_barangay", IdLayoutSide.Front, "Barangay title", IdLayoutKind.Text, 20, 4.5, 45, 6, 12.5, IdTextAlignment.Center, White, true, "BARANGAY SIBULAN"),
        new("front_municipality", IdLayoutSide.Front, "Municipality / Sta. Cruz", IdLayoutKind.Text, 20, 10.8, 45, 4.3, 7.2, IdTextAlignment.Center, White, false, "STA. CRUZ"),
        new("front_province", IdLayoutSide.Front, "Province / Davao del Sur", IdLayoutKind.Text, 20, 15, 45, 4.3, 7, IdTextAlignment.Center, White, false, "DAVAO DEL SUR"),
        new("front_id_title", IdLayoutSide.Front, "ID title", IdLayoutKind.Text, 20, 19.3, 45, 5, 8.6, IdTextAlignment.Center, Green, true, "BARANGAY EMPLOYEE ID"),
        new("front_photo", IdLayoutSide.Front, "Employee photo", IdLayoutKind.Image, 6, 27, 31, 40),
        new("front_name_label", IdLayoutSide.Front, "NAME label", IdLayoutKind.Text, 41, 28, 38, 4, 6.8, IdTextAlignment.Left, Green, true, "NAME"),
        new("front_name_value", IdLayoutSide.Front, "Employee name", IdLayoutKind.Text, 41, 33.2, 38, 12, 10.5, IdTextAlignment.Left, Black, true, "ROWENA A. TABO"),
        new("front_designation_label", IdLayoutSide.Front, "DESIGNATION label", IdLayoutKind.Text, 41, 47, 38, 4, 6.8, IdTextAlignment.Left, Green, true, "DESIGNATION"),
        new("front_designation_value", IdLayoutSide.Front, "Designation", IdLayoutKind.Text, 41, 52.2, 38, 12, 9.4, IdTextAlignment.Left, Black, true, "PUNONG BARANGAY"),
        new("front_employee_no_label", IdLayoutSide.Front, "EMPLOYEE NO. label", IdLayoutKind.Text, 41, 65, 38, 4, 6.8, IdTextAlignment.Left, Green, true, "EMPLOYEE NO."),
        new("front_employee_no_value", IdLayoutSide.Front, "Employee number", IdLayoutKind.Text, 41, 70.2, 34, 5, 10, IdTextAlignment.Left, Black, true, "2026001"),
        new("front_signature", IdLayoutSide.Front, "Holder signature", IdLayoutKind.Image, 7, 79, 33, 10),
        new("front_signature_label", IdLayoutSide.Front, "Signature label", IdLayoutKind.Text, 6, 90.5, 35, 4, 6.6, IdTextAlignment.Center, Green, true, "SIGNATURE OF HOLDER"),
        new("front_qr_label", IdLayoutSide.Front, "QR label", IdLayoutKind.Text, 55, 77.5, 24, 4, 6.3, IdTextAlignment.Center, Green, true, "SCAN TO VERIFY"),
        new("front_qr", IdLayoutSide.Front, "QR image", IdLayoutKind.Image, 57, 82.5, 20, 20),

        new("back_dob_label", IdLayoutSide.Back, "DATE OF BIRTH label", IdLayoutKind.Text, 7, 7, 22, 4, 7, IdTextAlignment.Left, Green, true, "DATE OF BIRTH:"),
        new("back_dob_value", IdLayoutSide.Back, "Date of birth", IdLayoutKind.Text, 30, 7, 48, 4, 7.8, IdTextAlignment.Left, Black, false, "January 12, 1987"),
        new("back_sex_label", IdLayoutSide.Back, "SEX label", IdLayoutKind.Text, 7, 14, 10, 4, 7, IdTextAlignment.Left, Green, true, "SEX:"),
        new("back_sex_value", IdLayoutSide.Back, "Sex", IdLayoutKind.Text, 17, 14, 18, 4, 7.8, IdTextAlignment.Left, Black, false, "Female"),
        new("back_civil_label", IdLayoutSide.Back, "CIVIL STATUS label", IdLayoutKind.Text, 40, 14, 23, 4, 7, IdTextAlignment.Left, Green, true, "CIVIL STATUS:"),
        new("back_civil_value", IdLayoutSide.Back, "Civil status", IdLayoutKind.Text, 63, 14, 15, 4, 7.2, IdTextAlignment.Left, Black, false, "Married"),
        new("back_address_label", IdLayoutSide.Back, "ADDRESS label", IdLayoutKind.Text, 7, 21, 71, 4, 7, IdTextAlignment.Left, Green, true, "ADDRESS:"),
        new("back_address_value", IdLayoutSide.Back, "Address", IdLayoutKind.Text, 7, 26, 71, 9, 7.2, IdTextAlignment.Left, Black, false, "Sitio Tungcaling, Barangay Sibulan, Sta. Cruz, Davao del Sur"),
        new("back_identification_heading", IdLayoutSide.Back, "IDENTIFICATION heading", IdLayoutKind.Text, 9, 38.5, 67, 5, 8.5, IdTextAlignment.Center, Green, true, "IDENTIFICATION"),
        new("back_identification_body", IdLayoutSide.Back, "Identification paragraph", IdLayoutKind.Text, 9, 44, 67, 18, 7, IdTextAlignment.Left, Black, false, "This identification card is issued to the bearer whose photograph appears herein and who is a bona fide employee of the Barangay Local Government Unit of Sibulan."),
        new("back_issued_label", IdLayoutSide.Back, "ISSUED BY label", IdLayoutKind.Text, 8, 64, 30, 4, 7.5, IdTextAlignment.Left, Green, true, "ISSUED BY:"),
        new("back_issuer_value", IdLayoutSide.Back, "Issuer", IdLayoutKind.Text, 8, 70, 30, 5, 8, IdTextAlignment.Left, Black, true, "BLGU - SIBULAN"),
        new("back_approved_label", IdLayoutSide.Back, "APPROVED BY label", IdLayoutKind.Text, 45, 64, 31, 4, 7.5, IdTextAlignment.Left, Green, true, "APPROVED BY:"),
        new("back_captain_signature", IdLayoutSide.Back, "Approver signature", IdLayoutKind.Image, 47, 67.5, 27, 8),
        new("back_captain_name", IdLayoutSide.Back, "Approver name", IdLayoutKind.Text, 45, 76, 33, 4, 7.4, IdTextAlignment.Left, Black, true, "ROWENA A. TABO"),
        new("back_captain_title", IdLayoutSide.Back, "Approver title", IdLayoutKind.Text, 45, 80, 31, 4, 6.2, IdTextAlignment.Left, Black, false, "Punong Barangay"),
        new("back_notice_heading", IdLayoutSide.Back, "IMPORTANT NOTICE heading", IdLayoutKind.Text, 9, 84, 67, 4, 7.8, IdTextAlignment.Center, Green, true, "IMPORTANT NOTICE"),
        new("back_notice_body", IdLayoutSide.Back, "Important notice", IdLayoutKind.Text, 9, 88.5, 67, 14, 5.7, IdTextAlignment.Left, Black, false, "– This ID is non-transferable.\n– This ID remains the property of the BLGU of Sibulan.\n– If lost, report immediately to the Barangay Office.\n– Unauthorized use, alteration, or reproduction is prohibited."),
        new("back_footer_address", IdLayoutSide.Back, "Footer address", IdLayoutKind.Text, 5, 104, 75, 4, 5.4, IdTextAlignment.Center, White, false, "Barangay Hall, Sitio Centro, Barangay Sibulan, Sta. Cruz, Davao del Sur"),
        new("back_footer_contact", IdLayoutSide.Back, "Footer contact", IdLayoutKind.Text, 5, 108, 75, 4, 5.3, IdTextAlignment.Center, White, false, "brgysibulan8001@gmail.com  |  0970 972 3363")
    };

    public static IEnumerable<LayoutElementDefinition> ForSide(IdLayoutSide side) => Elements.Where(x => x.Side == side);

    public static LayoutElementDefinition? Find(string key) => Elements.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public static ElementPlacement DefaultPlacement(LayoutElementDefinition definition) => new()
    {
        XMm = definition.DefaultXmm,
        YMm = definition.DefaultYmm,
        WidthMm = definition.DefaultWidthMm,
        HeightMm = definition.DefaultHeightMm,
        FontSizePt = definition.DefaultFontPt,
        FontFamilyKey = "sans",
        Bold = definition.DefaultBold,
        Alignment = definition.DefaultAlignment,
        TextColor = definition.DefaultColor,
        UnderlineEnabled = false,
        UnderlineColor = definition.DefaultColor,
        UnderlineThicknessPt = 0.45,
        UnderlineOffsetMm = 0.7,
        UnderlineWidthMode = IdUnderlineWidthMode.Text,
        TextOutlineEnabled = false,
        TextOutlineColor = definition.DefaultColor.Equals(White, StringComparison.OrdinalIgnoreCase) ? Black : White,
        TextOutlineWidthPt = 0.35,
        ShadowEnabled = false,
        ShadowColor = Black,
        ShadowOpacity = 0.35,
        ShadowDxMm = 0.45,
        ShadowDyMm = 0.45,
        ShadowRadiusPt = 0,
        Visible = true
    };

    public static LayoutProfile CreateDefaultProfile()
    {
        var profile = new LayoutProfile();
        foreach (var definition in Elements)
        {
            profile.Elements[definition.Key] = DefaultPlacement(definition);
        }
        return profile;
    }
}
