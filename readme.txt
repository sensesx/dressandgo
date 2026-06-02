# Dress & Go

## Integrantes
- RA: 3024103486 — Kauan Faria Nascimento  
- RA: 3024103700 — Gustavo Venâncio Vieira da Conceição  
- RA: 3023100946 — Vinicius Moura Cardoso  
- RA: 3024105228 — Guilherme Messias  
- RA: 3024104172 — Matheus Paulucci Ferreira  
- RA: 3023104368 — Erick Fernando Priore Michelan  

## Linguagem e Tecnologias
- **Backend:** C# (.NET 9) com ASP.NET Core  
- **Banco de Dados:** SQLite via P/Invoke  
- **Autenticação:** SHA-256 com salt fixo  
- **Frontend:** HTML + Bootstrap, HTTP e CSS
- **Sessão:** sessionStorage  

> O `SQLiteHelper` é um wrapper que possibilita a utilização das funções da biblioteca SQLite escrita em C diretamente no C#, além de gerenciar a conexão com o banco de dados de forma eficiente.

## Objetivo
O projeto **Dress & Go** tem como propósito fornecer uma plataforma integrada para a **gestão de produtos, pedidos e usuários**, unificando frontend e backend em um único ambiente.  
A solução permite **catalogar produtos**, **armazenar informações** e **controlar o estoque** com praticidade e segurança.

## Arquitetura
- API REST e arquivos estáticos em servidor único  
- Estrutura modular, clara e de fácil manutenção  
- URLs relativas, compatíveis com qualquer host  

### Serviços
- **AuthService** → Gerenciamento de login e tokens  
- **UserService** → Administração de usuários e endereços  
- **ProductService** → Controle de produtos e estoque  
- **OrderService** → Processamento de pedidos e pagamentos  

### Endpoints
- **Auth:** `/auth/login`, `/auth/me`  
- **Produtos:** `/products`, `/inventory`  
- **Usuários:** `/users/{id}`, `/users/{id}/addresses`  
- **Pedidos:** `/orders`, `/orders/{id}/status`  
- **Pagamentos:** `/payments`  

## Instalação e Uso
1. Instale o [SDK .NET 9.0](https://dotnet.microsoft.com/download/dotnet/9)  
2. Baixe o [SQLite x64](https://www.sqlite.org/download.html) e copie os arquivos para a pasta principal do projeto  
3. No Git Bash, execute:  
   ```bash
   ./run.sh

Contas para teste:

admin@dressandgo.com.br / admin123 
cliente@dressandgo.com.br / cliente123 
estoque@dressandgo.com.br / estoque123
