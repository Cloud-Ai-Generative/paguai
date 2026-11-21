using System.Globalization;
using System.Text;

namespace PagUai.Api.Modulos.Pagamentos.Interno;

/// <summary>O cursor é opaco para quem consome: a data e o id do último item, em Base64 seguro para URL.</summary>
public static class CursorDeMovimentos
{
    public static string Criar(DateTime data, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{data.Ticks}:{id}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool TentarLer(string? cursor, out DateTime data, out Guid id)
    {
        data = default;
        id = default;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            var partes = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split(':');
            if (partes.Length == 2 && long.TryParse(partes[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) && Guid.TryParse(partes[1], out id))
            {
                data = new DateTime(ticks, DateTimeKind.Utc);
                return true;
            }
        }
        catch (FormatException)
        {
        }

        return false;
    }
}
