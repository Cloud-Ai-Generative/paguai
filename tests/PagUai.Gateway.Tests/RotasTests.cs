using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace PagUai.Gateway.Tests;

/// <summary>
/// Sprint 3: o Strangler Fig do Extrato terminou. A rota do extrato vai sempre para o serviço novo.
/// (Na Sprint 2, uma configuração decidia o destino; veja a tag sprint-02.)
/// </summary>
public class RotasTests
{
    private static IProxyConfig Configuracao()
    {
        using var fabrica = new WebApplicationFactory<Program>();
        return fabrica.Services.GetRequiredService<IProxyConfigProvider>().GetConfig();
    }

    [Fact]
    public void Rota_do_extrato_vai_para_o_servico_de_Extrato() =>
        Assert.Equal("extrato", Configuracao().Routes.Single(r => r.RouteId == "extrato").ClusterId);

    [Fact]
    public void Todo_o_resto_vai_para_o_monolito()
    {
        var config = Configuracao();

        var resto = config.Routes.Single(r => r.RouteId == "todo-o-resto");
        Assert.Equal("monolito", resto.ClusterId);
        Assert.True(resto.Order > config.Routes.Single(r => r.RouteId == "extrato").Order);
    }
}
