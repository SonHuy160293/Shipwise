using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shipwise.AspNetCore.DependencyInjection;

public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddTrustedForwarding(this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection networkSection = configuration.GetSection("ForwardedHeaders:KnownNetworks");
        if (!string.IsNullOrEmpty(networkSection.Value))
        {
            throw new InvalidOperationException("ForwardedHeaders:KnownNetworks must be a list of CIDR ranges.");
        }

        var networks = networkSection.GetChildren().Select(entry => ParseNetwork(entry.Value)).ToArray();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardLimit = 1;
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            string[] proxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];

            // An empty allowlist must never mean trust every peer.
            if (proxies.Length == 0 && networks.Length == 0)
            {
                proxies = [IPAddress.Loopback.ToString(), IPAddress.IPv6Loopback.ToString()];
            }

            foreach (string proxy in proxies)
            {
                if (!IPAddress.TryParse(proxy, out var address))
                {
                    throw new InvalidOperationException("Trusted forwarding requires valid proxy IP addresses.");
                }

                options.KnownProxies.Add(address);
            }

            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(network);
            }

            foreach (string host in configuration.GetSection("ForwardedHeaders:AllowedHosts").Get<string[]>() ?? [])
            {
                options.AllowedHosts.Add(host);
            }
        });
        return services;
    }

    private static System.Net.IPNetwork ParseNetwork(string? cidr)
    {
        if (cidr is null || !System.Net.IPNetwork.TryParse(cidr, out var network))
        {
            throw new InvalidOperationException("ForwardedHeaders:KnownNetworks contains an invalid CIDR range.");
        }

        // IPNetwork also accepts legacy IPv4 spellings, e.g. 10/8 parses as 0.0.0.0/8.
        if (network.BaseAddress.AddressFamily == AddressFamily.InterNetwork)
        {
            string address = cidr[..cidr.IndexOf('/')];
            if (IPAddress.Parse(address).ToString() != address)
            {
                throw new InvalidOperationException("ForwardedHeaders:KnownNetworks requires dotted-decimal IPv4 addresses.");
            }
        }

        return network;
    }
}
