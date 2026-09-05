using System.IO;
using System.Text.Json;
using Dikerma.Windows.Models;
using Dikerma.Windows.Services;

static void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    Console.WriteLine("PASS: " + description);
}

var profile = LayoutCatalog.CreateDefaultProfile();
var settings = new AppSettingsModel();
var alice = new EmployeeRecord { FullName = "ALICE", PhotoPath = "alice.png" };
var bob = new EmployeeRecord { FullName = "BOB", PhotoPath = "bob.png" };
var municipality = LayoutCatalog.Find("front_municipality")!;
Check(PdfExportService.ResolveElementText(municipality, alice, settings, profile) == "STA. CRUZ", "Default static text survives migration");
profile.Get(municipality.Key).TextOverride = "NEW MUNICIPALITY";
Check(PdfExportService.ResolveElementText(municipality, bob, settings, profile) == "NEW MUNICIPALITY", "Built-in text override reaches PDF resolver");
profile.Get(municipality.Key).TextOverride = "";
Check(PdfExportService.ResolveElementText(municipality, alice, settings, profile) == "", "Empty text is not replaced with defaults");
profile.Get(municipality.Key).TextOverride = null;
Check(PdfExportService.ResolveElementText(municipality, alice, settings, profile) == "STA. CRUZ", "Original content can be restored");

foreach (var definition in LayoutCatalog.Elements)
{
    profile.Get(definition.Key).Deleted = true;
    var reloaded = JsonSerializer.Deserialize<LayoutProfile>(JsonSerializer.Serialize(profile))!;
    Check(!reloaded.ForSide(definition.Side).Any(x => x.Key == definition.Key) && !reloaded.IsVisible(definition.Key), "Deleted element stays deleted after reload: " + definition.Key);
    profile.Get(definition.Key).Deleted = false;
}

var nameCopy = new CustomLayoutElement { SourceKey = "front_name_value", Side = IdLayoutSide.Front, Kind = IdLayoutKind.Text };
profile.CustomElements.Add(nameCopy);
Check(PdfExportService.ResolveElementText(nameCopy.ToDefinition(), alice, settings, profile) == "ALICE", "Duplicate keeps first employee binding");
Check(PdfExportService.ResolveElementText(nameCopy.ToDefinition(), bob, settings, profile) == "BOB", "Duplicate switches to second employee binding");
profile.Get(nameCopy.Key).TextOverride = "FIXED TEXT";
Check(PdfExportService.ResolveElementText(nameCopy.ToDefinition(), bob, settings, profile) == "FIXED TEXT" && bob.FullName == "BOB", "Editing template text does not mutate record");

var photoCopy = new CustomLayoutElement { SourceKey = "front_photo", Side = IdLayoutSide.Front, Kind = IdLayoutKind.Image };
profile.CustomElements.Add(photoCopy);
Check(PdfExportService.ResolveElementImage(photoCopy.ToDefinition(), bob, settings, profile) == "bob.png", "Duplicate photo retains record binding");
profile.Get(photoCopy.Key).ImageOverride = "template.png";
Check(PdfExportService.ResolveElementImage(photoCopy.ToDefinition(), alice, settings, profile) == "template.png" && alice.PhotoPath == "alice.png", "Template image replacement preserves original record photo");

profile.Get("front_background").Deleted = true;
Check(!profile.ForSide(IdLayoutSide.Front).Any(x => x.Key == "front_background"), "Background is deletable");
profile.Get("front_barangay").ZIndex = 500;
Check(profile.ForSide(IdLayoutSide.Front).Last().Key == "front_barangay", "Built-in and custom elements share layer ordering");
var customText = new CustomLayoutElement { Content = "MY CUSTOM TEXT" };
profile.CustomElements.Add(customText);
Check(PdfExportService.ResolveElementText(customText.ToDefinition(), alice, settings, profile) == "MY CUSTOM TEXT", "Existing custom text remains supported");
var legacy = JsonSerializer.Deserialize<LayoutProfile>("{\"SchemaVersion\":2,\"Elements\":{},\"CustomElements\":[]}")!;
Check(legacy.ForSide(IdLayoutSide.Front).Any(x => x.Key == "front_background"), "Old layouts acquire an editable background without losing fields");
Check(LayoutCatalog.CardWidthMm == 85 && LayoutCatalog.CardHeightMm == 115, "ID print dimensions remain 85 x 115 mm");

var pdfPath = Path.Combine(Path.GetTempPath(), "dikerma-editor-check-" + Guid.NewGuid().ToString("N") + ".pdf");
try
{
    new PdfExportService().Export(pdfPath, alice, bob, settings, profile);
    Check(new FileInfo(pdfPath).Length > 0, "Two-person PDF exports with edited/deleted/custom elements");
}
finally { if (File.Exists(pdfPath)) File.Delete(pdfPath); }
