using DressAndGo.Data;
using DressAndGo.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();

var dbCaminho    = Path.Combine(AppContext.BaseDirectory, "dressandgo.db");
var chave_secreta = builder.Configuration["ChaveSecreta"]
    ?? throw new Exception("ChaveSecreta nao configurada.");

builder.Services.AddSingleton(_ => new AuthService(dbCaminho, chave_secreta));
builder.Services.AddSingleton(_ => new UserService(dbCaminho));
builder.Services.AddSingleton(_ => new ProductService(dbCaminho));
builder.Services.AddSingleton(_ => new OrderService(dbCaminho));
builder.Services.AddCors(x => x.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.Use(async (ctx, next) => {
    var h = ctx.Request.Headers.Authorization.ToString();
    if (h.StartsWith("Bearer "))
        ctx.Items["tk"] = ctx.RequestServices.GetRequiredService<AuthService>().Validar(h[7..]);
    await next();
});

info_token? Tk(HttpContext ctx) => ctx.Items["tk"] as info_token;
bool EhGestor(info_token? tk) => tk?.Perfil is "manager" or "stockist";
bool PodeAcessarUsuario(info_token? tk, long id) => tk != null && (tk.Perfil == "manager" || tk.UsuarioId == id);

app.MapPost("/api/cadastro", (dados_cadastro form, HttpContext ctx) => {
    if (form.Email == null || form.Password == null)
        return Results.BadRequest(new { erro = "preencha email e senha" });
    var (ok, msg, novoId) = ctx.RequestServices.GetRequiredService<UserService>()
        .CriarCliente(form.Name ?? "", form.Email, form.Password, form.Phone);
    return ok ? Results.Ok(new { id = novoId, msg }) : Results.BadRequest(new { erro = msg });
});

app.MapPost("/auth/login", (dados_login form, HttpContext ctx) => {
    var r = ctx.RequestServices.GetRequiredService<AuthService>().Login(form.Email ?? "", form.Password ?? "");
    if (!r.Ok) return Results.Json(new { erro = r.Erro }, statusCode: 401);
    return Results.Ok(new { token = r.Token, role = r.Perfil, userId = r.UsuarioId, name = r.Nome });
});

app.MapGet("/users", (HttpContext ctx) => {
    if (Tk(ctx)?.Perfil != "manager") return Results.Json(new { erro = "sem permissao" }, statusCode: 403);
    return Results.Ok(ctx.RequestServices.GetRequiredService<UserService>().Listar());
});

app.MapGet("/users/{id:long}", (HttpContext ctx, long id) => {
    if (!PodeAcessarUsuario(Tk(ctx), id)) return Results.Json(new { erro = "sem permissao" }, statusCode: 403);
    var u = ctx.RequestServices.GetRequiredService<UserService>().BuscarPorId(id);
    return u == null ? Results.NotFound(new { erro = "nao encontrado" }) : Results.Ok(u);
});

app.MapPost("/users", (dados_novo_user form, HttpContext ctx) => {
    if (Tk(ctx)?.Perfil != "manager") return Results.Json(new { erro = "sem permissao" }, statusCode: 403);
    var (ok, msg, id) = ctx.RequestServices.GetRequiredService<UserService>()
        .Criar(form.Name ?? "", form.Email ?? "", form.Password ?? "", form.Role ?? "stockist", form.Phone);
    return ok ? Results.Ok(new { id, msg }) : Results.BadRequest(new { erro = msg });
});

app.MapPatch("/users/{id:long}", (dados_atualiza_user form, HttpContext ctx, long id) => {
    if (!PodeAcessarUsuario(Tk(ctx), id)) return Results.Json(new { erro = "sem permissao" }, statusCode: 403);
    var (ok, msg) = ctx.RequestServices.GetRequiredService<UserService>().Atualizar(id, form.Name, form.Role, form.Active, form.Phone);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { erro = msg });
});

app.MapPost("/users/{id:long}/change-password", (dados_troca_senha form, HttpContext ctx, long id) => {
    if (!PodeAcessarUsuario(Tk(ctx), id)) return Results.Json(new { erro = "sem permissao" }, statusCode: 403);
    var (ok, msg) = ctx.RequestServices.GetRequiredService<UserService>()
        .AlterarSenha(id, form.SenhaAtual ?? "", form.SenhaNova ?? "");
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { erro = msg });
});

