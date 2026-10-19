using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DeidSharp.Presidio;

/// <summary>Registers a <see cref="PresidioDetector"/> with dependency injection.</summary>
public static class PresidioServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers one <see cref="PresidioDetector"/> as a typed HTTP client (pooled handlers,
        /// works with Microsoft.Extensions.Http.Resilience). Resolve it and add it to a
        /// <see cref="Detection.CompositePhiDetector"/> with the other detectors. Call it once;
        /// to stack two Presidio services, register each under a name with the overload that
        /// takes one.
        /// </summary>
        public IHttpClientBuilder AddPresidioDetector(Action<PresidioOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);
            services.AddOptions<PresidioOptions>().Configure(configure).Validate(o =>
            {
                o.Validate();
                return true;
            });

            return services
                .AddHttpClient<PresidioDetector>()
                .AddTypedClient((http, sp) => new PresidioDetector(http, sp.GetRequiredService<IOptions<PresidioOptions>>().Value));
        }

        /// <summary>
        /// Registers a <see cref="PresidioDetector"/> under <paramref name="name"/>, with its own
        /// options and its own HTTP client, so several Presidio services (the stock image and
        /// the clinical sidecar) can be stacked. Resolve it with
        /// <c>GetRequiredKeyedService&lt;PresidioDetector&gt;(name)</c>. The detector's
        /// <see cref="PresidioOptions.Name"/>, the source on its spans, defaults to
        /// <paramref name="name"/>, so two detectors in one stack never share one.
        /// </summary>
        /// <returns>The builder of the detector's HTTP client, to add handlers such as a resilience pipeline.</returns>
        /// <exception cref="ArgumentException">A detector is already registered under <paramref name="name"/>.</exception>
        public IHttpClientBuilder AddPresidioDetector(string name, Action<PresidioOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(configure);

            // A second registration under one name would merge into the first, options and
            // handlers alike, and leave one detector configured by both.
            if (services.Any(d => d.IsKeyedService && d.ServiceType == typeof(PresidioDetector) && Equals(d.ServiceKey, name)))
            {
                throw new ArgumentException($"A Presidio detector is already registered under '{name}'.", nameof(name));
            }

            services.AddOptions<PresidioOptions>(name)
                .Configure(o => o.Name = name)
                .Configure(configure)
                .Validate(o =>
                {
                    o.Validate();
                    return true;
                });

            var client = $"{nameof(PresidioDetector)}:{name}";
            services.AddKeyedTransient(name, (sp, _) => new PresidioDetector(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(client),
                sp.GetRequiredService<IOptionsMonitor<PresidioOptions>>().Get(name)));
            return services.AddHttpClient(client);
        }
    }
}
