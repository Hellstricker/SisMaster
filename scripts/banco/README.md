# Cofre de dados

Guarda uma cópia do banco do Docker numa instância SQL Server da sua máquina e a traz de volta quando você quiser.
Usa backup e restauração (copia o banco inteiro: temporada, inscrições, equipes, jogos, súmulas e eventos).

```powershell
.\scripts\banco\cofre.ps1 Guardar -Rotulo etapa-0     # Docker -> cofre (cria SisMaster_cofre_AAAAMMDD_HHMM_etapa-0, somente leitura)
.\scripts\banco\cofre.ps1 Listar                      # bancos do cofre e do Docker
.\scripts\banco\cofre.ps1 Trazer -Nome <banco do cofre>                       # cofre -> Docker como banco NOVO (não toca no SisMaster)
.\scripts\banco\cofre.ps1 Trazer -Nome <banco do cofre> -Substituir -Confirmo # troca o SisMaster do Docker pelo do cofre
```

- **Guardar nunca altera o Docker.** O backup é `COPY_ONLY`; o cofre é conferido contra a origem (contagens de pessoas, inscrições, equipes, jogos, súmulas e eventos).
- **Trazer nunca sobrescreve sem `-Substituir -Confirmo`.** Com a substituição, faz antes um backup de segurança do banco atual em `backups/`.
- **Configuração** (no `.env`, opcional): `COFRE_SERVIDOR` (a instância local, com Windows Authentication) e `COFRE_PASTA_TROCA` (padrão `C:\Users\Public\SisMasterCofre`, pasta que o serviço SQL local consegue ler e gravar). A pasta de troca só guarda o arquivo durante a operação e é esvaziada no fim; ela é legível por todos os usuários da máquina.
- **Versões:** o cofre precisa ser SQL Server 2022 ou mais novo que o do Docker (2022). Um banco guardado numa instância 2025 **não volta** para o Docker 2022.
- Os arquivos `.bak` gerados ficam também em `backups/` (ignorado pelo Git).
