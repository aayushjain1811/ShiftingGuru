using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Models;

namespace ShiftingGuru.Data;

/// <summary>
/// The full city list the business serves, plus the main corridors between
/// them, as SEO landing pages.
///
/// Unlike SeoSeeder this one is ADDITIVE: it runs on every start, adds only
/// the cities and routes that are missing, and never touches a row that
/// already exists. So anything an admin has edited or published is safe.
///
/// Pages are created as DRAFTS. Set PublishNewPages to true if you'd rather
/// they go live immediately - but read the note on that field first.
/// </summary>
public static class CityCatalogSeeder
{
    /// <summary>
    /// Drafts by default. Eighty-odd pages that differ only by city name are
    /// treated as thin, duplicated content by search engines, and that can
    /// drag down the whole site rather than just those pages. The safer path
    /// is to publish each city from /admin/locations once it has a couple of
    /// lines written for it.
    /// </summary>
    private const bool PublishNewPages = false;

    /// <summary>
    /// Cities that are already in the database under a different spelling.
    /// Key = the slug in the list below, value = the slug already stored.
    /// Without this, "bengaluru" would be added alongside an existing
    /// "bangalore" and the two pages would compete with each other.
    /// </summary>
    private static readonly Dictionary<string, string> KnownAliases = new()
    {
        ["bengaluru"] = "bangalore",
        ["gurugram"] = "gurgaon",
        ["kochi"] = "cochin",
        ["puducherry"] = "pondicherry",
        ["mangaluru"] = "mangalore",
        ["aurangabad"] = "sambhajinagar"
    };

    /// <summary>Name, slug, state. India is assumed for the country.</summary>
    private static readonly (string Name, string Slug, string State)[] Cities =
    {
        ("Agra", "agra", "Uttar Pradesh"),
        ("Ahmedabad", "ahmedabad", "Gujarat"),
        ("Ajmer", "ajmer", "Rajasthan"),
        ("Ambala", "ambala", "Haryana"),
        ("Amritsar", "amritsar", "Punjab"),
        ("Aurangabad", "aurangabad", "Maharashtra"),
        ("Ballari", "ballari", "Karnataka"),
        ("Bareilly", "bareilly", "Uttar Pradesh"),
        ("Bathinda", "bathinda", "Punjab"),
        ("Bengaluru", "bengaluru", "Karnataka"),
        ("Bhagalpur", "bhagalpur", "Bihar"),
        ("Bhilai", "bhilai", "Chhattisgarh"),
        ("Bhilwara", "bhilwara", "Rajasthan"),
        ("Bhopal", "bhopal", "Madhya Pradesh"),
        ("Bhubaneswar", "bhubaneswar", "Odisha"),
        ("Bikaner", "bikaner", "Rajasthan"),
        ("Bilaspur", "bilaspur", "Chhattisgarh"),
        ("Chandigarh", "chandigarh", "Chandigarh"),
        ("Chandrapur", "chandrapur", "Maharashtra"),
        ("Chennai", "chennai", "Tamil Nadu"),
        ("Chhatarpur", "chhatarpur", "Madhya Pradesh"),
        ("Chhindwara", "chhindwara", "Madhya Pradesh"),
        ("Kochi", "kochi", "Kerala"),
        ("Coimbatore", "coimbatore", "Tamil Nadu"),
        ("Dehradun", "dehradun", "Uttarakhand"),
        ("Delhi", "delhi", "Delhi"),
        ("Durgapur", "durgapur", "West Bengal"),
        ("Faridabad", "faridabad", "Haryana"),
        ("Gandhidham", "gandhidham", "Gujarat"),
        ("Ghaziabad", "ghaziabad", "Uttar Pradesh"),
        ("Goa", "goa", "Goa"),
        ("Gorakhpur", "gorakhpur", "Uttar Pradesh"),
        ("Greater Noida", "greater-noida", "Uttar Pradesh"),
        ("Gurugram", "gurugram", "Haryana"),
        ("Guwahati", "guwahati", "Assam"),
        ("Gwalior", "gwalior", "Madhya Pradesh"),
        ("Haridwar", "haridwar", "Uttarakhand"),
        ("Hisar", "hisar", "Haryana"),
        ("Hubli", "hubli", "Karnataka"),
        ("Hyderabad", "hyderabad", "Telangana"),
        ("Indore", "indore", "Madhya Pradesh"),
        ("Jabalpur", "jabalpur", "Madhya Pradesh"),
        ("Jaipur", "jaipur", "Rajasthan"),
        ("Jalgaon", "jalgaon", "Maharashtra"),
        ("Jammu", "jammu", "Jammu and Kashmir"),
        ("Jamnagar", "jamnagar", "Gujarat"),
        ("Jamshedpur", "jamshedpur", "Jharkhand"),
        ("Jhansi", "jhansi", "Uttar Pradesh"),
        ("Jodhpur", "jodhpur", "Rajasthan"),
        ("Kanpur", "kanpur", "Uttar Pradesh"),
        ("Kolkata", "kolkata", "West Bengal"),
        ("Kota", "kota", "Rajasthan"),
        ("Latur", "latur", "Maharashtra"),
        ("Lucknow", "lucknow", "Uttar Pradesh"),
        ("Ludhiana", "ludhiana", "Punjab"),
        ("Mangaluru", "mangaluru", "Karnataka"),
        ("Meerut", "meerut", "Uttar Pradesh"),
        ("Moradabad", "moradabad", "Uttar Pradesh"),
        ("Mumbai", "mumbai", "Maharashtra"),
        ("Nagpur", "nagpur", "Maharashtra"),
        ("Nashik", "nashik", "Maharashtra"),
        ("Noida", "noida", "Uttar Pradesh"),
        ("Patna", "patna", "Bihar"),
        ("Puducherry", "puducherry", "Puducherry"),
        ("Prayagraj", "prayagraj", "Uttar Pradesh"),
        ("Pune", "pune", "Maharashtra"),
        ("Raigarh", "raigarh", "Chhattisgarh"),
        ("Raipur", "raipur", "Chhattisgarh"),
        ("Rajkot", "rajkot", "Gujarat"),
        ("Ranchi", "ranchi", "Jharkhand"),
        ("Sagar", "sagar", "Madhya Pradesh"),
        ("Sambalpur", "sambalpur", "Odisha"),
        ("Shivpuri", "shivpuri", "Madhya Pradesh"),
        ("Silchar", "silchar", "Assam"),
        ("Siliguri", "siliguri", "West Bengal"),
        ("Sonbhadra", "sonbhadra", "Uttar Pradesh"),
        ("Surat", "surat", "Gujarat"),
        ("Udaipur", "udaipur", "Rajasthan"),
        ("Vadodara", "vadodara", "Gujarat"),
        ("Vapi", "vapi", "Gujarat"),
        ("Varanasi", "varanasi", "Uttar Pradesh"),
        ("Vijayawada", "vijayawada", "Andhra Pradesh"),
        ("Visakhapatnam", "visakhapatnam", "Andhra Pradesh"),    };

