using System;
using System.Collections.Generic;
using System.Linq;

namespace AncorRA.AR
{
    /// <summary>
    /// Localization method of the tour, replacing the SDK's DeviceLocalization (work in progress: the ILocalizationMethod
    /// part comes next). DeviceLocalization always asks the plugin for every loaded map; this one asks only for the maps
    /// PaseoSearchPlan chose for each attempt.
    /// </summary>
    public sealed class PaseoLocalization
    {
        /// <summary>
        /// The map ids to hand the plugin: the wanted ones the SDK configured and the plugin loaded (one id without a
        /// handle fails the whole attempt). Null means every loaded map (n = 0), used when nothing usable is left or
        /// when the subset is every map anyway.
        /// </summary>
        public static int[] Searchable(int[] wanted, ICollection<int> configured, Func<int, bool> inPlugin)
        {
            var usable = wanted.Where(id => configured.Contains(id) && inPlugin(id)).Distinct().ToArray();
            if (usable.Length == 0 || usable.Length == configured.Count(inPlugin))
                return null;
            return usable;
        }
    }
}
