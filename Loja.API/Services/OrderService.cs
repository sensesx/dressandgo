using DressAndGo.Data;

namespace DressAndGo.Services;

public record item_pedido_input(long InventoryItemId, int Quantidade);

public class OrderService
{
    readonly string dbCaminho;
    public OrderService(string db) => dbCaminho = db;

    public object ListarPedidos(long? userId, string? status)
    {
        using var db = new SqliteConnection(dbCaminho);
        var condicoes = new List<string>();
        var par = new Dictionary<string, object?>();
        int i = 0;

        if (userId.HasValue) { condicoes.Add("o.user_id = ?"); par["p" + i++] = userId.Value; }
        if (status != null)  { condicoes.Add("o.status = ?");  par["p" + i++] = status; }

        string filtro = condicoes.Count > 0 ? "WHERE " + string.Join(" AND ", condicoes) : "";
        string sql = @"SELECT o.*, u.name as user_name, COUNT(oi.id) as item_count
                       FROM orders o JOIN users u ON u.id = o.user_id
                       LEFT JOIN order_items oi ON oi.order_id = o.id
                       " + filtro + " GROUP BY o.id ORDER BY o.created_at DESC";

        return db.Query(sql, par.Count > 0 ? par : null).Select(MapPedido).ToList();
    }

