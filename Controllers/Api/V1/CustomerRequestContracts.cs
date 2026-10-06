using System.ComponentModel.DataAnnotations;
using ShiftingGuru.Services;

namespace ShiftingGuru.Controllers.Api.V1;

// NEW (mobile API): requests made in the customer app.

/// <summary>One item from the Home Shifting inventory picker.</summary>
public class InventoryLineRequest
{
    [Required]
    [StringLength(60)]
    public string? Name { get; set; }

    [Range(1, 99)]
    public int Quantity { get; set; }
}

/// <summary>
/// What the app sends. The service answers arrive as the app's own keys
/// ("bhk", "carType"...) and the server decides where each one is stored.
/// Name, phone and email are never sent - they come from the account.
/// </summary>
public class CreateCustomerRequest
{
    [Required(ErrorMessage = "Choose a service.")]
    public string? ServiceSlug { get; set; }

    [StringLength(80)]
    public string? MovingFrom { get; set; }

    [StringLength(80)]
    public string? MovingTo { get; set; }

    public DateOnly? MovingDate { get; set; }

    public Dictionary<string, string?> Details { get; set; } = new();

    public List<InventoryLineRequest> Inventory { get; set; } = new();
}

/// <summary>What a successful request returns.</summary>
public record CustomerRequestCreatedDto(int RequestId, string RequestNumber);

/// <summary>
/// Turns the app's answers into the Leads columns the website, the admin
/// panel and the partner app already show. Answers that have no column of
/// their own go, labelled, into "additional requirements" - so nothing the
/// customer told us is lost, and no database change is needed.
/// </summary>
public static class AppRequestMapper
{
    private const int AdditionalLimit = 600;   // Leads.AdditionalRequirements
    private const int ValueLimit = 80;

