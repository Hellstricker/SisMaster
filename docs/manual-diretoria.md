# Manual da diretoria

Do cadastro à classificação, na área de **Gestão** (`/admin`). Cada perfil vê só o que pode: o Administrador vê todas as associações; o Administrador de associação, só as dele.

## 1. Preparar a temporada

1. **Associação** > **Categorias**: cadastre as categorias (nome, idade mínima, sexo, se aceita atleta abaixo da idade mínima, mínimo de períodos em quadra e fora para o rodízio).
2. **Associação** > **Locais**: os ginásios onde os jogos acontecem.
3. **Associação** > **Página pública** e **Textos legais**: o que aparece no site (quem somos, diretoria, contato) e os dados da controladora para a política de privacidade. Preencha o CNPJ, o endereço, o encarregado de dados e o foro.
4. Crie o **campeonato** e a **temporada** (ano e datas das inscrições). Na temporada, escolha as categorias, defina a **taxa de inscrição** e o **valor de cada categoria** (e descontos por combinação, se houver).
5. Abra as inscrições (chave "Inscrições habilitadas"). Elas aparecem no site público dentro das datas.

## 2. Inscrições

Cada pessoa se inscreve pelo site (ou a diretoria cadastra em **+ Nova inscrição**, ou importa uma planilha). Na tela **Inscrições** da temporada:

- **Aprovar ou recusar por categoria.** Se a idade ou o sexo fogem do esperado, o sistema avisa e pede uma justificativa para aprovar mesmo assim. A recusa exige o motivo, que a pessoa vê no site; quem se inscreveu pelo site recebe e-mail a cada aprovação, recusa e pagamento confirmado.
- **Registrar a taxa.** Com a taxa paga, os pedidos aprovados viram *Efetivada* e a pessoa passa a Associado. Estorne um pagamento errado em **Ações > Ver / estornar pagamentos** (com motivo; o lançamento fica no histórico).
- **Corrigir dados** da ficha (altura, peso, posição, plano, e-mail, telefone) em **Ações**. Nome, CPF, nascimento e sexo só o Administrador corrige, em **Corrigir identificação** (fica na auditoria).
- **Desfazer aprovação** e **Reabrir pedido recusado** ficam em **Ações**. Quem já está em equipe precisa sair dela antes.
- **Cadastro duplicado?** O Administrador usa **Ações > Unir cadastro duplicado**, informando o CPF do duplicado: as fichas e a conta do site passam para o cadastro principal e o duplicado é apagado. Não desfaz; fica na auditoria.
- A coluna da pessoa mostra se a inscrição veio do **Site** (com o protocolo, por exemplo 2027-0142) ou da **Diretoria**, e se a conta do site está ligada.
- **Exportar** gera a lista em planilha (CPF e contato só saem se você marcar; cada exportação fica registrada).
- Depois de encerrar as inscrições, **Gerar cobranças** calcula o total por pessoa (categorias menos descontos) e o saldo em aberto; registre os pagamentos do saldo na ficha.

## 3. Equipes, fases e jogos

1. **Equipes**: crie as equipes e coloque os atletas efetivados. Trocar um atleta de equipe mantém o histórico das súmulas.
2. **Fases**: monte as fases (tabela, grupos, mata-mata) e encerre o cadastro de fases para gerar a tabela de jogos.
3. **Jogos**: acompanhe, remarque e imprima a agenda. O sistema bloqueia dois jogos no mesmo local com menos de 90 minutos de intervalo e na mesma equipe; atleta em duas equipes só gera aviso.
4. **W.O.**: pode ser aplicado e desfeito com motivo (tudo fica no histórico da auditoria).

## 4. Durante e depois do jogo

A mesa opera o jogo pelo celular (veja o *Manual da mesa*). A diretoria:

- **Importa a súmula do FIBA LiveStats** (Tela de importação, por código do jogo), quando for o caso. A conferência bloqueia a importação se algo não bate.
- **Reabre uma súmula encerrada** por engano, com motivo.
- **Classificação**: atualiza sozinha ao encerrar cada jogo e avança as fases. O site público mostra jogos, resultados e classificação.

## 5. Contas e acessos

- **Usuários**: o Administrador cria as contas da equipe (Administrador de associação, Árbitro/mesário) e define os acessos. A pessoa recebe uma senha temporária e troca no primeiro acesso.
- **O público** cria a própria conta no site e só enxerga as próprias inscrições. Contas do público não entram na Gestão.
- **Auditoria**: correções de identificação, estornos, uniões de cadastro e exportações ficam registradas com quem fez e quando.

## 6. Dados pessoais (LGPD)

Peça de cópia, correção ou eliminação de dados chega pelo e-mail de privacidade informado na política. Corrija o que for possível na ficha; a eliminação de um cadastro com histórico de jogos deve ser decidida pela diretoria. Mantenha os **Textos legais** atualizados: cada mudança é uma versão nova; marque "mudança relevante" quando alterar finalidades ou o uso de imagem.
