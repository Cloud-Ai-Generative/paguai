using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace PagUai.Gateway.Tests;

/// <summary>Strangler Fig: a configuração decide para onde vai a rota do extrato.</summary>
public class RotasTests
{
    private static IProxyConfig ConfiguracaoCom(string? strangler)
    {
        using var fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            if (strangler is not null)
            {
                b.UseSetting("Strangler:Extrato", strangler);
            }
        });
        return fabrica.Services.GetRequiredService<IProxyConfigProvider>().GetConfig();
    }

    [Theory]
    [InlineData(null, "extrato")]
    [InlineData("servico", "extrato")]
    [InlineData("monolito", "monolito")]
    public void Rota_do_extrato_segue_a_configuracao(string? strangler, string destino)
    {
        var config = ConfiguracaoCom(strangler);

        Assert.Equal(destino, config.Routes.Single(r => r.RouteId == "extrato").ClusterId);
    }

    [Fact]
    public void Todo_o_resto_vai_para_o_monolito()
    {
        var config = ConfiguracaoCom(null);

        var resto = config.Routes.Single(r => r.RouteId == "todo-o-resto");
        Assert.Equal("monolito", resto.ClusterId);
        Assert.True(resto.Order > config.Routes.Single(r => r.RouteId == "extrato").Order);
    }

    [Fact]
    public void Valor_invalido_impede_o_gateway_de_subir()
    {
        var erro = Assert.ThrowsAny<Exception>(() => ConfiguracaoCom("talvez"));
        Assert.Contains("Strangler:Extrato", erro.ToString());
    }
}
