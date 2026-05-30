using System.Security.Cryptography;
using System.Text;
using DressAndGo.Data;

namespace DressAndGo.Services;

public class AuthService
{
    readonly string dbCaminho;
    readonly string chave;

    public AuthService(string db, string chave_assinatura) {
        dbCaminho = db;
        chave = chave_assinatura;
    }

    public record ResultadoLogin(bool Ok, string? Token, string? Perfil, long? UsuarioId, string? Nome, string? Erro);

    public ResultadoLogin Login(string email, string senha)
    {
        using var db = new SqliteConnection(dbCaminho);
        var hashSenha = HashSenha(senha);
        var usuario = db.QueryOne("SELECT id, name, role, active FROM users WHERE email = ? AND password_hash = ?", P(email, hashSenha));

        if (usuario == null) return new ResultadoLogin(false, null, null, null, null, "E-mail ou senha incorretos.");
        if (Convert.ToInt64(usuario["active"]) == 0) return new ResultadoLogin(false, null, null, null, null, "Conta desativada.");

        long usuarioId = Convert.ToInt64(usuario["id"]);
        string perfil  = usuario["role"]!.ToString()!;
        string nome    = usuario["name"]!.ToString()!;

        return new ResultadoLogin(true, GerarToken(usuarioId, perfil, nome), perfil, usuarioId, nome, null);
    }

    public info_token? Validar(string token) {
        try {
            var partes = token.Split(':', 4);
            if (partes.Length != 4) return null;

            long id = long.Parse(partes[0]);
            string perf = partes[1], nome = partes[2], assin = partes[3];

            if (assin != Assinar(id + "|" + perf + "|" + nome)) return null;
            return new info_token(id, perf, nome);
        }
        catch { return null; }
    }

    string GerarToken(long id, string perfil, string nome)
    {
        string dados = $"{id}|{perfil}|{nome}";
        return $"{id}:{perfil}:{nome}:{Assinar(dados)}";
    }

    string Assinar(string dados) {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(chave));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(dados))).ToLower();
    }

    public static string HashSenha(string senha)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes("Str33tWear" + senha))).ToLower();
    }

    public static string Hash(string senha) => HashSenha(senha);

    static Dictionary<string, object?> P(params object?[] v) {
        var d = new Dictionary<string, object?>();
        for (int i = 0; i < v.Length; i++) d["p" + i] = v[i];
        return d;
    }
}

public record info_token(long UsuarioId, string Perfil, string Nome);
