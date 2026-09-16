using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Models;

namespace ShiftingGuru.Data;

/// <summary>
/// Demo SEO content so the location and route pages have something to show.
///
/// Everything here is DRAFT (IsPublished = false) on purpose. A page that says
/// nothing specific is worse than no page at all, so an admin reads and edits
/// each one before publishing it. Nothing in this file claims vendor counts,
/// prices, ratings or coverage.
///
/// Runs once: if any location already exists, it does nothing.
/// </summary>
public static class SeoSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeoSeeder");

        if (await db.Locations.AnyAsync()) return;

        var now = DateTime.UtcNow;

        var cities = new (string Name, string Slug, string State, string Intro)[]
        {
            ("Delhi", "delhi", "Delhi",
                "Moving within Delhi means narrow lanes, lift access and parking permissions as much as distance."),
            ("Gurgaon", "gurgaon", "Haryana",
                "Most Gurgaon moves are high-rise to high-rise, where society timings matter more than kilometres."),
            ("Noida", "noida", "Uttar Pradesh",
                "Noida sectors are well laid out, which usually makes access easier than in older parts of the NCR."),
            ("Mumbai", "mumbai", "Maharashtra",
                "Mumbai moves are shaped by building rules, tight staircases and when a truck is allowed on the road."),
            ("Bangalore", "bangalore", "Karnataka",
                "Bangalore traffic decides the schedule. Most crews start early to get a move done in one day."),
            ("Pune", "pune", "Maharashtra",
                "Pune has a steady flow of moves to and from Mumbai, which keeps route capacity reasonably available."),
            ("Hyderabad", "hyderabad", "Telangana",
                "Hyderabad's spread means the distance between two addresses in the same city can still be substantial.")
        };

        var locations = cities.Select(city => new Location
        {
            Name = city.Name,
            Slug = city.Slug,
            City = city.Name,
            State = city.State,
            Country = "India",
            H1 = $"Packers and Movers in {city.Name}",
            ShortDescription =
                $"Compare moving and logistics quotes in {city.Name} from verified professionals.",
            Content =
                $"{city.Intro}\n\n"
                + $"ShiftingGuru is a marketplace, not a moving company. You describe what you're moving "
                + $"and we pass the requirement to professionals who work in {city.Name}. They respond with "
                + $"what they'd charge and what's included, and you decide.\n\n"
                + "What affects the cost is usually the volume of goods, the floor and lift access at both "
                + "ends, and whether you want packing included. Two homes on the same street can differ "
                + "substantially, which is why comparing a few offers is more useful than a single headline price.\n\n"
                + "[DRAFT CONTENT - replace this with something specific to the city before publishing.]",
            MetaTitle = $"Packers and Movers in {city.Name}",
            MetaDescription =
                $"Compare quotes from verified packers and movers in {city.Name}. "
                + "Home shifting, office relocation, vehicle transport and storage. Free to use.",
            IsPublished = false,
            CreatedAt = now
        }).ToList();

        db.Locations.AddRange(locations);
        await db.SaveChangesAsync();

        var byslug = locations.ToDictionary(l => l.Slug);

        var corridors = new (string From, string To)[]
        {
            ("delhi", "bangalore"),
            ("gurgaon", "bangalore"),
            ("delhi", "mumbai"),
            ("mumbai", "pune"),
            ("gurgaon", "hyderabad")
        };

        var routes = corridors.Select(corridor =>
        {
            var from = byslug[corridor.From];
            var to = byslug[corridor.To];

            return new MovingRoute
            {
                FromLocationId = from.Id,
                ToLocationId = to.Id,
                Slug = MovingRoute.BuildSlug(from.Slug, to.Slug),
                H1 = $"Packers and Movers from {from.Name} to {to.Name}",
                ShortDescription =
                    $"Compare quotes for moving from {from.Name} to {to.Name} with verified professionals.",
                Content =
                    $"A move from {from.Name} to {to.Name} is a long-distance job, which changes what matters. "
                    + "Packing quality counts for more than it does on a local shift, because goods spend days "
                    + "in transit rather than hours.\n\n"
                    + "Most providers on this kind of route pack and load on one day, run the distance over the "
                    + "following days, and deliver into the new address. Ask what packing material is included, "
                    + "whether unpacking is quoted, and who handles loading labour at both ends.\n\n"
                    + "If you're sending a vehicle as well, that usually travels separately on a carrier rather "
                    + "than with your household goods. It can be arranged alongside the same enquiry.\n\n"
                    + "[DRAFT CONTENT - replace with route-specific detail before publishing.]",
                MetaTitle = $"Packers and Movers from {from.Name} to {to.Name}",
                MetaDescription =
                    $"Compare moving quotes from {from.Name} to {to.Name}. Packing, transport and delivery "
                    + "from verified professionals. Free to use, no obligation.",
                IsPublished = false,
                CreatedAt = now
            };
        }).ToList();

        db.Routes.AddRange(routes);
        await db.SaveChangesAsync();

        // A couple of genuinely useful questions per page, rather than filler.
        var faqs = new List<Faq>();

        foreach (var location in locations)
        {
            faqs.Add(new Faq
            {
                LocationId = location.Id,
                Question = $"How much does moving in {location.Name} cost?",
                Answer = "It depends on the volume of goods, distance, floor and lift access, and whether "
                       + "packing is included. Rather than quoting a figure that wouldn't fit your move, "
                       + "we pass your requirement to several providers so you can compare real offers.",
                DisplayOrder = 1,
                IsPublished = true,
                CreatedAt = now
            });

            faqs.Add(new Faq
            {
                LocationId = location.Id,
                Question = "How far in advance should I book?",
                Answer = "A week or two is comfortable for a local move. Month-end is busier across the "
                       + "industry, so more notice helps if your date falls then.",
                DisplayOrder = 2,
                IsPublished = true,
                CreatedAt = now
            });
        }

        foreach (var route in routes)
        {
            faqs.Add(new Faq
            {
                RouteId = route.Id,
                Question = "How long does the move take?",
                Answer = "Long-distance moves are usually quoted as a delivery window rather than a fixed "
                       + "day, because road conditions vary. Providers give an estimate when they quote.",
                DisplayOrder = 1,
                IsPublished = true,
                CreatedAt = now
            });

            faqs.Add(new Faq
            {
                RouteId = route.Id,
                Question = "Can I send my car on the same move?",
                Answer = "Yes, though it normally travels on a separate vehicle carrier rather than with "
                       + "household goods. Mention it in your enquiry and providers will quote for both.",
                DisplayOrder = 2,
                IsPublished = true,
                CreatedAt = now
            });
        }

        db.Faqs.AddRange(faqs);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Seeded {Locations} draft locations, {Routes} draft routes and {Faqs} FAQs. "
            + "All pages are unpublished until an admin reviews the content.",
            locations.Count, routes.Count, faqs.Count);
    }
}