app.MapGet("/users/{id:long}/addresses", (HttpContext ctx, long id) => {
    if (!PodeAcessarUsuario(Tk(ctx), id)) return Results.Json(new { erro = "sem permissao" }, statusCode: 403);
    return Results.Ok(ctx.RequestServices.GetRequiredService<UserService>().ListarEnderecos(id));
});

app.MapPost("/users/{id:long}/addresses", (endereco_req end, HttpContext ctx, long id) => {
    if (!PodeAcessarUsuario(Tk(ctx), id)) return Results.Json(new { erro = "sem permissao" }, statusCode: 403);
    var (ok, msg, novoId) = ctx.RequestServices.GetRequiredService<UserService>().AdicionarEndereco(id, end);
    return ok ? Results.Ok(new { id = novoId, msg }) : Results.BadRequest(new { erro = msg });
});

app.MapPatch("/users/{uid:long}/addresses/{aid:long}", (endereco_req end, HttpContext ctx, long uid, long aid) => {
    if (!PodeAcessarUsuario(Tk(ctx), uid)) return Results.Json(new { erro = "sem permissao" }, statusCode: 403);
    var (ok, msg) = ctx.RequestServices.GetRequiredService<UserService>().AtualizarEndereco(aid, uid, end);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { erro = msg });
});

app.MapDelete("/users/{uid:long}/addresses/{aid:long}", (HttpContext ctx, long uid, long aid) => {
    if (!PodeAcessarUsuario(Tk(ctx), uid)) return Results.Json(new { erro = "sem permissao" }, statusCode: 403);
    var (ok, msg) = ctx.RequestServices.GetRequiredService<UserService>().RemoverEndereco(aid, uid);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { erro = msg });
});

app.MapGet("/products", (HttpContext ctx) => {
    var cat  = ctx.Request.Query["category"].ToString();
    var av   = ctx.Request.Query["active"].ToString();
    bool? ativo = av == "true" ? true : av == "false" ? false : null;
    return Results.Ok(ctx.RequestServices.GetRequiredService<ProductService>()
        .ListarProdutos(string.IsNullOrEmpty(cat) ? null : cat, ativo));
});

app.MapGet("/products/{id:long}", (HttpContext ctx, long id) => {
    var prod = ctx.RequestServices.GetRequiredService<ProductService>().BuscarProduto(id);
    return prod == null ? Results.NotFound(new { erro = "nao encontrado" }) : Results.Ok(prod);
});

app.MapPost("/products", (dados_produto form, HttpContext ctx) => {
    if (Tk(ctx)?.Perfil != "manager") return Results.Json(new { erro = "sem permissao" }, statusCode: 403);
    if (form.Name == null) return Results.BadRequest(new { erro = "nome obrigatorio" });
    var (ok, msg, prodId) = ctx.RequestServices.GetRequiredService<ProductService>()
        .CriarProduto(form.Name, form.Description ?? "", form.Category ?? "", form.Price);
    return ok ? Results.Ok(new { id = prodId, msg }) : Results.BadRequest(new { erro = msg });
});

app.MapPatch("/products/{id:long}", (dados_atualiza_produto form, HttpContext ctx, long id) => {
    if (Tk(ctx)?.Perfil != "manager") return Results.Json(new { erro = "sem permissao" }, statusCode: 403);
    var (ok, msg) = ctx.RequestServices.GetRequiredService<ProductService>()
        .AtualizarProduto(id, form.Name, form.Description, form.Category, form.Price, form.Active);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { erro = msg });
});

app.MapGet("/inventory", (HttpContext ctx) => {
    if (Tk(ctx) == null) return Results.Json(new { erro = "nao autenticado" }, statusCode: 401);
    long? prodId = long.TryParse(ctx.Request.Query["productId"].ToString(), out long pid) ? pid : null;
    return Results.Ok(ctx.RequestServices.GetRequiredService<ProductService>().ListarEstoque(prodId, null));
});

app.MapPost("/inventory", (dados_sku form, HttpContext ctx) => {
    if (!EhGestor(Tk(ctx))) return Results.Json(new { erro = "sem permissao" }, statusCode: 403);
    var (ok, msg, skuId) = ctx.RequestServices.GetRequiredService<ProductService>()
        .AdicionarSku(form.ProductId, form.Size ?? "", form.Color ?? "", form.Quantity);
    return ok ? Results.Ok(new { id = skuId, msg }) : Results.BadRequest(new { erro = msg });
});

