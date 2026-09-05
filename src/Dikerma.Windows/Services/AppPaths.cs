namespace Dikerma.Windows.Services;

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DIKERMA");

    public static string Data { get; } = Path.Combine(Root, "data");
    public static string Assets { get; } = Path.Combine(Root, "assets");
    public static string Exports { get; } = Path.Combine(Root, "exports");

    public static string EmployeesFile => Path.Combine(Data, "employees.json");
    public static string SettingsFile => Path.Combine(Data, "settings.json");
    public static string LayoutFile => Path.Combine(Data, "layout.json");

    public static void Ensure()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Assets);
        Directory.CreateDirectory(Exports);
    }
}
