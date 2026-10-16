using Microsoft.EntityFrameworkCore;

namespace Desk.Core.Data;

// Starting lists, taken from MSShift (2026-10-16) and cleaned up. Admins change them
// later in Settings. Cleanup notes are in docs/phase1-lists.md.
public static class SeedData
{
    public static void Apply(ModelBuilder m)
    {
        m.Entity<Hotel>().HasData(
            new Hotel { Id = 1, Code = "SHO", Name = "Shore Hotel", Address = "1515 Ocean Ave, Santa Monica, CA" },
            new Hotel { Id = 2, Code = "SMH", Name = "Santa Monica Hotel" },
            new Hotel { Id = 3, Code = "OVH", Name = "Ocean View Hotel" });

        string[] departments =
        [
            "Accounting", "Admin", "Concierge", "Engineering", "F&B Kitchen", "F&B Mini Bar", "F&B Restaurant",
            "Finance", "Guest Services", "Housekeeping", "Human Resources", "IT", "Laundry", "Ownership",
            "Parking and Valet", "Reservations", "Revenue Management", "Safety", "Sales",
        ];
        m.Entity<Department>().HasData(departments.Select((n, i) => new Department { Id = i + 1, Name = n }));

        // Areas (first level). HotelId null = applies to every hotel.
        (int? hotel, string name)[] areas =
        [
            (1, "A Building 1st Floor"), (1, "A Building 2nd Floor"), (1, "A Building 3rd Floor"), (1, "A Building 4th Floor"),
            (1, "B Building 1st Floor"), (1, "B Building 2nd Floor"), (1, "B Building 3rd Floor"), (1, "B Building 4th Floor"),
            (1, "Garage"), (1, "Hallway"), (1, "Mezzanine"), (1, "Parking Structure"), (1, "Property (grounds)"), (1, "BPT"),
            (2, "Santa Monica Hotel"), (3, "Ocean View Hotel"),
            (null, "Guest Room"), (null, "IDF"), (null, "Server Room"), (null, "Lost and Found"), (null, "Remote"),
        ];
        m.Entity<LocationArea>().HasData(areas.Select((a, i) => new LocationArea
        {
            Id = i + 1, HotelId = a.hotel, Name = a.name, SortOrder = (i + 1) * 10
        }));
    }
}
