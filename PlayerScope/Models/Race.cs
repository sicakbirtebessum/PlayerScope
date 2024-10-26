using System.Collections.Generic;
using System;
using System.Linq;
using static PlayerScope.Models.GenderSubRace;

namespace PlayerScope.Models;
//https://github.com/Ottermandias/Penumbra.GameData/blob/main/Enums/Race.cs
/// <summary> Available character races for players. </summary>
public enum Race : byte
{
    Unknown,
    Hyur,
    Elezen,
    Lalafell,
    Miqote,
    Roegadyn,
    AuRa,
    Hrothgar,
    Viera,
}

/// <summary> Available character genders. </summary>
public enum Gender : byte
{
    Male,
    Female,
    Unknown,
}

/// <summary> Available model races, which includes Highlanders as a separate model base to Midlanders. </summary>
public enum ModelRace : byte
{
    Unknown,
    Midlander,
    Highlander,
    Elezen,
    Lalafell,
    Miqote,
    Roegadyn,
    AuRa,
    Hrothgar,
    Viera,
}

/// <summary> Available sub-races or clans for player characters. </summary>
public enum SubRace : byte
{
    Unknown,
    Midlander,
    Highlander,
    Wildwood,
    Duskwight,
    Plainsfolk,
    Dunesfolk,
    SeekerOfTheSun,
    KeeperOfTheMoon,
    Seawolf,
    Hellsguard,
    Raen,
    Xaela,
    Helion,
    Lost,
    Rava,
    Veena,
}

/// <summary> The combined gender-race-npc numerical code as used by the game. </summary>
public enum GenderSubRace : short
{
    Unknown = 0,
    MidlanderMale = 1,
    MidlanderFemale = 2,
    HighlanderMale = 3,
    HighlanderFemale = 4,

    WildwoodMale = 5,
    WildwoodFemale = 6,
    DuskwightMale = 7,
    DuskwightFemale = 8,

    PlainsfolkMale = 9,
    PlainsfolkFemale = 10,
    DunesfolkMale = 11,
    DunesfolkFemale = 12,

    SeekerOfTheSunMale = 13,
    SeekerOfTheSunFemale = 14,
    KeeperOfTheMoonMale = 15,
    KeeperOfTheMoonFemale = 16,

    SeawolfMale = 17,
    SeawolfFemale = 18,
    HellsguardMale = 19,
    HellsguardFemale = 20,

    RaenMale = 21,
    RaenFemale = 22,
    XaelaMale = 23,
    XaelaFemale = 24,

    HelionMale = 25,
    HelionFemale = 26,
    LostMale = 27,
    LostFemale = 28,

    RavaMale = 29,
    RavaFemale = 30,
    VeenaMale = 31,
    VeenaFemale = 32,
}

public static class RaceEnumExtensions
{
    /// <summary> Convert a ModelRace to a Race, i.e. Midlander and Highlander to Hyur. </summary>
    public static Race ToRace(this ModelRace race)
        => race switch
        {
            ModelRace.Unknown => Race.Unknown,
            ModelRace.Midlander => Race.Hyur,
            ModelRace.Highlander => Race.Hyur,
            ModelRace.Elezen => Race.Elezen,
            ModelRace.Lalafell => Race.Lalafell,
            ModelRace.Miqote => Race.Miqote,
            ModelRace.Roegadyn => Race.Roegadyn,
            ModelRace.AuRa => Race.AuRa,
            ModelRace.Hrothgar => Race.Hrothgar,
            ModelRace.Viera => Race.Viera,
            _ => Race.Unknown,
        };

