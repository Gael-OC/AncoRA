using System;
using System.Collections.Generic;
using System.Linq;

namespace AncorRA.AR
{
    /// <summary>Why an attempt searched what it searched: every map, the building nearest by GPS (or its rank), the
    /// building already located in view, or the order of the configuration (no GPS).</summary>
    public enum PaseoSearchReason { All, Gps, InView, Order }

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

        public PaseoSearch(string buildingId, string buildingName, int[] mapIds, PaseoSearchReason reason, double gpsDistanceMeters)
        {
            BuildingId = buildingId;
            BuildingName = buildingName;
            MapIds = mapIds;
            Reason = reason;
            GpsDistanceMeters = gpsDistanceMeters;
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
            double toRadians = Math.PI / 180.0;
            double north = (latitudeB - latitudeA) * toRadians * EarthRadiusMeters;
            double east = (longitudeB - longitudeA) * toRadians * EarthRadiusMeters * Math.Cos((latitudeA + latitudeB) * 0.5 * toRadians);
            return Math.Sqrt(north * north + east * east);
        }
    }

    /// <summary>
    /// Chooses the maps each localization attempt searches. The SDK's device localization searches every loaded map, and
    /// in the field test of 14-oct a try against the seven maps took 9.5 s on average when nothing matched (0.7 s when the
    /// first map did). One building per attempt makes a failed try several times shorter, so the building the user
    /// faces is tried far more often. Buildings are ranked by the one already located in view, then by GPS distance
    /// (then the configuration order), and each cycle of six attempts searches ranks 0, 1, 0, 2, 0 and 3, so every
    /// building is tried each cycle even when GPS points the wrong way. Without GPS nor a building in view the
    /// buildings are tried one by one in configuration order. Searching every map at once is gone: on the Xiaomi
    /// 25069PTEBG (27-oct logs) a try costs about 1.6 s per 20 000 map points, so the eight-map try took 13 s on average
    /// (up to 23 s), half of all the search time, and a match inside it showed up only when it ended.
    /// </summary>
    public sealed class PaseoSearchPlan
    {
        static readonly int[] Cycle = { 0, 1, 0, 2, 0, 3 };

        readonly List<PaseoSearchBuilding> buildings;
        readonly int[] allMaps;
        int step;
        int orderStep;

        public PaseoSearchPlan(IReadOnlyList<PaseoSearchBuilding> tourBuildings)
        {
            buildings = tourBuildings.ToList();
            allMaps = buildings.SelectMany(b => b.MapIds).ToArray();
        }

        public PaseoSearch Next(PaseoGpsFix fix, string inViewBuildingId)
        {
            bool inView = buildings.Any(b => b.Id == inViewBuildingId);
            if (buildings.Count == 0)
                return Everything();
            if (!fix.Valid && !inView)
            {
                var next = buildings[orderStep++ % buildings.Count];
                return new PaseoSearch(next.Id, next.Name, next.MapIds, PaseoSearchReason.Order, double.NaN);
            }

            int rank = Cycle[step];
            step = (step + 1) % Cycle.Length;

            var ranked = Rank(fix, inView ? inViewBuildingId : null);
            var chosen = ranked[rank < ranked.Count ? rank : 0];
            var reason = chosen.Id == inViewBuildingId ? PaseoSearchReason.InView
                : fix.Valid && chosen.HasGps ? PaseoSearchReason.Gps
                : PaseoSearchReason.Order;
            return new PaseoSearch(chosen.Id, chosen.Name, chosen.MapIds, reason, Distance(fix, chosen));
        }

        /// <summary>Starts the cycle again, e.g. after AR tracking was lost.</summary>
        public void Reset() => step = orderStep = 0;

        PaseoSearch Everything() => new(null, null, allMaps, PaseoSearchReason.All, double.NaN);

        // The building in view first, then the ones with a GPS centre by distance, then the rest in configuration order.
        List<PaseoSearchBuilding> Rank(PaseoGpsFix fix, string first)
        {
            var ranked = buildings.Where(b => b.Id == first).ToList();
            var others = buildings.Where(b => b.Id != first).ToList();
            if (fix.Valid)
            {
                ranked.AddRange(others.Where(b => b.HasGps).OrderBy(b => Distance(fix, b)));
                ranked.AddRange(others.Where(b => !b.HasGps));
            }
            else
                ranked.AddRange(others);
            return ranked;
        }

        static double Distance(PaseoGpsFix fix, PaseoSearchBuilding building) =>
            fix.Valid && building.HasGps ? PaseoGeo.DistanceMeters(fix.Latitude, fix.Longitude, building.Latitude, building.Longitude) : double.NaN;
    }
}
