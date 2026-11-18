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

- **Aprovar ou recusar por categoria.** Se a idade ou o sexo fogem do esperado, o sistema avisa e pede uma justificativa para aprovar mesmo assim. A recusa exige o motivo, que a pessoa vê no site; quem se inscreveu pelo site recebe e-mail a cada aprovação, recusa e pagamento confirmado. Nas fichas feitas pela diretoria o aviso é opcional: marque **Avisar a pessoa por e-mail** na janela (o e-mail do cadastro pode estar desatualizado).
- **Registrar a taxa.** Com a taxa paga, os pedidos aprovados viram *Efetivada* e a pessoa é **Associada naquela temporada**: a associação vale por temporada, então a lista mostra "Associado na temporada" ou "Não associado na temporada", e a pessoa vê isso em Minha conta > Associação. Estorne um pagamento errado em **Ações > Ver / estornar pagamentos** (com motivo; o lançamento fica no histórico).
- **Corrigir dados** da ficha (altura, peso, posição, plano, e-mail, telefone) em **Ações**. Nome, CPF, nascimento e sexo só o Administrador corrige, em **Corrigir identificação** (fica na auditoria).
- **Desfazer aprovação** e **Reabrir pedido recusado** ficam em **Ações**. Quem já está em equipe precisa sair dela antes.
- **Cadastro duplicado?** O Administrador usa **Ações > Unir cadastro duplicado**, informando o CPF do duplicado: as fichas e a conta do site passam para o cadastro principal e o duplicado é apagado. Não desfaz; fica na auditoria.
- **Conta criada com outro e-mail?** O Administrador usa **Ações > Autorizar ligação com conta do site**, informando o e-mail da conta. Quando a pessoa entrar nessa conta (e-mail confirmado) e digitar o CPF certo, o cadastro é ligado a ela, sem mudar nenhum dado. A autorização some depois de usada e pode ser cancelada (e-mail em branco).
- A coluna da pessoa mostra se a inscrição veio do **Site** (com o protocolo, por exemplo 2027-0142) ou da **Diretoria**, e se a conta do site está ligada.
- **Exportar** gera a lista em planilha (CPF e contato só saem se você marcar; cada exportação fica registrada).
- Depois de encerrar as inscrições, **Gerar cobranças** calcula o total por pessoa (categorias menos descontos) e o saldo em aberto; registre os pagamentos do saldo na ficha.

## 2B. Torneio aberto e equipes inscritas

Na temporada, o campo **Público do campeonato** pode ser **Aberto (qualquer pessoa)**: participar **não cria vínculo de associado** (a pessoa aparece como *Participante*). Em campeonato aberto, o site pergunta se a pessoa é **responsável por uma equipe** ou quer jogar **sem equipe** (inscrição individual, como sempre).

Fluxo da equipe, na tela **Equipes inscritas** (cartão na temporada):

1. O responsável inscreve a equipe pelo site (de 5 a 15 atletas) e cada atleta recebe um e-mail com um link pessoal, válido por 14 dias, para confirmar os dados e as autorizações. Menores de 18 anos não entram por aqui: a diretoria os inscreve, com a autorização do responsável legal.
2. Com pelo menos 5 atletas confirmados, **Aprovar** a equipe (ou **Recusar**, com o motivo, que o responsável vê). Aprovar não cobra nada e cria a equipe, ainda sem elenco. Se algum atleta estiver fora da idade ou do sexo da categoria, o sistema pede uma justificativa.
3. **Ver atletas e taxas**: marque os atletas cuja taxa foi recebida e use **Registrar taxa dos selecionados**. Cada um fica *Efetivado* e **entra no elenco da equipe sozinho**; a equipe não espera os outros. Quem pagou antes da aprovação, ou confirmou o convite depois dela, entra do mesmo jeito.
4. Atleta que se inscreveu sozinho continua na tela **Inscrições**; depois de efetivado, você o coloca em uma equipe pela tela **Equipes**.

Enquanto as inscrições estão abertas, a equipe criada na aprovação só muda pelo responsável (adicionar, reenviar convite, remover quem não confirmou). Não dá para voltar a temporada para *Só associados* se já há equipe inscrita.

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

Peça de cópia, correção ou eliminação de dados chega pelo e-mail de privacidade informado na política. Corrija o que for possível na ficha; a eliminação de um cadastro com histórico de jogos deve ser decidida pela diretoria. Mantenha os **Textos legais** atualizados: cada mudança é uma versão nova; marque "mudança relevante" quando alterar finalidades ou o uso de imagem. Mudança relevante sobe o número principal (2.0) e **todas as contas do site precisam aceitar de novo** no próximo acesso (aparece o resumo da mudança que você escreveu); mudança pequena (1.1) não pede nada.