    /// <summary> Convert a clan to its race. </summary>
    public static Race ToRace(this SubRace subRace)
        => subRace switch
        {
            SubRace.Unknown => Race.Unknown,
            SubRace.Midlander => Race.Hyur,
            SubRace.Highlander => Race.Hyur,
            SubRace.Wildwood => Race.Elezen,
            SubRace.Duskwight => Race.Elezen,
            SubRace.Plainsfolk => Race.Lalafell,
            SubRace.Dunesfolk => Race.Lalafell,
            SubRace.SeekerOfTheSun => Race.Miqote,
            SubRace.KeeperOfTheMoon => Race.Miqote,
            SubRace.Seawolf => Race.Roegadyn,
            SubRace.Hellsguard => Race.Roegadyn,
            SubRace.Raen => Race.AuRa,
            SubRace.Xaela => Race.AuRa,
            SubRace.Helion => Race.Hrothgar,
            SubRace.Lost => Race.Hrothgar,
            SubRace.Rava => Race.Viera,
            SubRace.Veena => Race.Viera,
            _ => Race.Unknown,
        };

    /// <summary> Obtain a human-readable name for a ModelRace. </summary>
    public static string ToName(this ModelRace modelRace)
        => modelRace switch
        {
            ModelRace.Midlander => SubRace.Midlander.ToName(),
            ModelRace.Highlander => SubRace.Highlander.ToName(),
            ModelRace.Elezen => Race.Elezen.ToName(),
            ModelRace.Lalafell => Race.Lalafell.ToName(),
            ModelRace.Miqote => Race.Miqote.ToName(),
            ModelRace.Roegadyn => Race.Roegadyn.ToName(),
            ModelRace.AuRa => Race.AuRa.ToName(),
            ModelRace.Hrothgar => Race.Hrothgar.ToName(),
            ModelRace.Viera => Race.Viera.ToName(),
            _ => Race.Unknown.ToName(),
        };

    /// <summary> Obtain a human-readable name for Race. </summary>
    public static string ToName(this Race race)
        => race switch
        {
            Race.Hyur => "Hyur",
            Race.Elezen => "Elezen",
            Race.Lalafell => "Lalafell",
            Race.Miqote => "Miqo'te",
            Race.Roegadyn => "Roegadyn",
            Race.AuRa => "Au Ra",
            Race.Hrothgar => "Hrothgar",
            Race.Viera => "Viera",
            _ => "Unknown",
        };

    public static string ToRaceName(this GenderSubRace race)
       => race switch
       {
           MidlanderMale => "Hyur",
           MidlanderFemale => "Hyur",
           HighlanderMale => "Hyur",
           HighlanderFemale => "Hyur",

           WildwoodMale => "Elezen",
           WildwoodFemale => "Elezen",
           DuskwightMale => "Elezen",
           DuskwightFemale => "Elezen",

           PlainsfolkMale => "Lalafell",
           PlainsfolkFemale => "Lalafell",
           DunesfolkMale => "Lalafell",
           DunesfolkFemale => "Lalafell",

           SeekerOfTheSunMale => "Miqo'te",
           SeekerOfTheSunFemale => "Miqo'te",
           KeeperOfTheMoonMale => "Miqo'te",
           KeeperOfTheMoonFemale => "Miqo'te",

           SeawolfMale => "Roegadyn",
           SeawolfFemale => "Roegadyn",
           HellsguardMale => "Roegadyn",
           HellsguardFemale => "Roegadyn",

           RaenMale => "Au Ra",
           RaenFemale => "Au Ra",
           XaelaMale => "Au Ra",
           XaelaFemale => "Au Ra",

           HelionMale => "Hrothgar",
           HelionFemale => "Hrothgar",
           LostMale => "Hrothgar",
           LostFemale => "Hrothgar",

           RavaMale => "Viera",
           RavaFemale => "Viera",
           VeenaMale => "Viera",
           VeenaFemale => "Viera",
           _ => "Unknown",
       };

