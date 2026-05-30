using DressAndGo.Data;

namespace DressAndGo.Services;

public class UserService
{
    readonly string dbCaminho;
    public UserService(string db) => dbCaminho = db;

    public object Listar()
    {
        using var db = new SqliteConnection(dbCaminho);
        return db.Query("SELECT id, name, email, role, phone, active FROM users ORDER BY id").Select(MapUsuario).ToList();
    }

    public object? BuscarPorId(long id) {
        using var db = new SqliteConnection(dbCaminho);
        var linha = db.QueryOne("SELECT id, name, email, role, phone, active FROM users WHERE id = ?", P(id));
        return linha == null ? null : MapUsuario(linha);
    }

    public (bool ok, string msg, long id) Criar(string nome, string email, string senha, string perfil, string? telefone = null)
    {
        if (!email.Contains('@')) return (false, "E-mail invalido.", 0);
        if (senha.Length < 6) return (false, "Senha muito curta.", 0);

        using var db = new SqliteConnection(dbCaminho);
        if (db.Scalar<long>("SELECT COUNT(*) FROM users WHERE email = ?", P(email)) > 0)
            return (false, "E-mail ja cadastrado.", 0);

        long novoId = db.ExecuteInsert("INSERT INTO users (name, email, password_hash, role, phone) VALUES (?,?,?,?,?)",
            P(nome, email, AuthService.Hash(senha), perfil, telefone));
        return (true, "Usuario criado.", novoId);
    }

    public (bool ok, string msg, long id) CriarCliente(string nome, string email, string senha, string? telefone) {
        if (!email.Contains('@')) return (false, "E-mail invalido.", 0);
        if (senha.Length < 6)    return (false, "Senha muito curta.", 0);

        using var db = new SqliteConnection(dbCaminho);
        if (db.Scalar<long>("SELECT COUNT(*) FROM users WHERE email = ?", P(email)) > 0)
            return (false, "E-mail ja cadastrado.", 0);

        long novoId = db.ExecuteInsert(
            "INSERT INTO users (name, email, password_hash, role, phone) VALUES (?,?,?,'customer',?)",
            P(nome, email, AuthService.Hash(senha), telefone));
        return (true, "Conta criada.", novoId);
    }

    public (bool ok, string msg) Atualizar(long id, string? nome, string? perfil, bool? ativo, string? tel)
    {
        using var db = new SqliteConnection(dbCaminho);
        if (db.Scalar<long>("SELECT COUNT(*) FROM users WHERE id = ?", P(id)) == 0) return (false, "Usuario nao encontrado.");

        if (nome != null)   db.Execute("UPDATE users SET name   = ? WHERE id = ?", P(nome, id));
        if (tel  != null)   db.Execute("UPDATE users SET phone  = ? WHERE id = ?", P(tel, id));
        if (perfil != null) db.Execute("UPDATE users SET role   = ? WHERE id = ?", P(perfil, id));
        if (ativo.HasValue) db.Execute("UPDATE users SET active = ? WHERE id = ?", P(ativo.Value ? 1 : 0, id));
        return (true, "Atualizado.");
    }

    public (bool ok, string msg) AlterarSenha(long id, string senhaAtual, string senhaNova)
    {
        using var db    = new SqliteConnection(dbCaminho);
        string hashAtual = AuthService.Hash(senhaAtual);

        if (db.QueryOne("SELECT id FROM users WHERE id = ? AND password_hash = ?", P(id, hashAtual)) == null)
            return (false, "Senha atual incorreta.");
        if (senhaNova.Length < 6)
            return (false, "Nova senha muito curta.");

        db.Execute("UPDATE users SET password_hash = ? WHERE id = ?", P(AuthService.Hash(senhaNova), id));
        return (true, "Senha alterada.");
    }

    public object ListarEnderecos(long userId) {
        using var db = new SqliteConnection(dbCaminho);
        return db.Query("SELECT * FROM addresses WHERE user_id = ? ORDER BY is_default DESC, id", P(userId)).Select(MapEndereco).ToList();
    }