    /// <summary>
    /// The corridors worth their own page. Every possible pair would be
    /// thousands of pages nobody searches for, so this is a hand-picked list.
    /// Add to it as you learn which routes customers actually ask about.
    /// </summary>
    private static readonly (string From, string To)[] Corridors =
    {
        ("delhi", "mumbai"), ("delhi", "bengaluru"), ("delhi", "hyderabad"),
        ("delhi", "chennai"), ("delhi", "pune"), ("delhi", "kolkata"),
        ("delhi", "ahmedabad"), ("delhi", "jaipur"), ("delhi", "chandigarh"),
        ("delhi", "lucknow"), ("delhi", "indore"), ("delhi", "dehradun"),
        ("gurugram", "mumbai"), ("gurugram", "bengaluru"), ("gurugram", "pune"),
        ("noida", "bengaluru"), ("noida", "mumbai"),
        ("mumbai", "bengaluru"), ("mumbai", "hyderabad"), ("mumbai", "chennai"),
        ("mumbai", "delhi"), ("mumbai", "ahmedabad"), ("mumbai", "indore"),
        ("mumbai", "jaipur"), ("mumbai", "nagpur"),
        ("bengaluru", "hyderabad"), ("bengaluru", "chennai"), ("bengaluru", "pune"),
        ("bengaluru", "kolkata"), ("bengaluru", "delhi"), ("bengaluru", "mumbai"),
        ("pune", "hyderabad"), ("pune", "bengaluru"),
        ("hyderabad", "chennai"), ("chennai", "kochi"), ("chennai", "coimbatore"),
        ("kolkata", "guwahati"), ("kolkata", "patna"), ("kolkata", "bhubaneswar"),
        ("ahmedabad", "surat"), ("jaipur", "bengaluru"), ("lucknow", "mumbai"),
        ("chandigarh", "bengaluru"), ("indore", "pune"), ("nagpur", "hyderabad")
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>().CreateLogger("CityCatalogSeeder");

        var now = DateTime.UtcNow;

        // ---------- Cities ----------

        // One query for every slug already stored, so the loop below doesn't
        // hit the database once per city.
        var existingSlugs = await db.Locations
            .Select(l => l.Slug)
            .ToListAsync();

        var stored = new HashSet<string>(existingSlugs, StringComparer.OrdinalIgnoreCase);

        var newLocations = new List<Location>();

        foreach (var city in Cities)
        {
            // Already there under this slug, or under an older spelling?
            if (stored.Contains(city.Slug)) continue;
            if (KnownAliases.TryGetValue(city.Slug, out var alias) && stored.Contains(alias)) continue;

            newLocations.Add(new Location
            {
                Name = city.Name,
                Slug = city.Slug,
                City = city.Name,
                State = city.State,
                Country = "India",
                H1 = $"Car Transport in {city.Name}",
                ShortDescription =
                    $"Compare car transport and moving quotes in {city.Name} from verified professionals.",
                Content =
                    $"We arrange car transport, household shifting and logistics to and from {city.Name}.\n\n"
                    + "Tell us what you're moving and where it needs to go, and professionals who cover this "
                    + "city respond with what they'd charge and what's included.\n\n"
                    + "[DRAFT - replace this with two or three lines that are true only of "
                    + $"{city.Name}: the areas you cover, how vehicles are picked up, typical transit "
                    + "times from here. A page that could describe any city is the one that won't rank.]",
                MetaTitle = $"Car Transport in {city.Name} | ShiftingGuru",
                MetaDescription =
                    $"Car transport and packers and movers in {city.Name}. "
                    + "Compare quotes from verified professionals. Free to use, no obligation.",
                IsPublished = PublishNewPages,
                CreatedAt = now
            });
        }

        if (newLocations.Count > 0)
        {
            db.Locations.AddRange(newLocations);
            await db.SaveChangesAsync();
        }

        // ---------- Routes ----------

        // Re-read so both the pre-existing rows and the ones just added are here.
        var locationsBySlug = await db.Locations
            .ToDictionaryAsync(l => l.Slug, StringComparer.OrdinalIgnoreCase);

        var existingRouteSlugs = new HashSet<string>(
            await db.Routes.Select(r => r.Slug).ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        var newRoutes = new List<MovingRoute>();

        foreach (var corridor in Corridors)
        {
            var from = Resolve(locationsBySlug, corridor.From);
            var to = Resolve(locationsBySlug, corridor.To);

            // A corridor naming a city that isn't stored, or one that resolved
            // to the same city both ends, is skipped rather than crashing.
            if (from is null || to is null || from.Id == to.Id) continue;

            var slug = MovingRoute.BuildSlug(from.Slug, to.Slug);
            if (!existingRouteSlugs.Add(slug)) continue;

            newRoutes.Add(new MovingRoute
            {
                FromLocationId = from.Id,
                ToLocationId = to.Id,
                Slug = slug,
                H1 = $"Car Transport from {from.Name} to {to.Name}",
                ShortDescription =
                    $"Compare quotes for moving from {from.Name} to {to.Name} with verified professionals.",
                Content =
                    $"Vehicles and household goods travel between {from.Name} and {to.Name} regularly, "
                    + "so capacity on this corridor is usually available at short notice.\n\n"
                    + "Cars travel on a carrier rather than being driven, and household goods are packed "
                    + "and loaded on one day, then delivered into the new address.\n\n"
                    + $"[DRAFT - add what is specific to {from.Name} to {to.Name}: usual transit days, "
                    + "pickup points, anything customers ask about this route.]",
                MetaTitle = $"Car Transport from {from.Name} to {to.Name} | ShiftingGuru",
                MetaDescription =
                    $"Car transport and movers from {from.Name} to {to.Name}. "
                    + "Compare quotes from verified professionals. Free to use, no obligation.",
                IsPublished = PublishNewPages,
                CreatedAt = now
            });
        }

        if (newRoutes.Count > 0)
        {
            db.Routes.AddRange(newRoutes);
            await db.SaveChangesAsync();
        }

        if (newLocations.Count > 0 || newRoutes.Count > 0)
        {
            logger.LogInformation(
                "City catalog: added {Locations} locations and {Routes} routes (published: {Published}).",
                newLocations.Count, newRoutes.Count, PublishNewPages);
        }
    }

    /// <summary>Finds a city by its slug, falling back to an older spelling.</summary>
    private static Location? Resolve(IDictionary<string, Location> bySlug, string slug)
    {
        if (bySlug.TryGetValue(slug, out var found)) return found;

        return KnownAliases.TryGetValue(slug, out var alias) && bySlug.TryGetValue(alias, out var aliased)
            ? aliased
            : null;
    }
}