    public static string ToSubRaceName(this GenderSubRace race)
       => race switch
       {
           MidlanderMale => "Midlander",
           MidlanderFemale => "Midlander",
           HighlanderMale => "Highlander",
           HighlanderFemale => "Highlander",

           WildwoodMale => "Wildwood",
           WildwoodFemale => "Wildwood",
           DuskwightMale => "Duskwight",
           DuskwightFemale => "Duskwight",

           PlainsfolkMale => "Plainsfolk",
           PlainsfolkFemale => "Plainsfolk",
           DunesfolkMale => "Dunesfolk",
           DunesfolkFemale => "Dunesfolk",

           SeekerOfTheSunMale => "Seeker Of The Sun",
           SeekerOfTheSunFemale => "Seeker Of The Sun",
           KeeperOfTheMoonMale => "Keeper Of The Moon",
           KeeperOfTheMoonFemale => "Keeper Of The Moon",

           SeawolfMale => "Seawolf",
           SeawolfFemale => "Seawolf",
           HellsguardMale => "Hellsguard",
           HellsguardFemale => "Hellsguard",

           RaenMale => "Raen",
           RaenFemale => "Raen",
           XaelaMale => "Xaela",
           XaelaFemale => "Xaela",

           HelionMale => "Helion",
           HelionFemale => "Helion",
           LostMale => "Lost",
           LostFemale => "Lost",

           RavaMale => "Rava",
           RavaFemale => "Rava",
           VeenaMale => "Veena",
           VeenaFemale => "Veena",
           _ => "Unknown",
       };

    /// <summary> Obtain a human-readable name for Gender. </summary>
    public static string ToName(this Gender gender)
        => gender switch
        {
            Gender.Male => "Male",
            Gender.Female => "Female",
            _ => "Unknown",
        };

    /// <summary> Obtain a human-readable name for SubRace. </summary>
    public static string ToName(this SubRace subRace)
        => subRace switch
        {
            SubRace.Midlander => "Midlander",
            SubRace.Highlander => "Highlander",
            SubRace.Wildwood => "Wildwood",
            SubRace.Duskwight => "Duskwight",
            SubRace.Plainsfolk => "Plainsfolk",
            SubRace.Dunesfolk => "Dunesfolk",
            SubRace.SeekerOfTheSun => "Seeker Of The Sun",
            SubRace.KeeperOfTheMoon => "Keeper Of The Moon",
            SubRace.Seawolf => "Seawolf",
            SubRace.Hellsguard => "Hellsguard",
            SubRace.Raen => "Raen",
            SubRace.Xaela => "Xaela",
            SubRace.Helion => "Hellion",
            SubRace.Lost => "Lost",
            SubRace.Rava => "Rava",
            SubRace.Veena => "Veena",
            _ => "Unknown",
        };


    /// <summary> Obtain abbreviated names for SubRace. </summary>
    public static string ToShortName(this SubRace subRace)
        => subRace switch
        {
            SubRace.SeekerOfTheSun => "Sunseeker",
            SubRace.KeeperOfTheMoon => "Moonkeeper",
            _ => subRace.ToName(),
        };

    /// <summary> Check if a clan and race agree. </summary>
    public static bool FitsRace(this SubRace subRace, Race race)
        => subRace.ToRace() == race;

