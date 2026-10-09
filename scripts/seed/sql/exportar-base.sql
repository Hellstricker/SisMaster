SET NOCOUNT ON;
SELECT N'SET NOCOUNT ON;';
SELECT N'-- Seed base da ABABAS (sem dados pessoais): associacao, categorias e locais.';
SELECT N'-- Gerado por scripts/seed/exportar-seed.ps1. Idempotente: so insere o que ainda nao existe (pelo Id).';
SELECT N'IF NOT EXISTS (SELECT 1 FROM Associacoes WHERE Id = ''' + CONVERT(nvarchar(36), Id) + N''') INSERT INTO Associacoes (Id, Nome, Uf, Ativa, Sigla) VALUES ('''
  + CONVERT(nvarchar(36), Id) + N''', N''' + REPLACE(Nome, N'''', N'''''') + N''', N''' + REPLACE(Uf, N'''', N'''''') + N''', '
  + CAST(Ativa AS nvarchar(1)) + N', N''' + REPLACE(Sigla, N'''', N'''''') + N''');'
FROM Associacoes;
SELECT N'IF NOT EXISTS (SELECT 1 FROM Categorias WHERE Id = ''' + CONVERT(nvarchar(36), Id) + N''') INSERT INTO Categorias (Id, Nome, AssociacaoId, IdadeMinima, MinimoPeriodosEmQuadra, MinimoPeriodosForaQuadra, Sexo, AceitaAbaixoIdadeMinima) VALUES ('''
  + CONVERT(nvarchar(36), Id) + N''', N''' + REPLACE(Nome, N'''', N'''''') + N''', ''' + CONVERT(nvarchar(36), AssociacaoId) + N''', '
  + CAST(IdadeMinima AS nvarchar(10)) + N', ' + CAST(MinimoPeriodosEmQuadra AS nvarchar(10)) + N', ' + CAST(MinimoPeriodosForaQuadra AS nvarchar(10)) + N', '
  + ISNULL(N'N''' + REPLACE(Sexo, N'''', N'''''') + N'''', N'NULL') + N', ' + CAST(AceitaAbaixoIdadeMinima AS nvarchar(1)) + N');'
FROM Categorias;
SELECT N'IF NOT EXISTS (SELECT 1 FROM Locais WHERE Id = ''' + CONVERT(nvarchar(36), Id) + N''') INSERT INTO Locais (Id, AssociacaoId, Nome, Cidade, Estado) VALUES ('''
  + CONVERT(nvarchar(36), Id) + N''', ''' + CONVERT(nvarchar(36), AssociacaoId) + N''', N''' + REPLACE(Nome, N'''', N'''''') + N''', N''' + REPLACE(Cidade, N'''', N'''''') + N''', '
  + ISNULL(N'N''' + REPLACE(Estado, N'''', N'''''') + N'''', N'NULL') + N');'
FROM Locais;
