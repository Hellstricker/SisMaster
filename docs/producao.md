# Publicação e operação (Etapa 5)

O que já está pronto no repositório e o que só você pode decidir ou fazer. Nada daqui foi testado **num servidor de verdade** (não há servidor ainda); o que foi testado: o arquivo de produção carrega (`docker compose ... config`), os scripts de backup e de restauração rodaram contra um banco real em Docker, o de saúde foi conferido contra o login real.

## O que está pronto

| Item | O que existe | Onde |
|---|---|---|
| D3 Implantação | Arquivo de produção (só o proxy HTTPS fica exposto, tudo reinicia sozinho), proxy com certificado automático, script que faz backup, atualiza, sobe, confere e diz como voltar atrás | `docker-compose.prod.yml`, `scripts/producao/Caddyfile`, `scripts/producao/implantar.sh` |
| D4 Backup e restauração | Backup diário comprimido e conferido dos dois bancos, retenção de 14 dias, gancho para cópia fora do servidor; teste de restauração em banco temporário, com contagens e tempo | `scripts/producao/backup.sh`, `scripts/producao/testar-restauracao.sh` |
| D5 Observabilidade | Conferência de site, API pública e login a cada 5 minutos, com aviso por webhook opcional | `scripts/producao/verificar-saude.sh` |
| D6 Manual | Guia da diretoria e da mesa | `docs/manual-diretoria.md`, `docs/manual-mesa.md` |

## O que depende de você

| Item | Decisão ou ação |
|---|---|
| **D1 Hospedagem** | Escolher e contratar o servidor. Recomendação: uma VPS Linux com Docker (2 vCPU, 4 GB de RAM e 40 GB de disco bastam para uma associação). Criar usuário sem senha de login (só chave SSH), firewall liberando apenas 22, 80 e 443, atualizações automáticas de segurança. |
| **D2 Domínio e HTTPS** | Escolher o domínio (lembrando que `ababas.org.br` está comprometido) e apontar o DNS para o IP do servidor. O certificado é automático assim que o DNS responde. Depois, configurar SPF/DKIM do e-mail desse domínio. |
| **D7 Ensaio em produção** | Diretoria e mesários fazendo um jogo de ponta a ponta, sem o desenvolvedor, no servidor de verdade. |
| **D8 Go-live** | Congelar a versão (`git tag v1.0`), avisar os usuários e combinar quem atende nas primeiras semanas. |
| E-mail real | `EMAIL_PROVEDOR=Smtp` e os dados do SMTP no `.env` do servidor, e `EMAIL_NOME` (ex.: a sigla da associação) para o remetente e o topo dos e-mails. Os e-mails saem em HTML com versão em texto; testar a chegada do e-mail de confirmação de conta e ver se não cai no spam (SPF/DKIM dependem do domínio, D2). |
| Textos legais | A diretoria revisa a política de privacidade e os termos e preenche CNPJ, endereço, encarregado e foro (Gestão > Associação > Página pública e Textos legais). |

## Primeira instalação no servidor

```bash
git clone <repositório> /opt/sismaster && cd /opt/sismaster
cp .env.example .env     # preencher: SA_PASSWORD, JWT_SEGREDO, ADMIN_EMAIL, ADMIN_SENHA_INICIAL, DOMINIO, APP_URL=https://<DOMINIO>,
                         # EMAIL_PROVEDOR=Smtp + SMTP_*, SITE_ASSOCIACAO_SIGLA, COPIA_EXTERNA/DESTINO_EXTERNO (cópia do backup fora do servidor)
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```

Depois, uma vez: `crontab -e` com o backup diário (`0 3 * * * cd /opt/sismaster && ./scripts/producao/backup.sh >> /var/log/sismaster-backup.log 2>&1`) e a conferência (`*/5 * * * * cd /opt/sismaster && ./scripts/producao/verificar-saude.sh >> /var/log/sismaster-saude.log 2>&1`). Rode `./scripts/producao/testar-restauracao.sh` logo no primeiro dia e depois uma vez por mês: backup que nunca foi restaurado não é backup.

Pode ser preciso `chmod +x scripts/producao/*.sh` na primeira vez.

## Atualizar e voltar atrás

```bash
./scripts/producao/implantar.sh              # backup, git pull, build, sobe e confere
./scripts/producao/implantar.sh v1.0         # ou uma versão (tag) específica
```

Se a conferência falhar, o script imprime o comando para voltar à versão anterior. As migrations do banco são aplicadas sozinhas quando a API sobe; por isso o backup vem antes. Se a versão nova já tiver alterado o banco, voltar só o código não basta: restaure também o backup.

## Restaurar o banco de verdade (emergência)

1. Pare as APIs: `docker compose -f docker-compose.yml -f docker-compose.prod.yml stop campeonato-api identidade-api`.
2. Copie o backup para o contêiner e restaure por cima do banco (`SisMaster` e, se preciso, `SisMasterIdentidade`):
   ```bash
   docker cp /var/backups/sismaster/SisMaster_AAAAMMDD_HHMM.bak sismaster-sqlserver:/var/opt/mssql/restaurar.bak
   docker exec sismaster-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -Q \
     "ALTER DATABASE [SisMaster] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [SisMaster] FROM DISK='/var/opt/mssql/restaurar.bak' WITH REPLACE; ALTER DATABASE [SisMaster] SET MULTI_USER;"
   ```
3. Suba as APIs de novo: `docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d`.

## Banco: SQL Server Express

O sistema usa a edição **Express** do SQL Server (gratuita e licenciada para produção, banco de até 10 GB por base); a Developer não pode ser usada em produção nem em demonstração pública. A edição vem de `MSSQL_PID` (padrão Express no `docker-compose.yml`). Trocar de Developer para Express num volume existente foi testado e mantém os dados. O Express não tem SQL Server Agent: o backup agendado é o `backup.sh` (cron).

## Segredos

O `.env` do servidor guarda senhas e nunca vai para o Git. Troque `SA_PASSWORD` e `JWT_SEGREDO` por valores novos e fortes na instalação (não reutilize os de desenvolvimento) e guarde uma cópia do `.env` em lugar seguro, fora do servidor: sem ele não há como reconstruir o ambiente.
