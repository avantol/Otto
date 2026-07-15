using System;
using System.Collections.Generic;
using System.Linq;

namespace WSJTX_Controller
{
    public class Spot
    {
        public string ReceiverCall { get; set; }
        public string ReceiverGrid { get; set; }
        public int SnrDb { get; set; }
        public long FrequencyHz { get; set; }
        public DateTime TimestampUtc { get; set; }
        public double LatDeg { get; set; }
        public double LonDeg { get; set; }
        public double DistanceKm { get; set; }
        public double AzimuthDeg { get; set; }
    }

    public class SpotCache
    {
        // outer key = band (e.g. "20m"), inner key = receiver callsign (latest-per-spotter)
        private readonly Dictionary<string, Dictionary<string, Spot>> bandSpots = new Dictionary<string, Dictionary<string, Spot>>();
        private readonly object lockObj = new object();
        public const double MaxAgeMinutes = 15.0;

        public void AddSpot(Spot spot, string band)
        {
            if (string.IsNullOrEmpty(band)) return;
            lock (lockObj)
            {
                if (!bandSpots.ContainsKey(band))
                    bandSpots[band] = new Dictionary<string, Spot>(StringComparer.OrdinalIgnoreCase);
                bandSpots[band][spot.ReceiverCall] = spot;
            }
        }

        public void PruneExpired()
        {
            lock (lockObj)
            {
                DateTime cutoff = DateTime.UtcNow.AddMinutes(-MaxAgeMinutes);
                foreach (var band in bandSpots.Keys.ToList())
                {
                    var dict = bandSpots[band];
                    var expired = dict.Where(kv => kv.Value.TimestampUtc < cutoff).Select(kv => kv.Key).ToList();
                    foreach (var key in expired)
                        dict.Remove(key);
                }
            }
        }

        public List<Spot> GetSpots(string band)
        {
            lock (lockObj)
            {
                if (band != null && bandSpots.ContainsKey(band))
                    return bandSpots[band].Values.ToList();
                return new List<Spot>();
            }
        }

        public int GetSpotCount(string band)
        {
            lock (lockObj)
            {
                if (band != null && bandSpots.ContainsKey(band))
                    return bandSpots[band].Count;
                return 0;
            }
        }

        public void Clear()
        {
            lock (lockObj)
            {
                bandSpots.Clear();
            }
        }
    }

    public static class GeoUtils
    {
        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;
        private const double EarthRadiusKm = 6371.0;

        /// <summary>
        /// Convert 4-char or 6-char Maidenhead grid to center lat/lon in degrees.
        /// </summary>
        public static bool GridToLatLon(string grid, out double lat, out double lon)
        {
            lat = 0;
            lon = 0;
            if (grid == null || grid.Length < 4) return false;

            grid = grid.ToUpper();
            if (grid[0] < 'A' || grid[0] > 'R' || grid[1] < 'A' || grid[1] > 'R') return false;
            if (grid[2] < '0' || grid[2] > '9' || grid[3] < '0' || grid[3] > '9') return false;

            // Field
            lon = (grid[0] - 'A') * 20.0 - 180.0;
            lat = (grid[1] - 'A') * 10.0 - 90.0;
            // Square
            lon += (grid[2] - '0') * 2.0;
            lat += (grid[3] - '0') * 1.0;

            if (grid.Length >= 6 && grid[4] >= 'A' && grid[4] <= 'X' && grid[5] >= 'A' && grid[5] <= 'X')
            {
                // Subsquare
                lon += (grid[4] - 'A') * (2.0 / 24.0);
                lat += (grid[5] - 'A') * (1.0 / 24.0);
                // Center of subsquare
                lon += 1.0 / 24.0;
                lat += 0.5 / 24.0;
            }
            else
            {
                // Center of square
                lon += 1.0;
                lat += 0.5;
            }
            return true;
        }

        /// <summary>
        /// Great-circle distance in km using haversine formula.
        /// </summary>
        public static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            double dLat = (lat2 - lat1) * DegToRad;
            double dLon = (lon2 - lon1) * DegToRad;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * DegToRad) * Math.Cos(lat2 * DegToRad) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusKm * c;
        }

        /// <summary>
        /// Initial bearing (azimuth) from point 1 to point 2 in degrees [0, 360).
        /// </summary>
        public static double InitialBearing(double lat1, double lon1, double lat2, double lon2)
        {
            double lat1R = lat1 * DegToRad;
            double lat2R = lat2 * DegToRad;
            double dLon = (lon2 - lon1) * DegToRad;
            double y = Math.Sin(dLon) * Math.Cos(lat2R);
            double x = Math.Cos(lat1R) * Math.Sin(lat2R) - Math.Sin(lat1R) * Math.Cos(lat2R) * Math.Cos(dLon);
            double bearing = Math.Atan2(y, x) * RadToDeg;
            return (bearing + 360.0) % 360.0;
        }

        /// <summary>
        /// Azimuthal equidistant projection: project target lat/lon relative to center.
        /// Returns (xKm, yKm) where x=east, y=north.
        /// </summary>
        public static void AzimuthalEquidistantProject(
            double centerLatDeg, double centerLonDeg,
            double targetLatDeg, double targetLonDeg,
            out double xKm, out double yKm)
        {
            double lat1 = centerLatDeg * DegToRad;
            double lon1 = centerLonDeg * DegToRad;
            double lat2 = targetLatDeg * DegToRad;
            double lon2 = targetLonDeg * DegToRad;

            double dLon = lon2 - lon1;
            double cosC = Math.Sin(lat1) * Math.Sin(lat2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

            // Clamp to valid range
            if (cosC > 1.0) cosC = 1.0;
            if (cosC < -1.0) cosC = -1.0;

            double c = Math.Acos(cosC);

            if (c < 1e-10)
            {
                xKm = 0;
                yKm = 0;
                return;
            }

            double k = c / Math.Sin(c);
            xKm = EarthRadiusKm * k * Math.Cos(lat2) * Math.Sin(dLon);
            yKm = EarthRadiusKm * k * (Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon));
        }

        /// <summary>
        /// Convert frequency in Hz to band string (e.g. "20m").
        /// </summary>
        public static string FreqToBand(long freqHz)
        {
            double freqMhz = freqHz / 1e6;
            if (freqMhz >= 0.1357 && freqMhz <= 0.1378) return "2200m";
            if (freqMhz >= 0.472 && freqMhz <= 0.479) return "630m";
            if (freqMhz >= 1.8 && freqMhz <= 2.0) return "160m";
            if (freqMhz >= 3.5 && freqMhz <= 4.0) return "80m";
            if (freqMhz >= 5.35 && freqMhz <= 5.37) return "60m";
            if (freqMhz >= 7.0 && freqMhz <= 7.3) return "40m";
            if (freqMhz >= 10.1 && freqMhz <= 10.15) return "30m";
            if (freqMhz >= 14.0 && freqMhz <= 14.35) return "20m";
            if (freqMhz >= 18.068 && freqMhz <= 18.168) return "17m";
            if (freqMhz >= 21.0 && freqMhz <= 21.45) return "15m";
            if (freqMhz >= 24.89 && freqMhz <= 24.99) return "12m";
            if (freqMhz >= 28.0 && freqMhz <= 29.7) return "10m";
            if (freqMhz >= 50.0 && freqMhz <= 54.0) return "6m";
            if (freqMhz >= 144.0 && freqMhz <= 148.0) return "2m";
            return "";
        }
    }
}