    /// <summary>Readable labels for answers that land in "additional requirements".</summary>
    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["floor"] = "Floor",
        ["lift"] = "Lift available",
        ["parking"] = "Parking near entrance",
        ["packing"] = "Packing required",
        ["loading"] = "Loading help",
        ["unloading"] = "Unloading help",
        ["carrier"] = "Carrier preference",
        ["petType"] = "Pet",
        ["breed"] = "Breed",
        ["petSize"] = "Pet size",
        ["documents"] = "Vaccination papers ready",
        ["moveKind"] = "Moving",
        ["customs"] = "Customs assistance",
        ["equipment"] = "IT equipment",
        ["furniture"] = "Furniture to move",
        ["pickup"] = "Pickup required"
    };

    /// <summary>Which answers each service may send, and which go to extra text.</summary>
    private static readonly Dictionary<string, string[]> Extras = new()
    {
        ["home-shifting"] = new[] { "floor", "lift", "parking", "packing", "loading", "unloading" },
        ["car-transportation"] = new[] { "carrier" },
        ["bike-transportation"] = Array.Empty<string>(),
        ["pet-relocation"] = new[] { "petType", "breed", "petSize", "documents" },
        ["international-relocation"] = new[] { "moveKind", "packing", "customs" },
        ["office-shifting"] = new[] { "equipment", "furniture", "packing" },
        ["goods-transportation"] = new[] { "loading", "unloading" },
        ["warehouse-storage"] = new[] { "pickup" }
    };

    public static AppLeadInput Map(
        Models.Customer customer, Models.Service service, CreateCustomerRequest request)
    {
        string? V(string key) =>
            request.Details.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? Cap(value.Trim(), ValueLimit)
                : null;

        var storage = service.Slug == "warehouse-storage";
        var from = Cap(request.MovingFrom?.Trim(), 80);
        var to = Cap(request.MovingTo?.Trim(), 80);

        // ---- Service details with a column of their own ----
        string? propertyType = null, moveSize = null, officeSize = null, deskCount = null,
            vehicleType = null, vehicleModel = null, vehicleCondition = null,
            goodsType = null, loadDetails = null, vehicleRequirement = null,
            storageType = null, storageSize = null, storageDuration = null;

        switch (service.Slug)
        {
            case "home-shifting":
                propertyType = Cap(V("propertyType"), 40);
                moveSize = Cap(V("bhk"), 120);
                break;

            case "car-transportation":
                vehicleType = Cap(V("carType"), 40);
                vehicleModel = Cap(V("carModel"), 80);
                vehicleCondition = Cap(V("running"), 20);
                break;

            case "bike-transportation":
                vehicleType = Cap(V("bikeType"), 40);
                vehicleModel = Cap(V("bikeModel"), 80);
                vehicleCondition = Cap(V("running"), 20);
                break;

            case "office-shifting":
                officeSize = V("employees") is { } employees ? Cap($"{employees} employees", 60) : null;
                deskCount = Cap(V("workstations"), 30);
                break;

            case "goods-transportation":
                goodsType = Cap(V("goodsType"), 80);
                loadDetails = Cap(V("quantity"), 120);
                vehicleRequirement = Cap(V("vehicle"), 60);
                break;

            case "warehouse-storage":
                storageType = Cap(V("storageType"), 40);
                storageSize = Cap(V("volume"), 60);
                storageDuration = Cap(V("duration"), 40);
                break;

            case "international-relocation":
                moveSize = Cap(V("shipmentSize"), 120);
                break;
        }

        // ---- Everything else, labelled, in "additional requirements" ----
        var answers = (Extras.TryGetValue(service.Slug, out var keys) ? keys : Array.Empty<string>())
            .Select(key => V(key) is { } value ? $"{Labels[key]}: {value}" : null)
            .Where(line => line is not null)
            .ToList();

        var parts = new List<string>();
        if (answers.Count > 0) parts.Add(string.Join("; ", answers));

        var notes = V("notes") is not null && request.Details.TryGetValue("notes", out var rawNotes)
            ? rawNotes!.Trim()
            : null;
        if (notes is not null) parts.Add($"Notes: {notes}");

        var additional = string.Join("\n", parts);

        // Inventory last, trimmed to whatever room is left.
        var items = request.Inventory
            .Where(i => !string.IsNullOrWhiteSpace(i.Name) && i.Quantity > 0)
            .Select(i => $"{i.Name!.Trim()} x{i.Quantity}")
            .ToList();

        if (items.Count > 0)
        {
            var room = AdditionalLimit - additional.Length - (additional.Length > 0 ? 1 : 0) - "Items: ".Length;
            var line = FitList(items, room);
            if (line is not null) additional = additional.Length > 0 ? $"{additional}\nItems: {line}" : $"Items: {line}";
        }

        return new AppLeadInput
        {
            CustomerId = customer.Id,
            CustomerName = Cap(customer.FullName, 80)!,
            Phone = customer.Phone,
            Email = customer.Email,

            MovingFrom = storage ? null : from,
            MovingTo = storage ? null : to,
            StorageLocation = storage ? from : null,
            MovingDate = request.MovingDate,

            PropertyType = propertyType,
            MoveSize = moveSize,
            OfficeSize = officeSize,
            DeskCount = deskCount,
            VehicleType = vehicleType,
            VehicleModel = vehicleModel,
            VehicleCondition = vehicleCondition,
            GoodsType = goodsType,
            LoadDetails = loadDetails,
            VehicleRequirement = vehicleRequirement,
            StorageType = storageType,
            StorageSize = storageSize,
            StorageDuration = storageDuration,

            AdditionalRequirements = string.IsNullOrWhiteSpace(additional) ? null : Cap(additional, AdditionalLimit)
        };
    }

    /// <summary>"Sofa x1, Bed x2 and 4 more" - as many items as fit.</summary>
    private static string? FitList(List<string> items, int room)
    {
        if (room < 20) return null;

        for (var count = items.Count; count > 0; count--)
        {
            var shown = string.Join(", ", items.Take(count));
            var text = count == items.Count ? shown : $"{shown} and {items.Count - count} more";
            if (text.Length <= room) return text;
        }

        return null;
    }

    private static string? Cap(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}