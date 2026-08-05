using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FluxoCaixa.Plataforma.Telemetria;

public static class TelemetriaExtensions
{
    private const string _prefixoDasFontesProprias = "FluxoCaixa.*";

    public static IHostApplicationBuilder AdicionarTelemetria(this IHostApplicationBuilder builder, string nomeDoServico)
    {
        var recurso = ResourceBuilder.CreateDefault().AddService(nomeDoServico);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(recurso);
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.AddOtlpExporter();
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(nomeDoServico))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddSource("MassTransit")
                .AddSource(_prefixoDasFontesProprias)
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsqlInstrumentation()
                .AddMeter(_prefixoDasFontesProprias)
                .AddOtlpExporter());

        return builder;
    }

    public static IApplicationBuilder UsarIdentificacaoDoComercianteNoLog(this IApplicationBuilder app, string claimDoComerciante)
        => app.Use(async (contexto, proximo) =>
        {
            var comercianteId = contexto.User.FindFirst(claimDoComerciante)?.Value;
            if (comercianteId is null)
            {
                await proximo(contexto);
                return;
            }

            var logger = contexto.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("FluxoCaixa.Plataforma.Telemetria");

            using (logger.BeginScope(new Dictionary<string, object> { ["comerciante.id"] = comercianteId }))
            {
                await proximo(contexto);
            }
        });
}
