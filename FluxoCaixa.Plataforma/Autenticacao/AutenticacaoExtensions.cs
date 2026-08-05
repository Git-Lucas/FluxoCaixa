using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FluxoCaixa.Plataforma.Autenticacao;

public static class AutenticacaoExtensions
{
    public static OpcoesAutenticacao AdicionarAutenticacaoDeComerciante(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<OpcoesAutenticacao>()
            .Bind(builder.Configuration.GetSection(OpcoesAutenticacao.SecaoDeConfiguracao))
            .Validate(opcoes => !string.IsNullOrWhiteSpace(opcoes.Authority))
            .ValidateOnStart();

        var opcoesAutenticacao = builder.Configuration
            .GetSection(OpcoesAutenticacao.SecaoDeConfiguracao)
            .Get<OpcoesAutenticacao>()
            ?? throw new InvalidOperationException("A seção 'Autenticacao' não foi configurada.");

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opcoesDoJwt =>
            {
                opcoesDoJwt.MapInboundClaims = false;
                opcoesDoJwt.Authority = opcoesAutenticacao.Authority;
                opcoesDoJwt.RequireHttpsMetadata = opcoesAutenticacao.RequererHttps;
                opcoesDoJwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = opcoesAutenticacao.Emissor,
                    ValidateAudience = true,
                    ValidAudience = opcoesAutenticacao.Audiencia,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        builder.Services.AddAuthorization();

        return opcoesAutenticacao;
    }
}