    /// <summary> Split a combined GenderRace into its corresponding Gender and ModelRace. </summary>
    public static (Gender Gender, ModelRace ModelRace) SplitRace(this GenderSubRace value)
    {
        return value switch
        {
            Unknown => (Gender.Unknown, ModelRace.Unknown),

            MidlanderMale => (Gender.Male, ModelRace.Midlander),
            MidlanderFemale => (Gender.Female, ModelRace.Midlander),
            HighlanderMale => (Gender.Male, ModelRace.Highlander),
            HighlanderFemale => (Gender.Female, ModelRace.Highlander),

            WildwoodMale => (Gender.Male, ModelRace.Elezen),
            WildwoodFemale => (Gender.Female, ModelRace.Elezen),
            DuskwightMale => (Gender.Male, ModelRace.Elezen),
            DuskwightFemale => (Gender.Female, ModelRace.Elezen),

            PlainsfolkMale => (Gender.Male, ModelRace.Lalafell),
            PlainsfolkFemale => (Gender.Female, ModelRace.Lalafell),
            DunesfolkMale => (Gender.Male, ModelRace.Lalafell),
            DunesfolkFemale => (Gender.Female, ModelRace.Lalafell),

            SeekerOfTheSunMale => (Gender.Male, ModelRace.Miqote),
            SeekerOfTheSunFemale => (Gender.Female, ModelRace.Miqote),
            KeeperOfTheMoonMale => (Gender.Male, ModelRace.Miqote),
            KeeperOfTheMoonFemale => (Gender.Female, ModelRace.Miqote),

            SeawolfMale => (Gender.Male, ModelRace.Roegadyn),
            SeawolfFemale => (Gender.Female, ModelRace.Roegadyn),
            HellsguardMale => (Gender.Male, ModelRace.Roegadyn),
            HellsguardFemale => (Gender.Female, ModelRace.Roegadyn),

            RaenMale => (Gender.Male, ModelRace.AuRa),
            RaenFemale => (Gender.Female, ModelRace.AuRa),
            XaelaMale => (Gender.Male, ModelRace.AuRa),
            XaelaFemale => (Gender.Female, ModelRace.AuRa),

            HelionMale => (Gender.Male, ModelRace.Hrothgar),
            HelionFemale => (Gender.Female, ModelRace.Hrothgar),
            LostMale => (Gender.Male, ModelRace.Hrothgar),
            LostFemale => (Gender.Female, ModelRace.Hrothgar),

            RavaMale => (Gender.Male, ModelRace.Viera),
            RavaFemale => (Gender.Female, ModelRace.Viera),
            VeenaMale => (Gender.Male, ModelRace.Viera),
            VeenaFemale => (Gender.Female, ModelRace.Viera),
            _ => (Gender.Unknown, ModelRace.Unknown),
        };
    }

    /// <summary> Check if a GenderRace code is valid. </summary>
    public static bool IsValid(this GenderSubRace value)
        => value != Unknown && Enum.IsDefined(typeof(GenderSubRace), value);

    /// <summary> Combine a Gender and a SubRace to a combined GenderSubRace. </summary>
    public static GenderSubRace CombinedRace(Gender gender, SubRace modelSubRace)
    {
        return gender switch
        {
            Gender.Male => modelSubRace switch
            {
                SubRace.Midlander => MidlanderMale,
                SubRace.Highlander => HighlanderMale,
                SubRace.Wildwood => WildwoodMale,
                SubRace.Duskwight => DuskwightMale,
                SubRace.Plainsfolk => PlainsfolkMale,
                SubRace.Dunesfolk => DunesfolkMale,
                SubRace.SeekerOfTheSun => SeekerOfTheSunMale,
                SubRace.KeeperOfTheMoon => KeeperOfTheMoonMale,
                SubRace.Seawolf => SeawolfMale,
                SubRace.Hellsguard => HellsguardMale,
                SubRace.Raen => RaenMale,
                SubRace.Xaela => XaelaMale,
                SubRace.Helion => HelionMale,
                SubRace.Lost => LostMale,
                SubRace.Rava => RavaMale,
                SubRace.Veena => VeenaMale,
                _ => Unknown,
            },
            Gender.Female => modelSubRace switch
            {
                SubRace.Midlander => MidlanderFemale,
                SubRace.Highlander => HighlanderFemale,
                SubRace.Wildwood => WildwoodFemale,
                SubRace.Duskwight => DuskwightFemale,
                SubRace.Plainsfolk => PlainsfolkFemale,
                SubRace.Dunesfolk => DunesfolkFemale,
                SubRace.SeekerOfTheSun => SeekerOfTheSunFemale,
                SubRace.KeeperOfTheMoon => KeeperOfTheMoonFemale,
                SubRace.Seawolf => SeawolfFemale,
                SubRace.Hellsguard => HellsguardFemale,
                SubRace.Raen => RaenFemale,
                SubRace.Xaela => XaelaFemale,
                SubRace.Helion => HelionFemale,
                SubRace.Lost => LostFemale,
                SubRace.Rava => RavaFemale,
                SubRace.Veena => VeenaFemale,
                _ => Unknown,
            },
            _ => Unknown,
        };
    }
}