app.MapPatch("/inventory/{id:long}/adjust", (dados_ajuste form, HttpContext ctx, long id) => {
    if (!EhGestor(Tk(ctx))) return Results.Json(new { erro = "sem permissao" }, statusCode: 403);
    var (ok, msg) = ctx.RequestServices.GetRequiredService<ProductService>()
        .AjustarEstoque(id, form.Delta, form.Motivo ?? "ajuste");
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { erro = msg });
});

app.MapGet("/orders", (HttpContext ctx) => {
    var tk = Tk(ctx);
    if (tk == null) return Results.Json(new { erro = "nao autenticado" }, statusCode: 401);
    long? uid = tk.Perfil == "customer" ? tk.UsuarioId : null;
    return Results.Ok(ctx.RequestServices.GetRequiredService<OrderService>().ListarPedidos(uid, null));
});

app.MapGet("/orders/{id:long}", (HttpContext ctx, long id) => {
    if (Tk(ctx) == null) return Results.Json(new { erro = "nao autenticado" }, statusCode: 401);
    var pedido = ctx.RequestServices.GetRequiredService<OrderService>().BuscarPedido(id);
    return pedido == null ? Results.NotFound(new { erro = "nao encontrado" }) : Results.Ok(pedido);
});

app.MapPost("/orders", (dados_pedido form, HttpContext ctx) => {
    var tk = Tk(ctx);
    if (tk == null) return Results.Json(new { erro = "nao autenticado" }, statusCode: 401);
    if (form.Items == null || form.Items.Count == 0) return Results.BadRequest(new { erro = "pedido sem itens" });
    long userId = tk.Perfil == "customer" ? tk.UsuarioId : (form.UserId ?? tk.UsuarioId);
    var lista = form.Items.Select(i => new item_pedido_input(i.InventoryItemId, i.Quantity)).ToList();
    var (ok, msg, pedidoId) = ctx.RequestServices.GetRequiredService<OrderService>()
        .CriarPedido(userId, lista, form.Notes, form.AddressId);
    return ok ? Results.Ok(new { id = pedidoId, msg }) : Results.BadRequest(new { erro = msg });
});

app.MapPatch("/orders/{id:long}/status", (dados_status form, HttpContext ctx, long id) => {
    var tk = Tk(ctx);
    if (tk == null) return Results.Json(new { erro = "nao autenticado" }, statusCode: 401);
    if (form.Status == null) return Results.BadRequest(new { erro = "status obrigatorio" });
    var (ok, msg) = ctx.RequestServices.GetRequiredService<OrderService>()
        .AlterarStatus(id, form.Status, tk.UsuarioId, tk.Perfil);
    return ok ? Results.Ok(new { msg }) : Results.BadRequest(new { erro = msg });
});

app.MapPost("/payments", (dados_pagamento form, HttpContext ctx) => {
    if (Tk(ctx) == null) return Results.Json(new { erro = "nao autenticado" }, statusCode: 401);
    var (ok, msg, pagId) = ctx.RequestServices.GetRequiredService<OrderService>()
        .RegistrarPagamento(form.OrderId, form.Amount, form.Method ?? "pix", form.Installments);
    return ok ? Results.Ok(new { id = pagId, msg }) : Results.BadRequest(new { erro = msg });
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Servidor: http://localhost:5000");
Console.ResetColor();
app.Run("http://localhost:5000");

record dados_cadastro(string? Name, string? Email, string? Password, string? Phone = null);
record dados_login(string? Email, string? Password);
record dados_novo_user(string? Name, string? Email, string? Password, string? Role, string? Phone = null);
record dados_atualiza_user(string? Name, string? Role, bool? Active, string? Phone);
record dados_troca_senha(string? SenhaAtual, string? SenhaNova);
record dados_produto(string? Name, string? Description, string? Category, decimal Price);
record dados_atualiza_produto(string? Name, string? Description, string? Category, decimal? Price, bool? Active);
record dados_sku(long ProductId, string? Size, string? Color, int Quantity);
record dados_ajuste(int Delta, string? Motivo);
record item_do_pedido(long InventoryItemId, int Quantity);
record dados_pedido(long? UserId, List<item_do_pedido>? Items, string? Notes, long? AddressId);
record dados_status(string? Status);
record dados_pagamento(long OrderId, double Amount, string? Method, int Installments = 1);

public record endereco_req(
    string? Label, string? Cep, string? Street, string? Number,
    string? Complement, string? District, string? City, string? State
);
