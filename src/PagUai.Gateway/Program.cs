using PagUai.Gateway;

// PagUai: fintech FICTÍCIA usada na disciplina "Arquitetura de Microsserviços e APIs".
// Sprint 2: o GATEWAY (YARP), porta de entrada única. O app e o portal do lojista continuam
// chamando o mesmo endereço; quem decide qual serviço atende é o gateway. Veja Rotas.cs.
// Sprint 3: o extrato vai sempre para o serviço de Extrato (o Strangler Fig dessa rota terminou).
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromMemory(Rotas.Criar(), Rotas.Destinos(builder.Configuration));

var app = builder.Build();

app.Logger.LogInformation("GET /contas/{{id}}/extrato → extrato. Todo o resto → monolito.");

app.MapReverseProxy();

app.Run();

// Necessário para os testes (WebApplicationFactory).
public partial class Program;
