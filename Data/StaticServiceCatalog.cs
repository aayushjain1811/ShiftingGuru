using ShiftingGuru.Models;

namespace ShiftingGuru.Data;

/// <summary>
/// In-memory service catalog. Registered as a singleton in Program.cs.
/// To add a service: append one entry to the list below. Nothing else changes.
/// </summary>
public class StaticServiceCatalog : IServiceCatalog
{
    private static readonly IReadOnlyList<Service> Items = new List<Service>
    {
        new()
        {
            Id = 1,
            Name = "Home Shifting",
            Slug = "home-shifting",
            IsFeatured = true,
            ShortDescription = "Full household moves, packed and handled door to door.",
            IconPath = "M3 9l7-6 7 6v8a1 1 0 01-1 1H4a1 1 0 01-1-1V9z",
            HeroTitle = "Move your home without the hassle.",
            HeroDescription = "From a one-room flat to a four-bedroom house, tell us what you're moving and compare offers from professionals who handle household relocations every day.",
            MetaTitle = "Home Shifting Services",
            MetaDescription = "Compare home shifting quotes from verified packers and movers across India. Packing, loading, transport, unloading and unpacking, all in one enquiry.",
            Overview = new[]
            {
                "A household move is rarely just boxes. There's furniture that needs dismantling, appliances that need careful handling, and a stack of fragile items nobody wants to pack twice.",
                "Rather than calling around, describe your move once. Providers who work on your route respond with what they'd charge and what's included, so you can compare like for like."
            },
            Features = new[]
            {
                new ServiceItem { Title = "Household packing", Body = "Cartons, wrapping and labelling for crockery, clothing, books and fragile items." },
                new ServiceItem { Title = "Furniture handling", Body = "Dismantling beds, wardrobes and modular units where needed, and reassembly at the other end." },
                new ServiceItem { Title = "Appliance transportation", Body = "Fridges, washing machines, ACs and televisions handled with the right packing material." },
                new ServiceItem { Title = "Loading", Body = "Trained crews load the vehicle so weight is distributed and items are secured for transit." },
                new ServiceItem { Title = "Transportation", Body = "Local shifts or long-distance runs, in a vehicle sized to your consignment." },
                new ServiceItem { Title = "Unloading and unpacking", Body = "Delivery into the correct rooms, with unpacking if you've asked for it." }
            },
            Benefits = new[]
            {
                new ServiceItem { Title = "One enquiry, several replies", Body = "You hear from multiple providers instead of committing to the first quote you find." },
                new ServiceItem { Title = "Scope you can compare", Body = "Offers state what's included, so a low number with packing excluded is easy to spot." },
                new ServiceItem { Title = "Providers who cover your route", Body = "Your requirement goes to movers who actually operate between your two cities." },
                new ServiceItem { Title = "No booking fee", Body = "Using ShiftingGuru to collect quotes costs you nothing." }
            },
            Faqs = new[]
            {
                new FaqItem { Question = "What affects the cost of a home shift?", Answer = "Distance, the volume of goods, floor and lift access at both ends, and whether packing and unpacking are included. Two homes on the same route can differ substantially." },
                new FaqItem { Question = "Do I need to pack anything myself?", Answer = "Not usually. Most providers offer full packing. Some customers prefer to pack personal documents, jewellery and valuables themselves, which is sensible." },
                new FaqItem { Question = "How far in advance should I book?", Answer = "A week or two is comfortable for local moves. For long-distance shifts, or around month-end when demand rises, more notice helps." },
                new FaqItem { Question = "Can I move on a weekend?", Answer = "Usually yes, though availability is tighter and some providers price weekends differently. Mention your preferred date in your enquiry." }
            }
        },

        new()
        {
            Id = 2,
            Name = "Car Transportation",
            Slug = "car-transportation",
            IsFeatured = true,
            ShortDescription = "Enclosed or open carriers for single or multiple vehicles.",
            IconPath = "M3 13l1.5-4.5A2 2 0 016.4 7h7.2a2 2 0 011.9 1.5L17 13v3h-2.5M3 16v-3m0 3h2.5m9 0H5.5",
            HeroTitle = "Send your car ahead. Arrive to it waiting.",
            HeroDescription = "Door-to-door vehicle transport on open or enclosed carriers, with documentation handled properly and pickup arranged around your schedule.",
            MetaTitle = "Car Transportation Services",
            MetaDescription = "Compare car transportation quotes across India. Door-to-door pickup and delivery on open or enclosed carriers, with proper transit documentation.",
            Overview = new[]
            {
                "Driving a car 1,500 km to a new city means fuel, tolls, two days of your time and wear on the vehicle. Putting it on a carrier is usually the simpler call.",
                "Car transport is priced by route, carrier type and how quickly you need it. Comparing a few offers is the fastest way to see what's reasonable for your particular route."
            },
            Features = new[]
            {
                new ServiceItem { Title = "Door-to-door transport", Body = "Collection from your address and delivery to the new one, where the route allows." },
                new ServiceItem { Title = "Vehicle pickup", Body = "A scheduled pickup slot, with condition noted and recorded before loading." },
                new ServiceItem { Title = "Open or enclosed carriers", Body = "Open carriers are the common choice; enclosed suits premium or low-clearance vehicles." },
                new ServiceItem { Title = "Transit handling", Body = "Vehicles secured on the carrier with wheel straps rather than chassis chaining." },
                new ServiceItem { Title = "Documentation", Body = "Transit paperwork, vehicle condition record and delivery acknowledgement." },
                new ServiceItem { Title = "Delivery confirmation", Body = "A check against the pickup condition record when the vehicle is handed over." }
            },
            Benefits = new[]
            {
                new ServiceItem { Title = "No highway kilometres added", Body = "Your odometer, tyres and service interval stay where they were." },
                new ServiceItem { Title = "Route-specific providers", Body = "Carriers already running your route can often quote better than general movers." },
                new ServiceItem { Title = "Carrier choice", Body = "Decide between open and enclosed once you can see the price difference for your route." },
                new ServiceItem { Title = "Coordinate with your home move", Body = "Many customers arrange vehicle transport alongside a household shift." }
            },
            Faqs = new[]
            {
                new FaqItem { Question = "How long does car transport take?", Answer = "Short intercity routes often complete in one to two days. Long-distance runs across the country typically take five to eight days depending on the route and loading schedule." },
                new FaqItem { Question = "Can I keep belongings in the car?", Answer = "Most carriers ask you not to. Loose items aren't covered under transit terms and add weight. Check with your chosen provider before loading anything." },
                new FaqItem { Question = "How much fuel should be in the tank?", Answer = "Providers generally ask for a quarter tank or less. Enough to drive on and off the carrier, no more." },
                new FaqItem { Question = "What documents are needed?", Answer = "Registration certificate, valid insurance and your ID. Some providers ask for a copy of the pollution certificate as well." }
            }
        },

        new()
        {
            Id = 3,
            Name = "Office Shifting",
            Slug = "office-shifting",
            ShortDescription = "Workstations, IT and files with minimal downtime.",
            IconPath = "M4 17V4h8v13M12 8h4v9M6 7h2M6 10h2M6 13h2",
            HeroTitle = "Relocate the office. Keep the business running.",
            HeroDescription = "Workstations, servers, documents and equipment moved on a planned schedule, usually over a weekend, so your team walks into a working office on Monday.",
            MetaTitle = "Office Shifting Services",
            MetaDescription = "Compare office relocation quotes in India. Workstation dismantling, IT equipment handling, document transfer and setup assistance in one enquiry.",
            Overview = new[]
            {
                "An office move has a deadline a household move doesn't: every hour the team can't work costs money. That changes how the job is planned.",
                "Providers who do commercial relocations work to a sequence, label by department and desk, and often move outside business hours. Tell us your size and timeline and compare how each proposes to handle it."
            },
            Features = new[]
            {
                new ServiceItem { Title = "Office furniture", Body = "Workstations, chairs, storage units and conference tables dismantled and reassembled." },
                new ServiceItem { Title = "Computers and equipment", Body = "Desktops, monitors, servers and network hardware packed with anti-static material." },
                new ServiceItem { Title = "Documents and files", Body = "Sealed, labelled file transfer that keeps records in order and traceable." },
                new ServiceItem { Title = "Packing", Body = "Crates and cartons labelled by department and destination desk." },
                new ServiceItem { Title = "Transportation", Body = "Scheduled runs, often overnight or over a weekend to limit downtime." },
                new ServiceItem { Title = "Setup assistance", Body = "Furniture reassembled and equipment placed according to your floor plan." }
            },
            Benefits = new[]
            {
                new ServiceItem { Title = "Planned around your downtime", Body = "Weekend and after-hours schedules are standard for commercial moves." },
                new ServiceItem { Title = "Labelled by department", Body = "Cartons arrive where they're meant to, so unpacking isn't a treasure hunt." },
                new ServiceItem { Title = "Providers used to IT loads", Body = "Server and network equipment needs different handling from office furniture." },
                new ServiceItem { Title = "Written scope to compare", Body = "Quotes state what's dismantled, moved and reassembled, so you can weigh them properly." }
            },
            Faqs = new[]
            {
                new FaqItem { Question = "Can the move happen over a weekend?", Answer = "Yes, and it's the most common arrangement. Say so in your enquiry so providers price the schedule you actually need." },
                new FaqItem { Question = "Who disconnects the IT equipment?", Answer = "Some providers handle basic disconnection and reconnection; others expect your IT team or vendor to do it. Confirm this before you book." },
                new FaqItem { Question = "How is confidential paperwork handled?", Answer = "Files are typically moved in sealed, numbered crates. If you have specific confidentiality requirements, raise them at the quotation stage." },
                new FaqItem { Question = "How much notice do providers need?", Answer = "For a small office, a week or two. For larger sites needing a survey and phased plan, three to four weeks is more realistic." }
            }
        },

        new()
        {
            Id = 4,
            Name = "Bike Transportation",
            Slug = "bike-transportation",
            ShortDescription = "Two-wheelers crated and shipped city to city.",
            IconPath = "M5 15a2.5 2.5 0 100-5 2.5 2.5 0 000 5zm10 0a2.5 2.5 0 100-5 2.5 2.5 0 000 5zM7.5 12.5L10 6h3",
            HeroTitle = "Your bike, delivered to your new city.",
            HeroDescription = "Pickup from your address, secure crating or carrier loading, and delivery at the other end, with transit support you can actually reach.",
            MetaTitle = "Bike Transportation Services",
            MetaDescription = "Compare two-wheeler transport quotes across India. Bike pickup, secure packing, carrier transport and doorstep delivery in a single enquiry.",
            Overview = new[]
            {
                "Two-wheelers are easy to damage in transit if they're loaded casually. Handlebars, mirrors and exhausts take the hit when bikes are packed too close together.",
                "Providers who move bikes regularly wrap vulnerable parts, secure the bike upright and load it so nothing rests against it. Compare a few offers and check what packing is included."
            },
            Features = new[]
            {
                new ServiceItem { Title = "Bike pickup", Body = "Collection from your address at a scheduled slot, with condition recorded." },
                new ServiceItem { Title = "Protective packing", Body = "Wrapping for the tank, handlebars, mirrors and exhaust before loading." },
                new ServiceItem { Title = "Secure transportation", Body = "Bikes loaded upright and strapped so they don't shift or lean during the run." },
                new ServiceItem { Title = "Doorstep delivery", Body = "Delivery to your new address where the route and access allow." },
                new ServiceItem { Title = "Handling", Body = "Loading and unloading by crew rather than riding the bike on and off." },
                new ServiceItem { Title = "Transit support", Body = "A contact point to check status while the bike is on the road." }
            },
            Benefits = new[]
            {
                new ServiceItem { Title = "Cheaper than riding it there", Body = "For long distances, transport usually costs less than fuel, stays and two days of riding." },
                new ServiceItem { Title = "Combine with a household move", Body = "Bike and home goods can go through the same enquiry." },
                new ServiceItem { Title = "Packing you can see quoted", Body = "Offers state what wrapping is included, which is where cheap quotes usually cut corners." },
                new ServiceItem { Title = "Multiple providers per route", Body = "Popular corridors have several operators, so there's genuine choice." }
            },
            Faqs = new[]
            {
                new FaqItem { Question = "How much fuel should be left in the tank?", Answer = "Almost none. Providers typically ask for the tank to be close to empty before pickup." },
                new FaqItem { Question = "Should I remove accessories?", Answer = "Yes. Take off saddlebags, phone mounts, decorative parts and anything loose. Keep the documents with you, not on the bike." },
                new FaqItem { Question = "How long does bike transport take?", Answer = "Two to four days on shorter intercity routes, and around five to eight for long-distance runs, depending on the schedule." },
                new FaqItem { Question = "Can several bikes go together?", Answer = "Yes, and per-bike cost usually drops. Mention the number of bikes when you send your requirement." }
            }
        },

        new()
        {
            Id = 5,
            Name = "Goods Transportation",
            Slug = "goods-transportation",
            ShortDescription = "Part or full loads for commercial consignments.",
            IconPath = "M3 6h8v8H3V6zm8 3h3l3 3v2h-6V9zM6 17a1.5 1.5 0 100-3 1.5 1.5 0 000 3zm8 0a1.5 1.5 0 100-3 1.5 1.5 0 000 3z",
            HeroTitle = "Commercial loads, moved to schedule.",
            HeroDescription = "Full truckload or part load transport for cargo, stock and equipment, with the vehicle type matched to what you're actually sending.",
            MetaTitle = "Goods Transportation Services",
            MetaDescription = "Compare goods and cargo transportation quotes in India. Full truckload and part load options, loading and unloading, matched to your consignment.",
            Overview = new[]
            {
                "Commercial transport turns on two questions: how much space your consignment needs, and whether you're paying for a whole vehicle or sharing one.",
                "A part load is cheaper but moves on the operator's schedule. A full truckload costs more and goes direct. Getting a few quotes shows you where the crossover sits for your consignment."
            },
            Features = new[]
            {
                new ServiceItem { Title = "Commercial goods", Body = "Stock, raw material, machinery, retail inventory and equipment." },
                new ServiceItem { Title = "Full and part loads", Body = "Book a dedicated vehicle, or share capacity when your consignment doesn't fill one." },
                new ServiceItem { Title = "Vehicle matching", Body = "Tempo, LCV, container or open-body truck chosen to suit weight and dimensions." },
                new ServiceItem { Title = "Loading and unloading", Body = "Labour arranged at both ends where you need it." },
                new ServiceItem { Title = "Transportation", Body = "Intercity and interstate movement with agreed pickup and delivery windows." },
                new ServiceItem { Title = "Consignment paperwork", Body = "Standard transport documentation issued for the consignment." }
            },
            Benefits = new[]
            {
                new ServiceItem { Title = "Right-sized vehicles", Body = "Paying for a 32-foot container to move two pallets is the most common overspend." },
                new ServiceItem { Title = "Route operators, not brokers", Body = "Providers already running your corridor tend to quote sharper." },
                new ServiceItem { Title = "Repeat consignments", Body = "If you ship regularly, comparing once gives you a benchmark to work from." },
                new ServiceItem { Title = "Clear inclusions", Body = "Loading labour is a common hidden cost. Quotes make it visible." }
            },
            Faqs = new[]
            {
                new FaqItem { Question = "What's the difference between full and part load?", Answer = "A full truckload is a vehicle dedicated to your consignment, going direct. A part load shares the vehicle with other consignments, costs less and takes longer." },
                new FaqItem { Question = "How is the price worked out?", Answer = "Mainly by distance, weight or volume, vehicle type, and whether loading labour is included. Fragile or oversized goods can change it." },
                new FaqItem { Question = "Do I need to arrange loading?", Answer = "Not necessarily. Say whether you need labour at pickup, delivery or both, so it's priced into the quote rather than added later." },
                new FaqItem { Question = "Can you handle oversized items?", Answer = "Often yes, but it depends on dimensions and route clearances. Give measurements in your enquiry so providers can respond accurately." }
            }
        },

        new()
        {
            Id = 6,
            Name = "Warehouse & Storage",
            Slug = "warehouse-storage",
            ShortDescription = "Short or long term storage while you settle in.",
            IconPath = "M3 8l7-4 7 4v9H3V8zm4 9v-5h6v5",
            HeroTitle = "Somewhere to keep it until you're ready.",
            HeroDescription = "Short-term and long-term storage for household goods and business stock, when possession dates don't line up or you simply need the space.",
            MetaTitle = "Warehouse & Storage Services",
            MetaDescription = "Compare warehouse and storage quotes in India. Short-term and long-term household and business storage, with transport in and out arranged.",
            Overview = new[]
            {
                "Storage usually gets booked for one of two reasons: the new place isn't ready yet, or there's more stuff than space. Both are common enough that most movers offer it.",
                "What varies is the unit size, how long you're committing for, and how easily you can get to your goods. Compare on those, not just the monthly rate."
            },
            Features = new[]
            {
                new ServiceItem { Title = "Short-term storage", Body = "Weeks or a couple of months while dates settle, often between two legs of a move." },
                new ServiceItem { Title = "Long-term storage", Body = "Monthly arrangements for goods you don't need in the near future." },
                new ServiceItem { Title = "Household storage", Body = "Furniture, appliances and cartons kept packed and inventoried." },
                new ServiceItem { Title = "Business storage", Body = "Stock, records and equipment held off-site to free up working space." },
                new ServiceItem { Title = "Warehouse options", Body = "Racked warehouse space or dedicated units, depending on the provider." },
                new ServiceItem { Title = "Transport in and out", Body = "Movement into storage and delivery out when you need it, arranged together." }
            },
            Benefits = new[]
            {
                new ServiceItem { Title = "Bridges a date gap", Body = "Handover and possession rarely align. Storage stops that becoming a crisis." },
                new ServiceItem { Title = "Pay for the space you use", Body = "Unit sizes vary, so a small consignment shouldn't be charged as a large one." },
                new ServiceItem { Title = "One provider, two jobs", Body = "Using the same provider for the move and the storage avoids double handling." },
                new ServiceItem { Title = "Terms you can compare", Body = "Notice periods and access rules differ more than headline rates do." }
            },
            Faqs = new[]
            {
                new FaqItem { Question = "How is storage priced?", Answer = "Usually per month, based on the space or unit size your goods occupy. Transport in and out is normally quoted separately." },
                new FaqItem { Question = "Can I access my goods while they're stored?", Answer = "It varies. Some facilities allow scheduled access; warehouse-racked storage often doesn't. Ask before committing if access matters to you." },
                new FaqItem { Question = "Is there a minimum period?", Answer = "Many providers set a one-month minimum. Confirm the notice period for taking goods out as well." },
                new FaqItem { Question = "How are goods kept track of?", Answer = "Reputable providers inventory items on the way in and issue you a copy. Ask for it, and check it before you sign." }
            }
        },

        new()
        {
            Id = 7,
            Name = "Packers & Movers",
            Slug = "packers-movers",
            ShortDescription = "Packing, loading, transit and unpacking.",
            IconPath = "M3 7l7-3 7 3-7 3-7-3zm0 0v6l7 3 7-3V7",
            HeroTitle = "The full job, handled end to end.",
            HeroDescription = "When you'd rather hand the whole thing over: packing, loading, transport, unloading and unpacking arranged as one service.",
            MetaTitle = "Packers & Movers",
            MetaDescription = "Compare packers and movers quotes across India. Full-service packing, loading, transport and unpacking from verified providers.",
            Overview = new[]
            {
                "\"Packers and movers\" is the full-service option: you point at your belongings and a crew does the rest, from wrapping the crockery to putting the bed back together.",
                "It costs more than transport alone. Whether it's worth it depends on how much you're moving and how much of your own time you want to spend on it."
            },
            Features = new[]
            {
                new ServiceItem { Title = "Professional packing", Body = "Materials and technique matched to each category of item." },
                new ServiceItem { Title = "Labelled inventory", Body = "Cartons numbered and listed so you know what went where." },
                new ServiceItem { Title = "Loading", Body = "Crews load in a planned order to protect fragile items in transit." },
                new ServiceItem { Title = "Transportation", Body = "Vehicle sized to your consignment for a local or long-distance run." },
                new ServiceItem { Title = "Unloading", Body = "Delivery into the right rooms rather than a pile at the door." },
                new ServiceItem { Title = "Unpacking", Body = "Optional unpacking and debris removal at the destination." }
            },
            Benefits = new[]
            {
                new ServiceItem { Title = "One provider, whole job", Body = "No coordinating a packer, a truck and labour separately." },
                new ServiceItem { Title = "Fewer breakages", Body = "Most transit damage traces back to packing, not driving." },
                new ServiceItem { Title = "Your time back", Body = "Packing a full household typically takes days spread over a fortnight." },
                new ServiceItem { Title = "Comparable scope", Body = "Quotes spell out which of these stages are actually included." }
            },
            Faqs = new[]
            {
                new FaqItem { Question = "Is unpacking always included?", Answer = "No. It's often quoted as an add-on. Check the line items before you compare two prices." },
                new FaqItem { Question = "Do they supply packing material?", Answer = "Yes, in a full-service quote. Material cost is usually built into the price rather than billed separately." },
                new FaqItem { Question = "How long does packing take?", Answer = "A two-bedroom home is commonly packed in a day. Larger homes may need two, with loading the following morning." },
                new FaqItem { Question = "Should I pack valuables myself?", Answer = "Yes. Jewellery, cash, documents and small electronics are best carried personally rather than loaded." }
            }
        },

        new()
        {
            Id = 8,
            Name = "Truck / Tempo",
            Slug = "truck-tempo",
            ShortDescription = "Hire a tempo or truck on demand for smaller jobs.",
            IconPath = "M3 7h9v7H3V7zm9 2h2.5L17 12v2h-5V9zM6 17a1.5 1.5 0 100-3 1.5 1.5 0 000 3z",
            HeroTitle = "Just need the vehicle? Book one.",
            HeroDescription = "Tempo and truck hire for smaller shifts, single-item moves and one-off runs, with or without loading labour.",
            MetaTitle = "Truck & Tempo Hire",
            MetaDescription = "Compare tempo and truck hire quotes in India. On-demand vehicle hire for small moves, single items and one-off transport runs.",
            Overview = new[]
            {
                "Not every move needs a full packing crew. Shifting a few rooms across town, or sending one large item across the city, is often just a vehicle-and-labour job.",
                "Pricing turns on vehicle size, distance and whether you want loading help. Say which you need and compare what comes back."
            },
            Features = new[]
            {
                new ServiceItem { Title = "Vehicle choice", Body = "Small tempo through to larger trucks, matched to what you're shifting." },
                new ServiceItem { Title = "On-demand booking", Body = "Single runs rather than a full relocation package." },
                new ServiceItem { Title = "Optional labour", Body = "Add loading and unloading help, or handle it yourself and pay less." },
                new ServiceItem { Title = "Local and intercity", Body = "Within-city runs as well as shorter intercity trips." },
                new ServiceItem { Title = "Single-item moves", Body = "One sofa, one fridge, one machine, without paying for a full move." },
                new ServiceItem { Title = "Flexible timing", Body = "Same-day and next-day slots are common where operators have capacity." }
            },
            Benefits = new[]
            {
                new ServiceItem { Title = "Pay for what you use", Body = "No packing charges on a job that doesn't need packing." },
                new ServiceItem { Title = "Quick to arrange", Body = "Smaller jobs need far less notice than a full household relocation." },
                new ServiceItem { Title = "Right vehicle size", Body = "Local operators know what fits, which avoids a second trip." },
                new ServiceItem { Title = "Labour is optional", Body = "Decide once you can see what adding it costs." }
            },
            Faqs = new[]
            {
                new FaqItem { Question = "What size vehicle do I need?", Answer = "Describe what you're moving in your enquiry and providers will suggest a size. Guessing low usually ends in a second trip." },
                new FaqItem { Question = "Is loading help included?", Answer = "Not by default. Ask for it explicitly so the quote reflects it." },
                new FaqItem { Question = "Can I book for the same day?", Answer = "Often, for local runs, depending on the time of day and availability. Longer distances need more notice." },
                new FaqItem { Question = "Is this cheaper than full packers and movers?", Answer = "For small jobs, usually. For a full household you'd typically end up paying for the labour separately anyway." }
            }
        }
    };

    public IReadOnlyList<Service> GetAll() =>
        Items.Where(s => s.IsActive).ToList();

    public Service? GetBySlug(string slug) =>
        Items.FirstOrDefault(s =>
            s.IsActive && string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase));
}