    public (bool ok, string msg, long id) AdicionarEndereco(long userId, endereco_req req)
    {
        if (string.IsNullOrWhiteSpace(req.Cep))    return (false, "CEP obrigatorio.", 0);
        if (string.IsNullOrWhiteSpace(req.Street)) return (false, "Rua obrigatoria.", 0);
        if (string.IsNullOrWhiteSpace(req.City))   return (false, "Cidade obrigatoria.", 0);

        using var db = new SqliteConnection(dbCaminho);
        long qtd = db.Scalar<long>("SELECT COUNT(*) FROM addresses WHERE user_id = ?", P(userId));
        long novoId = db.ExecuteInsert(
            "INSERT INTO addresses (user_id,label,cep,street,number,complement,district,city,state,is_default) VALUES (?,?,?,?,?,?,?,?,?,?)",
            P(userId, req.Label ?? "Principal", req.Cep, req.Street, req.Number ?? "s/n", req.Complement, req.District ?? "", req.City, req.State ?? "", qtd == 0 ? 1 : 0));
        return (true, "Endereco adicionado.", novoId);
    }

    public (bool ok, string msg) AtualizarEndereco(long endId, long userId, endereco_req req) {
        using var db = new SqliteConnection(dbCaminho);
        if (db.Scalar<long>("SELECT COUNT(*) FROM addresses WHERE id = ? AND user_id = ?", P(endId, userId)) == 0)
            return (false, "Endereco nao encontrado.");

        if (req.Cep        != null) db.Execute("UPDATE addresses SET cep        = ? WHERE id = ?", P(req.Cep, endId));
        if (req.Street     != null) db.Execute("UPDATE addresses SET street     = ? WHERE id = ?", P(req.Street, endId));
        if (req.Number     != null) db.Execute("UPDATE addresses SET number     = ? WHERE id = ?", P(req.Number, endId));
        if (req.City       != null) db.Execute("UPDATE addresses SET city       = ? WHERE id = ?", P(req.City, endId));
        if (req.State      != null) db.Execute("UPDATE addresses SET state      = ? WHERE id = ?", P(req.State, endId));
        if (req.District   != null) db.Execute("UPDATE addresses SET district   = ? WHERE id = ?", P(req.District, endId));
        if (req.Label      != null) db.Execute("UPDATE addresses SET label      = ? WHERE id = ?", P(req.Label, endId));
        if (req.Complement != null) db.Execute("UPDATE addresses SET complement = ? WHERE id = ?", P(req.Complement, endId));
        return (true, "Atualizado.");
    }

    public (bool ok, string msg) RemoverEndereco(long endId, long userId) {
        using var db = new SqliteConnection(dbCaminho);
        if (db.Scalar<long>("SELECT COUNT(*) FROM addresses WHERE id = ? AND user_id = ?", P(endId, userId)) == 0)
            return (false, "Endereco nao encontrado.");
        db.Execute("DELETE FROM addresses WHERE id = ?", P(endId));
        return (true, "Endereco removido.");
    }

    static object MapUsuario(Dictionary<string, object?> r) => new
    {
        id       = Convert.ToInt64(r["id"]),
        nome     = r["name"]?.ToString(),
        email    = r["email"]?.ToString(),
        perfil   = r["role"]?.ToString(),
        telefone = r["phone"]?.ToString(),
        ativo    = Convert.ToInt64(r["active"]) == 1
    };

    static object MapEndereco(Dictionary<string, object?> r) => new {
        id = Convert.ToInt64(r["id"]), userId = Convert.ToInt64(r["user_id"]),
        label = r["label"]?.ToString(), cep = r["cep"]?.ToString(),
        street = r["street"]?.ToString(), number = r["number"]?.ToString(),
        complement = r["complement"]?.ToString(), district = r["district"]?.ToString(),
        city = r["city"]?.ToString(), state = r["state"]?.ToString(),
        isPadrao = Convert.ToInt64(r["is_default"]) == 1
    };

    static Dictionary<string, object?> P(params object?[] v)
    {
        var d = new Dictionary<string, object?>();
        for (int i = 0; i < v.Length; i++) d["p" + i] = v[i];
        return d;
    }
}
