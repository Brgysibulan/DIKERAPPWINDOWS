namespace Dikerma.Windows.Models;

public sealed class AppSettingsModel
{
    public string? FrontBackgroundPath { get; set; }
    public string? BackBackgroundPath { get; set; }
    public string? Logo1Path { get; set; }
    public string? Logo2Path { get; set; }
    public string? CaptainSignaturePath { get; set; }

    public string CaptainName { get; set; } = "ROWENA A. TABO";
    public string CaptainTitle { get; set; } = "PUNONG BARANGAY";
    public string IssuerName { get; set; } = "BLGU - SIBULAN";
    public string FooterAddress { get; set; } = "Barangay Hall, Sitio Centro, Barangay Sibulan, Sta. Cruz, Davao del Sur";
    public string FooterContact { get; set; } = "brgysibulan8001@gmail.com  |  0970 972 3363";

    public bool OuterCutGuideEnabled { get; set; } = true;
    public bool PhotoOutlineEnabled { get; set; }
    public bool EmployeeDividerEnabled { get; set; }
    public bool SignatureLineEnabled { get; set; }
    public bool QrOutlineEnabled { get; set; }
    public bool BackDividerEnabled { get; set; }
    public double OutlineThicknessPt { get; set; } = 0.5;
}
