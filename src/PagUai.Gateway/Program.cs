using PagUai.Gateway;

// PagUai: fintech FICTÍCIA usada na disciplina "Arquitetura de Microsserviços e APIs".
// Sprint 2: o GATEWAY (YARP), porta de entrada única. O app e o portal do lojista continuam
// chamando o mesmo endereço; quem decide qual serviço atende é o gateway. Veja Rotas.cs.
var builder = WebApplication.CreateBuilder(args);

var destinoDoExtrato = Rotas.DestinoDoExtrato(builder.Configuration);
builder.Services.AddReverseProxy()
    .LoadFromMemory(Rotas.Criar(destinoDoExtrato), Rotas.Destinos(builder.Configuration));

var app = builder.Build();

app.Logger.LogInformation("Strangler Fig: GET /contas/{{id}}/extrato → {Destino}. Todo o resto → monolito.", destinoDoExtrato);

app.MapReverseProxy();

app.Run();

// Necessário para os testes (WebApplicationFactory).
public partial class Program;
