using Microsoft.EntityFrameworkCore;

namespace Desk.Core.Data;

// Starting pick-lists for the report forms (phase 2), taken from MSShift (2026-10-16)
// and cleaned up. Cleanup notes and lists still to check are in docs/phase2-lists.md.
// Ids are numbered in the order below and the database is seeded from them, so never
// insert or remove names here (that would renumber later lists): change lists in the app.
// A NEW list may be added at the very end (its ids follow the last ones).
public static class SeedLists
{
    static readonly (string Key, string[] Names)[] Lists =
    [
        (ListKeys.IncidentType,
        [
            "Alleged Bug Bites", "Blackout/Power Outage", "Bugs in Room", "Burglary", "Chemical Fumes", "Chemical Spill",
            "Cyber Security Breach", "Damaged Property", "Defrauding an Innkeeper - Arrest", "Drill", "Earthquake",
            "Elevator Entrapment", "Employee Opened Guest Door with Key Card", "Employee Violation - Altercation",
            "Employee Violation - Key Control", "Employee Violation - Other", "Employee Violation - Traffic/Parking",
            "Employee Violation - Unauthorized Entry", "Eviction - Non-Registered Guest", "Eviction - Registered Guest",
            "Fire", "Fire/Smoke Detector Alarm", "Flood", "Gasoline Leak", "Guest Altercation",
            "Guest Harassing/Abuse to Staff/Other", "Guest Incident", "Guest Intoxication", "Guest Traffic/Parking Violation",
            "In-House Guest Not Properly Checked In", "Informational", "Informational - No Crime", "Missing Money",
            "Missing Person - Adult", "Missing Person - Child", "Natural Gas Leak", "Noise Disturbance", "Possible Bed Bugs",
            "Possible Missing Friend of Guest", "Property Missing/Damaged", "Safety Hazard", "Scam/Fraud",
            "Smoking in Guest Room", "Smoking in Unauthorized Area", "Suspicious Odor", "Trespassing - Warned, No Arrest",
            "Trespassing - Arrest", "Unknown Substance", "Unpaid/Defrauding an Innkeeper", "Vandalism", "Welfare Check",
            "Welfare Check in Room",
        ]),
        (ListKeys.HighLevelType,
        [
            "Assault", "Assault with Weapon", "BOLO", "Bomb Threat", "Burglary", "Computer Room Breach",
            "Death/Suicide/Murder", "Defrauding an Innkeeper - Arrest", "Domestic Assault", "Employee Violation - Drugs",
            "Employee Violation - Harassment", "Employee Violation - Liquor", "Fire Alarm", "Fraud", "Guest Harassing/Abuse",
            "Identity Theft/Fraud", "Phone Call Impersonation", "Robbery", "Sexual Assault", "Sexual Offense",
            "SMS Impersonation", "System Breach", "Theft (over $950)", "Threats/Harassment", "Trespassing - Arrest",
        ]),
        (ListKeys.InjuryType,
        [
            "Abrasion/Scrape", "Allergic Reaction", "Bite/Sting", "Bruise/Contusion", "Burn", "Concussion/Head Injury",
            "Cut/Laceration", "Dislocation", "Eye Injury", "Food Illness", "Fracture", "Illness - Other", "Pain/Soreness",
            "Puncture", "Sprain/Strain", "Other",
        ]),
        (ListKeys.BodyPart,
        [
            "Ankle", "Arm", "Back", "Chest", "Ear", "Elbow", "Eye", "Face", "Finger", "Foot", "Hand", "Head", "Hip",
            "Knee", "Leg", "Mouth", "Neck", "Nose", "Shoulder", "Stomach", "Teeth", "Toe", "Wrist", "Multiple/Whole Body",
        ]),
        (ListKeys.BodyPartLocation,
        [
            "Back", "Both", "Face", "Front", "Head-Top", "Left", "Left Dominant", "Lower", "Right", "Right Dominant", "Upper",
        ]),
        (ListKeys.ContributingFactor,
        [
            "Slip/Fall", "Trip/Fall", "Slippery Floor", "Uneven Concrete", "Lack of PPE", "Unsafe Work Practice",
            "Unsafe Working Condition", "Push/Pull/Lift", "Struck by Object", "Chemical", "Inhalation", "Ingestion",
            "Medical Condition", "Weather", "Weapon", "Motor Vehicle Collision", "Needle/Sharp", "Other",
        ]),
        (ListKeys.PropertyCategory,
        [
            "Alcohol", "Appliance", "Art", "Baby", "Beauty/Hygiene", "Bedding", "Books", "Cell Phone", "Clothing",
            "Computer", "Dishware", "Dog & Cat", "Electronics", "Glasses", "Jewelry", "Luggage", "Medication", "Misc",
            "Money", "Musical Equipment", "Office Supplies", "Passport", "Personal Items", "Shoes", "Sport", "Toys",
            "Weapons", "Vehicle", "Hotel Property", "Other",
        ]),
        (ListKeys.Color,
        [
            "Beige", "Black", "Blue", "Brown", "Burgundy", "Clear", "Copper", "Cream", "Gold", "Gray", "Green",
            "Multi-Color", "Navy", "Orange", "Pink", "Purple", "Red", "Silver", "Tan", "Teal", "Turquoise", "White",
            "Yellow", "Other",
        ]),
        (ListKeys.VehicleCircumstance,
        [
            "Abandoned", "Hit and Run", "Stolen", "Towed", "Vehicle Break-in", "Damaged by Guest", "Damaged by Associate", "Other",
        ]),
        (ListKeys.EvidenceStatus, ["In Storage", "Sent to Claims", "Sent to Attorney", "Destroyed"]),
        // Phase 2 event log (2026-10-19).
        (ListKeys.EventCategory,
        [
            "Administrative", "Alarm Activation", "Call Off", "Cybersecurity", "Desk Duty", "Door Lock Assist", "Elevator",
            "Employee Assist", "Escort", "Eviction", "Field Contact", "Fire Command Station", "Fire Drill",
            "Fire Extinguisher", "Fire Watch", "Guest Assist", "Guest Warning", "Hard Key", "Hazards", "Investigation",
            "Key Concern - Employee", "Lock Interrogation", "Lockout", "Lost and Found", "Medical", "Meeting",
            "Monthly Inventory", "Officer", "Orange Safety Card", "Packages and Mail", "Parking", "Safe Assist", "Safety",
            "Vendor", "Water Leak/Plumbing", "Wellness Check", "Wheelchair", "Other",
        ]),
    ];

    public static void Apply(ModelBuilder m)
    {
        var id = 0;
        var items = new List<ListItem>();
        foreach (var (key, names) in Lists)
            items.AddRange(names.Select((n, i) => new ListItem { Id = ++id, ListKey = key, Name = n, SortOrder = (i + 1) * 10 }));
        m.Entity<ListItem>().HasData(items);
    }
}
