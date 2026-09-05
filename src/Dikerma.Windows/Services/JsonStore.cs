using System.Text.Json;
using Dikerma.Windows.Models;

namespace Dikerma.Windows.Services;

public sealed class JsonStore
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public JsonStore() => AppPaths.Ensure();

    public List<EmployeeRecord> LoadEmployees() => Load(AppPaths.EmployeesFile, new List<EmployeeRecord>());
    public void SaveEmployees(IEnumerable<EmployeeRecord> employees) => Save(AppPaths.EmployeesFile, employees.ToList());

    public AppSettingsModel LoadSettings() => Load(AppPaths.SettingsFile, new AppSettingsModel());
    public void SaveSettings(AppSettingsModel settings) => Save(AppPaths.SettingsFile, settings);

    public LayoutProfile LoadLayout()
    {
        var profile = Load<LayoutProfile?>(AppPaths.LayoutFile, null);
        if (profile is null)
        {
            return LayoutCatalog.CreateDefaultProfile();
        }

        profile.CustomElements ??= new List<CustomLayoutElement>();

        foreach (var definition in LayoutCatalog.Elements)
        {
            if (!profile.Elements.ContainsKey(definition.Key))
            {
                profile.Elements[definition.Key] = LayoutCatalog.DefaultPlacement(definition);
            }
            profile.Elements[definition.Key].Clamp();
        }
        return profile;
    }

    public void SaveLayout(LayoutProfile layout) => Save(AppPaths.LayoutFile, layout);

    private T Load<T>(string path, T fallback)
    {
        try
        {
            if (!File.Exists(path)) return fallback;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, _options) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private void Save<T>(string path, T value)
    {
        AppPaths.Ensure();
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, _options));
        File.Move(temp, path, true);
    }
}
