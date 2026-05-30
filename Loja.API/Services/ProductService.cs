using DressAndGo.Data;

namespace DressAndGo.Services;

public class ProductService
{
    readonly string dbCaminho;
    public ProductService(string db) => dbCaminho = db;

    public object ListarProdutos(string? categoria, bool? ativo) {
        using var db = new SqliteConnection(dbCaminho);
        var condicoes = new List<string>();
        var parametros = new Dictionary<string, object?>();
        int indice = 0;

        if (categoria != null) { condicoes.Add("p.category = ?"); parametros["p" + indice++] = categoria; }
        if (ativo.HasValue)    { condicoes.Add("p.active = ?");   parametros["p" + indice++] = ativo.Value ? 1 : 0; }

        string filtro = condicoes.Count > 0 ? "WHERE " + string.Join(" AND ", condicoes) : "";
        string sql = @"SELECT p.*, COALESCE(SUM(i.qty_available),0) as total_available, COUNT(i.id) as sku_count
                       FROM products p LEFT JOIN inventory_items i ON i.product_id = p.id
                       " + filtro + " GROUP BY p.id ORDER BY p.name";

        return db.Query(sql, parametros.Count > 0 ? parametros : null).Select(MapProduto).ToList();
    }

    public object? BuscarProduto(long id)
    {
        using var db = new SqliteConnection(dbCaminho);
        var linha = db.QueryOne(@"SELECT p.*, COALESCE(SUM(i.qty_available),0) as total_available
                                  FROM products p LEFT JOIN inventory_items i ON i.product_id = p.id
                                  WHERE p.id = ? GROUP BY p.id", P(id));
        if (linha == null) return null;

        var itens = db.Query("SELECT * FROM inventory_items WHERE product_id = ? ORDER BY size, color", P(id));
        return new { produto = MapProduto(linha), itens = itens.Select(MapSku).ToList() };
    }

    public (bool ok, string msg, long id) CriarProduto(string nome, string descricao, string categoria, decimal preco) {
        if (string.IsNullOrWhiteSpace(nome)) return (false, "Nome obrigatorio.", 0);
        if (preco <= 0) return (false, "Preco deve ser maior que zero.", 0);
        using var db = new SqliteConnection(dbCaminho);
        long novoId = db.ExecuteInsert("INSERT INTO products (name, description, category, price) VALUES (?,?,?,?)", P(nome, descricao, categoria, (double)preco));
        return (true, "Produto criado.", novoId);
    }

    public (bool ok, string msg) AtualizarProduto(long id, string? nome, string? descricao, string? categoria, decimal? preco, bool? ativo)
    {
        using var db = new SqliteConnection(dbCaminho);
        if (db.Scalar<long>("SELECT COUNT(*) FROM products WHERE id = ?", P(id)) == 0)
            return (false, "Produto nao encontrado.");

        if (nome      != null) db.Execute("UPDATE products SET name        = ? WHERE id = ?", P(nome, id));
        if (descricao != null) db.Execute("UPDATE products SET description = ? WHERE id = ?", P(descricao, id));
        if (categoria != null) db.Execute("UPDATE products SET category    = ? WHERE id = ?", P(categoria, id));
        if (preco.HasValue)    db.Execute("UPDATE products SET price       = ? WHERE id = ?", P((double)preco.Value, id));
        if (ativo.HasValue)    db.Execute("UPDATE products SET active      = ? WHERE id = ?", P(ativo.Value ? 1 : 0, id));

        return (true, "Produto atualizado.");
    }

    public object ListarEstoque(long? produtoId, string? sku)
    {
        using var db = new SqliteConnection(dbCaminho);
        var condicoes  = new List<string>();
        var parametros = new Dictionary<string, object?>();
        int indice = 0;

        if (produtoId.HasValue) { condicoes.Add("i.product_id = ?"); parametros["p" + indice++] = produtoId.Value; }
        if (sku != null)        { condicoes.Add("i.sku LIKE ?");     parametros["p" + indice++] = "%" + sku + "%"; }

