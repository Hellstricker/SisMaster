# Seed da ABABAS (uso sob demanda)

O sistema **não carrega seed sozinho**: ele sobe vazio. Este seed só roda quando você mandar.

| Arquivo | O que tem | Vai para o Git? |
|---|---|---|
| `01-ababas-base.sql` | associação, categorias e locais (sem dados pessoais) | sim |
| `dados-locais/02-pessoas-ababas.sql` | as pessoas da ABABAS (**dados pessoais**) | **não** (ignorado) |
| `backups/*.bak` (na raiz) | backup completo do banco | **não** (ignorado) |

## Aplicar (o banco precisa ter as migrations; a API aplica ao subir)
```powershell
.\scripts\seed\aplicar-seed.ps1                 # só associação, categorias e locais
.\scripts\seed\aplicar-seed.ps1 -ComPessoas     # e também as pessoas
```
Para outro ambiente: `-Container <nome> -Banco <banco> -Senha <senha do SA>`. É idempotente (não duplica; insere só o que falta, pelo Id) e **nunca apaga nada**.

## Atualizar os arquivos a partir de um banco que já tenha os dados
```powershell
.\scripts\seed\exportar-seed.ps1            # regera 01-ababas-base.sql e 02-pessoas-ababas.sql (só lê o banco)
.\scripts\seed\exportar-seed.ps1 -Backup    # e gera também um backup completo (.bak) em backups/
```

## Restaurar um backup completo
O `.bak` é uma cópia inteira do banco (inclui temporada, inscrições, equipes, jogos e súmulas). Para restaurar num SQL Server do Docker:
copie o arquivo para o container e use `RESTORE DATABASE ... FROM DISK = N'/tmp/arquivo.bak' WITH REPLACE`.
