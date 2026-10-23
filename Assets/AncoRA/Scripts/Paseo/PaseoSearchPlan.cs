using System;
using System.Collections.Generic;
using System.Linq;

namespace AncorRA.AR
{
    /// <summary>Why an attempt searched what it searched: every map, the building nearest by GPS (or its rank), the
    /// building already located in view, the order of the configuration (no GPS), or a building the camera faces by
    /// the compass (1.3.5).</summary>
    public enum PaseoSearchReason { All, Gps, InView, Order, Compass }

    /// <summary>A building of the tour as the search sees it: its maps and, when edificio.json has it, its GPS centre.</summary>
    public readonly struct PaseoSearchBuilding
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int[] MapIds;
        public readonly bool HasGps;
        public readonly double Latitude;
        public readonly double Longitude;

        public PaseoSearchBuilding(string id, string name, int[] mapIds, double latitude, double longitude)
        {
            Id = id;
            Name = name;
            MapIds = mapIds;
            HasGps = true;
            Latitude = latitude;
            Longitude = longitude;
        }

        public PaseoSearchBuilding(string id, string name, int[] mapIds)
        {
            Id = id;
            Name = name;
            MapIds = mapIds;
            HasGps = false;
            Latitude = Longitude = 0.0;
        }
    }

    /// <summary>What one localization attempt searches. BuildingId null means every map.</summary>
    public readonly struct PaseoSearch
    {
        public readonly string BuildingId;
        public readonly string BuildingName;
        public readonly int[] MapIds;
        public readonly PaseoSearchReason Reason;
        // Phone to the building centre by GPS; NaN without a fix or for a search over every map.
        public readonly double GpsDistanceMeters;
        // Heading of the back camera when the attempt was chosen (NaN without a usable one), and how far the GPS bearing from
        // the phone to the searched building is from it (NaN without a building, a GPS fix or a heading). Logged, even when
        // the team turned the ranking by compass off, to compare both on the same walk.
        public readonly double HeadingDegrees;
        public readonly double HeadingDifferenceDegrees;

        public PaseoSearch(string buildingId, string buildingName, int[] mapIds, PaseoSearchReason reason, double gpsDistanceMeters,
            double headingDegrees = double.NaN, double headingDifferenceDegrees = double.NaN)
        {
            BuildingId = buildingId;
            BuildingName = buildingName;
            MapIds = mapIds;
            Reason = reason;
            GpsDistanceMeters = gpsDistanceMeters;
            HeadingDegrees = headingDegrees;
            HeadingDifferenceDegrees = headingDifferenceDegrees;
        }
    }

    /// <summary>The phone's GPS position, usable only when it is fresh and accurate enough to tell the buildings apart.</summary>
    public readonly struct PaseoGpsFix
    {
        // The buildings stand 21-60 m apart: a worse fix cannot tell which one is nearest.
        public const float MaxAccuracyMeters = 50f;
        public const double MaxAgeSeconds = 30.0;

        public readonly bool Valid;
        public readonly double Latitude;
        public readonly double Longitude;
        public readonly float AccuracyMeters;

        PaseoGpsFix(double latitude, double longitude, float accuracyMeters)
        {
            Valid = true;
            Latitude = latitude;
            Longitude = longitude;
            AccuracyMeters = accuracyMeters;
        }

        public static PaseoGpsFix None => default;

        public static PaseoGpsFix From(bool running, double latitude, double longitude, float accuracyMeters, double ageSeconds) =>
            running && accuracyMeters > 0f && accuracyMeters <= MaxAccuracyMeters && ageSeconds <= MaxAgeSeconds
                ? new PaseoGpsFix(latitude, longitude, accuracyMeters)
                : None;
    }

    public static class PaseoGeo
    {
        const double EarthRadiusMeters = 6378137.0;

        /// <summary>Ground distance; flat-earth approximation, exact enough across a campus.</summary>
        public static double DistanceMeters(double latitudeA, double longitudeA, double latitudeB, double longitudeB)
        {
            Offset(latitudeA, longitudeA, latitudeB, longitudeB, out double north, out double east);
            return Math.Sqrt(north * north + east * east);
        }

        /// <summary>
        /// Compass bearing from A to B in degrees, 0 = north, clockwise, in [0, 360); flat-earth like
        /// <see cref="DistanceMeters"/>. Two equal points give 0.
        /// </summary>
        public static double BearingDegrees(double latitudeA, double longitudeA, double latitudeB, double longitudeB)
        {
            Offset(latitudeA, longitudeA, latitudeB, longitudeB, out double north, out double east);
            return NormalizeDegrees(Math.Atan2(east, north) * 180.0 / Math.PI);
        }

        /// <summary>An angle in [0, 360).</summary>
        public static double NormalizeDegrees(double degrees)
        {
            // The second % turns the 360.0 that a tiny negative angle rounds to into 0.
            return (degrees % 360.0 + 360.0) % 360.0;
        }

        /// <summary>The smaller angle between two bearings, in [0, 180].</summary>
        public static double AngleDifferenceDegrees(double degreesA, double degreesB)
        {
            double difference = NormalizeDegrees(degreesA - degreesB);
            return difference > 180.0 ? 360.0 - difference : difference;
        }

        // Metres from A to B, north and east.
        static void Offset(double latitudeA, double longitudeA, double latitudeB, double longitudeB, out double north, out double east)
        {
            double toRadians = Math.PI / 180.0;
            north = (latitudeB - latitudeA) * toRadians * EarthRadiusMeters;
            east = (longitudeB - longitudeA) * toRadians * EarthRadiusMeters * Math.Cos((latitudeA + latitudeB) * 0.5 * toRadians);
        }
    }

    /// <summary>
    /// Chooses the maps each localization attempt searches. The SDK's device localization searches every loaded map, and
    /// in the field test of 14-oct a try against the seven maps took 9.5 s on average when nothing matched (0.7 s when the
    /// first map did). One building per attempt makes a failed try several times shorter, so the building the user
    /// faces is tried far more often. Buildings are ranked by the one already located in view, then (1.3.5) those the
    /// camera faces by the compass, then by GPS distance (then the configuration order), and each cycle of six attempts
    /// searches ranks 0, 1, 0, 2, 0 and finally every map, in case GPS points the wrong way. Without GPS nor a building in
    /// view every attempt searches every map.
    /// In the field logs of 21 and 23-oct only 11-27 % of the attempts the GPS ranking chose matched, against 67-100 % when
    /// the building was already in view: the nearest building is not the one the user looks at. With a usable GPS fix AND a
    /// usable heading of the camera (PaseoCompass), the buildings whose GPS bearing from the phone lies within
    /// <see cref="ViewHalfAngleDegrees"/> of the heading come first, nearest first.
    /// </summary>
    public sealed class PaseoSearchPlan
    {
        static readonly int[] Cycle = { 0, 1, 0, 2, 0, -1 }; // -1: every map

        /// <summary>
        /// Half the angle in front of the camera in which a building counts as faced. The phone camera sees about 45-70°
        /// across, so this is the field of view with some room for the error of a phone compass.
        /// </summary>
        public const double ViewHalfAngleDegrees = 35.0;

        // Closer than the GPS error the bearing to a building means nothing: a building under the phone counts as faced.
        public const double MinBearingDistanceMeters = 5.0;

        readonly List<PaseoSearchBuilding> buildings;
        readonly int[] allMaps;
        int step;

        public PaseoSearchPlan(IReadOnlyList<PaseoSearchBuilding> tourBuildings)
        {
            buildings = tourBuildings.ToList();
            allMaps = buildings.SelectMany(b => b.MapIds).ToArray();
        }

        public PaseoSearch Next(PaseoGpsFix fix, string inViewBuildingId) => Next(fix, PaseoHeading.None, inViewBuildingId);

        /// <param name="heading">Heading of the back camera, if usable.</param>
        /// <param name="rankByHeading">False (the team's «Brújula: no»): the heading is only logged, the ranking ignores it.</param>
        public PaseoSearch Next(PaseoGpsFix fix, PaseoHeading heading, string inViewBuildingId, bool rankByHeading = true)
        {
            double headingDegrees = heading.Valid ? heading.Degrees : double.NaN;
            bool inView = buildings.Any(b => b.Id == inViewBuildingId);
            if (buildings.Count == 0 || (!fix.Valid && !inView))
                return Everything(headingDegrees);

            int rank = Cycle[step];
            step = (step + 1) % Cycle.Length;
            if (rank < 0)
                return Everything(headingDegrees);

            bool useHeading = rankByHeading && fix.Valid && heading.Valid;
            var ranked = Rank(fix, useHeading ? heading : PaseoHeading.None, inView ? inViewBuildingId : null);
            var chosen = ranked[rank < ranked.Count ? rank : 0];
            var reason = chosen.Id == inViewBuildingId ? PaseoSearchReason.InView
                : useHeading && IsFaced(fix, heading, chosen) ? PaseoSearchReason.Compass
                : fix.Valid && chosen.HasGps ? PaseoSearchReason.Gps
                : PaseoSearchReason.Order;
            double difference = fix.Valid && heading.Valid && chosen.HasGps
                ? PaseoGeo.AngleDifferenceDegrees(Bearing(fix, chosen), heading.Degrees)
                : double.NaN;
            return new PaseoSearch(chosen.Id, chosen.Name, chosen.MapIds, reason, Distance(fix, chosen), headingDegrees, difference);
        }

        /// <summary>Starts the cycle again, e.g. after AR tracking was lost.</summary>
        public void Reset() => step = 0;

        /// <summary>A search over every map of the tour, outside the cycle (the team's detection test).</summary>
        public PaseoSearch EveryMap => Everything();

        PaseoSearch Everything(double headingDegrees = double.NaN) =>
            new(null, null, allMaps, PaseoSearchReason.All, double.NaN, headingDegrees);

        // The building in view first, then (with a usable heading) the ones with a GPS centre the camera faces, nearest first,
        // then the other ones with a GPS centre by distance, then the rest in configuration order.
        List<PaseoSearchBuilding> Rank(PaseoGpsFix fix, PaseoHeading heading, string first)
        {
            var ranked = buildings.Where(b => b.Id == first).ToList();
            var others = buildings.Where(b => b.Id != first).ToList();
            if (fix.Valid)
            {
                var located = others.Where(b => b.HasGps).OrderBy(b => Distance(fix, b)).ToList();
                if (heading.Valid)
                {
                    ranked.AddRange(located.Where(b => IsFaced(fix, heading, b)));
                    ranked.AddRange(located.Where(b => !IsFaced(fix, heading, b)));
                }
                else
                    ranked.AddRange(located);
                ranked.AddRange(others.Where(b => !b.HasGps));
            }
            else
                ranked.AddRange(others);
            return ranked;
        }

        // The GPS bearing from the phone to the building is within the camera's half field of view of its heading.
        static bool IsFaced(PaseoGpsFix fix, PaseoHeading heading, PaseoSearchBuilding building) =>
            fix.Valid && heading.Valid && building.HasGps &&
            (Distance(fix, building) < MinBearingDistanceMeters ||
             PaseoGeo.AngleDifferenceDegrees(Bearing(fix, building), heading.Degrees) <= ViewHalfAngleDegrees);

        static double Distance(PaseoGpsFix fix, PaseoSearchBuilding building) =>
            fix.Valid && building.HasGps ? PaseoGeo.DistanceMeters(fix.Latitude, fix.Longitude, building.Latitude, building.Longitude) : double.NaN;

        static double Bearing(PaseoGpsFix fix, PaseoSearchBuilding building) =>
            PaseoGeo.BearingDegrees(fix.Latitude, fix.Longitude, building.Latitude, building.Longitude);
    }
}
