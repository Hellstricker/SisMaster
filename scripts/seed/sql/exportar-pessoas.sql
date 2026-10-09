SET NOCOUNT ON;
SELECT N'SET NOCOUNT ON;';
SELECT N'-- Pessoas da ABABAS (DADOS PESSOAIS: nao versionar, fica em scripts/seed/dados-locais/).';
SELECT N'-- Gerado por scripts/seed/exportar-seed.ps1. Idempotente: so insere o que ainda nao existe (pelo Id).';
SELECT N'IF NOT EXISTS (SELECT 1 FROM Pessoas WHERE Id = ''' + CONVERT(nvarchar(36), Id) + N''') INSERT INTO Pessoas (Id, Nome, Cpf, Nascimento, Sexo, Email, Telefone, Perfil) VALUES ('''
  + CONVERT(nvarchar(36), Id) + N''', N''' + REPLACE(Nome, N'''', N'''''') + N''', N''' + Cpf + N''', ''' + CONVERT(nvarchar(10), Nascimento, 23) + N''', N'''
  + REPLACE(Sexo, N'''', N'''''') + N''', N''' + REPLACE(Email, N'''', N'''''') + N''', '
  + ISNULL(N'N''' + REPLACE(Telefone, N'''', N'''''') + N'''', N'NULL') + N', N''' + REPLACE(Perfil, N'''', N'''''') + N''');'
FROM Pessoas ORDER BY Nome;
