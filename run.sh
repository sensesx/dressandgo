#!/bin/bash
echo " Servidor aberto em —> http://localhost:5000"
echo ""
DIR="$(dirname "$0")/Loja.API"
cd "$DIR"
dotnet run
