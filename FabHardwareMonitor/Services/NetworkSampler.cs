using System.Net.NetworkInformation;
using FabHardwareMonitor.Models;

namespace FabHardwareMonitor.Services;

public sealed class NetworkSampler
{
    private readonly Dictionary<string, (long Sent, long Recv, DateTime Stamp)> _previous = new();

    public IReadOnlyList<NamedOption> ListAdapters()
    {
        var list = new List<NamedOption>
        {
            new() { Id = "", Name = "Auto (busiest)" }
        };

        foreach (var nic in UsableAdapters())
        {
            list.Add(new NamedOption { Id = nic.Id, Name = nic.Name });
        }

        return list;
    }

    public (double Up, double Down, string? Name, string? Id) Sample(string? preferredId)
    {
        var now = DateTime.UtcNow;
        string? bestId = null;
        string? bestName = null;
        double bestUp = 0;
        double bestDown = 0;
        double bestTotal = -1;

        foreach (var nic in UsableAdapters())
        {
            long sent;
            long recv;
            try
            {
                var stats = nic.GetIPStatistics();
                sent = stats.BytesSent;
                recv = stats.BytesReceived;
            }
            catch
            {
                continue;
            }

            if (!_previous.TryGetValue(nic.Id, out var prev))
            {
                _previous[nic.Id] = (sent, recv, now);
                continue;
            }

            var seconds = Math.Max(0.2, (now - prev.Stamp).TotalSeconds);
            var up = Math.Max(0, (sent - prev.Sent) / seconds);
            var down = Math.Max(0, (recv - prev.Recv) / seconds);
            _previous[nic.Id] = (sent, recv, now);

            if (!string.IsNullOrWhiteSpace(preferredId) && nic.Id == preferredId)
            {
                return (up, down, nic.Name, nic.Id);
            }

            var total = up + down;
            if (total >= bestTotal)
            {
                bestTotal = total;
                bestUp = up;
                bestDown = down;
                bestId = nic.Id;
                bestName = nic.Name;
            }
        }

        return (bestUp, bestDown, bestName, bestId);
    }

    private static IEnumerable<NetworkInterface> UsableAdapters()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel
                or NetworkInterfaceType.Unknown)
            {
                continue;
            }

            var name = nic.Description + nic.Name;
            if (name.Contains("virtual", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("ethernet", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("wi-fi", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("wifi", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return nic;
        }
    }
}
