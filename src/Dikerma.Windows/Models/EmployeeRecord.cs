namespace Dikerma.Windows.Models;

public sealed class EmployeeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string ControlNumber { get; set; } = string.Empty;
    public string Birthdate { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Sex { get; set; } = string.Empty;
    public string CivilStatus { get; set; } = string.Empty;
    public string? PhotoPath { get; set; }
    public string? SignaturePath { get; set; }
    public string? QrImagePath { get; set; }
    public string Status { get; set; } = "Active";

    public override string ToString() => string.IsNullOrWhiteSpace(ControlNumber)
        ? FullName
        : $"{FullName} ({ControlNumber})";
}