    public object? BuscarPedido(long id) {
        using var db = new SqliteConnection(dbCaminho);
        var pedido = db.QueryOne(@"SELECT o.*, u.name as user_name FROM orders o
                                   JOIN users u ON u.id = o.user_id WHERE o.id = ?", P(id));
        if (pedido == null) return null;

        var itens = db.Query(@"SELECT oi.*, inv.sku, inv.size, inv.color, p.name as product_name
                               FROM order_items oi
                               JOIN inventory_items inv ON inv.id = oi.inventory_item_id
                               JOIN products p ON p.id = inv.product_id
                               WHERE oi.order_id = ?", P(id));
        var pagamentos = db.Query("SELECT * FROM payments WHERE order_id = ?", P(id));

        return new { order = MapPedido(pedido), items = itens.Select(MapItem).ToList(), payments = pagamentos.Select(MapPagamento).ToList() };
    }

    public (bool ok, string msg, long id) CriarPedido(long userId, List<item_pedido_input> itens, string? obs, long? endId)
    {
        if (itens.Count == 0) return (false, "Pedido precisa de ao menos um item.", 0);

        using var db = new SqliteConnection(dbCaminho);
        double total = 0;
        var itensOk = new List<(long invId, int qtd, double preco)>();

        foreach (var item in itens)
        {
            var inv = db.QueryOne(@"SELECT i.id, i.qty_available, p.price
                                    FROM inventory_items i JOIN products p ON p.id = i.product_id
                                    WHERE i.id = ?", P(item.InventoryItemId));

            if (inv == null)
                return (false, "Item " + item.InventoryItemId + " nao encontrado.", 0);

            if (Convert.ToInt64(inv["qty_available"]) < item.Quantidade)
                return (false, "Estoque insuficiente para o item " + item.InventoryItemId + ".", 0);

            double preco = Convert.ToDouble(inv["price"]);
            total += preco * item.Quantidade;
            itensOk.Add((item.InventoryItemId, item.Quantidade, preco));
        }

        long pedidoId = db.ExecuteInsert("INSERT INTO orders (user_id, address_id, status, total, notes) VALUES (?,?,'criado',?,?)", P(userId, endId, total, obs));

        foreach (var (invId, qtd, preco) in itensOk)
            db.Execute("INSERT INTO order_items (order_id, inventory_item_id, quantity, unit_price) VALUES (?,?,?,?)", P(pedidoId, invId, qtd, preco));

        return (true, "Pedido criado.", pedidoId);
    }

    public (bool ok, string msg) AlterarStatus(long pedidoId, string novoStatus, long atorId, string atorPerfil)
    {
        using var db = new SqliteConnection(dbCaminho);
        var pedido = db.QueryOne("SELECT id, status, user_id FROM orders WHERE id = ?", P(pedidoId));
        if (pedido == null) return (false, "Pedido nao encontrado.");

        string statusAtual = pedido["status"]?.ToString() ?? "";
        long donoId = Convert.ToInt64(pedido["user_id"]);
        bool gestor = atorPerfil == "manager" || atorPerfil == "stockist";

        if (!gestor && atorId != donoId) return (false, "Sem permissao.");

        bool transicaoValida = (statusAtual, novoStatus) switch
        {
            ("criado", "aguardando") => true,
            ("criado", "cancelado") => true,
            ("aguardando", "pago") => gestor,
            ("aguardando", "cancelado") => true,
            ("pago", "finalizado") => gestor,
            ("pago", "cancelado") => gestor,
            _ => false
        };

        if (!transicaoValida)
            return (false, "Transicao invalida: " + statusAtual + " -> " + novoStatus);

        if (novoStatus == "pago") ConfirmarPagamento(db, pedidoId);
        if (novoStatus == "cancelado" && statusAtual == "pago") EstornarEstoque(db, pedidoId);

        db.Execute("UPDATE orders SET status = ?, updated_at = datetime('now') WHERE id = ?", P(novoStatus, pedidoId));
        return (true, "Status atualizado para " + novoStatus + ".");
    }

    void ConfirmarPagamento(SqliteConnection db, long pedidoId) {
        foreach (var item in db.Query("SELECT * FROM order_items WHERE order_id = ?", P(pedidoId))) {
            long invId = Convert.ToInt64(item["inventory_item_id"]);
            int qtd = Convert.ToInt32(item["quantity"]);
            db.Execute("UPDATE inventory_items SET qty_available=qty_available-?, qty_sold=qty_sold+? WHERE id=?", P(qtd, qtd, invId));
        }
    }

    void EstornarEstoque(SqliteConnection db, long pedidoId)
    {
        var itens = db.Query("SELECT * FROM order_items WHERE order_id = ?", P(pedidoId));
        foreach (var item in itens)
        {
            long invId = Convert.ToInt64(item["inventory_item_id"]);
            int qtd    = Convert.ToInt32(item["quantity"]);
            db.Execute("UPDATE inventory_items SET qty_available=qty_available+?, qty_sold=qty_sold-? WHERE id=?", P(qtd, qtd, invId));
        }
    }

    public (bool ok, string msg, long id) RegistrarPagamento(long pedidoId, double valor, string metodo, int parcelas) {
        using var db = new SqliteConnection(dbCaminho);
        var pedido = db.QueryOne("SELECT status FROM orders WHERE id = ?", P(pedidoId));
        if (pedido == null) return (false, "Pedido nao encontrado.", 0);
        if (pedido["status"]?.ToString() != "aguardando") return (false, "Pedido nao aguarda pagamento.", 0);

        string txId = "TX-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + pedidoId;
        long pagId  = db.ExecuteInsert(
            "INSERT INTO payments (order_id, amount, method, installments, status, transaction_id) VALUES (?,?,?,?,'aprovado',?)",
            P(pedidoId, valor, metodo, parcelas, txId));

        AlterarStatus(pedidoId, "pago", 0, "manager");
        return (true, "Pagamento registrado.", pagId);
    }

    static object MapPedido(Dictionary<string, object?> r) => new {
        id        = Convert.ToInt64(r["id"]),
        userId    = Convert.ToInt64(r["user_id"]),
        userName  = r.ContainsKey("user_name") ? r["user_name"]?.ToString() : null,
        status    = r["status"]?.ToString(),
        total     = Convert.ToDouble(r["total"]),
        notes     = r["notes"]?.ToString(),
        itemCount = r.ContainsKey("item_count") ? Convert.ToInt64(r["item_count"]) : 0,
        createdAt = r["created_at"]?.ToString()
    };

    static object MapItem(Dictionary<string, object?> r) => new {
        id          = Convert.ToInt64(r["id"]),
        sku         = r["sku"]?.ToString(),
        size        = r["size"]?.ToString(),
        color       = r["color"]?.ToString(),
        productName = r["product_name"]?.ToString(),
        quantity    = Convert.ToInt32(r["quantity"]),
        unitPrice   = Convert.ToDouble(r["unit_price"]),
        subtotal    = Convert.ToDouble(r["unit_price"]) * Convert.ToInt32(r["quantity"])
    };

    static object MapPagamento(Dictionary<string, object?> r) =>
        new { id = Convert.ToInt64(r["id"]), amount = Convert.ToDouble(r["amount"]), method = r["method"]?.ToString(), status = r["status"]?.ToString() };

    static Dictionary<string, object?> P(params object?[] v) {
        var d = new Dictionary<string, object?>();
        for (int i = 0; i < v.Length; i++) d["p" + i] = v[i];
        return d;
    }
}