        string filtro = condicoes.Count > 0 ? "WHERE " + string.Join(" AND ", condicoes) : "";
        string sql = @"SELECT i.*, p.name as product_name, p.price, p.category
                       FROM inventory_items i JOIN products p ON p.id = i.product_id
                       " + filtro + " ORDER BY p.name, i.size";

        return db.Query(sql, parametros.Count > 0 ? parametros : null).Select(MapSkuCompleto).ToList();
    }

    public (bool ok, string msg, long id) AdicionarSku(long produtoId, string tamanho, string cor, int quantidade) {
        if (quantidade < 0) return (false, "Quantidade nao pode ser negativa.", 0);

        using var db = new SqliteConnection(dbCaminho);
        if (db.Scalar<long>("SELECT COUNT(*) FROM products WHERE id = ?", P(produtoId)) == 0)
            return (false, "Produto nao encontrado.", 0);

        var linha    = db.QueryOne("SELECT name FROM products WHERE id = ?", P(produtoId));
        string prefixo   = (linha?["name"]?.ToString() ?? "PRD").Split(' ')[0].ToUpper()[..3];
        string codigoSku = prefixo + "-" + tamanho.ToUpper() + "-" + cor.Replace(" ", "").ToUpper() + "-" + produtoId;

        if (db.Scalar<long>("SELECT COUNT(*) FROM inventory_items WHERE sku = ?", P(codigoSku)) > 0)
            return (false, "SKU ja existe.", 0);

        long novoId = db.ExecuteInsert("INSERT INTO inventory_items (product_id, sku, size, color, qty_available) VALUES (?,?,?,?,?)", P(produtoId, codigoSku, tamanho, cor, quantidade));
        return (true, "SKU adicionado.", novoId);
    }

    public (bool ok, string msg) AjustarEstoque(long itemId, int delta, string motivo)
    {
        using var db = new SqliteConnection(dbCaminho);
        var item = db.QueryOne("SELECT id, qty_available FROM inventory_items WHERE id = ?", P(itemId));
        if (item == null) return (false, "Item nao encontrado.");

        long qtdAtual = Convert.ToInt64(item["qty_available"]);
        long qtdNova  = qtdAtual + delta;
        if (qtdNova < 0) return (false, "Estoque insuficiente.");

        db.Execute("UPDATE inventory_items SET qty_available = ? WHERE id = ?", P(qtdNova, itemId));
        return (true, motivo + ". Disponivel: " + qtdNova);
    }

    static object MapProduto(Dictionary<string, object?> r) => new
    {
        id             = Convert.ToInt64(r["id"]),
        name           = r["name"]?.ToString(),
        description    = r["description"]?.ToString(),
        category       = r["category"]?.ToString(),
        price          = Convert.ToDouble(r["price"]),
        active         = Convert.ToInt64(r["active"]) == 1,
        totalAvailable = r.ContainsKey("total_available") ? Convert.ToInt64(r["total_available"]) : 0,
        skuCount       = r.ContainsKey("sku_count") ? Convert.ToInt64(r["sku_count"]) : 0
    };

    static object MapSku(Dictionary<string, object?> r) => new {
        id = Convert.ToInt64(r["id"]), productId = Convert.ToInt64(r["product_id"]),
        sku = r["sku"]?.ToString(), size = r["size"]?.ToString(),
        color = r["color"]?.ToString(), qtyAvailable = Convert.ToInt64(r["qty_available"])
    };

    static object MapSkuCompleto(Dictionary<string, object?> r) => new
    {
        id          = Convert.ToInt64(r["id"]),
        productId   = Convert.ToInt64(r["product_id"]),
        productName = r["product_name"]?.ToString(),
        sku         = r["sku"]?.ToString(),
        size        = r["size"]?.ToString(),
        color       = r["color"]?.ToString(),
        price       = Convert.ToDouble(r["price"]),
        qtyAvailable = Convert.ToInt64(r["qty_available"])
    };

    static Dictionary<string, object?> P(params object?[] v) {
        var d = new Dictionary<string, object?>();
        for (int i = 0; i < v.Length; i++) d["p" + i] = v[i];
        return d;
    }
}
