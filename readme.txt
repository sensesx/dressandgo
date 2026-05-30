Linguagem C# pro backend (.NET 9) e ASP.NET, SQLite com P/Invoke, sha-256 para hash de senhas.

Quando abrir rodar o servidor a database roda (databaseinitializer.cs), basicamente cria todas as tables e informações caso elas não existam no computador.

sqlite helper é um wrapper que possibilita chamar as funções do sqlite através de C (só que em C#)
e se conectar com o banco de dados.

ANTES DE RODAR O PROJETO

Instalar o sdk correto: https://dotnet.microsoft.com/download/dotnet/9
baixar sqlite x64: https://www.sqlite.org/download.html e colocar na pasta principal (se não estiver presente)
rodar no git bash: ./run.sh

http://localhost:5000
cliente123
admin@dressandgo.com.br / admin123
cliente@dressandgo.com.br / cliente123


O banco de dados tá em /bin/debug/net9.0/
